using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Gui
{
    /// <summary>
    /// Loads and caches the background pictures used by maps (embedded or from disk).
    /// </summary>
    internal static class ArenaImages
    {
        private static readonly Dictionary<string, Bitmap?> cache = new Dictionary<string, Bitmap?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns the map's background picture, or null if missing.</summary>
        public static Bitmap? Get(ArenaInfo arena)
        {
            if (string.IsNullOrEmpty(arena.BackgroundFile)) return null;
            if (cache.TryGetValue(arena.BackgroundFile, out Bitmap? cached)) return cached;

            Bitmap? result = LoadImageFile(arena.BackgroundFile);

            // Alias lookup for elevator filename variations
            if (result == null && arena.BackgroundFile.Contains("elevator", StringComparison.OrdinalIgnoreCase))
            {
                result = LoadImageFile("elevator_lobby.png") ?? LoadImageFile("elevator_16bit.png");
            }

            cache[arena.BackgroundFile] = result;
            return result;
        }

        private static Bitmap? LoadImageFile(string filename)
        {
            try
            {
                Assembly asm = typeof(ArenaImages).Assembly;

                // 1. Search Embedded Resources matching filename suffix
                string? resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("." + filename, StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("/" + filename, StringComparison.OrdinalIgnoreCase) ||
                                         n.Equals(filename, StringComparison.OrdinalIgnoreCase));

                if (resName != null)
                {
                    using Stream? stream = asm.GetManifestResourceStream(resName);
                    if (stream != null) return ToFastBitmap(new Bitmap(stream));
                }

                // 2. Search File System Paths (Output & Source Directories)
                string baseDir = AppContext.BaseDirectory;
                string currentDir = Directory.GetCurrentDirectory();

                List<string> candidatePaths = new List<string>
                {
                    Path.Combine(baseDir, "Assets", filename),
                    Path.Combine(baseDir, filename),
                    Path.Combine(currentDir, "Assets", filename),
                    Path.Combine(currentDir, filename),
                    filename
                };

                // Traverse up to 4 parent directories (locates files directly in project source root)
                DirectoryInfo? dir = new DirectoryInfo(baseDir);
                for (int i = 0; i < 4 && dir != null; i++)
                {
                    candidatePaths.Add(Path.Combine(dir.FullName, "Assets", filename));
                    candidatePaths.Add(Path.Combine(dir.FullName, filename));
                    dir = dir.Parent;
                }

                foreach (string path in candidatePaths.Distinct())
                {
                    if (File.Exists(path))
                    {
                        return ToFastBitmap(new Bitmap(path));
                    }
                }

                // 3. Fuzzy fallback: the file may have been renamed by a download ("name (1).jpg", spaces instead of
                //    underscores, different case). Look for a close match in the Assets folders and then the project tree.
                string? fuzzy = FindCloseMatch(filename, baseDir);
                if (fuzzy != null)
                {
                    return ToFastBitmap(new Bitmap(fuzzy));
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string Normalize(string name)
        {
            return new string(Path.GetFileNameWithoutExtension(name).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        private static string? FindCloseMatch(string filename, string baseDir)
        {
            string wanted = Normalize(filename);
            if (wanted.Length == 0) return null;
            string[] exts = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

            bool Matches(string file)
            {
                if (!exts.Contains(Path.GetExtension(file).ToLowerInvariant())) return false;
                string n = Normalize(file);
                return n == wanted || n.StartsWith(wanted);
            }

            // Assets folders next to the exe, the current folder, and up to 4 parents
            List<string> folders = new List<string>();
            DirectoryInfo? d = new DirectoryInfo(baseDir);
            for (int i = 0; i < 5 && d != null; i++)
            {
                folders.Add(d.FullName);
                folders.Add(Path.Combine(d.FullName, "Assets"));
                d = d.Parent;
            }
            folders.Add(Directory.GetCurrentDirectory());
            folders.Add(Path.Combine(Directory.GetCurrentDirectory(), "Assets"));

            foreach (string folder in folders.Distinct())
            {
                if (!Directory.Exists(folder)) continue;
                try
                {
                    string? hit = Directory.GetFiles(folder).FirstOrDefault(Matches);
                    if (hit != null) return hit;
                }
                catch { }
            }

            // Last resort: search the whole project folder (3 levels above bin\Debug\netX)
            DirectoryInfo? root = new DirectoryInfo(baseDir);
            for (int i = 0; i < 3 && root?.Parent != null; i++) root = root.Parent;
            if (root != null && root.Exists)
            {
                try
                {
                    return Directory.EnumerateFiles(root.FullName, "*.*", SearchOption.AllDirectories).FirstOrDefault(Matches);
                }
                catch { }
            }
            return null;
        }

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