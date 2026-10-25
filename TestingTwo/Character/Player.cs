using System;

namespace Gui
{
    /// <summary>
    /// Per-fighter state for TestingTwo (WinForms).
    /// Tracks the age stage ("The Mortal Toll"), the shadow meter, dash/duck/block flags, and the sprite animation.
    /// The numbers behind each age live in CombatRules / GameConfig.
    /// </summary>
    public class Player
    {
        // --- "THE MORTAL TOLL" AGE STAGES ---
        public AgeStage CurrentAge { get; private set; } = AgeStage.Youth;

        // Health Pool & Stat Tracking
        public float MaxHealth { get; private set; } = 100f;
        public float CurrentHealth { get; set; } = 100f;

        // Chrono-Shadow Meter (0 to 100)
        public float ShadowMeter { get; set; } = 0f;

        // Movement & Mechanic Tracking
        public int AirDashCount { get; set; } = 0;
        public bool IsBlocking { get; set; } = false;
        public bool IsDucking { get; set; } = false;

        // Animation State Outputs
        public string CurrentAnimation { get; private set; } = "Idle";
        public int Frame { get; private set; } = 0;
        public int FighterIndex { get; set; } = 0;     // which roster character this is (picks the sprite set)
        public bool Hurt { get; private set; } = false;

        private float frameTimer = 0f;

        /// <summary>
        /// Sets the age stage for a new round: recalibrates the HP pool and clears the per-round state.
        /// </summary>
        public void SetAge(AgeStage age)
        {
            CurrentAge = age;
            MaxHealth = CombatRules.MaxHealth(age);
            CurrentHealth = MaxHealth;

            ShadowMeter = 0f;
            AirDashCount = 0;
            IsBlocking = false;
            IsDucking = false;

            CurrentAnimation = "Idle";
            Frame = 0;
            frameTimer = 0f;
            Hurt = false;
        }

        /// <summary>Movement speed multiplier based on current age stage.</summary>
        public float GetSpeedMultiplier()
        {
            return CombatRules.SpeedMult(CurrentAge);
        }

        /// <summary>Damage multiplier for attacks based on current age stage.</summary>
        public float GetDamageMultiplier()
        {
            return CombatRules.DamageMult(CurrentAge);
        }

        /// <summary>Share of a blocked hit that still goes through ("chip damage").</summary>
        public float GetChipRatio()
        {
            return CombatRules.ChipRatio(CurrentAge);
        }

        /// <summary>Youth can dash once more in the air (double-dash). Veteran and Elder cannot.</summary>
        public int MaxAirDashes => CombatRules.MaxAirDashes(CurrentAge);

        /// <summary>Elder cannot dash at all.</summary>
        public bool CanGroundDash => CombatRules.CanGroundDash(CurrentAge);

        /// <summary>
        /// Elder stage gain: Hyper Armor during attacks prevents hitstun flinching.
        /// </summary>
        public bool HasHyperArmor(int currentAction)
        {
            return CurrentAge == AgeStage.Elder && CombatRules.IsAttack(currentAction);
        }

        // Animations that repeat. Everything else plays once and holds its last frame.
        private static bool Loops(string animation)
        {
            return animation == "Idle" || animation == "Running_Walking";
        }

        /// <summary>
        /// Synchronizes the animation with the game state each tick.
        /// Animation names here must match the ones FighterSprites / GodotAtlas know about.
        /// Action codes: 0 none, 1 punch, 2 cross, 3 kick, 4 hurt, 5 wall splat, 6 down attack, 8 jump attack, 99 defeated.
        /// </summary>
        public void SyncFromGame(float x, float velX, float velY, bool isOnGround, int action, int actionTimer, bool isP2)
        {
            // Reset air dash count when landing on floor
            if (isOnGround)
            {
                AirDashCount = 0;
            }

            Hurt = action == CombatRules.ActHurt;

            string next;
            if (action == CombatRules.ActDefeated) next = "Defeated";
            else if (action == CombatRules.ActHurt || action == CombatRules.ActWallSplat) next = "Hurt";
            else if (!isOnGround) next = action > 0 ? "Jump_Attack" : "Jump";
            else if (action == CombatRules.ActDownAttack) next = "Down_Attack";
            else if (action == CombatRules.ActPunch1) next = "Punch1";
            else if (action == CombatRules.ActPunch2) next = "Punch2";
            else if (action == CombatRules.ActKick) next = "Kick";
            else if (IsBlocking) next = "Block";
            else if (IsDucking) next = "Duck";
            else if (Math.Abs(velX) > 1f) next = "Running_Walking";
            else next = "Idle";

            // A new animation always starts from its first frame
            if (next != CurrentAnimation)
            {
                CurrentAnimation = next;
                Frame = 0;
                frameTimer = 0f;
            }

            // Step through the frames of the current animation
            int frameCount = Math.Max(1, FighterSprites.FrameCountForAge(FighterIndex, CurrentAge, CurrentAnimation));
            frameTimer += 0.25f;
            if (frameTimer >= 1f)
            {
                frameTimer = 0f;
                if (Loops(CurrentAnimation)) Frame = (Frame + 1) % frameCount;
                else Frame = Math.Min(Frame + 1, frameCount - 1);
            }
        }
    }
}