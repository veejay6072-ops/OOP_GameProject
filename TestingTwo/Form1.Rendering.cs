using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gui
{
    /// <summary>
    /// Main paint routine, arena scenery, and fighter drawing (including armor and skin visuals).
    /// </summary>
    public partial class Form1 : Form
    {
        private void RenderGame(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (currentState == GameState.ModeSelect)
            {
                DrawModeSelection(g);
            }
            else if (currentState == GameState.CharSelect)
            {
                DrawCharacterSelection(g);
            }
            else if (currentState == GameState.ArenaSelect)
            {
                DrawArenaSelection(g);
            }
            else
            {
                int w = this.ClientSize.Width, h = this.ClientSize.Height;

                if (rageCinematicFlash > 0)
                {
                    using (SolidBrush darkFlash = new SolidBrush(Color.FromArgb(195, 5, 5, 10)))
                    {
                        g.FillRectangle(darkFlash, 0, 0, w, h);
                    }
                }

                // Stage image background fixed to viewport
                Bitmap? stageBg = ArenaImages.Get(GameData.Arenas[selectedArenaIndex]);
                if (stageBg != null) g.DrawImage(stageBg, 0, 0, w, h);

                // STATIC CAMERA: the world is drawn as-is, nothing follows the fighters.
                // The only camera effect left is the small shake when a hit lands.
                if (screenShake > 0)
                {
                    g.TranslateTransform(rng.Next(-screenShake, screenShake + 1), rng.Next(-screenShake, screenShake + 1));
                }

                DrawDynamicArenaScenery(g, w / 2f);

                foreach (var c in craters)
                {
                    int alpha = Math.Min(255, c.Life * 3);
                    using (Pen cp = new Pen(Color.FromArgb(alpha, 10, 10, 10), 3))
                    {
                        g.DrawLine(cp, c.X, c.Y, c.X - 18, c.Y - 6);
                        g.DrawLine(cp, c.X, c.Y, c.X + 22, c.Y - 5);
                        g.DrawLine(cp, c.X, c.Y, c.X - 8, c.Y + 8);
                        g.DrawLine(cp, c.X, c.Y, c.X + 15, c.Y + 7);
                    }
                }

                var ar = GameData.Arenas[selectedArenaIndex];
                foreach (var m in ambientMotes)
                {
                    if (ar.BackgroundFile != null) break;
                    using (SolidBrush amb = new SolidBrush(Color.FromArgb(m.Alpha, ar.ParticleColor)))
                    {
                        g.FillEllipse(amb, m.X, m.Y, m.Size, m.Size);
                    }
                }

                foreach (var t in trails)
                {
                    if (FighterSprites.Available)
                    {
                        // After-image of the real sprite pose, in the fighter's aura color
                        FighterSprites.DrawGhost(g, t.Fighter, t.Animation, t.Frame, t.X + 54f, t.Y + SpriteFootOffset, SpriteScale, t.FaceRight, t.Color, t.Alpha, t.Age);
                    }
                    else
                    {
                        using (SolidBrush tb = new SolidBrush(Color.FromArgb(Math.Max(0, t.Alpha), t.Color)))
                        {
                            g.FillEllipse(tb, t.X + 35, t.Y, 38, 44);
                            g.FillRectangle(tb, t.X + 30, t.Y + 42, 48, 58);
                        }
                    }
                }

                DrawFighterReflection(g, p1X, p1Y, p1Action, facingRight[1], GameData.Roster[p1CharIndex]);
                DrawFighterReflection(g, p2X, p2Y, p2Action, facingRight[2], GameData.Roster[p2CharIndex]);

                DrawShadows(g);

                DrawArticulatedFighter(g, p1X, p1Y, p1Action, facingRight[1], GameData.Roster[p1CharIndex], p1Sprite);
                DrawArticulatedFighter(g, p2X, p2Y, p2Action, facingRight[2], GameData.Roster[p2CharIndex], p2Sprite);

                foreach (var s in shockwaves)
                {
                    using (Pen sp = new Pen(Color.FromArgb(s.Alpha, s.Color), 4))
                    {
                        g.DrawEllipse(sp, s.Center.X - s.Radius, s.Center.Y - (s.Radius * 0.5f), s.Radius * 2, s.Radius);
                    }
                }

                foreach (var l in lightnings)
                {
                    using (Pen lp = new Pen(l.Color, 3))
                    {
                        for (int i = 0; i < l.Points.Count - 1; i++) g.DrawLine(lp, l.Points[i], l.Points[i + 1]);
                    }
                }

                foreach (var p in particles)
                {
                    int alpha = (p.Life * 255) / p.MaxLife;
                    using (SolidBrush pb = new SolidBrush(Color.FromArgb(Math.Max(0, alpha), p.Color)))
                    {
                        g.FillEllipse(pb, p.X, p.Y, p.Size, p.Size);
                    }
                }

                if (speedLineTimer > 0)
                {
                    using (Pen slp = new Pen(Color.FromArgb(160, 255, 255, 255), 2))
                    {
                        for (int a = 0; a < 360; a += 15)
                        {
                            double rad = a * Math.PI / 180.0;
                            float x1 = speedLineCenter.X + (float)(Math.Cos(rad) * 90);
                            float y1 = speedLineCenter.Y + (float)(Math.Sin(rad) * 90);
                            float x2 = speedLineCenter.X + (float)(Math.Cos(rad) * 450);
                            float y2 = speedLineCenter.Y + (float)(Math.Sin(rad) * 450);
                            g.DrawLine(slp, x1, y1, x2, y2);
                        }
                    }
                }

                g.ResetTransform();
                DrawModernHUD(g);

                DrawRoundOverlay(g, w);
                DrawPauseUi(g, w, h);
            }
        }

        // Chrono-Shadow phantoms: frozen in the pose they were dropped in, then they replay the strike
        private void DrawShadows(Graphics g)
        {
            double seconds = Environment.TickCount64 / 1000.0;

            foreach (ChronoShadow s in shadows)
            {
                float centerX = s.X + 54f;
                float footY = s.Y + SpriteFootOffset;

                int frame = s.Frame;
                int alpha = (int)(150 + (50 * Math.Sin(seconds * 6.0)));
                if (s.Executed)
                {
                    int frames = FighterSprites.PhantomFrameCount(s.FighterIndex, s.Age, s.Animation);
                    frame = Math.Min(s.ExecTicks / 4, Math.Max(0, frames - 1));
                    alpha = 235;
                }

                using (GraphicsPath glowPath = new GraphicsPath())
                {
                    glowPath.AddEllipse(s.X - 15, s.Y - 20, 150, 220);
                    using (PathGradientBrush glow = new PathGradientBrush(glowPath))
                    {
                        glow.CenterColor = Color.FromArgb(s.Executed ? 150 : 80, 80, 180, 255);
                        glow.SurroundColors = new Color[] { Color.Transparent };
                        g.FillPath(glow, glowPath);
                    }
                }

                FighterSprites.DrawPhantom(g, s.FighterIndex, s.Animation, frame, centerX, footY, SpriteScale, s.FaceRight, s.Age, alpha);

                if (!s.Executed)
                {
                    string count = Math.Ceiling(Math.Max(0f, s.Timer)).ToString();
                    using (Font cf = new Font("Impact", 22, FontStyle.Italic))
                    using (SolidBrush cb = new SolidBrush(Color.FromArgb(230, 120, 200, 255)))
                    {
                        SizeF sz = g.MeasureString(count, cf);
                        g.DrawString(count, cf, cb, centerX - (sz.Width / 2f), footY - FighterSprites.DrawnHeight(s.FighterIndex, s.Age) - 50f);
                    }
                }
            }
        }

        // "ROUND n / FIGHT!" at the start of a round and the K.O. screen between rounds
        private void DrawRoundOverlay(Graphics g, int w)
        {
            if (currentState == GameState.FightIntro)
            {
                bool announcing = introCounter > 40;
                string title = announcing ? (roundNumber >= 3 ? "FINAL ROUND" : $"ROUND {roundNumber}") : "FIGHT!";
                DrawAnnouncement(g, title, Color.Yellow);

                if (announcing)
                {
                    using (Font af = new Font("Impact", 20, FontStyle.Italic))
                    {
                        string ages = $"P1: {CombatRules.AgeName(p1Sprite.CurrentAge)}        P2: {CombatRules.AgeName(p2Sprite.CurrentAge)}";
                        DrawTextCentered(g, ages, af, Brushes.Cyan, w, 272);
                    }
                }
            }
            else if (currentState == GameState.KO)
            {
                DrawAnnouncement(g, koHeadline, Color.Red);

                using (Font wf = new Font("Impact", 24, FontStyle.Italic))
                using (Font infoFont = new Font("Segoe UI", 12, FontStyle.Bold))
                {
                    DrawTextCentered(g, roundResultText, wf, Brushes.Gold, w, 270);

                    if (matchOver)
                    {
                        using (Font mf = new Font("Impact", 30, FontStyle.Italic))
                        {
                            DrawTextCentered(g, matchResultText, mf, Brushes.Cyan, w, 320);
                        }
                        DrawTextCentered(g, "[ R ] Return to Mode Menu", infoFont, Brushes.White, w, 375);
                    }
                    else
                    {
                        DrawTextCentered(g, nextRoundText, infoFont, Brushes.OrangeRed, w, 325);
                    }
                }
            }
        }

        private bool TryDrawImageScenery(Graphics g, ArenaInfo ar, bool isPreview)
        {
            Bitmap? bg = ArenaImages.Get(ar);
            if (bg != null)
            {
                if (isPreview) g.DrawImage(bg, 0, -20, 1024, 620);
                return true;
            }

            // Fallback rendering if image file is missing
            using (LinearGradientBrush sky = new LinearGradientBrush(new Rectangle(-400, -150, 1800, 520), ar.SkyTop, ar.SkyBottom, LinearGradientMode.Vertical))
            {
                g.FillRectangle(sky, -400, -150, 1800, 520);
            }
            Point[] octagon = { new Point(20, 360), new Point(1004, 360), new Point(1200, 720), new Point(-180, 720) };
            using (SolidBrush floorB = new SolidBrush(ar.FloorColor)) g.FillPolygon(floorB, octagon);
            using (Pen ringSolid = new Pen(ar.RingNeon, 4)) g.DrawLine(ringSolid, 20, 360, 1004, 360);

            return true;
        }

        private void DrawDynamicArenaScenery(Graphics g, float midX, int arenaIndex = -1)
        {
            int arenaIdx = arenaIndex >= 0 ? arenaIndex : selectedArenaIndex;
            var ar = GameData.Arenas[arenaIdx];

            if (ar.BackgroundFile != null && TryDrawImageScenery(g, ar, arenaIndex >= 0)) return;
        }

        private void DrawArticulatedFighter(Graphics g, float x, float y, int action, bool faceRight, FighterInfo fighter, Player? sprite = null)
        {
            if (FighterSprites.Available)
            {
                DrawSpriteFighter(g, x, y, action, faceRight, fighter, sprite);
                return;
            }

            float bob = 0f; // Disabled idle up-and-down floating motion
            float cy = y + bob;

            float shadowScale = Math.Max(0.4f, 1f - ((GroundLevel - y) / 250f));
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
            {
                g.FillEllipse(sh, x + 15, GroundLevel + 160, 90 * shadowScale, 20 * shadowScale);
            }

            Color skin = (action == 4 || action == 5) ? Color.FromArgb(255, 120, 120) : Color.FromArgb(235, 195, 165);

            if (action == 3)
            {
                using (GraphicsPath auraPath = new GraphicsPath())
                {
                    auraPath.AddEllipse(x - 25, cy - 25, 170, 230);
                    using (PathGradientBrush pgb = new PathGradientBrush(auraPath))
                    {
                        pgb.CenterColor = Color.FromArgb(210, fighter.AuraColor);
                        pgb.SurroundColors = new Color[] { Color.Transparent };
                        g.FillPath(pgb, auraPath);
                    }
                }
            }


            g.FillEllipse(new SolidBrush(skin), x + 35, cy, 38, 44);
            Point[] hair = faceRight
                ? new Point[] { new Point((int)x + 35, (int)cy + 15), new Point((int)x + 22, (int)cy - 12), new Point((int)x + 60, (int)cy - 8), new Point((int)x + 75, (int)cy + 15) }
                : new Point[] { new Point((int)x + 35, (int)cy + 15), new Point((int)x + 55, (int)cy - 12), new Point((int)x + 88, (int)cy - 8), new Point((int)x + 75, (int)cy + 15) };
            g.FillPolygon(new SolidBrush(fighter.PrimaryColor == Color.FromArgb(255, 185, 0) ? Color.Gold : Color.Black), hair);

            g.FillRectangle(new SolidBrush(skin), x + 30, cy + 42, 48, 58);
            using (Pen rib = new Pen(Color.FromArgb(190, 150, 120), 2)) g.DrawLine(rib, x + 54, cy + 48, x + 54, cy + 96);

            g.FillRectangle(new SolidBrush(Color.FromArgb(15, 15, 20)), x + 25, cy + 100, 58, 20);
            using (Pen belt = new Pen(fighter.PrimaryColor, 3)) g.DrawLine(belt, x + 25, cy + 103, x + 83, cy + 103);

            if (action == 2)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(15, 15, 20)), x + 40, cy + 115, 22, 65);
                if (faceRight)
                {
                    g.FillRectangle(new SolidBrush(fighter.PrimaryColor), x + 58, cy + 48, 50, 22);
                    g.FillRectangle(new SolidBrush(fighter.PrimaryColor), x + 105, cy + 34, 55, 20);
                    g.FillRectangle(Brushes.Gold, x + 155, cy + 32, 18, 24);
                }
                else
                {
                    g.FillRectangle(new SolidBrush(fighter.PrimaryColor), x - 2, cy + 48, 50, 22);
                    g.FillRectangle(new SolidBrush(fighter.PrimaryColor), x - 52, cy + 34, 55, 20);
                    g.FillRectangle(Brushes.Gold, x - 65, cy + 32, 18, 24);
                }
            }
            else
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(15, 15, 20)), x + 26, cy + 115, 24, 65);
                g.FillRectangle(new SolidBrush(Color.FromArgb(15, 15, 20)), x + 58, cy + 115, 24, 65);
                g.FillRectangle(new SolidBrush(fighter.PrimaryColor), x + 26, cy + 172, 24, 12);
                g.FillRectangle(new SolidBrush(fighter.PrimaryColor), x + 58, cy + 172, 24, 12);
            }

            if (action == 1)
            {
                if (faceRight)
                {
                    g.FillRectangle(new SolidBrush(skin), x + 55, cy + 46, 35, 18);
                    g.FillRectangle(new SolidBrush(skin), x + 85, cy + 46, 40, 16);
                    g.FillRectangle(new SolidBrush(fighter.AuraColor), x + 120, cy + 43, 24, 22);
                }
                else
                {
                    g.FillRectangle(new SolidBrush(skin), x + 18, cy + 46, 35, 18);
                    g.FillRectangle(new SolidBrush(skin), x - 18, cy + 46, 40, 16);
                    g.FillRectangle(new SolidBrush(fighter.AuraColor), x - 36, cy + 43, 24, 22);
                }
            }
            else if (action == 3)
            {
                if (faceRight)
                {
                    g.FillRectangle(new SolidBrush(fighter.AuraColor), x + 80, cy + 38, 130, 40);
                    g.FillEllipse(Brushes.White, x + 185, cy + 30, 56, 56);
                }
                else
                {
                    g.FillRectangle(new SolidBrush(fighter.AuraColor), x - 105, cy + 38, 130, 40);
                    g.FillEllipse(Brushes.White, x - 125, cy + 30, 56, 56);
                }
            }
            else if (action != 2)
            {
                g.FillRectangle(new SolidBrush(fighter.AuraColor), x + (faceRight ? 70 : 15), cy + 55, 20, 30);
                g.FillRectangle(new SolidBrush(fighter.AuraColor), x + (faceRight ? 20 : 75), cy + 55, 20, 30);
            }

            using (Font nf = new Font("Impact", 11, FontStyle.Italic))
            {
                bool isCpu = isVsCPU && sprite != null && ReferenceEquals(sprite, p2Sprite);
                string tag = isCpu ? "CPU: " + fighter.Name : fighter.Name;
                g.DrawString(tag, nf, Brushes.White, x + 5, cy - 25);
            }
        }

        private void DrawFighterReflection(Graphics g, float x, float y, int action, bool faceRight, FighterInfo fighter)
        {
            if (FighterSprites.Available) return; // the old shape reflection looked like colored blobs under the sprite
            if (y < GroundLevel - 30) return;
            float refY = GroundLevel + 160 + (GroundLevel + 160 - (y + 160));
            using (SolidBrush refBrush = new SolidBrush(Color.FromArgb(35, fighter.AuraColor)))
            {
                g.FillEllipse(refBrush, x + 35, refY + 20, 38, 30);
                g.FillRectangle(refBrush, x + 30, refY - 20, 48, 40);
            }
        }
    }
}