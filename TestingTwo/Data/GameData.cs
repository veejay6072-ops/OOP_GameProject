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
            new FighterInfo { Name = "Vyte",    Style = "Balanced Fighter",           PrimaryColor = Color.FromArgb(0, 150, 255),  AuraColor = Color.Cyan },
            new FighterInfo { Name = "Aloy",    Style = "Fast Combatant",             PrimaryColor = Color.FromArgb(220, 20, 60),  AuraColor = Color.MediumPurple },
            new FighterInfo { Name = "Mokutin", Style = "Samurai from the Heian Era", PrimaryColor = Color.FromArgb(255, 185, 0), AuraColor = Color.OrangeRed },
            new FighterInfo { Name = "Aerin",   Style = "Good with Books",            PrimaryColor = Color.FromArgb(0, 230, 120),  AuraColor = Color.Lime }
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
            },
            new ArenaInfo {
                Name = "HAUNTED HALLWAY",
                Subtitle = "Halloween Night - Lecture Room Block",
                SkyTop = Color.FromArgb(40, 60, 90),
                SkyBottom = Color.FromArgb(20, 30, 50),
                FloorColor = Color.FromArgb(190, 170, 140),
                RingNeon = Color.FromArgb(255, 140, 0),
                ParticleColor = Color.FromArgb(255, 170, 60),
                BackgroundFile = "hallway_hollween_ver.jpg"
            },
            new ArenaInfo {
                Name = "CELLAR 13 ELEVATORS",
                Subtitle = "Halloween Night - Slime Leaking Lift",
                SkyTop = Color.FromArgb(70, 60, 100),
                SkyBottom = Color.FromArgb(40, 35, 65),
                FloorColor = Color.FromArgb(150, 140, 170),
                RingNeon = Color.FromArgb(90, 255, 80),
                ParticleColor = Color.FromArgb(150, 255, 120),
                BackgroundFile = "elevator_holloween.jpg"
            }
        };
    }
}