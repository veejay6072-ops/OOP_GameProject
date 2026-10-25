using System.Drawing;

namespace Gui
{
    /// <summary>
    /// All fixed game content: fighters and arenas.
    /// </summary>
    internal static class GameData
    {
        // --- CHARACTER ROSTER ---
        // The order here is the order of the characters in Plater.tscn (Vyte, Aloy, Mokutin, Aerin).
        // Fighter number N uses the Nth character of the scene, so keep both lists in the same order.
        public static readonly FighterInfo[] Roster = new FighterInfo[]
        {
            new FighterInfo { Name = "Vyte",    Style = "Balanced Fighter", PrimaryColor = Color.FromArgb(0, 150, 255),  AuraColor = Color.Cyan,         SpecialSkill = "Chrono-Shadow" },
            new FighterInfo { Name = "Aloy",    Style = "Balanced Fighter", PrimaryColor = Color.FromArgb(220, 20, 60),  AuraColor = Color.MediumPurple, SpecialSkill = "Chrono-Shadow" },
            new FighterInfo { Name = "Mokutin", Style = "Balanced Fighter", PrimaryColor = Color.FromArgb(255, 185, 0), AuraColor = Color.OrangeRed,    SpecialSkill = "Chrono-Shadow" },
            new FighterInfo { Name = "Aerin",   Style = "Balanced Fighter", PrimaryColor = Color.FromArgb(0, 230, 120),  AuraColor = Color.Lime,         SpecialSkill = "Chrono-Shadow" }
        };

        // --- ARENAS ---
        public static readonly ArenaInfo[] Arenas = new ArenaInfo[]
        {
            new ArenaInfo {
                Name = "16-BIT HALLWAY",
                Subtitle = "Lecture Room Block",
                SkyTop = Color.FromArgb(40, 40, 50),
                SkyBottom = Color.FromArgb(20, 20, 30),
                FloorColor = Color.FromArgb(180, 170, 150),
                RingNeon = Color.Cyan,
                ParticleColor = Color.LightSkyBlue,
                BackgroundFile = "Hallway.jpg"
            },
            new ArenaInfo {
                Name = "ELEVATOR LOBBY",
                Subtitle = "Polished Marble Hall & Gleaming Elevators",
                SkyTop = Color.FromArgb(165, 145, 115),
                SkyBottom = Color.FromArgb(235, 215, 170),
                FloorColor = Color.FromArgb(205, 175, 125),
                RingNeon = Color.FromArgb(70, 160, 255),
                ParticleColor = Color.FromArgb(255, 240, 190),
                BackgroundFile = "elevator_16bit.png"
            }
        };
    }
}