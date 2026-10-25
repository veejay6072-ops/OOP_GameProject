using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Gui
{
    internal static class FighterSprites
    {
        public const int CellW = 46;
        public const int CellH = 50;

        private static readonly Dictionary<string, (string File, int Frames)> Animations = new Dictionary<string, (string File, int Frames)>
        {
            ["Idle"] = ("Idle.png", 6),
            ["Running_Walking"] = ("Walk.png", 6),
            ["Jump"] = ("Jump.png", 10),
            ["Duck"] = ("Duck.png", 4),
            ["Hurt"] = ("Hurt.png", 3),
            ["Defeated"] = ("Dead.png", 3),
            ["Punch1"] = ("Attack_1.png", 4),
            ["Punch2"] = ("Attack_2.png", 3),
            ["Kick"] = ("Attack_3.png", 4),
            ["Jump_Attack"] = ("Jump_Attack.png", 4),
            ["Down_Attack"] = ("Attack_Down.png", 6),
            ["Block"] = ("Block.png", 5)
        };

        private static Dictionary<string, Bitmap> strips = new Dictionary<string, Bitmap>();
        private static Dictionary<int, Dictionary<string, Bitmap>> recoloredStrips = new Dictionary<int, Dictionary<string, Bitmap>>();
        private static bool loadTried;
        private static readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

        private static readonly ImageAttributes normalAttributes = MakeAttributes(false);
        private static readonly ImageAttributes hurtAttributes = MakeAttributes(true);

        public static string Status
        {
            get
            {
                EnsureLoaded();
                string strips_status = strips.Count > 0 ? $"LOADED {strips.Count} STRIPS" : "MISSING SPRITES\n" + log.ToString();
                return strips_status + "\nGodot scene: " + GodotAtlas.Status;
            }
        }

        public static bool Available
        {
            get
            {
                EnsureLoaded();
                return strips.Count > 0 || GodotAtlas.Available;
            }
        }

        /// <summary>Frame count for the character of a given age, as defined in the Godot scene.</summary>
        public static int FrameCountForAge(int fighterIndex, AgeStage age, string animation)
        {
            EnsureLoaded();
            int godotFrames = GodotAtlas.FrameCount(fighterIndex, age, animation);
            return godotFrames > 0 ? godotFrames : FrameCount(fighterIndex, animation);
        }

        public static int FrameCount(int fighterIndex, string animation)
        {
            int godotFrames = GodotAtlas.FrameCount(fighterIndex, AgeStage.Youth, animation);
            if (godotFrames > 0) return godotFrames;
            if (Animations.TryGetValue(animation, out var data))
                return data.Frames;
            return 1;
        }

        // The sprite is drawn this much bigger than the old 46x50 cell (see DrawBitmap)
        private const float SizeBoost = 1.15f;

        /// <summary>How tall a fighter is drawn on screen, in pixels (used to place the countdown above a phantom).</summary>
        public static float DrawnHeight(int fighterIndex, AgeStage age, float scale = 3f)
        {
            return CellH * SizeBoost * scale;
        }

        public static void Draw(Graphics g, int fighterIndex, string animation, int frame, float centerX, float footY, float scale, bool faceRight, bool hurt, AgeStage age = AgeStage.Youth)
        {
            DrawInternal(g, fighterIndex, animation, frame, centerX, footY, scale, faceRight, hurt ? hurtAttributes : normalAttributes, age, true);
        }

        /// <summary>Draws the same sprite as a flat, see-through silhouette in one color (used for the dash after-images).</summary>
        public static void DrawGhost(Graphics g, int fighterIndex, string animation, int frame, float centerX, float footY, float scale, bool faceRight, Color tint, int alpha, AgeStage age = AgeStage.Youth)
        {
            using (ImageAttributes ghost = MakeGhostAttributes(tint, alpha))
            {
                DrawInternal(g, fighterIndex, animation, frame, centerX, footY, scale, faceRight, ghost, age, false);
            }
        }

        /// <summary>Number of frames in the phantom (Chrono-Shadow) version of an animation.</summary>
        public static int PhantomFrameCount(int fighterIndex, AgeStage age, string animation)
        {
            EnsureLoaded();
            int n = GodotAtlas.ShadowFrameCount(fighterIndex, age, animation);
            return n > 0 ? n : FrameCountForAge(fighterIndex, age, animation);
        }

        /// <summary>
        /// Draws a Chrono-Shadow phantom: the shadow_mechanic sprite set of the fighter's age when the Godot scene
        /// has it (Yshadowver / Mshadowver / Oshadowver), otherwise a glowing blue silhouette of the normal sprite.
        /// </summary>
        public static void DrawPhantom(Graphics g, int fighterIndex, string animation, int frame, float centerX, float footY, float scale, bool faceRight, AgeStage age, int alpha)
        {
            EnsureLoaded();
            if (GodotAtlas.Available && GodotAtlas.TryGetShadowFrame(fighterIndex, age, animation, frame, out Bitmap? image, out Rectangle source) && image != null)
            {
                using (ImageAttributes attributes = MakePhantomAttributes(alpha))
                {
                    DrawBitmap(g, image, source, centerX, footY, scale, faceRight, attributes);
                }
                return;
            }
            DrawGhost(g, fighterIndex, animation, frame, centerX, footY, scale, faceRight, Color.DeepSkyBlue, alpha, age);
        }

        // Translucent, with a cold blue push so every phantom reads as the same kind of thing
        private static ImageAttributes MakePhantomAttributes(int alpha)
        {
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            float a = Math.Clamp(alpha, 0, 255) / 255f;
            attributes.SetColorMatrix(new ColorMatrix(new float[][]
            {
                new float[] { 0.65f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0.90f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 1f, 0f, 0f },
                new float[] { 0f, 0f, 0f, a, 0f },
                new float[] { 0.05f, 0.18f, 0.40f, 0f, 1f }
            }));
            return attributes;
        }

        private static void DrawInternal(Graphics g, int fighterIndex, string animation, int frame, float centerX, float footY, float scale, bool faceRight, ImageAttributes attributes, AgeStage age, bool showPlaceholder)
        {
            EnsureLoaded();

            Bitmap? image = null;
            Rectangle source = Rectangle.Empty;

            // 1. The Godot scene (your youngver / middle / oldver characters) wins whenever it is loaded.
            //    If this age has no such animation (for example no "hurt"), the standing pose is used.
            if (GodotAtlas.Available)
            {
                if (!GodotAtlas.TryGetFrame(fighterIndex, age, animation, frame, out image, out source))
                {
                    GodotAtlas.TryGetFrame(fighterIndex, age, "Idle", 0, out image, out source);
                }
            }

            // The strip pictures below are only Vyte's. Never show them for another fighter: that is what made
            // Mokutin look like Vyte whenever his pictures could not be found. A magenta box marks the problem instead.
            if (image == null && fighterIndex != 0)
            {
                if (showPlaceholder) DrawMissingPlaceholder(g, centerX, footY, scale);
                return;
            }

            // 2. Otherwise use the separate strip pictures, if they exist.
            if (image == null)
            {
                if (!Animations.ContainsKey(animation)) animation = "Idle";
                Bitmap? strip = GetStrip(0, animation);
                if (strip == null) return;

                int totalFrames = Animations[animation].Frames;
                int currentFrame = Math.Clamp(frame, 0, totalFrames - 1);
                int cellW = strip.Width / totalFrames;
                image = strip;
                source = new Rectangle(currentFrame * cellW, 0, cellW, strip.Height);
            }

            DrawBitmap(g, image, source, centerX, footY, scale, faceRight, attributes);
        }

        private static void DrawBitmap(Graphics g, Bitmap image, Rectangle source, float centerX, float footY, float scale, bool faceRight, ImageAttributes attributes)
        {
            // Normalizes the sprite to the original character size
            float sizeCorrection = (float)CellH / source.Height;
            sizeCorrection *= SizeBoost; // Tweak SizeBoost to make the character slightly bigger or smaller

            int w = (int)Math.Round(source.Width * scale * sizeCorrection);
            int h = (int)Math.Round(source.Height * scale * sizeCorrection);
            Rectangle dest = new Rectangle((int)Math.Round(centerX - (w / 2f)), (int)Math.Round(footY - h), w, h);

            GraphicsState savedState = g.Save();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            if (!faceRight)
            {
                g.TranslateTransform(centerX, 0);
                g.ScaleTransform(-1, 1);
                g.TranslateTransform(-centerX, 0);
            }

            g.DrawImage(image, dest, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            g.Restore(savedState);
        }

        private static void DrawMissingPlaceholder(Graphics g, float centerX, float footY, float scale)
        {
            float h = CellH * SizeBoost * scale;
            float w = h * 0.45f;
            using (Pen outline = new Pen(Color.Magenta, 3))
            using (Font f = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                g.DrawRectangle(outline, centerX - (w / 2f), footY - h, w, h);
                g.DrawString("NO SPRITE", f, Brushes.Magenta, centerX - (w / 2f), footY - (h / 2f));
            }
        }

        private static void EnsureLoaded()
        {
            if (loadTried) return;
            loadTried = true;

            string baseDir = AppContext.BaseDirectory;
            string projectDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\"));

            foreach (var kvp in Animations)
            {
                string fileName = kvp.Value.File;
                string? foundPath = null;

                string sourcePath = Path.Combine(projectDir, "fighter_all_sets", "young", "shadow_mechanic", fileName);
                string buildPath = Path.Combine(baseDir, "fighter_all_sets", "young", "shadow_mechanic", fileName);

                if (File.Exists(sourcePath))
                {
                    foundPath = sourcePath;
                }
                else if (File.Exists(buildPath))
                {
                    foundPath = buildPath;
                }
                else
                {
                    try
                    {
                        string[] matches = Directory.GetFiles(projectDir, fileName, SearchOption.AllDirectories);
                        if (matches.Length > 0) foundPath = matches[0];
                    }
                    catch { }
                }

                if (foundPath != null)
                {
                    strips[kvp.Key] = ToFastBitmap(new Bitmap(foundPath));
                }
                else
                {
                    log.AppendLine($"Missing: {fileName}");
                }
            }
        }

        private static Bitmap? GetStrip(int fighterIndex, string animation)
        {
            if (!strips.TryGetValue(animation, out var baseStrip) || baseStrip == null) return null;

            // Bypass tinting so your sprite retains its natural colors
            return baseStrip;
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

        private static ImageAttributes MakeGhostAttributes(Color tint, int alpha)
        {
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            float a = Math.Clamp(alpha, 0, 255) / 255f * 0.6f;
            attributes.SetColorMatrix(new ColorMatrix(new float[][]
            {
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, a, 0f },
                new float[] { tint.R / 255f, tint.G / 255f, tint.B / 255f, 0f, 1f }
            }));
            return attributes;
        }

        private static ImageAttributes MakeAttributes(bool hurt)
        {
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            if (hurt)
            {
                attributes.SetColorMatrix(new ColorMatrix(new float[][]
                {
                    new float[] { 1f, 0f, 0f, 0f, 0f },
                    new float[] { 0f, 0.55f, 0f, 0f, 0f },
                    new float[] { 0f, 0f, 0.55f, 0f, 0f },
                    new float[] { 0f, 0f, 0f, 1f, 0f },
                    new float[] { 0.25f, 0f, 0f, 0f, 1f }
                }));
            }
            return attributes;
        }
    }
}