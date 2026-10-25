using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gui
{
    /// <summary>
    /// In-match HUD: health bars, age tags, round wins, Chrono-Shadow meters, combo text, and announcements.
    /// </summary>
    public partial class Form1 : Form
    {
        private void DrawModernHUD(Graphics g)
        {
            int w = this.ClientSize.Width;
            int barW = 380, barH = 26, topY = 32;

            DrawFighterPanel(g, 1, 50, barW, barH, topY);
            DrawFighterPanel(g, 2, w - 50 - barW, barW, barH, topY);

            // Round timer
            string t = roundTimer.ToString();
            using (Font tf = new Font("Impact", 36, FontStyle.Bold))
            {
                SizeF sz = g.MeasureString(t, tf);
                g.DrawString(t, tf, Brushes.Yellow, (w / 2f) - (sz.Width / 2f), topY - 10);
            }

            // Round number and round wins (two pips each, filled once a round is won)
            using (Font rf = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                string roundLabel = RoundLabel();
                SizeF rs = g.MeasureString(roundLabel, rf);
                g.DrawString(roundLabel, rf, Brushes.Silver, (w / 2f) - (rs.Width / 2f), topY + 44);
            }
            for (int i = 0; i < GameConfig.RoundsToWin; i++)
            {
                DrawWinPip(g, (w / 2f) - 38 - (i * 20), topY + 66, i < p1RoundWins, Color.Cyan);
                DrawWinPip(g, (w / 2f) + 24 + (i * 20), topY + 66, i < p2RoundWins, Color.OrangeRed);
            }

            if (p1ComboHits > 1)
            {
                using (Font cf = new Font("Impact", 28, FontStyle.Italic))
                {
                    g.DrawString($"{p1ComboHits} HITS COMBO!", cf, Brushes.Red, 60, 130);
                    g.DrawString($"{p1ComboHits} HITS COMBO!", cf, Brushes.Gold, 58, 128);
                }
            }
            if (p2ComboHits > 1)
            {
                using (Font cf = new Font("Impact", 28, FontStyle.Italic))
                {
                    g.DrawString($"{p2ComboHits} HITS COMBO!", cf, Brushes.Red, w - 280, 130);
                    g.DrawString($"{p2ComboHits} HITS COMBO!", cf, Brushes.Gold, w - 282, 128);
                }
            }

            using (Font gf = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                string helpControls = isVsCPU
                    ? "[A/D] Move (tap twice = Dash)  [W] Jump  [S] Duck  [Q/E/R] Attack  [S+Q/E] Down Atk  [W+Q/E] Jump Atk  [X] Shadow (mid-attack)  [T] Block"
                    : "P1: [A/D/W/S] Move  [Q/E/R] Attack  [X] Shadow  [T] Block   |   P2: [J/L/I/K] Move  [U/O/P] Attack  [M] Shadow  [Y] Block";
                g.DrawString(helpControls, gf, Brushes.Silver, 20, this.ClientSize.Height - 26);
            }
        }

        private static void DrawWinPip(Graphics g, float x, float y, bool won, Color color)
        {
            RectangleF r = new RectangleF(x, y, 14, 14);
            if (won)
            {
                using (SolidBrush fill = new SolidBrush(color)) g.FillEllipse(fill, r);
            }
            using (Pen outline = new Pen(won ? Color.White : Color.Gray, 2)) g.DrawEllipse(outline, r);
        }

        // Health bar, age tag and Chrono-Shadow meter for one fighter. Player 1 is on the left, player 2 on the right.
        private void DrawFighterPanel(Graphics g, int who, int barX, int barW, int barH, int topY)
        {
            bool left = who == 1;
            Player sp = SpriteOf(who);
            int health = left ? p1Health : p2Health;
            int redHealth = left ? p1RedHealth : p2RedHealth;

            using (SolidBrush glass = new SolidBrush(Color.FromArgb(160, 20, 20, 30)))
            {
                g.FillRectangle(glass, barX - 6, topY - 6, barW + 12, barH + 66);
            }
            using (Pen frame = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
            {
                g.DrawRectangle(frame, barX - 6, topY - 6, barW + 12, barH + 66);
            }

            // The bars fill toward the center. A fighter who has aged has a smaller health pool, so the
            // outer part of the bar is greyed out: that is the max HP lost to The Mortal Toll.
            int redW = (int)(barW * ((float)redHealth / MaxHealth));
            int curW = (int)(barW * ((float)health / MaxHealth));
            int maxW = (int)(barW * (sp.MaxHealth / MaxHealth));
            int lostW = barW - maxW;

            int redX = left ? barX + (barW - redW) : barX;
            g.FillRectangle(Brushes.DarkRed, redX, topY, redW, barH);

            if (curW > 0)
            {
                int curX = left ? barX + (barW - curW) : barX;
                Color c1 = left ? Color.Gold : Color.OrangeRed;
                Color c2 = left ? Color.OrangeRed : Color.Gold;
                using (LinearGradientBrush gb = new LinearGradientBrush(new Rectangle(barX, topY, barW, barH), c1, c2, LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(gb, curX, topY, curW, barH);
                }
            }

            if (lostW > 0)
            {
                int lostX = left ? barX : barX + barW - lostW;
                using (SolidBrush lost = new SolidBrush(Color.FromArgb(200, 25, 25, 35)))
                using (HatchBrush hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(90, 120, 120, 140), Color.FromArgb(200, 25, 25, 35)))
                {
                    g.FillRectangle(hatch, lostX, topY, lostW, barH);
                }
            }
            using (Pen border = new Pen(Color.White, 2)) g.DrawRectangle(border, barX, topY, barW, barH);

            // Name and age
            string name = FighterDisplayName(who);
            string age = CombatRules.AgeName(sp.CurrentAge);
            Color ageColor = sp.CurrentAge == AgeStage.Youth ? Color.LimeGreen : (sp.CurrentAge == AgeStage.Veteran ? Color.Orange : Color.Crimson);
            using (Font nf = new Font("Impact", 14, FontStyle.Italic))
            using (Font af = new Font("Impact", 11))
            using (SolidBrush ageBrush = new SolidBrush(ageColor))
            {
                SizeF ns = g.MeasureString(name, nf);
                SizeF ags = g.MeasureString(age, af);
                if (left)
                {
                    g.DrawString(name, nf, Brushes.White, barX, topY + barH + 2);
                    g.DrawString(age, af, ageBrush, barX + ns.Width, topY + barH + 5);
                }
                else
                {
                    g.DrawString(name, nf, Brushes.White, barX + barW - ns.Width, topY + barH + 2);
                    g.DrawString(age, af, ageBrush, barX + barW - ns.Width - ags.Width, topY + barH + 5);
                }
            }

            // Chrono-Shadow meter. The notch marks how much one phantom costs.
            int meterY = topY + barH + 24;
            int meterH = 16;
            int charges = (int)(sp.ShadowMeter / GameConfig.ShadowCost);
            bool ready = charges > 0;

            using (SolidBrush back = new SolidBrush(Color.FromArgb(255, 10, 20, 40)))
            {
                g.FillRectangle(back, barX, meterY, barW, meterH);
            }

            int meterW = (int)(barW * (sp.ShadowMeter / GameConfig.MaxShadowMeter));
            if (meterW > 0)
            {
                int meterX = left ? barX + (barW - meterW) : barX;
                Color m1 = ready ? Color.DeepSkyBlue : Color.SteelBlue;
                Color m2 = ready ? Color.White : Color.LightSteelBlue;
                using (LinearGradientBrush mb = new LinearGradientBrush(new Rectangle(meterX, meterY, Math.Max(1, meterW), meterH), m1, m2, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(mb, meterX, meterY, meterW, meterH);
                }
            }

            int costW = (int)(barW * (GameConfig.ShadowCost / GameConfig.MaxShadowMeter));
            int notchX = left ? barX + barW - costW : barX + costW;
            g.DrawLine(Pens.White, notchX, meterY - 3, notchX, meterY + meterH + 3);
            using (Pen meterBorder = new Pen(ready ? Color.DeepSkyBlue : Color.LightSteelBlue, 2))
            {
                g.DrawRectangle(meterBorder, barX, meterY, barW, meterH);
            }

            using (Font lf = new Font("Segoe UI", 9, FontStyle.Bold))
            {
                string label = ready ? $"CHRONO-SHADOW  x{charges}  READY" : "CHRONO-SHADOW";
                SizeF ls = g.MeasureString(label, lf);
                float lx = left ? barX : barX + barW - ls.Width;
                float ly = meterY + meterH + 3;
                Brush main = ready ? Brushes.DeepSkyBlue : Brushes.White;
                g.DrawString(label, lf, Brushes.Black, lx + 1, ly + 1);   // outline so it reads on any background
                g.DrawString(label, lf, main, lx, ly);
            }
        }

        private void DrawTextCentered(Graphics g, string text, Font font, Brush brush, int width, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (width / 2f) - (size.Width / 2f), y);
        }

        // Small PAUSE button at the top of the screen, and the dimmed pause menu
        private void DrawPauseUi(Graphics g, int w, int h)
        {
            Rectangle b = PauseButtonRect;
            using (SolidBrush back = new SolidBrush(Color.FromArgb(170, 15, 15, 25)))
            using (Pen edge = new Pen(Color.FromArgb(200, 255, 255, 255), 1))
            using (Font f = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                g.FillRectangle(back, b);
                g.DrawRectangle(edge, b);
                DrawTextCentered(g, paused ? "RESUME" : "II PAUSE", f, Brushes.White, w, b.Y + 3);
            }

            if (!paused) return;

            using (SolidBrush dim = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            {
                g.FillRectangle(dim, 0, 0, w, h);
            }
            using (Font big = new Font("Impact", 54, FontStyle.Italic))
            using (Font small = new Font("Segoe UI", 14, FontStyle.Bold))
            {
                DrawTextCentered(g, "PAUSED", big, Brushes.Gold, w, 190);
                DrawTextCentered(g, "[ ESC ] or [ ENTER ]  Resume", small, Brushes.White, w, 300);
                DrawTextCentered(g, "[ R ]  Quit to menu", small, Brushes.LightGray, w, 332);
            }
        }

        private void DrawAnnouncement(Graphics g, string text, Color color)
        {
            int w = this.ClientSize.Width;
            using (Font af = new Font("Impact", 60, FontStyle.Italic))
            using (SolidBrush brush = new SolidBrush(color))
            {
                SizeF sz = g.MeasureString(text, af);
                g.DrawString(text, af, Brushes.DarkRed, (w / 2f) - (sz.Width / 2f) + 4, 184);
                g.DrawString(text, af, brush, (w / 2f) - (sz.Width / 2f), 180);
            }
        }
    }
}