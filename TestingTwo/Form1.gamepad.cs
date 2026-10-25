using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gui
{
    /// <summary>
    /// Controller support: PlayStation (DualShock 4 / DualSense) pads directly, and Xbox-style pads through XInput.
    /// Each controller "presses" the same keys the keyboard uses, so menus, fights, pause, double-tap dash and the
    /// cabinet's lit buttons all work without touching the battle code.
    ///
    ///   Controller 1 = Player 1 (A/D/W/S, Q/E/R, X, T)      Controller 2 = Player 2 (J/L/I/K, U/O/P, M, Y)
    ///
    ///   PS4 button     Xbox      In a fight                         In menus
    ///   D-pad / L-stick           move (tap twice = dash)            move
    ///   Cross          A         jump (up also jumps)               confirm
    ///   Circle         B         attack 3 (kick)                    back
    ///   Square         X         attack 1 (punch)
    ///   Triangle       Y         attack 2 (cross)
    ///   R1 or R2       RB / RT   Chrono-Shadow
    ///   L1 or L2       LB / LT   block
    ///   Options        Start     pause (paused: Cross resumes, Circle quits to menu)    confirm
    ///
    /// With one controller plugged in it drives Player 1 (the keyboard can still be Player 2).
    /// Controllers are found automatically; plug them in before or during the game.
    /// If a PS4 pad is also being run through Steam Input or DS4Windows, it shows up as an Xbox pad and is used that way.
    /// </summary>
    public partial class Form1 : Form
    {
        // ---------------------------------------------------------------
        //  One controller, reduced to the buttons the game cares about
        // ---------------------------------------------------------------
        private struct PadInput
        {
            public bool Left, Right, Up, Down;
            public bool A, B, X, Y;          // A = bottom face button (Cross), B = right (Circle), X = left (Square), Y = top (Triangle)
            public bool Start, Shadow, Block;
        }

        // ---------------------------------------------------------------
        //  XInput (Xbox-style pads)
        // ---------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint PacketNumber;
            public XInputGamepad Gamepad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState(int userIndex, out XInputState state);

        private const ushort XDUp = 0x0001, XDDown = 0x0002, XDLeft = 0x0004, XDRight = 0x0008, XStart = 0x0010;
        private const ushort XLB = 0x0100, XRB = 0x0200, XA = 0x1000, XB = 0x2000, XX = 0x4000, XY = 0x8000;
        private const int XStickDeadzone = 12000;     // out of 32767
        private const int XTriggerThreshold = 100;    // out of 255

        // ---------------------------------------------------------------
        //  Windows joystick API (PlayStation pads show up here as plain joysticks)
        // ---------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        private struct JoyInfoEx
        {
            public uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, POV, Reserved1, Reserved2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct JoyCaps
        {
            public ushort Mid, Pid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public uint XMin, XMax, YMin, YMax, ZMin, ZMax, NumButtons, PeriodMin, PeriodMax;
            public uint RMin, RMax, UMin, UMax, VMin, VMax, Caps, MaxAxes, NumAxes, MaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string OemVxD;
        }

        [DllImport("winmm.dll")]
        private static extern int joyGetPosEx(int joyId, ref JoyInfoEx info);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int joyGetDevCapsW(int joyId, out JoyCaps caps, int size);

        private const ushort SonyVendorId = 0x054C;
        private const uint JoyReturnAll = 0xFF;
        private const int JoyCenter = 32768, JoyDeadzone = 12000;   // axes run 0..65535

        // PlayStation pad buttons as Windows numbers them (1-based)
        private const int PsSquare = 1, PsCross = 2, PsCircle = 3, PsTriangle = 4, PsL1 = 5, PsR1 = 6, PsL2 = 7, PsR2 = 8, PsOptions = 10;

        private readonly List<int> sonyPads = new List<int>();   // joystick ids of connected PlayStation pads
        private int sonyRescan;                                  // ticks until we look for new PlayStation pads

        // ---------------------------------------------------------------
        //  Polling
        // ---------------------------------------------------------------
        private readonly HashSet<Keys>[] padKeys = { new HashSet<Keys>(), new HashSet<Keys>() };   // keys each player's pad is "holding"
        private readonly int[] xinputRecheck = new int[4];   // ticks to wait before looking for an unplugged Xbox pad again
        private bool xinputBroken, joystickBroken;

        // Called every engine tick (see EngineLoop_Tick in Form1.cs)
        private void PollGamepads()
        {
            if (xinputBroken && joystickBroken) return;
            bool active = ReferenceEquals(Form.ActiveForm, this);

            // The first two controllers found become player 1 and player 2 (Xbox-style first, then PlayStation)
            List<PadInput> pads = new List<PadInput>(2);
            CollectXInputPads(pads);
            if (pads.Count < 2) CollectSonyPads(pads);

            for (int slot = 0; slot < 2; slot++)
            {
                HashSet<Keys> wanted = new HashSet<Keys>();
                if (active && slot < pads.Count) BuildPadKeys(slot, pads[slot], wanted);
                ApplyPadKeys(slot, wanted);
            }
        }

        private void CollectXInputPads(List<PadInput> pads)
        {
            if (xinputBroken) return;
            for (int i = 0; i < 4 && pads.Count < 2; i++)
            {
                if (xinputRecheck[i] > 0) { xinputRecheck[i]--; continue; }
                try
                {
                    if (XInputGetState(i, out XInputState state) != 0)
                    {
                        xinputRecheck[i] = 90;   // not connected: check again in ~1.5 seconds
                        continue;
                    }

                    XInputGamepad s = state.Gamepad;
                    ushort b = s.Buttons;
                    pads.Add(new PadInput
                    {
                        Left = (b & XDLeft) != 0 || s.ThumbLX < -XStickDeadzone,
                        Right = (b & XDRight) != 0 || s.ThumbLX > XStickDeadzone,
                        Up = (b & XDUp) != 0 || s.ThumbLY > XStickDeadzone,
                        Down = (b & XDDown) != 0 || s.ThumbLY < -XStickDeadzone,
                        A = (b & XA) != 0,
                        B = (b & XB) != 0,
                        X = (b & XX) != 0,
                        Y = (b & XY) != 0,
                        Start = (b & XStart) != 0,
                        Shadow = (b & XRB) != 0 || s.RightTrigger > XTriggerThreshold,
                        Block = (b & XLB) != 0 || s.LeftTrigger > XTriggerThreshold
                    });
                }
                catch (DllNotFoundException) { xinputBroken = true; return; }
                catch (EntryPointNotFoundException) { xinputBroken = true; return; }
            }
        }

        private void CollectSonyPads(List<PadInput> pads)
        {
            if (joystickBroken) return;
            try
            {
                if (sonyRescan > 0) sonyRescan--;
                else
                {
                    sonyRescan = 120;   // look for newly plugged-in pads every ~2 seconds
                    sonyPads.Clear();
                    int capsSize = Marshal.SizeOf<JoyCaps>();
                    for (int id = 0; id < 16; id++)
                    {
                        if (joyGetDevCapsW(id, out JoyCaps caps, capsSize) != 0 || caps.Mid != SonyVendorId) continue;
                        JoyInfoEx probe = new JoyInfoEx { Size = (uint)Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnAll };
                        if (joyGetPosEx(id, ref probe) == 0) sonyPads.Add(id);
                    }
                }

                foreach (int id in sonyPads.ToList())
                {
                    if (pads.Count >= 2) break;
                    JoyInfoEx j = new JoyInfoEx { Size = (uint)Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnAll };
                    if (joyGetPosEx(id, ref j) != 0)
                    {
                        sonyPads.Remove(id);   // unplugged
                        continue;
                    }

                    // D-pad (hat switch) in hundredths of a degree; anything above 36000 means "not pressed"
                    bool hat = j.POV <= 36000;
                    uint pov = j.POV;
                    bool hatUp = hat && (pov >= 31500 || pov <= 4500);
                    bool hatRight = hat && pov >= 4500 && pov <= 13500;
                    bool hatDown = hat && pov >= 13500 && pov <= 22500;
                    bool hatLeft = hat && pov >= 22500 && pov <= 31500;

                    bool Btn(int n) => (j.Buttons & (1u << (n - 1))) != 0;

                    pads.Add(new PadInput
                    {
                        Left = hatLeft || j.X < JoyCenter - JoyDeadzone,
                        Right = hatRight || j.X > JoyCenter + JoyDeadzone,
                        Up = hatUp || j.Y < JoyCenter - JoyDeadzone,
                        Down = hatDown || j.Y > JoyCenter + JoyDeadzone,
                        A = Btn(PsCross),
                        B = Btn(PsCircle),
                        X = Btn(PsSquare),
                        Y = Btn(PsTriangle),
                        Start = Btn(PsOptions),
                        Shadow = Btn(PsR1) || Btn(PsR2),
                        Block = Btn(PsL1) || Btn(PsL2)
                    });
                }
            }
            catch (DllNotFoundException) { joystickBroken = true; }
            catch (EntryPointNotFoundException) { joystickBroken = true; }
        }

        // ---------------------------------------------------------------
        //  Turning pad buttons into keyboard keys
        // ---------------------------------------------------------------
        private void BuildPadKeys(int pad, PadInput p, HashSet<Keys> keys)
        {
            bool inFight = currentState == GameState.FightIntro || currentState == GameState.Battle || currentState == GameState.KO;

            if (paused)
            {
                if (p.A || p.Start) keys.Add(Keys.Enter);   // resume
                if (p.B) keys.Add(Keys.R);                  // quit to menu
            }
            else if (inFight)
            {
                if (currentState == GameState.KO && matchOver)
                {
                    if (p.A || p.Start) keys.Add(Keys.R);   // back to the main menu after the match
                    return;
                }

                if (p.Start) keys.Add(Keys.Escape);         // pause

                if (currentState != GameState.Battle) return;
                if (pad == 1 && isVsCPU) return;            // in arcade mode only controller 1 plays

                if (pad == 0)
                {
                    if (p.Left) keys.Add(Keys.A);
                    if (p.Right) keys.Add(Keys.D);
                    if (p.Up || p.A) keys.Add(Keys.W);
                    if (p.Down) keys.Add(Keys.S);
                    if (p.X) keys.Add(Keys.Q);
                    if (p.Y) keys.Add(Keys.E);
                    if (p.B) keys.Add(Keys.R);
                    if (p.Shadow) keys.Add(Keys.X);
                    if (p.Block) keys.Add(Keys.T);
                }
                else
                {
                    if (p.Left) keys.Add(Keys.J);
                    if (p.Right) keys.Add(Keys.L);
                    if (p.Up || p.A) keys.Add(Keys.I);
                    if (p.Down) keys.Add(Keys.K);
                    if (p.X) keys.Add(Keys.U);
                    if (p.Y) keys.Add(Keys.O);
                    if (p.B) keys.Add(Keys.P);
                    if (p.Shadow) keys.Add(Keys.M);
                    if (p.Block) keys.Add(Keys.Y);
                }
            }
            else
            {
                // Menus: both controllers can navigate (the 2-player character screen needs player 2's picks too)
                if (p.Left) keys.Add(Keys.Left);
                if (p.Right) keys.Add(Keys.Right);
                if (p.Up) keys.Add(Keys.Up);
                if (p.Down) keys.Add(Keys.Down);
                if (p.A || p.Start) keys.Add(Keys.Enter);
                if (p.B) keys.Add(Keys.Back);
            }
        }

        // Presses and releases keys so the pad matches what BuildPadKeys asked for
        private void ApplyPadKeys(int pad, HashSet<Keys> wanted)
        {
            HashSet<Keys> current = padKeys[pad];

            foreach (Keys k in current.Where(k => !wanted.Contains(k)).ToList())
            {
                current.Remove(k);
                OnKeyUp(new KeyEventArgs(k));
            }
            foreach (Keys k in wanted)
            {
                if (current.Add(k)) OnKeyDown(new KeyEventArgs(k));
            }
        }
    }
}