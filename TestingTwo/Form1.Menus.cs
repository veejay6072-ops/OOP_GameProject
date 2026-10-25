using System.Drawing.Drawing2D;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gui
{
    public partial class Form1 : Form
    {
        private void DrawModeSelection(Graphics g)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            g.Clear(Color.FromArgb(10, 10, 15));

            using (Font titleF = new Font("Impact", 44, FontStyle.Italic))
            {
                const string gameTitle = "Clash Arena";
                float titleX = (w / 2f) - (g.MeasureString(gameTitle, titleF).Width / 2f);
                g.DrawString(gameTitle, titleF, Brushes.OrangeRed, titleX + 3, 60);
                g.DrawString(gameTitle, titleF, Brushes.Gold, titleX, 57);
            }
            g.DrawString("SELECT BATTLE MODE", new Font("Segoe UI", 14, FontStyle.Bold), Brushes.Cyan, w / 2 - 100, 130);

            int cardW = 320, cardH = 260, y = 180;
            DrawModeCard(g, 140, y, cardW, cardH, "2-PLAYER BATTLE", "Player 1 vs Player 2 (Local PVP)", selectedMode == 0, Color.Cyan);
            DrawModeCard(g, 560, y, cardW, cardH, "ARCADE MODE (VS CPU)", "Player 1 vs Computer AI Opponent", selectedMode == 1, Color.Crimson);

            g.DrawString("[ ARROW KEYS ] Choose Mode   |   [ ENTER ] Confirm", new Font("Segoe UI", 12, FontStyle.Bold), Brushes.Gold, w / 2 - 210, h - 70);
        }

        private void DrawModeCard(Graphics g, int x, int y, int w, int h, string title, string sub, bool isSelected, Color neon, string selectedText = "★ READY TO FIGHT ★")
        {
            using (Pen p = new Pen(isSelected ? neon : Color.FromArgb(50, 50, 65), isSelected ? 4 : 2)) g.DrawRectangle(p, x, y, w, h);
            using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(x, y, w, h),
                isSelected ? Color.FromArgb(30, 25, 45) : Color.FromArgb(15, 15, 20),
                Color.FromArgb(10, 10, 15), LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, x + 2, y + 2, w - 4, h - 4);
            }
            using (Font tf = new Font("Impact", 22, FontStyle.Italic)) g.DrawString(title, tf, isSelected ? Brushes.White : Brushes.Gray, x + 25, y + 40);
            using (Font sf = new Font("Segoe UI", 10)) g.DrawString(sub, sf, Brushes.Silver, x + 25, y + 90);
            if (isSelected)
            {
                using (Font selF = new Font("Impact", 16)) g.DrawString(selectedText, selF, new SolidBrush(neon), x + 50, y + 200);
            }
        }

        private void DrawDifficultySelection(Graphics g)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            g.Clear(Color.FromArgb(10, 10, 15));

            using (Font titleF = new Font("Impact", 34, FontStyle.Italic))
            {
                const string header = "CHOOSE CPU DIFFICULTY";
                float titleX = (w / 2f) - (g.MeasureString(header, titleF).Width / 2f);
                g.DrawString(header, titleF, Brushes.Gold, titleX, 45);
            }

            string[] names = { "EASY", "MEDIUM", "HARD" };
            string[] notes =
            {
                "Slow to react and rarely defends.\nNever uses Chrono-Shadow.\nGood for learning the controls.",
                "A balanced opponent that blocks,\nducks and mixes up its attacks.\nUses Chrono-Shadow now and then.",
                "Reacts fast, blocks and ducks smartly,\ndashes in often, chains long strings\nand drops Chrono-Shadows on you."
            };
            Color[] neon = { Color.LimeGreen, Color.Gold, Color.Crimson };

            int cardW = 270, cardH = 260, gap = 30, y = 140;
            int startX = (w - ((cardW * 3) + (gap * 2))) / 2;
            for (int i = 0; i < 3; i++)
            {
                DrawModeCard(g, startX + (i * (cardW + gap)), y, cardW, cardH, names[i], notes[i], (int)cpuDifficulty == i, neon[i], "★ SELECTED ★");
            }

            const string prompt = "[ ARROWS ] Choose Difficulty   |   [ ENTER ] Confirm   |   [ BACKSPACE ] Back";
            using (Font promptF = new Font("Segoe UI", 12, FontStyle.Bold))
            {
                float px = (w / 2f) - (g.MeasureString(prompt, promptF).Width / 2f);
                g.DrawString(prompt, promptF, Brushes.Gold, px, h - 70);
            }
        }

        private void DrawCharacterSelection(Graphics g)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            g.Clear(Color.FromArgb(10, 10, 15));

            using (Font titleF = new Font("Impact", 34, FontStyle.Italic))
            {
                string header = isVsCPU ? "CHOOSE YOUR FIGHTER (VS CPU)" : "CHARACTER SELECT (PVP)";
                g.DrawString(header, titleF, Brushes.Gold, w / 2 - 200, 25);
            }

            int cardW = 210, cardH = 340, startY = 100, spacing = 240;
            int startX = (w - (((GameData.Roster.Length - 1) * spacing) + cardW)) / 2;
            for (int i = 0; i < GameData.Roster.Length; i++)
            {
                int cx = startX + (i * spacing);
                bool isP1 = p1CharIndex == i;
                bool isP2 = p2CharIndex == i;

                Color border = isP1 ? Color.Cyan : (isP2 ? Color.Crimson : Color.FromArgb(50, 50, 60));
                using (Pen bp = new Pen(border, (isP1 || isP2) ? 4 : 2)) g.DrawRectangle(bp, cx, startY, cardW, cardH);

                using (LinearGradientBrush cb = new LinearGradientBrush(new Rectangle(cx, startY, cardW, cardH),
                    Color.FromArgb(28, 28, 42), Color.FromArgb(10, 10, 15), LinearGradientMode.Vertical))
                {
                    g.FillRectangle(cb, cx + 2, startY + 2, cardW - 4, cardH - 4);
                }

                DrawArticulatedFighter(g, cx + 50, startY + 70, 0, true, GameData.Roster[i]);

                using (SolidBrush b = new SolidBrush(Color.Black)) g.FillRectangle(b, cx + 2, startY + cardH - 75, cardW - 4, 73);
                g.DrawString(GameData.Roster[i].Name, new Font("Impact", 13), Brushes.White, cx + 10, startY + cardH - 70);
                g.DrawString(GameData.Roster[i].Style, new Font("Segoe UI", 8), Brushes.LightGray, cx + 10, startY + cardH - 48);

                if (isP1) g.DrawString(p1Locked ? "★ P1 LOCKED" : "► P1 SELECT", new Font("Impact", 11), Brushes.Cyan, cx + 20, startY + 12);
                if (isP2 && p1Locked && !isVsCPU) g.DrawString("► P2 SELECT", new Font("Impact", 11), Brushes.Crimson, cx + 20, startY + 30);
            }

            string prompt = isVsCPU
                ? "[ ARROWS ] Choose Character   |   [ ENTER ] Confirm and Choose Arena   |   [ BACKSPACE ] Back to Mode"
                : (!p1Locked ? "[ ARROWS ] Choose P1  |  [ ENTER ] Lock P1" : "[ ARROWS ] Choose P2  |  [ ENTER ] Choose Arena");
            g.DrawString(prompt, new Font("Segoe UI", 12, FontStyle.Bold), Brushes.Gold, w / 2 - 320, h - 50);
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - (inv * inv * inv);
        }

        private void ChangeArena(int direction)
        {
            arenaPrevIndex = selectedArenaIndex;
            arenaTransitionDir = direction;
            selectedArenaIndex = (selectedArenaIndex + direction + GameData.Arenas.Length) % GameData.Arenas.Length;
            arenaTransition = 0f;
            Sfx.Play(520, 40);
        }

        private void DrawArenaPreview(Graphics g, int arenaIndex, Rectangle box, float offsetX)
        {
            GraphicsState state = g.Save();
            g.SetClip(box);
            g.TranslateTransform(box.X + offsetX, box.Y);
            float scale = box.Width / 1024f;
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(0, 20);

            DrawDynamicArenaScenery(g, 512f, arenaIndex);

            ArenaInfo arena = GameData.Arenas[arenaIndex];
            foreach (var m in ambientMotes)
            {
                if (arena.BackgroundFile != null) break;
                using (SolidBrush mote = new SolidBrush(Color.FromArgb(m.Alpha, arena.ParticleColor)))
                {
                    g.FillEllipse(mote, m.X, m.Y, m.Size, m.Size);
                }
            }

            g.Restore(state);
        }

        private void DrawArenaSelection(Graphics g)
        {
            int w = this.ClientSize.Width, h = this.ClientSize.Height;
            g.Clear(Color.FromArgb(10, 10, 15));

            using Font titleF = new Font("Impact", 30, FontStyle.Italic);
            using Font nameF = new Font("Impact", 16);
            using Font cardF = new Font("Impact", 11);
            using Font subF = new Font("Segoe UI", 9);
            using Font promptF = new Font("Segoe UI", 12, FontStyle.Bold);

            const string header = "SELECT BATTLE STAGE";
            SizeF headerSize = g.MeasureString(header, titleF);
            g.DrawString(header, titleF, Brushes.Cyan, (w - headerSize.Width) / 2f, 12);

            ArenaInfo current = GameData.Arenas[selectedArenaIndex];

            Rectangle box = new Rectangle((w - 520) / 2, 64, 520, 315);
            int glowAlpha = 130;
            using (Pen glow = new Pen(Color.FromArgb(glowAlpha, current.RingNeon), 10))
            {
                g.DrawRectangle(glow, box.X - 3, box.Y - 3, box.Width + 6, box.Height + 6);
            }

            if (arenaTransition < 1f)
            {
                float eased = EaseOutCubic(arenaTransition);
                DrawArenaPreview(g, arenaPrevIndex, box, -arenaTransitionDir * eased * box.Width);
                DrawArenaPreview(g, selectedArenaIndex, box, arenaTransitionDir * (1f - eased) * box.Width);
            }
            else
            {
                DrawArenaPreview(g, selectedArenaIndex, box, 0f);
            }

            Rectangle infoBar = new Rectangle(box.X, box.Bottom - 50, box.Width, 50);
            using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(190, 0, 0, 0)))
            using (SolidBrush nameBrush = new SolidBrush(current.RingNeon))
            {
                g.FillRectangle(barBrush, infoBar);
                g.DrawString(current.Name, nameF, nameBrush, infoBar.X + 12, infoBar.Y + 4);
                g.DrawString(current.Subtitle, subF, Brushes.LightGray, infoBar.X + 12, infoBar.Y + 29);
            }
            using (Pen boxBorder = new Pen(current.RingNeon, 3))
            {
                g.DrawRectangle(boxBorder, box);
            }

            int cardW = 210, cardH = 110, spacing = 260, baseY = 410;
            const float hoverLift = 16f;
            int startX = (w - ((spacing * (GameData.Arenas.Length - 1)) + cardW)) / 2;

            for (int i = 0; i < GameData.Arenas.Length; i++)
            {
                ArenaInfo arena = GameData.Arenas[i];
                float near = Math.Max(0f, 1f - Math.Abs(i - arenaSlide));
                RectangleF card = new RectangleF(startX + (i * spacing), baseY - (near * hoverLift), cardW, cardH);

                Bitmap? thumb = ArenaImages.Get(arena);
                if (thumb != null)
                {
                    RectangleF art = new RectangleF(card.X + 2, card.Y + 2, card.Width - 4, card.Height - 34);
                    float srcH = 1024f * art.Height / art.Width;
                    g.DrawImage(thumb, art, new RectangleF(0, 150, 1024, srcH), GraphicsUnit.Pixel);
                }

                using (SolidBrush nameBar = new SolidBrush(Color.Black))
                {
                    g.FillRectangle(nameBar, card.X + 2, card.Bottom - 32, card.Width - 4, 30);
                }
                g.DrawString(arena.Name, cardF, i == selectedArenaIndex ? Brushes.White : Brushes.Gray, card.X + 8, card.Bottom - 26);

                bool highlighted = near > 0.5f;
                using (Pen border = new Pen(highlighted ? arena.RingNeon : Color.FromArgb(50, 50, 65), highlighted ? 4 : 2))
                {
                    g.DrawRectangle(border, card.X, card.Y, card.Width, card.Height);
                }
            }

            float frameX = startX + (arenaSlide * spacing);
            using (Pen frame = new Pen(current.RingNeon, 3))
            {
                g.DrawRectangle(frame, frameX - 8, baseY - hoverLift - 8, cardW + 16, cardH + 16);
            }

            const string prompt = "[ ARROWS ] Choose Stage  |  [ ENTER ] START MATCH  |  [ BACKSPACE ] Back";
            SizeF promptSize = g.MeasureString(prompt, promptF);
            g.DrawString(prompt, promptF, Brushes.Gold, (w - promptSize.Width) / 2f, h - 48);
        }
    }
}