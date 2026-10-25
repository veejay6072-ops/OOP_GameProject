namespace Gui
{
    /// <summary>
    /// Tuning numbers for rounds, "The Mortal Toll" aging, movement, and the Chrono-Shadow.
    /// </summary>
    internal static class GameConfig
    {
        // --- ROUNDS ---
        public const int RoundsToWin = 2;              // Best of 3 rounds
        public const int RoundSeconds = 60;
        public const int BaseHealth = 100;
        public const int KoDisplayTicks = 170;         // how long the K.O. screen stays up between rounds

        // --- THE MORTAL TOLL (aging) ---
        public const float YouthHpMult = 1.00f;        // 100% HP (Round 1)
        public const float VeteranHpMult = 0.85f;      // -15% Max HP
        public const float ElderHpMult = 0.65f;        // Drastically reduced HP

        public const float YouthDmgMult = 1.00f;
        public const float VeteranDmgMult = 1.45f;     // Increased attack damage
        public const float ElderDmgMult = 2.40f;       // Massive damage (3-4 hit KO power)

        public const float YouthChipDmg = 0.10f;       // share of a blocked hit that still gets through
        public const float VeteranChipDmg = 0.35f;     // Heavy chip damage through block
        public const float ElderChipDmg = 0.50f;       // Devastating chip damage

        public const float YouthSpeedMult = 1.00f;
        public const float VeteranSpeedMult = 0.85f;
        public const float ElderSpeedMult = 0.60f;     // Slowest walk

        // --- MOVEMENT ---
        public const float WalkSpeed = 8f;
        public const float DashSpeed = 30f;
        public const int DashDoubleTapMs = 260;        // double-tap left/right inside this window to dash
        public const int DashPhaseTicks = 12;          // a dashing fighter can slip through the opponent
        public const float JumpSpeed = 22f;

        // --- CHRONO-SHADOW MECHANIC ---
        public const float MaxShadowMeter = 100f;
        public const float ShadowCost = 35f;            // Meter spent per phantom
        public const float ShadowDelaySeconds = 3.0f;   // Phantom comes to life after exactly 3 seconds
        public const float ShadowExecuteSeconds = 0.4f; // How long the phantom's strike lasts
        public const float ShadowGainDealt = 1.2f;      // meter gained per point of damage dealt
        public const float ShadowGainTaken = 1.6f;      // meter gained per point of damage taken
    }

    public enum AgeStage
    {
        Youth = 0,    // Double-dash, fast movement, long combo strings
        Veteran = 1,  // No double dash, -15% HP, +45% Dmg, high chip damage
        Elder = 2     // Slowest walk, low HP, Hyper Armor, massive 3-4 hit KO power
    }
}