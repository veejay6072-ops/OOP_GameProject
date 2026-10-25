using System;

namespace Gui
{
    /// <summary>
    /// One Chrono-Shadow phantom. It is frozen in the attack pose it was dropped in,
    /// then after exactly 3 seconds it replays that attack from where it stands.
    /// Because it hits from its own position, a phantom left in front of the opponent
    /// while its owner dashes behind them becomes an unblockable cross-up.
    /// </summary>
    public class ChronoShadow
    {
        public int OwnerPlayer { get; set; }       // 1 or 2
        public float X { get; set; }
        public float Y { get; set; }
        public bool FaceRight { get; set; }
        public int FighterIndex { get; set; }
        public int Action { get; set; }            // Recorded attack action code
        public string Animation { get; set; } = "Idle";  // Recorded attack animation
        public int Frame { get; set; }             // Frame it is frozen on while waiting
        public AgeStage Age { get; set; }          // Age stage when phantom was dropped
        public float Timer { get; set; } = GameConfig.ShadowDelaySeconds; // 3.0 second countdown
        public bool Executed { get; set; } = false;
        public float ExecutionTimer { get; set; } = GameConfig.ShadowExecuteSeconds;

        public int ExecTicks { get; set; }         // ticks since the strike started (drives the replay animation)
        public bool HitDone { get; set; }          // the strike only lands once

        public bool Finished => Executed && ExecutionTimer <= 0f;

        public void Update(float dt)
        {
            if (Timer > 0f)
            {
                Timer -= dt;
            }
            else if (!Executed)
            {
                Executed = true;
            }
            else
            {
                ExecutionTimer -= dt;
            }
        }
    }
}