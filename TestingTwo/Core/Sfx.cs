using System;
using System.Threading.Tasks;

namespace Gui
{
    /// <summary>
    /// Procedural sound effects.
    /// </summary>
    internal static class Sfx
    {
        /// <summary>Plays a short beep without blocking the game loop.</summary>
        public static void Play(int frequency, int durationMs)
        {
            Task.Run(() =>
            {
                try { Console.Beep(frequency, durationMs); } catch { }
            });
        }

        /// <summary>Plays several notes one after another (in order) without blocking the game loop.</summary>
        public static void Sequence(params (int Frequency, int DurationMs)[] notes)
        {
            Task.Run(() =>
            {
                foreach ((int frequency, int durationMs) in notes)
                {
                    try { Console.Beep(frequency, durationMs); } catch { return; }
                }
            });
        }

        /// <summary>Chrono-Shadow dropped: a quick rising whoosh.</summary>
        public static void ShadowDrop()
        {
            Sequence((500, 40), (750, 40), (1100, 40), (1650, 80));
        }

        /// <summary>Chrono-Shadow comes to life after its 3 seconds: a pulse, then a high ring.</summary>
        public static void ShadowAwaken()
        {
            Sequence((1700, 60), (1150, 60), (1700, 60), (2400, 130));
        }
    }
}