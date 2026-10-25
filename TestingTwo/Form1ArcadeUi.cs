using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Gui
{
    /// <summary>
    /// Arcade cabinet shell. The game is still drawn by RenderGame() into a 1024x620 picture;
    /// this file shows that picture inside the cabinet's screen and lights up the controls
    /// (joystick, six buttons, MENU and START) when the matching keys are pressed.
    /// </summary>
    public partial class Form1 : Form
    {
        // ---------------------------------------------------------------
        //  SETTINGS YOU MAY WANT TO CHANGE
        // ---------------------------------------------------------------
        private const string ArcadeTitle = "CLASH ARENA";   // text on the marquee at the top

        // ---------------------------------------------------------------
        //  LAYOUT (cabinet design units; the whole cabinet is scaled to fit your monitor)
        // ---------------------------------------------------------------
        private const int GameWidth = 1024;    // every game screen is laid out for this size
        private const int GameHeight = 620;
        private const int ArcadeW = 1240;         // cabinet design size
        private const int ArcadeH = 1130;

        private static readonly Rectangle ScreenRect = new Rectangle(108, 280, GameWidth, GameHeight);
        private static readonly Rectangle MarqueeInner = new Rectangle(44, 34, 1152, 110);

        private static readonly PointF JoystickPos = new PointF(370f, 1010f);
        private static readonly PointF MenuPos = new PointF(590f, 1000f);
        private static readonly PointF StartPos = new PointF(650f, 1000f);
        private static readonly PointF[] ButtonPos =
        {
            new PointF(830f, 988f), new PointF(906f, 988f), new PointF(982f, 988f),   // top row    = Player 1: attack, shadow, block
            new PointF(830f, 1046f), new PointF(906f, 1046f), new PointF(982f, 1046f) // bottom row = Player 2: attack, shadow, block
        };
        private static readonly Color[] ButtonColors =
        {
            Color.FromArgb(255, 205, 40), Color.FromArgb(255, 140, 30), Color.FromArgb(230, 50, 55),
            Color.FromArgb(60, 200, 95), Color.FromArgb(60, 125, 235), Color.FromArgb(255, 205, 40)
        };
        private static readonly Keys[] ButtonKeys = { Keys.F, Keys.G, Keys.T, Keys.U, Keys.O, Keys.P };
        private static readonly string[] ButtonLabels = { "F", "G", "T", "U", "O", "P" };

        private static readonly Keys[] JoyLeft = { Keys.Left, Keys.A, Keys.J };
        private static readonly Keys[] JoyRight = { Keys.Right, Keys.D, Keys.L };
        private static readonly Keys[] JoyUp = { Keys.Up, Keys.W, Keys.I };
        private static readonly Keys[] JoyDown = { Keys.Down, Keys.S, Keys.K };
        private static readonly Keys[] MenuKeys = { Keys.Back };
        private static readonly Keys[] StartKeys = { Keys.Enter, Keys.Space };

        // ---------------------------------------------------------------
        //  STATE
        // ---------------------------------------------------------------
        // The game code asks ClientSize for the size of the game "screen" (1024x620).
        // Inside the cabinet that is the game picture, not the whole window, so we answer with that.
        // (The setter is only here because Form1.Designer.cs assigns ClientSize.)
        private new Size ClientSize
        {
            get => new Size(GameWidth, GameHeight);
            set => base.ClientSize = value;
        }

        private float arcadeScaleX = 1f, arcadeScaleY = 1f;
        private Rectangle screenDevice;                 // where the game picture lands in the window (pixels)
        private Bitmap? arcadeLayer;                   // the cabinet itself, drawn once
        private Bitmap? glassLayer;                     // scanlines, vignette and glare over the screen, drawn once
        private Bitmap? gameBuffer;                     // the game is drawn here every frame
        private readonly ImageAttributes screenAttributes = new ImageAttributes();

        private readonly HashSet<Keys> heldKeys = new HashSet<Keys>();
        private readonly Dictionary<Keys, long> keyTapTicks = new Dictionary<Keys, long>();
        private float joyX = 0f, joyY = 0f;             // eased joystick tilt

        private bool OnMenuScreen =>
            currentState == GameState.ModeSelect || currentState == GameState.CharSelect ||
            currentState == GameState.ArenaSelect;

        // ---------------------------------------------------------------
        //  SETUP (called from the Form1 constructor)
        // ---------------------------------------------------------------
        private void InitArcadeUi()
        {
            AutoScaleMode = AutoScaleMode.None;   // exact pixel size, no surprise Windows display-scaling

            // Shrink the cabinet if the monitor is too small to show it at full size
            Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
            float fit = Math.Min((area.Width - 24f) / ArcadeW, (area.Height - 80f) / ArcadeH);
            float scale = Math.Min(1f, Math.Max(0.4f, fit));
            int w = (int)Math.Round(ArcadeW * scale);
            int h = (int)Math.Round(ArcadeH * scale);

            base.ClientSize = new Size(w, h);
            arcadeScaleX = w / (float)ArcadeW;
            arcadeScaleY = h / (float)ArcadeH;
            screenDevice = Rectangle.Round(new RectangleF(
                ScreenRect.X * arcadeScaleX, ScreenRect.Y * arcadeScaleY,
                ScreenRect.Width * arcadeScaleX, ScreenRect.Height * arcadeScaleY));

            BackColor = Color.FromArgb(8, 6, 18);
            StartPosition = FormStartPosition.CenterScreen;
            screenAttributes.SetWrapMode(WrapMode.TileFlipXY);   // no light edge when the picture is scaled
            gameBuffer = new Bitmap(GameWidth, GameHeight, PixelFormat.Format32bppPArgb);

            // Remember which keys are down so the cabinet controls can light up
            KeyDown += (sender, e) =>
            {
                if (heldKeys.Add(e.KeyCode)) keyTapTicks[e.KeyCode] = Environment.TickCount64;
            };
            KeyUp += (sender, e) => heldKeys.Remove(e.KeyCode);
            Deactivate += (sender, e) => heldKeys.Clear();

            FormClosed += (sender, e) =>
            {
                arcadeLayer?.Dispose();
                glassLayer?.Dispose();
                gameBuffer?.Dispose();
                screenAttributes.Dispose();
            };
        }

        // A key counts as "lit" while held, and for a moment after a quick tap
        private bool IsLit(Keys[] keys)
        {
            long now = Environment.TickCount64;
            foreach (Keys k in keys)
            {
                if (heldKeys.Contains(k)) return true;
                if (keyTapTicks.TryGetValue(k, out long tapped) && now - tapped < 140) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------
        //  PAINTING
        // ---------------------------------------------------------------
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Everything is painted in OnPaint, so skipping the background avoids flicker.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (arcadeLayer == null || glassLayer == null) BuildArcadeLayers();

            // 1. The game draws itself into its own 1024x620 picture
            using (Graphics gg = Graphics.FromImage(gameBuffer!))
            {
                gg.Clear(Color.FromArgb(10, 10, 15));
                RenderGame(gg);
            }

            // 2. Cabinet, then the game picture inside the screen, then the glass on top
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(arcadeLayer!, 0, 0, arcadeLayer!.Width, arcadeLayer.Height);
            g.DrawImage(gameBuffer!, screenDevice, 0, 0, GameWidth, GameHeight, GraphicsUnit.Pixel, screenAttributes);
            g.DrawImage(glassLayer!, screenDevice.X, screenDevice.Y, glassLayer!.Width, glassLayer.Height);

            // 3. Parts that move: marquee glow, joystick, buttons
            DrawArcadeLive(g);
        }

        private void BuildArcadeLayers()
        {
            int w = base.ClientSize.Width, h = base.ClientSize.Height;

            arcadeLayer = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(arcadeLayer))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.ScaleTransform(arcadeScaleX, arcadeScaleY);
                DrawArcadeBody(g);
            }

            glassLayer = new Bitmap(screenDevice.Width, screenDevice.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(glassLayer))
            {
                DrawScreenGlass(g, glassLayer.Width, glassLayer.Height);
            }
        }

        // ---------------------------------------------------------------
        //  STATIC CABINET (drawn once)
        // ---------------------------------------------------------------
        private void DrawArcadeBody(Graphics g)
        {
            // Dark room behind the cabinet
            using (LinearGradientBrush room = new LinearGradientBrush(new Rectangle(0, 0, ArcadeW, ArcadeH),
                Color.FromArgb(16, 11, 34), Color.FromArgb(4, 3, 10), LinearGradientMode.Vertical))
            {
                g.FillRectangle(room, 0, 0, ArcadeW, ArcadeH);
            }

            Color edgeDark = Color.FromArgb(7, 20, 28);
            using LinearGradientBrush bodyBrush = new LinearGradientBrush(new Rectangle(0, 0, ArcadeW, ArcadeH),
                Color.Black, Color.Black, LinearGradientMode.Horizontal);
            bodyBrush.InterpolationColors = new ColorBlend
            {
                Positions = new[] { 0f, 0.5f, 1f },
                Colors = new[] { Color.FromArgb(16, 40, 54), Color.FromArgb(34, 84, 106), Color.FromArgb(16, 40, 54) }
            };

            // Body: speaker section, screen section and the wider control-panel base
            PointF[] body =
            {
                new PointF(46, 140), new PointF(1194, 140), new PointF(1162, 250), new PointF(1162, 926),
                new PointF(1200, 1090), new PointF(40, 1090), new PointF(78, 926), new PointF(78, 250)
            };
            g.FillPolygon(bodyBrush, body);
            using (Pen outline = new Pen(edgeDark, 6f) { LineJoin = LineJoin.Round })
            {
                g.DrawPolygon(outline, body);
            }
            using (Pen shine = new Pen(Color.FromArgb(70, 130, 160, 175), 2f))
            {
                g.DrawLine(shine, 90, 262, 90, 915);
                g.DrawLine(shine, 1150, 262, 1150, 915);
            }

            DrawMarquee(g, bodyBrush, edgeDark);

            DrawSpeaker(g, 347f, 205f);
            DrawSpeaker(g, 893f, 205f);

            // Screen bezel
            Rectangle bezel = Rectangle.Inflate(ScreenRect, 24, 24);
            using (GraphicsPath bezelPath = RoundedRect(bezel, 16))
            using (LinearGradientBrush bezelBrush = new LinearGradientBrush(bezel,
                Color.FromArgb(8, 20, 28), Color.FromArgb(18, 44, 58), LinearGradientMode.Vertical))
            using (Pen bezelEdge = new Pen(Color.FromArgb(5, 14, 20), 4f))
            {
                g.FillPath(bezelBrush, bezelPath);
                g.DrawPath(bezelEdge, bezelPath);
            }
            // Screen light spilling onto the bezel
            Rectangle well = Rectangle.Inflate(ScreenRect, 6, 6);
            for (int i = 5; i >= 1; i--)
            {
                using (Pen spill = new Pen(Color.FromArgb(14, 70, 170, 220), 4f + (i * 3f)))
                {
                    g.DrawRectangle(spill, well);
                }
            }
            g.FillRectangle(Brushes.Black, well);
            using (Pen glowEdge = new Pen(Color.FromArgb(170, 70, 165, 205), 3f))
            {
                g.DrawRectangle(glowEdge, Rectangle.Inflate(ScreenRect, 4, 4));
            }

            DrawControlPanel(g, edgeDark);
        }

        private void DrawMarquee(Graphics g, Brush bodyBrush, Color edgeDark)
        {
            Rectangle outer = new Rectangle(16, 12, 1208, 148);
            using (GraphicsPath outerPath = RoundedRect(outer, 26))
            using (Pen outline = new Pen(edgeDark, 6f))
            {
                g.FillPath(bodyBrush, outerPath);
                g.DrawPath(outline, outerPath);
            }

            using (GraphicsPath inner = RoundedRect(MarqueeInner, 10))
            using (LinearGradientBrush panel = new LinearGradientBrush(MarqueeInner,
                Color.FromArgb(52, 34, 118), Color.FromArgb(104, 62, 172), LinearGradientMode.Vertical))
            {
                g.FillPath(panel, inner);

                // Title with a soft glow
                using Font titleFont = new Font("Impact", 72f, FontStyle.Regular, GraphicsUnit.Pixel);
                using StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                RectangleF textBox = new RectangleF(MarqueeInner.X, MarqueeInner.Y + 12, MarqueeInner.Width, MarqueeInner.Height - 24);
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(34, 255, 90, 225)))
                {
                    for (int dx = -4; dx <= 4; dx += 4)
                        for (int dy = -4; dy <= 4; dy += 4)
                            g.DrawString(ArcadeTitle, titleFont, glow, new RectangleF(textBox.X + dx, textBox.Y + dy, textBox.Width, textBox.Height), center);
                }
                using (LinearGradientBrush textBrush = new LinearGradientBrush(textBox,
                    Color.FromArgb(255, 170, 255), Color.FromArgb(190, 90, 240), LinearGradientMode.Vertical))
                {
                    g.DrawString(ArcadeTitle, titleFont, textBrush, textBox, center);
                }

                // LED dot-matrix look: a fine dark grid over the whole panel
                GraphicsState before = g.Save();
                g.SetClip(inner, CombineMode.Intersect);
                g.SmoothingMode = SmoothingMode.None;
                using (Pen grid = new Pen(Color.FromArgb(85, 10, 4, 30), 1f))
                {
                    for (int x = MarqueeInner.X; x < MarqueeInner.Right; x += 4) g.DrawLine(grid, x, MarqueeInner.Y, x, MarqueeInner.Bottom);
                    for (int y = MarqueeInner.Y; y < MarqueeInner.Bottom; y += 4) g.DrawLine(grid, MarqueeInner.X, y, MarqueeInner.Right, y);
                }
                g.Restore(before);
            }

            // Accent lines above and below the title
            using (Pen blue = new Pen(Color.FromArgb(230, 90, 120, 255), 3f))
            using (Pen yellow = new Pen(Color.FromArgb(230, 235, 235, 120), 3f))
            {
                g.DrawLine(blue, 190, MarqueeInner.Y + 15, 1050, MarqueeInner.Y + 15);
                g.DrawLine(yellow, 190, MarqueeInner.Bottom - 13, 1050, MarqueeInner.Bottom - 13);
            }
            using (GraphicsPath inner = RoundedRect(MarqueeInner, 10))
            using (Pen pink = new Pen(Color.FromArgb(150, 255, 120, 235), 7f))
            {
                g.DrawPath(pink, inner);
            }
        }

        private void DrawSpeaker(Graphics g, float cx, float cy)
        {
            const float R = 40f;
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillEllipse(shadow, cx - R - 2, cy - R + 4, (R * 2) + 4, (R * 2) + 4);
            }
            using (LinearGradientBrush rim = new LinearGradientBrush(new RectangleF(cx - R, cy - R, R * 2, R * 2),
                Color.FromArgb(125, 145, 155), Color.FromArgb(24, 34, 42), LinearGradientMode.ForwardDiagonal))
            {
                g.FillEllipse(rim, cx - R, cy - R, R * 2, R * 2);
            }
            using (SolidBrush well = new SolidBrush(Color.FromArgb(12, 20, 28)))
            {
                g.FillEllipse(well, cx - 31, cy - 31, 62, 62);
            }
            using (Pen ring = new Pen(Color.FromArgb(48, 82, 98), 2f))
            {
                foreach (float r in new[] { 27f, 20f, 14f }) g.DrawEllipse(ring, cx - r, cy - r, r * 2, r * 2);
            }
            using (SolidBrush cone = new SolidBrush(Color.FromArgb(34, 66, 82)))
            {
                g.FillEllipse(cone, cx - 9, cy - 9, 18, 18);
            }
            using (SolidBrush bolt = new SolidBrush(Color.FromArgb(185, 200, 208)))
            {
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4.0;
                    g.FillEllipse(bolt, cx + (float)(Math.Cos(a) * 35.5) - 2.5f, cy + (float)(Math.Sin(a) * 35.5) - 2.5f, 5f, 5f);
                }
            }
        }

        private void DrawControlPanel(Graphics g, Color edgeDark)
        {
            PointF[] face = { new PointF(78, 926), new PointF(1162, 926), new PointF(1200, 1090), new PointF(40, 1090) };

            using (LinearGradientBrush faceBrush = new LinearGradientBrush(new Rectangle(40, 926, 1160, 164),
                Color.FromArgb(40, 86, 108), Color.FromArgb(17, 42, 56), LinearGradientMode.Vertical))
            {
                g.FillPolygon(faceBrush, face);
            }

            // Faint scratches on the panel
            GraphicsState before = g.Save();
            using (GraphicsPath clip = new GraphicsPath())
            {
                clip.AddPolygon(face);
                g.SetClip(clip, CombineMode.Intersect);
            }
            Random scratch = new Random(11);
            using (Pen scratchPen = new Pen(Color.FromArgb(26, 255, 255, 255), 1f))
            {
                for (int i = 0; i < 70; i++)
                {
                    float x = scratch.Next(60, 1180), y = scratch.Next(932, 1085);
                    g.DrawLine(scratchPen, x, y, x + scratch.Next(-60, 60), y + scratch.Next(-8, 9));
                }
            }
            g.Restore(before);

            using (Pen top = new Pen(Color.FromArgb(150, 110, 180, 200), 3f))
            using (Pen outline = new Pen(edgeDark, 5f) { LineJoin = LineJoin.Round })
            {
                g.DrawLine(top, 80, 928, 1160, 928);
                g.DrawPolygon(outline, face);
            }

            // Front lip and the shadow under the cabinet
            using (SolidBrush floorShadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                g.FillEllipse(floorShadow, 30, 1108, 1180, 28);
            }
            Rectangle lip = new Rectangle(40, 1088, 1160, 34);
            using (GraphicsPath lipPath = RoundedRect(lip, 10))
            using (LinearGradientBrush lipBrush = new LinearGradientBrush(lip, Color.FromArgb(22, 54, 70), Color.FromArgb(8, 22, 30), LinearGradientMode.Vertical))
            using (Pen lipEdge = new Pen(edgeDark, 4f))
            {
                g.FillPath(lipBrush, lipPath);
                g.DrawPath(lipEdge, lipPath);
            }

            // Joystick base and direction markers
            using (SolidBrush baseDark = new SolidBrush(Color.FromArgb(6, 14, 20)))
            using (Pen baseRim = new Pen(Color.FromArgb(80, 120, 140), 3f))
            using (SolidBrush mark = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
            {
                g.FillEllipse(baseDark, JoystickPos.X - 40, JoystickPos.Y - 40, 80, 80);
                g.DrawEllipse(baseRim, JoystickPos.X - 40, JoystickPos.Y - 40, 80, 80);
                foreach (PointF d in new[] { new PointF(-60, 0), new PointF(60, 0), new PointF(0, 52), new PointF(0, -52) })
                {
                    g.FillEllipse(mark, JoystickPos.X + d.X - 3.5f, JoystickPos.Y + d.Y - 3.5f, 7f, 7f);
                }
            }

            // Button wells and labels
            using SolidBrush wellBrush = new SolidBrush(Color.FromArgb(5, 12, 18));
            using Pen wellRim = new Pen(Color.FromArgb(70, 110, 130), 2f);
            foreach (PointF p in ButtonPos)
            {
                g.FillEllipse(wellBrush, p.X - 32, p.Y - 32, 64, 64);
                g.DrawEllipse(wellRim, p.X - 32, p.Y - 32, 64, 64);
            }
            foreach (PointF p in new[] { MenuPos, StartPos })
            {
                g.FillEllipse(wellBrush, p.X - 19, p.Y - 19, 38, 38);
                g.DrawEllipse(wellRim, p.X - 19, p.Y - 19, 38, 38);
            }

            using Font labelFont = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
            using SolidBrush labelBrush = new SolidBrush(Color.FromArgb(170, 205, 215));
            DrawCentered(g, "MENU", labelFont, labelBrush, MenuPos.X, MenuPos.Y + 34);
            DrawCentered(g, "START", labelFont, labelBrush, StartPos.X, StartPos.Y + 34);
            DrawCentered(g, "P1", labelFont, labelBrush, 764, ButtonPos[0].Y);
            DrawCentered(g, "P2", labelFont, labelBrush, 764, ButtonPos[3].Y);
        }

        // Scanlines, dark edges, rounded corners and a glare streak over the game picture
        private void DrawScreenGlass(Graphics g, int w, int h)
        {
            g.SmoothingMode = SmoothingMode.None;
            using (Pen scan = new Pen(Color.FromArgb(32, 0, 0, 0), 1f))
            {
                for (int y = 0; y < h; y += 3) g.DrawLine(scan, 0, y, w, y);
            }

            // Vignette: each edge fades from dark to clear
            const int fade = 70;
            using (LinearGradientBrush top = new LinearGradientBrush(new Rectangle(0, 0, w, fade), Color.FromArgb(70, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Vertical))
            using (LinearGradientBrush bottom = new LinearGradientBrush(new Rectangle(0, h - fade, w, fade), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(70, 0, 0, 0), LinearGradientMode.Vertical))
            using (LinearGradientBrush left = new LinearGradientBrush(new Rectangle(0, 0, fade, h), Color.FromArgb(70, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Horizontal))
            using (LinearGradientBrush right = new LinearGradientBrush(new Rectangle(w - fade, 0, fade, h), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(70, 0, 0, 0), LinearGradientMode.Horizontal))
            {
                g.FillRectangle(top, 0, 0, w, fade);
                g.FillRectangle(bottom, 0, h - fade, w, fade);
                g.FillRectangle(left, 0, 0, fade, h);
                g.FillRectangle(right, w - fade, 0, fade, h);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Glass reflection in the top-left
            PointF[] streak = { new PointF(0, 0), new PointF(w * 0.42f, 0), new PointF(0, h * 0.30f) };
            using (LinearGradientBrush glare = new LinearGradientBrush(new PointF(0, 0), new PointF(w * 0.26f, h * 0.18f),
                Color.FromArgb(40, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)))
            {
                g.FillPolygon(glare, streak);
            }

            // Rounded screen corners (everything outside the rounded rectangle is painted bezel-black)
            using (GraphicsPath corners = new GraphicsPath(FillMode.Alternate))
            {
                corners.AddRectangle(new RectangleF(-1, -1, w + 2, h + 2));
                using (GraphicsPath rounded = RoundedRect(new RectangleF(0, 0, w, h), 22f))
                {
                    corners.AddPath(rounded, false);
                }
                g.FillPath(Brushes.Black, corners);
            }
        }

        // ---------------------------------------------------------------
        //  LIVE PARTS (drawn every frame)
        // ---------------------------------------------------------------
        private void DrawArcadeLive(Graphics g)
        {
            GraphicsState saved = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.ScaleTransform(arcadeScaleX, arcadeScaleY);
            long now = Environment.TickCount64;

            // Marquee neon border breathes
            int glow = 70 + (int)(60 * (1 + Math.Sin(now / 400.0)));
            using (GraphicsPath marquee = RoundedRect(MarqueeInner, 10))
            using (Pen neon = new Pen(Color.FromArgb(glow, 255, 120, 235), 7f))
            {
                g.DrawPath(neon, marquee);
            }

            // Joystick follows the arrow keys / A D W / J L I
            float targetX = ((IsLit(JoyLeft) ? -1f : 0f) + (IsLit(JoyRight) ? 1f : 0f)) * 17f;
            float targetY = ((IsLit(JoyUp) ? -1f : 0f) + (IsLit(JoyDown) ? 1f : 0f)) * 11f;
            joyX += (targetX - joyX) * 0.4f;
            joyY += (targetY - joyY) * 0.4f;
            DrawJoystick(g);

            // Six action buttons light up with F G T (player 1) and U O P (player 2)
            for (int i = 0; i < ButtonPos.Length; i++)
            {
                DrawLiveButton(g, ButtonPos[i].X, ButtonPos[i].Y, 25f, ButtonColors[i], IsLit(new[] { ButtonKeys[i] }), ButtonLabels[i], false);
            }

            // MENU = Backspace, START = Enter / Space (START pulses on menu screens)
            DrawLiveButton(g, MenuPos.X, MenuPos.Y, 13f, Color.FromArgb(240, 150, 40), IsLit(MenuKeys), "", false);
            DrawLiveButton(g, StartPos.X, StartPos.Y, 13f, Color.FromArgb(205, 210, 215), IsLit(StartKeys), "", OnMenuScreen);

            g.Restore(saved);
        }

        private void DrawJoystick(Graphics g)
        {
            PointF pivot = JoystickPos;
            PointF ball = new PointF(pivot.X + joyX, pivot.Y - 32f + (joyY * 0.6f));

            using (Pen shadow = new Pen(Color.FromArgb(120, 0, 0, 0), 13f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (Pen shaft = new Pen(Color.FromArgb(185, 190, 198), 10f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(shadow, pivot.X + 2, pivot.Y + 2, ball.X + 2, ball.Y + 2);
                g.DrawLine(shaft, pivot, ball);
            }

            const float R = 27f;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(ball.X - R, ball.Y - R, R * 2, R * 2);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = new PointF(ball.X - 8f, ball.Y - 10f);
                    brush.CenterColor = Color.FromArgb(255, 140, 130);
                    brush.SurroundColors = new[] { Color.FromArgb(185, 25, 30) };
                    g.FillPath(brush, path);
                }
            }
            using (Pen rim = new Pen(Color.FromArgb(120, 60, 0, 0), 2f))
            {
                g.DrawEllipse(rim, ball.X - R, ball.Y - R, R * 2, R * 2);
            }
        }

        private void DrawLiveButton(Graphics g, float cx, float cy, float r, Color color, bool lit, string label, bool hint)
        {
            if (lit)
            {
                for (int i = 3; i >= 1; i--)
                {
                    float gr = r + (i * 6f);
                    using (SolidBrush halo = new SolidBrush(Color.FromArgb(30, color)))
                    {
                        g.FillEllipse(halo, cx - gr, cy - gr, gr * 2, gr * 2);
                    }
                }
            }
            else if (hint)
            {
                int pulse = 25 + (int)(55 * (1 + Math.Sin(Environment.TickCount64 / 300.0)));
                float gr = r + 8f;
                using (SolidBrush halo = new SolidBrush(Color.FromArgb(pulse, 255, 200, 80)))
                {
                    g.FillEllipse(halo, cx - gr, cy - gr, gr * 2, gr * 2);
                }
            }

            Color body = lit ? color : Dim(color, 0.62f);
            float drop = lit ? 2f : 0f;   // a pressed button sinks a little
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(cx - r, cy - r + drop, r * 2, r * 2);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = new PointF(cx - (r * 0.3f), cy - (r * 0.35f) + drop);
                    brush.CenterColor = Lighten(body, 0.55f);
                    brush.SurroundColors = new[] { body };
                    g.FillPath(brush, path);
                }
            }
            using (Pen rim = new Pen(Color.FromArgb(130, 0, 0, 0), 2f))
            {
                g.DrawEllipse(rim, cx - r, cy - r + drop, r * 2, r * 2);
            }

            if (label.Length > 0)
            {
                using Font labelFont = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Pixel);
                using SolidBrush labelBrush = new SolidBrush(Color.FromArgb(lit ? 200 : 150, 0, 0, 0));
                DrawCentered(g, label, labelFont, labelBrush, cx, cy + drop);
            }
        }

        // ---------------------------------------------------------------
        //  SMALL HELPERS
        // ---------------------------------------------------------------
        private static void DrawCentered(Graphics g, string text, Font font, Brush brush, float cx, float cy)
        {
            using StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brush, new RectangleF(cx - 100, cy - 50, 200, 100), center);
        }

        private static Color Dim(Color c, float factor)
        {
            return Color.FromArgb(c.A, (int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor));
        }

        private static Color Lighten(Color c, float amount)
        {
            return Color.FromArgb(c.A, c.R + (int)((255 - c.R) * amount), c.G + (int)((255 - c.G) * amount), c.B + (int)((255 - c.B) * amount));
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}