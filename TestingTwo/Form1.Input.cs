using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gui
{
    public partial class Form1 : Form
    {
        private void TogglePause()
        {
            paused = !paused;
            Sfx.Play(paused ? 420 : 700, 70);
        }

        // Clicking the small PAUSE button at the top of the screen
        private void Form1_MouseClick(object? sender, MouseEventArgs e)
        {
            bool inFight = currentState == GameState.FightIntro || currentState == GameState.Battle || currentState == GameState.KO;
            if (!inFight || screenDevice.Width <= 0) return;

            int gx = (int)((e.X - screenDevice.X) * GameWidth / (float)screenDevice.Width);
            int gy = (int)((e.Y - screenDevice.Y) * GameHeight / (float)screenDevice.Height);
            if (PauseButtonRect.Contains(gx, gy)) TogglePause();
        }

        private void Form1_KeyUp(object? sender, KeyEventArgs e)
        {
            pressedKeys.Remove(e.KeyCode);
        }

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            // false while the key is just auto-repeating because it is held down
            bool fresh = pressedKeys.Add(e.KeyCode);

            // PAUSE: Esc or the MENU button (Backspace) during a fight. While paused, Enter also resumes and R quits to the menu.
            bool inFight = currentState == GameState.FightIntro || currentState == GameState.Battle || currentState == GameState.KO;
            if (inFight && fresh && (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Back || (paused && e.KeyCode == Keys.Enter)))
            {
                TogglePause();
                return;
            }
            if (paused)
            {
                if (e.KeyCode == Keys.R)
                {
                    paused = false;
                    currentState = GameState.ModeSelect;
                    p1Locked = false;
                }
                return;
            }

            if (currentState == GameState.ModeSelect)
            {
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up) selectedMode = 0;
                if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down) selectedMode = 1;
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    isVsCPU = (selectedMode == 1);
                    currentState = isVsCPU ? GameState.DifficultySelect : GameState.CharSelect;   // arcade mode picks the CPU difficulty first
                    p1Locked = false;
                    Sfx.Play(650, 90);
                }
            }
            else if (currentState == GameState.DifficultySelect)
            {
                if ((e.KeyCode == Keys.Left || e.KeyCode == Keys.Up) && cpuDifficulty > CpuDifficulty.Easy)
                {
                    cpuDifficulty--;
                    Sfx.Play(520, 40);
                }
                if ((e.KeyCode == Keys.Right || e.KeyCode == Keys.Down) && cpuDifficulty < CpuDifficulty.Hard)
                {
                    cpuDifficulty++;
                    Sfx.Play(520, 40);
                }
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    currentState = GameState.CharSelect;
                    p1Locked = false;
                    Sfx.Play(650, 90);
                }
                if (e.KeyCode == Keys.Back) currentState = GameState.ModeSelect;
            }
            else if (currentState == GameState.CharSelect)
            {
                if (!p1Locked)
                {
                    if (e.KeyCode == Keys.Left) p1CharIndex = (p1CharIndex - 1 + GameData.Roster.Length) % GameData.Roster.Length;
                    if (e.KeyCode == Keys.Right) p1CharIndex = (p1CharIndex + 1) % GameData.Roster.Length;
                    if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                    {
                        p1Locked = true;
                        Sfx.Play(600, 80);
                        if (isVsCPU)
                        {
                            p2CharIndex = (p1CharIndex + 1) % GameData.Roster.Length;
                            currentState = GameState.ArenaSelect;
                            Sfx.Play(750, 100);
                        }
                    }
                    if (e.KeyCode == Keys.Back) currentState = isVsCPU ? GameState.DifficultySelect : GameState.ModeSelect;
                }
                else if (!isVsCPU)
                {
                    if (e.KeyCode == Keys.Left) p2CharIndex = (p2CharIndex - 1 + GameData.Roster.Length) % GameData.Roster.Length;
                    if (e.KeyCode == Keys.Right) p2CharIndex = (p2CharIndex + 1) % GameData.Roster.Length;
                    if (e.KeyCode == Keys.Enter) { currentState = GameState.ArenaSelect; Sfx.Play(750, 100); }
                    if (e.KeyCode == Keys.Back) p1Locked = false;
                }
            }
            else if (currentState == GameState.ArenaSelect)
            {
                if (e.KeyCode == Keys.Left) ChangeArena(-1);
                if (e.KeyCode == Keys.Right) ChangeArena(1);

                if (e.KeyCode == Keys.Enter) StartMatch();

                if (e.KeyCode == Keys.Back)
                {
                    currentState = GameState.CharSelect;
                    if (isVsCPU) p1Locked = false;
                }
            }
            else if (currentState == GameState.Battle)
            {
                HandleBattleKey(e.KeyCode, fresh);
            }
            else if (currentState == GameState.KO && matchOver && e.KeyCode == Keys.R)
            {
                currentState = GameState.ModeSelect;
                p1Locked = false;
            }
        }

        /// <summary>
        /// One-shot battle keys. Walking, ducking and blocking are held keys and are read every tick instead
        /// (see ApplyHeldInputs in Form1.Battle.cs).
        ///
        ///   Player 1:  A / D move (double-tap to dash)   W jump   S duck   Q / E / R attacks   X shadow   T block
        ///   Player 2:  J / L move (double-tap to dash)   I jump   K duck   U / O / P attacks   M shadow   Y block
        ///
        /// Ground attacks:  Q = punch, E = cross, R = kick. Chaining them makes a string (Youth 5 hits, Veteran/Elder 3).
        /// Down attack:     hold S (duck) + Q or E.
        /// Jump attack:     W (jump) + Q or E while in the air.
        /// Shadow:          only works in the middle of an attack.
        /// </summary>
        private void HandleBattleKey(Keys key, bool fresh)
        {
            if (!fresh) return;

            if (key == Keys.A) RegisterDirectionTap(1, key, -1f);
            else if (key == Keys.D) RegisterDirectionTap(1, key, 1f);
            else if (key == Keys.W) TryJump(1);
            else if (key == Keys.Q) TryAttack(1, 0);
            else if (key == Keys.E) TryAttack(1, 1);
            else if (key == Keys.R) TryAttack(1, 2);
            else if (key == Keys.X) TryDropShadow(1);

            if (isVsCPU) return;

            if (key == Keys.J) RegisterDirectionTap(2, key, -1f);
            else if (key == Keys.L) RegisterDirectionTap(2, key, 1f);
            else if (key == Keys.I) TryJump(2);
            else if (key == Keys.U) TryAttack(2, 0);
            else if (key == Keys.O) TryAttack(2, 1);
            else if (key == Keys.P) TryAttack(2, 2);
            else if (key == Keys.M) TryDropShadow(2);
        }
    }
}