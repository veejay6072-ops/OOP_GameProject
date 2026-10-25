using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gui
{
    /// <summary>
    /// Main game window: shared game state, setup, and the engine loop. The rest of Form1 lives in the Form1.*.cs files.
    /// </summary>
    // Tells Visual Studio that every Form1.*.cs file is plain code, so double-clicking one opens
    // the code editor and no Design view (which creates the extra .resx / .Designer.cs files).
    [System.ComponentModel.DesignerCategory("Code")]
    public partial class Form1 : Form
    {
        // --- GAME FLOW ---
        private enum GameState { ModeSelect, CharSelect, ArenaSelect, FightIntro, Battle, KO }
        private GameState currentState = GameState.ModeSelect;

        private int selectedMode = 0;
        private bool isVsCPU = false;
        private int p1CharIndex = 0, p2CharIndex = 1;
        private bool p1Locked = false;
        private int selectedArenaIndex = 0;

        // Arena select animation
        private float arenaSlide = 0f;          // eased position of the selector (in card indexes)
        private int arenaPrevIndex = 0;         // arena that is sliding out of the preview box
        private int arenaTransitionDir = 1;     // +1 = new arena slides in from the right
        private float arenaTransition = 1f;     // 0 = just changed, 1 = finished

        // --- ROUNDS ("THE MORTAL TOLL") ---
        private int roundNumber = 1;                 // 1..3
        private int p1RoundWins = 0, p2RoundWins = 0;
        private bool p1LostRound = false, p2LostRound = false;   // has this fighter lost a round yet?
        private bool matchOver = false;
        private int lastRoundWinner = 0;             // 0 = draw
        private int koCounter = 0;
        private string koHeadline = "K.O.";
        private string roundResultText = "";
        private string nextRoundText = "";
        private string matchResultText = "";

        // --- BATTLE STATS ---
        private int p1Health = 100, p2Health = 100;
        private int p1RedHealth = 100, p2RedHealth = 100;
        private const int MaxHealth = 100;           // the full width of a health bar (aged fighters have less than this)
        private int p1ComboHits = 0, p2ComboHits = 0;
        private int p1ComboTimer = 0, p2ComboTimer = 0;
        private int roundTimer = GameConfig.RoundSeconds;

        // --- PER-FIGHTER COMBAT STATE (index 1 = player 1, index 2 = player 2, index 0 unused) ---
        private readonly bool[] facingRight = { true, true, false };
        private readonly int[] pendingMove = new int[3];      // attack waiting for its hit frame
        private readonly int[] pendingDelay = new int[3];
        private readonly int[] dueMove = new int[3];
        private readonly int[] chainIndex = new int[3];       // position inside the combo string
        private readonly int[] chainTimer = new int[3];
        private readonly int[] dashTimer = new int[3];        // > 0 while dashing (can slip through the opponent)
        private readonly Keys[] lastTapKey = new Keys[3];
        private readonly long[] lastTapTime = new long[3];
        private readonly HashSet<Keys> pressedKeys = new HashSet<Keys>();   // tells a fresh key press from auto-repeat

        // --- CHRONO-SHADOW ---
        private readonly List<ChronoShadow> shadows = new List<ChronoShadow>();

        // --- CPU ---
        private int cpuDecisionTimer = 0;
        private float cpuMoveDir = 0f;
        private int cpuBlockTimer = 0;
        private int cpuDuckTimer = 0;

        // --- VISUAL FX ---
        private List<ShockwaveRing> shockwaves = new List<ShockwaveRing>();
        private List<FloorCrater> craters = new List<FloorCrater>();
        private List<AmbientMote> ambientMotes = new List<AmbientMote>();
        private List<GhostTrail> trails = new List<GhostTrail>();
        private List<Particle> particles = new List<Particle>();
        private List<LightningArc> lightnings = new List<LightningArc>();

        // Physics
        private const float GroundLevel = 270f;
        private const float Gravity = 1.35f;
        private float p1X = 260, p1Y = GroundLevel;
        private float p2X = 660, p2Y = GroundLevel;
        private float p1VelX = 0, p1VelY = 0;
        private float p2VelX = 0, p2VelY = 0;
        private int p1Action = 0, p2Action = 0;
        private int p1ActionTimer = 0, p2ActionTimer = 0;

        // Visual Engine Timers
        private int speedLineTimer = 0;
        private PointF speedLineCenter = PointF.Empty;
        private int hitStopFreeze = 0;
        private bool paused = false;
        private Rectangle PauseButtonRect => new Rectangle((GameWidth / 2) - 34, 2, 68, 20);
        private int rageCinematicFlash = 0;
        private float cameraZoom = 1.0f;
        private float targetZoom = 1.0f;
        private int screenShake = 0;
        private int introCounter = 90;
        private float bounceCycle = 0;
        private float stageAnimationCycle = 0;
        private Random rng = new Random();
        private System.Windows.Forms.Timer renderTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer clockTimer = new System.Windows.Forms.Timer();

        public Form1()
        {
            InitializeComponent();

            this.Text = "Clash Arena";
            InitArcadeUi();   // sizes the window and builds the arcade cabinet (see Form1.ArcadeUi.cs)
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            for (int i = 0; i < 45; i++)
            {
                ambientMotes.Add(new AmbientMote
                {
                    X = rng.Next(-200, 1300),
                    Y = rng.Next(50, 580),
                    Vx = (float)(rng.NextDouble() * 1.5 - 0.75),
                    Vy = (float)(rng.NextDouble() * -2.0 - 0.5),
                    Size = rng.Next(3, 8),
                    Alpha = rng.Next(70, 220)
                });
            }

            renderTimer.Interval = 16;
            renderTimer.Tick += EngineLoop_Tick;
            renderTimer.Start();

            clockTimer.Interval = 1000;
            clockTimer.Tick += (s, e) =>
            {
                if (currentState == GameState.Battle && !paused && roundTimer > 0 && hitStopFreeze == 0)
                {
                    roundTimer--;
                }
            };
            clockTimer.Start();

            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.KeyUp += Form1_KeyUp;
            this.MouseClick += Form1_MouseClick;
            this.Deactivate += (s, e) =>
            {
                pressedKeys.Clear();
                if (currentState == GameState.Battle) paused = true;   // alt-tabbing pauses the fight
            };
        }

        private void EngineLoop_Tick(object? sender, EventArgs e)
        {
            if (paused)
            {
                this.Invalidate();
                return;
            }

            if (hitStopFreeze > 0)
            {
                hitStopFreeze--;
                this.Invalidate();
                return;
            }

            bounceCycle += 0.08f;
            arenaSlide += (selectedArenaIndex - arenaSlide) * 0.2f;
            if (arenaTransition < 1f) arenaTransition = Math.Min(1f, arenaTransition + 0.09f);
            stageAnimationCycle += 0.04f; // Drives the movement of lava, smoke, and billboards
            if (rageCinematicFlash > 0) rageCinematicFlash--;
            if (speedLineTimer > 0) speedLineTimer--;

            if (currentState == GameState.FightIntro)
            {
                introCounter--;
                if (introCounter <= 0) currentState = GameState.Battle;
            }
            else if (currentState == GameState.Battle)
            {
                if (isVsCPU) UpdateCPUAI();
                UpdateBattlePhysics();
            }
            else if (currentState == GameState.KO)
            {
                UpdateRoundEnd();
            }

            UpdateVisualEffects();
            UpdateFighterSprites();   // keep the sprite animations in step with the game (see Form1.Sprites.cs)
            this.Invalidate();
        }
    }
}