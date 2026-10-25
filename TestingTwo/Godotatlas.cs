using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gui
{
    /// <summary>
    /// Lets the Godot scene you made (Plater.tscn) drive the WinForms game.
    /// WinForms cannot run a .tscn, but the scene is a text file that says, for every character
    /// (youngver, middle, oldver and their shadow versions), which animations exist and which
    /// rectangle of which PNG each frame is. This class reads that text and hands the frames
    /// to FighterSprites. Edit the scene in Godot, copy it over, rebuild, and the game follows.
    /// </summary>
    internal static class GodotAtlas
    {
        // Scene file names to look for.
        private static readonly string[] SceneNames = { "Plater.tscn", "Plater_1.tscn", "Player.tscn" };

        // The scene can hold several characters (for example Vyte, Aloy, Mokutin, Aerin). Every AnimatedSprite2D that
        // is NOT a child of another AnimatedSprite2D is a character's young form. Its children are the older forms
        // ("middle..." and "old...") and the phantom sets (any node with "shad" in its name), found by name.
        private sealed class Character
        {
            public string Name = "";
            public string[] Node = new string[3];          // young, middle, old  (full node paths)
            public string[] ShadowNode = new string[3];    // phantom set of each age
        }

        private static readonly List<Character> characters = new List<Character>();

        // Set to false to ignore the Godot scene and go back to the plain strip files.
        public static readonly bool Enabled = true;

        // Game animation name -> animation names to look for in the scene (first one found wins).
        // Names are compared without case, spaces or underscores, so "Jump_attack", "jump attack" and "jumpattack" are the same.
        private static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            ["idle"] = new[] { "idle" },
            ["runningwalking"] = new[] { "walk" },
            ["jump"] = new[] { "jump" },
            ["duck"] = new[] { "duck" },
            ["hurt"] = new[] { "hurt" },
            ["defeated"] = new[] { "dead", "death" },
            ["punch1"] = new[] { "attack1" },
            ["punch2"] = new[] { "attack2" },
            ["kick"] = new[] { "attack3" },
            ["jumpattack"] = new[] { "jumpattack" },
            ["downattack"] = new[] { "downattack", "attackdown" },
            ["block"] = new[] { "block" },
            ["drop"] = new[] { "drop", "disappear" }
        };

        private sealed class GodotFrame
        {
            public string Path = "";          // e.g. "fighter_all_sets/young/Duck.png"
            public Rectangle Source;
        }

        // node name -> (normalized animation name -> frames)
        private static readonly Dictionary<string, Dictionary<string, List<GodotFrame>>> sets =
            new Dictionary<string, Dictionary<string, List<GodotFrame>>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Bitmap?> sheets = new Dictionary<string, Bitmap?>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> missing = new List<string>();
        private static readonly Dictionary<string, string> resolvedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> missingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static List<string>? projectPngs;
        private static List<string>? projectScenes;
        private static bool loadTried;
        private static string status = "";
        private static string droppedFramesNote = "";

        public static bool Available
        {
            get { EnsureLoaded(); return characters.Count > 0; }
        }

        public static string Status
        {
            get { EnsureLoaded(); return status; }
        }

        public static int CharacterCount
        {
            get { EnsureLoaded(); return characters.Count; }
        }

        // Roster fighter number -> character in the scene (wraps around if the roster is longer than the scene)
        private static Character? CharacterOf(int fighter)
        {
            EnsureLoaded();
            if (characters.Count == 0) return null;
            return characters[((fighter % characters.Count) + characters.Count) % characters.Count];
        }

        private static string? NodeFor(int fighter, AgeStage age)
        {
            return CharacterOf(fighter)?.Node[(int)age];
        }

        private static string? ShadowNodeFor(int fighter, AgeStage age)
        {
            return CharacterOf(fighter)?.ShadowNode[(int)age];
        }

        /// <summary>How many frames the phantom (shadow) set has for a game animation at this age (0 if none).</summary>
        public static int ShadowFrameCount(int fighter, AgeStage age, string animation)
        {
            return FrameCount(ShadowNodeFor(fighter, age), animation);
        }

        public static bool TryGetShadowFrame(int fighter, AgeStage age, string animation, int frame, out Bitmap? sheet, out Rectangle source)
        {
            return TryGetFrame(ShadowNodeFor(fighter, age), animation, frame, out sheet, out source);
        }

        /// <summary>How many frames the scene has for a game animation for this fighter at this age (0 if none).</summary>
        public static int FrameCount(int fighter, AgeStage age, string animation)
        {
            return FrameCount(NodeFor(fighter, age), animation);
        }

        public static bool TryGetFrame(int fighter, AgeStage age, string animation, int frame, out Bitmap? sheet, out Rectangle source)
        {
            return TryGetFrame(NodeFor(fighter, age), animation, frame, out sheet, out source);
        }

        private static int FrameCount(string? node, string animation)
        {
            EnsureLoaded();
            if (node == null || !sets.TryGetValue(node, out Dictionary<string, List<GodotFrame>>? set)) return 0;
            return Find(set, animation)?.Count ?? 0;
        }

        /// <summary>Finds the picture and rectangle to draw for one frame of one animation of one scene node.</summary>
        private static bool TryGetFrame(string? node, string animation, int frame, out Bitmap? sheet, out Rectangle source)
        {
            sheet = null;
            source = Rectangle.Empty;
            EnsureLoaded();

            if (node == null || !sets.TryGetValue(node, out Dictionary<string, List<GodotFrame>>? set)) return false;
            List<GodotFrame>? frames = Find(set, animation);
            if (frames == null || frames.Count == 0) return false;

            GodotFrame picked = frames[Math.Clamp(frame, 0, frames.Count - 1)];
            Bitmap? bitmap = GetSheet(picked.Path);
            if (bitmap == null) return false;

            sheet = bitmap;
            source = picked.Source;
            return true;
        }

        private static string Normalize(string name)
        {
            return new string(name.Where(c => c != '_' && c != ' ').ToArray()).ToLowerInvariant();
        }

        private static List<GodotFrame>? Find(Dictionary<string, List<GodotFrame>> set, string animation)
        {
            string key = Normalize(animation);
            string[] candidates = Aliases.TryGetValue(key, out string[]? names) ? names : new[] { key };

            // 1. exact name (walk, idle ...)
            foreach (string candidate in candidates)
            {
                if (set.TryGetValue(candidate, out List<GodotFrame>? frames) && frames.Count > 0) return frames;
            }

            // 2. names with a prefix in front, like VWalk, VOwalk, VMidle or VYSattack1.
            //    Backwards walking is never used as the normal walk.
            foreach (string candidate in candidates)
            {
                foreach (var kv in set)
                {
                    if (kv.Value.Count == 0 || kv.Key.Contains("back")) continue;
                    if (kv.Key.EndsWith(candidate, StringComparison.Ordinal)) return kv.Value;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ loading

        private static void EnsureLoaded()
        {
            if (loadTried) return;
            loadTried = true;

            if (!Enabled)
            {
                status = "turned off (strip files are used)";
                return;
            }

            try
            {
                string? usedScene = null;
                foreach (string scene in FindScenes())
                {
                    if (TryParseScene(scene))
                    {
                        usedScene = scene;
                        break;
                    }
                }

                if (usedScene == null)
                {
                    sets.Clear();
                    characters.Clear();
                    status = "no scene with a fighter found (looked for " + string.Join(", ", SceneNames) + ")";
                    return;
                }

                // Load every picture the scene uses now, so a missing file shows up in the startup message.
                foreach (string path in sets.Values.SelectMany(s => s.Values).SelectMany(f => f).Select(f => f.Path).Distinct())
                {
                    GetSheet(path);
                }

                // A frame whose rectangle sticks out of its picture (the scene counts more frames than the picture has)
                // would be drawn mirrored by the tiling mode, so the character seems to look back and forth. Drop those frames.
                List<string> droppedInfo = new List<string>();
                foreach (Dictionary<string, List<GodotFrame>> set in sets.Values.Distinct())
                {
                    foreach (KeyValuePair<string, List<GodotFrame>> anim in set)
                    {
                        int removed = anim.Value.RemoveAll(f =>
                        {
                            Bitmap? b = GetSheet(f.Path);
                            return b != null && (f.Source.X < 0 || f.Source.Y < 0 || f.Source.Right > b.Width || f.Source.Bottom > b.Height);
                        });
                        if (removed > 0 && anim.Value.Count > 0)
                        {
                            string file = resolvedFiles.TryGetValue(anim.Value[0].Path, out string? rf) ? rf : anim.Value[0].Path;
                            droppedInfo.Add($"{anim.Key}: {removed} frame(s) outside {Path.GetFileName(file)}");
                        }
                    }
                }
                if (droppedInfo.Count > 0) droppedFramesNote = "\nDROPPED frames past the end of their picture: " + string.Join("; ", droppedInfo.Take(6));

                status = $"LOADED {characters.Count} characters ({string.Join(", ", characters.Select(c => c.Name))}) from {Path.GetFileName(usedScene)}";
                status += droppedFramesNote;
                if (missing.Count > 0)
                {
                    status += $"\nMISSING {missing.Count} PNG files: " + string.Join(", ", missing.Take(5)) + (missing.Count > 5 ? ", ..." : "");

                    // Say which fighter is affected, so a "wrong character" is easy to trace to its missing pictures
                    for (int i = 0; i < characters.Count; i++)
                    {
                        Character c = characters[i];
                        List<string> gone = new List<string>();
                        foreach (string node in c.Node.Concat(c.ShadowNode).Where(n => n != null).Distinct())
                        {
                            if (!sets.TryGetValue(node, out Dictionary<string, List<GodotFrame>>? set)) continue;
                            foreach (string path in set.Values.SelectMany(f => f).Select(f => f.Path).Distinct())
                            {
                                if (missingPaths.Contains(path) && !gone.Contains(path)) gone.Add(path);
                            }
                        }
                        if (gone.Count == 0) continue;
                        string label = i < GameData.Roster.Length ? GameData.Roster[i].Name : c.Name;
                        status += $"\n  {label}: {gone.Count} picture(s) not found, for example {gone[0]}";
                    }
                }
            }
            catch (Exception ex)
            {
                sets.Clear();
                characters.Clear();
                status = "failed to read scene: " + ex.Message;
            }
        }

        private static bool TryParseScene(string scenePath)
        {
            sets.Clear();
            characters.Clear();
            string text = File.ReadAllText(scenePath);

            // ext_resource: id -> picture path inside the Godot project, e.g. res://fighter_all_sets/young/Duck.png
            Dictionary<string, string> pictures = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(text, @"\[ext_resource[^\]]*?path=""res://([^""]+)""[^\]]*?id=""([^""]+)""\]"))
            {
                pictures[m.Groups[2].Value] = m.Groups[1].Value;
            }

            // AtlasTexture: id -> (picture, rectangle)
            Dictionary<string, GodotFrame> regions = new Dictionary<string, GodotFrame>();
            foreach (Match m in Regex.Matches(text,
                @"\[sub_resource type=""AtlasTexture"" id=""([^""]+)""\]\s*atlas = ExtResource\(""([^""]+)""\)[^\[]*?region = Rect2\(\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+)\s*\)"))
            {
                if (!pictures.TryGetValue(m.Groups[2].Value, out string? picturePath)) continue;
                regions[m.Groups[1].Value] = new GodotFrame
                {
                    Path = picturePath,
                    Source = new Rectangle(ToInt(m.Groups[3]), ToInt(m.Groups[4]), ToInt(m.Groups[5]), ToInt(m.Groups[6]))
                };
            }

            // SpriteFrames: id -> animations (name -> ordered frames)
            Dictionary<string, Dictionary<string, List<GodotFrame>>> frameSets = new Dictionary<string, Dictionary<string, List<GodotFrame>>>();
            foreach (Match block in Regex.Matches(text, @"\[sub_resource type=""SpriteFrames"" id=""([^""]+)""\]\r?\n(.*?)(?=\r?\n\[)", RegexOptions.Singleline))
            {
                Dictionary<string, List<GodotFrame>> animations = new Dictionary<string, List<GodotFrame>>();
                foreach (Match a in Regex.Matches(block.Groups[2].Value,
                    @"""frames"":\s*\[(.*?)\],\s*""loop"":\s*\w+,\s*""name"":\s*&?""([^""]+)""", RegexOptions.Singleline))
                {
                    List<GodotFrame> frames = new List<GodotFrame>();
                    foreach (Match f in Regex.Matches(a.Groups[1].Value, @"SubResource\(""([^""]+)""\)"))
                    {
                        if (regions.TryGetValue(f.Groups[1].Value, out GodotFrame? region)) frames.Add(region);
                    }

                    string key = Normalize(a.Groups[2].Value);
                    if (frames.Count > 0 && !animations.ContainsKey(key)) animations[key] = frames;
                }
                frameSets[block.Groups[1].Value] = animations;
            }

            // Nodes: every AnimatedSprite2D, keyed by its full path (names repeat between characters, paths do not)
            List<(string Path, string Name, string Parent)> sprites = new List<(string, string, string)>();
            foreach (Match n in Regex.Matches(text,
                @"\[node name=""([^""]+)"" type=""AnimatedSprite2D""(?: parent=""([^""]*)"")?[^\]]*\]([^\[]*?)sprite_frames = SubResource\(""([^""]+)""\)"))
            {
                if (!frameSets.TryGetValue(n.Groups[4].Value, out Dictionary<string, List<GodotFrame>>? animations)) continue;

                string name = n.Groups[1].Value;
                string parent = n.Groups[2].Value;
                string path = (parent == "" || parent == ".") ? name : parent + "/" + name;
                sets[path] = animations;
                sprites.Add((path, name, parent));
            }

            // Characters. The scene lays them out in one of two ways:
            //   Vyte / (middle, oldver as children of Vyte)      - older forms are children of the young form
            //   Aloy, middle, old (all under one Node2D)         - older forms are siblings of the young form
            // Either way the first non-shadow sprite of a group is the young form, a name starting with "m" is the middle
            // form, one starting with "o" the old form, and any node with "shad" in its name is a phantom set.
            HashSet<string> spritePaths = new HashSet<string>(sprites.Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
            foreach (var group in sprites.Where(x => !spritePaths.Contains(x.Parent) && !IsShadow(x.Name)).GroupBy(x => x.Parent))
            {
                var members = group.ToList();
                bool grouped = group.Key != "" && group.Key != ".";
                Character c = new Character { Name = grouped ? group.Key : members[0].Name };

                // The young form, plus any older forms that sit beside it or under it
                List<(string Path, string Name, string Parent)> candidates = new List<(string, string, string)>(members);
                candidates.AddRange(sprites.Where(x => string.Equals(x.Parent, members[0].Path, StringComparison.OrdinalIgnoreCase) && !IsShadow(x.Name)));
                foreach (var cand in candidates)
                {
                    int age = AgeOfName(cand.Name);
                    if (c.Node[age] == null) c.Node[age] = cand.Path;
                }
                c.Node[0] ??= members[0].Path;

                // A missing older form just keeps showing the form before it
                for (int i = 1; i < 3; i++) c.Node[i] ??= c.Node[i - 1];
                for (int i = 0; i < 3; i++) c.ShadowNode[i] = FindShadowChild(sprites, c.Node[i]) ?? c.Node[i];
                characters.Add(c);
            }

            return characters.Count > 0;
        }

        // Node names such as Vyoung / Vmiddle / Voldver, ALyoung / ALmiddle / ALold, Mmiddle, Mold, AEold ...
        private static int AgeOfName(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("old") || n.Contains("elder")) return 2;
            if (n.Contains("mid")) return 1;
            return 0;
        }

        private static bool IsShadow(string name)
        {
            return name.IndexOf("shad", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string? FindShadowChild(List<(string Path, string Name, string Parent)> sprites, string parentPath)
        {
            foreach (var x in sprites)
            {
                if (string.Equals(x.Parent, parentPath, StringComparison.OrdinalIgnoreCase) && IsShadow(x.Name)) return x.Path;
            }
            return null;
        }

        private static int ToInt(Group group)
        {
            return (int)Math.Round(float.Parse(group.Value, CultureInfo.InvariantCulture));
        }

        // ------------------------------------------------------------------ finding files

        private static string ProjectDir()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        }

        private static IEnumerable<string> FindScenes()
        {
            string baseDir = AppContext.BaseDirectory;
            string projectDir = ProjectDir();
            List<string> found = new List<string>();

            foreach (string name in SceneNames)
            {
                foreach (string dir in new[] { projectDir, Path.Combine(projectDir, "Assets"), baseDir, Path.Combine(baseDir, "Assets") })
                {
                    string candidate = Path.Combine(dir, name);
                    if (File.Exists(candidate)) found.Add(candidate);
                }
            }

            if (projectScenes == null)
            {
                try { projectScenes = Directory.GetFiles(projectDir, "*.tscn", SearchOption.AllDirectories).ToList(); }
                catch { projectScenes = new List<string>(); }
            }
            foreach (string name in SceneNames)
            {
                found.AddRange(projectScenes.Where(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase)));
            }

            return found.Distinct();
        }

        private static Bitmap? GetSheet(string resPath)
        {
            if (sheets.TryGetValue(resPath, out Bitmap? cached)) return cached;

            Bitmap? bitmap = null;
            string? file = ResolvePath(resPath);
            if (file != null) resolvedFiles[resPath] = file;
            if (file != null)
            {
                try { bitmap = ToFastBitmap(new Bitmap(file)); }
                catch { bitmap = null; }
            }

            if (bitmap == null)
            {
                string name = Path.GetFileName(resPath);
                if (!missing.Contains(name)) missing.Add(name);
                missingPaths.Add(resPath);
            }

            sheets[resPath] = bitmap;
            return bitmap;
        }

        // resPath is the path inside the Godot project, e.g. "fighter_all_sets/young/Duck.png" or just "Idle.png".
        private static string? ResolvePath(string resPath)
        {
            string baseDir = AppContext.BaseDirectory;
            string projectDir = ProjectDir();
            string relative = resPath.Replace('/', Path.DirectorySeparatorChar);
            string fileName = Path.GetFileName(resPath);
            bool rootFile = !resPath.Contains('/');

            List<string> candidates = new List<string>
            {
                Path.Combine(projectDir, relative),
                Path.Combine(baseDir, relative),
                Path.Combine(projectDir, "Assets", relative),
                Path.Combine(baseDir, "Assets", relative)
            };
            if (rootFile)
            {
                // The scene keeps the young fighter's pictures at the Godot project root.
                candidates.Add(Path.Combine(projectDir, "fighter_all_sets", "young", fileName));
                candidates.Add(Path.Combine(baseDir, "fighter_all_sets", "young", fileName));
            }
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            if (projectPngs == null)
            {
                try { projectPngs = Directory.GetFiles(projectDir, "*.png", SearchOption.AllDirectories).ToList(); }
                catch { projectPngs = new List<string>(); }
            }

            // The fighter's own folder (Vyte, Aloy, Mokutin, Aerin). The scene still remembers the old folder names
            // (Samurai, Fighter_Sprites_Complete, Book_Girl_Sprites_Complete ...), so those are mapped to the new ones.
            string? inOwnFolder = FindInCharacterFolder(resPath, projectPngs);
            if (inOwnFolder != null) return inOwnFolder;

            // The same path ending anywhere in the project. The full path is tried first, then shorter endings
            // (so "Samurai/Samurai_Sprites_Complete/Samurai_Sprites_Complete/young/normal/Idle.png" still finds
            // ".../Samurai_Sprites_Complete/young/normal/Idle.png"). A shorter ending always keeps the age folder
            // (young / middle_age / elderly) and must sit inside the character's own top folder, so one fighter
            // never borrows another fighter's picture and one age never borrows another age's.
            if (!rootFile)
            {
                string[] parts = resPath.Split('/');
                string topFolder = "/" + parts[0] + "/";
                int minKeep = Math.Min(parts.Length, 3);
                for (int keep = parts.Length; keep >= minKeep; keep--)
                {
                    string tail = "/" + string.Join("/", parts.Skip(parts.Length - keep));
                    foreach (string p in projectPngs)
                    {
                        string normalized = p.Replace('\\', '/');
                        if (!normalized.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) continue;
                        if (keep < parts.Length && normalized.IndexOf(topFolder, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        return p;
                    }
                }
            }

            // Young Vyte pictures: same file name anywhere under his own "young" folder (never guess another age or fighter).
            if (rootFile || resPath.StartsWith("fighter_all_sets/young/", StringComparison.OrdinalIgnoreCase))
            {
                string? inYoung = null;
                foreach (string p in projectPngs)
                {
                    string normalized = p.Replace('\\', '/');
                    if (!string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!normalized.Contains("/young/")) continue;
                    if (IsOtherFighterFolder(normalized)) continue;
                    if (!normalized.Contains("/shadow_mechanic/")) return p;
                    inYoung ??= p;
                }
                return inYoung;
            }

            return null;
        }

        // Old folder names used inside Plater.tscn -> the roster folder that holds that fighter's pictures now
        private static readonly Dictionary<string, string> FolderOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fighter_all_sets"] = "Vyte",
            ["Vyte"] = "Vyte",
            ["Fighter_Sprites_Complete"] = "Aloy",
            ["Aloy"] = "Aloy",
            ["Samurai"] = "Mokutin",
            ["Mokutin"] = "Mokutin",
            ["Book_Girl_Sprites_Complete"] = "Aerin",
            ["Girl_3"] = "Aerin",
            ["Aerin"] = "Aerin"
        };

        private static readonly string[] FighterFolders = { "Vyte", "Aloy", "Mokutin", "Aerin" };

        private static bool IsOtherFighterFolder(string normalizedPath)
        {
            return FighterFolders.Any(f => normalizedPath.IndexOf("/" + f + "/", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool AgeMatches(string normalizedPath, string age)
        {
            string n = normalizedPath.ToLowerInvariant();
            return age switch
            {
                "middle" => n.Contains("/middle_age/") || n.Contains("/middle/"),
                "elder" => n.Contains("/elderly/") || n.Contains("/older/") || n.Contains("/old/"),
                _ => n.Contains("/young/")
            };
        }

        /// <summary>
        /// Finds a picture by who owns it, the age and the file name, inside that fighter's own folder, e.g.
        /// "Samurai/.../elderly/normal/Walk.png" -> ".../Mokutin/Mokutin/elderly/Walk.png". Phantom pictures stay phantom
        /// pictures, normal ones stay normal.
        /// </summary>
        private static string? FindInCharacterFolder(string resPath, List<string> pngs)
        {
            string[] parts = resPath.Split('/');
            string? owner = null;
            if (parts.Length == 1) owner = "Vyte";                         // pictures at the Godot project root are Vyte's
            else if (FolderOwner.TryGetValue(parts[0], out string? o)) owner = o;
            if (owner == null) return null;

            string lower = resPath.ToLowerInvariant();
            string age = (lower.Contains("middle")) ? "middle" : (lower.Contains("elder") || lower.Contains("/older/") || lower.Contains("/old/")) ? "elder" : "young";
            bool wantShadow = lower.Contains("shadow");
            string fileName = parts[parts.Length - 1];

            // The scene's loose young pictures (Aloy/Attack.png, Samurai/Shield.png, Samurai/Dead.png ...) have other
            // names in the full sprite sets, so the same picture is looked for under its set name too.
            List<string> names = new List<string> { fileName };
            if (FileAlternatives.TryGetValue(fileName, out string[]? alternatives)) names.AddRange(alternatives);

            foreach (string name in names)
            {
                string? best = null;
                int bestScore = int.MinValue;
                foreach (string p in pngs)
                {
                    if (!string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase)) continue;
                    string n = p.Replace('\\', '/');
                    if (n.IndexOf("/" + owner + "/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!AgeMatches(n, age)) continue;
                    if ((n.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0) != wantShadow) continue;

                    int score = (n.Contains("/normal/", StringComparison.OrdinalIgnoreCase) ? 100 : 0) - n.Length;
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                if (best != null) return best;
            }
            return null;
        }

        // Picture names in the scene -> the name of the same picture in the full sprite sets
        private static readonly Dictionary<string, string[]> FileAlternatives = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Attack.png"] = new[] { "Attack_1.png" },
            ["Shield.png"] = new[] { "Block.png" },
            ["Dead.png"] = new[] { "Death.png" },
            ["Death.png"] = new[] { "Dead.png" }
        };

        private static Bitmap ToFastBitmap(Bitmap original)
        {
            using (original)
            {
                Bitmap fast = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppPArgb);
                using Graphics g = Graphics.FromImage(fast);
                g.DrawImage(original, 0, 0, original.Width, original.Height);
                return fast;
            }
        }
    }
}