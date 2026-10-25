using System;

namespace Gui
{
    /// <summary>One attack: how hard it hits, how far it reaches, and how long it lasts (all times in game ticks, 60 per second).</summary>
    internal readonly struct MoveInfo
    {
        public readonly string Name;
        public readonly int Damage;        // base damage before the age multiplier
        public readonly int Reach;         // how far in front of the attacker it connects
        public readonly int HitDelay;      // ticks from the button press until the hit lands (matches the swing in the animation)
        public readonly int ActionFrames;  // total length of the animation
        public readonly int CancelAt;      // the next hit of a string may start once this many ticks (or fewer) are left; 0 = no chaining
        public readonly int Stun;          // hitstun on the victim
        public readonly int Lunge;         // small step forward when the move starts
        public readonly float Knockback;
        public readonly float Launch;      // negative = pops the victim into the air
        public readonly bool High;         // high attacks pass over a ducking fighter

        public MoveInfo(string name, int damage, int reach, int hitDelay, int actionFrames, int cancelAt,
            int stun, int lunge, float knockback, float launch, bool high)
        {
            Name = name; Damage = damage; Reach = reach; HitDelay = hitDelay; ActionFrames = actionFrames;
            CancelAt = cancelAt; Stun = stun; Lunge = lunge; Knockback = knockback; Launch = launch; High = high;
        }
    }

    /// <summary>
    /// Pure combat math and move data. Touches no UI and no game state.
    /// </summary>
    internal static class CombatRules
    {
        // Action codes shared by the engine and the sprite animator (Player.cs)
        public const int ActNone = 0;
        public const int ActPunch1 = 1;
        public const int ActPunch2 = 2;
        public const int ActKick = 3;
        public const int ActHurt = 4;
        public const int ActWallSplat = 5;
        public const int ActDownAttack = 6;
        public const int ActJumpAttack = 8;
        public const int ActDefeated = 99;

        //                                              name        dmg reach delay frames cancel stun lunge  kb   launch high
        private static readonly MoveInfo Punch1 = new MoveInfo("Punch", 8, 130, 6, 16, 9, 14, 3, 5f, 0f, true);
        private static readonly MoveInfo Punch2 = new MoveInfo("Cross", 9, 140, 4, 12, 6, 16, 3, 6f, 0f, true);
        private static readonly MoveInfo Kick = new MoveInfo("Kick", 12, 165, 7, 16, 0, 24, 5, 16f, -9f, false);
        private static readonly MoveInfo DownAttack = new MoveInfo("Down Attack", 10, 155, 12, 28, 0, 26, 0, 8f, -12f, false);
        private static readonly MoveInfo JumpAttack = new MoveInfo("Jump Attack", 9, 125, 8, 24, 0, 18, 0, 8f, 0f, false);

        // Youth gets a long five-hit string; Veteran and Elder get the short three-hit string.
        private static readonly int[] YouthString = { ActPunch1, ActPunch2, ActPunch1, ActPunch2, ActKick };
        private static readonly int[] ShortString = { ActPunch1, ActPunch2, ActKick };

        public static bool IsAttack(int action)
        {
            return action == ActPunch1 || action == ActPunch2 || action == ActKick
                || action == ActDownAttack || action == ActJumpAttack;
        }

        public static MoveInfo GetMove(int action)
        {
            return action switch
            {
                ActPunch1 => Punch1,
                ActPunch2 => Punch2,
                ActKick => Kick,
                ActDownAttack => DownAttack,
                ActJumpAttack => JumpAttack,
                _ => default
            };
        }

        /// <summary>The sprite animation that plays for an attack action (names match FighterSprites / GodotAtlas).</summary>
        public static string AnimationFor(int action)
        {
            return action switch
            {
                ActPunch1 => "Punch1",
                ActPunch2 => "Punch2",
                ActKick => "Kick",
                ActDownAttack => "Down_Attack",
                ActJumpAttack => "Jump_Attack",
                _ => "Idle"
            };
        }

        public static int[] ComboString(AgeStage age)
        {
            return age == AgeStage.Youth ? YouthString : ShortString;
        }

        // ----- The Mortal Toll -----

        public static string AgeName(AgeStage age)
        {
            return age switch { AgeStage.Veteran => "VETERAN", AgeStage.Elder => "ELDER", _ => "YOUTH" };
        }

        public static int MaxHealth(AgeStage age)
        {
            float mult = age switch
            {
                AgeStage.Veteran => GameConfig.VeteranHpMult,
                AgeStage.Elder => GameConfig.ElderHpMult,
                _ => GameConfig.YouthHpMult
            };
            return (int)Math.Round(GameConfig.BaseHealth * mult);
        }

        public static float DamageMult(AgeStage age)
        {
            return age switch
            {
                AgeStage.Veteran => GameConfig.VeteranDmgMult,
                AgeStage.Elder => GameConfig.ElderDmgMult,
                _ => GameConfig.YouthDmgMult
            };
        }

        public static float ChipRatio(AgeStage age)
        {
            return age switch
            {
                AgeStage.Veteran => GameConfig.VeteranChipDmg,
                AgeStage.Elder => GameConfig.ElderChipDmg,
                _ => GameConfig.YouthChipDmg
            };
        }

        public static float SpeedMult(AgeStage age)
        {
            return age switch
            {
                AgeStage.Veteran => GameConfig.VeteranSpeedMult,
                AgeStage.Elder => GameConfig.ElderSpeedMult,
                _ => GameConfig.YouthSpeedMult
            };
        }

        /// <summary>Youth can dash again in mid-air (the double-dash). Veteran and Elder cannot.</summary>
        public static int MaxAirDashes(AgeStage age)
        {
            return age == AgeStage.Youth ? 1 : 0;
        }

        /// <summary>Elder has no dash at all.</summary>
        public static bool CanGroundDash(AgeStage age)
        {
            return age != AgeStage.Elder;
        }

        /// <summary>Damage of one hit after the attacker's age multiplier. Always at least 1.</summary>
        public static int RawDamage(MoveInfo move, AgeStage attackerAge)
        {
            return Math.Max(1, (int)Math.Round(move.Damage * DamageMult(attackerAge)));
        }

        /// <summary>Damage that still gets through a block. Always at least 1.</summary>
        public static int ChipDamage(int rawDamage, AgeStage attackerAge)
        {
            return Math.Max(1, (int)Math.Round(rawDamage * ChipRatio(attackerAge)));
        }
    }
}