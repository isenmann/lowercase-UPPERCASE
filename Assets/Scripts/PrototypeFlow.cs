using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>
    /// Restart keys, scene switching, falling out of the world, the level clock, and the HUD. It wakes first, so that the camera
    /// already finds the letter at the checkpoint when it centres itself in its own Awake.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PrototypeFlow : MonoBehaviour
    {
        const string MissingLetterColor = "#FFD23F";
        const int MenuBuildIndex = 0;
        const float OffscreenMargin = 0.3f;
        const float LowTimeSeconds = 10f;
        const float BankSeconds = 1.2f;
        // Lets the life-lost sound finish before the respawn sound starts.
        const float RespawnSoundDelay = 0.3f;

        public LetterController letter;
        public float killY = -6f;
        [Tooltip("No clock and no lives, for test rooms: deaths still return to the checkpoint and cost the asterisks.")]
        public bool practice;
        [Tooltip("Custom levels: a death costs no life, only the asterisks, and time running out restarts the level.")]
        public bool endlessLives;
        [Tooltip("The level's clock, in seconds.")]
        public float levelSeconds = LevelRun.LevelSeconds;
        public Checkpoint[] checkpoints;

        [Header("Sentence")]
        public TMP_Text sentence;
        public string sentenceBefore = "the c";
        public string missingLetter = "a";
        public string sentenceAfter = "t sat on the mat";

        [Header("Clock and score")]
        public TMP_Text timer;
        public TMP_Text score;
        [Tooltip("How many # have been picked up out of how many the level has. Hidden in levels without any.")]
        public TMP_Text collectibles;
        [Tooltip("The run's lives, one letter each; lost ones stay in place, dimmed.")]
        public TMP_Text lives;
        public string lifeGlyph = "a";
        public Color lifeColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        public Color lostLifeColor = new Color32(0x6B, 0x71, 0x80, 0xFF);
        public Color timerColor = new Color32(0xF2, 0xF0, 0xEA, 0xFF);
        public Color lowTimeColor = new Color32(0xFF, 0x4D, 0x5E, 0xFF);

        [Header("Pause")]
        public PauseMenu pauseMenu;

        [Header("Game over")]
        public GameOverScreen gameOver;

        [Header("Form chips")]
        public SpriteRenderer lowerChip;
        public SpriteRenderer upperChip;
        public TMP_Text lowerChipLabel;
        public TMP_Text upperChipLabel;
        public Color activeChip = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        public Color idleChip = new Color32(0x1A, 0x1D, 0x24, 0xFF);
        public Color activeLabel = new Color32(0x11, 0x13, 0x18, 0xFF);
        public Color idleLabel = new Color32(0x6B, 0x71, 0x80, 0xFF);

        [Header("Status readout: Caps Lock and asterisks")]
        public TMP_Text readout;

        LetterShooter shooter;
        string shootHint = "";
        LevelTransition transition;
        float timeAtSlot;
        int scoreAtSlot;
        float bankProgress;
        int collectibleCount;
        Checkpoint resumedAt;
        int lastTickSecond = -1;
        bool fallSoundPlayed;

        public bool IsGameOver { get; private set; }

        // The clock also reads zero once the time left has been banked at the exit.
        bool RunIsLost => !practice && LevelRun.TimeIsUp && !letter.IsSlotted;

        void Awake()
        {
            Camera view = Camera.main;
            if (view != null && view.GetComponent<ScreenFit>() == null)
                view.gameObject.AddComponent<ScreenFit>().hud = timer.transform.parent;
            if (LevelRun.EnterLevel(levelSeconds)) Sfx.Play(Sound.Respawn, RespawnSoundDelay);
            resumedAt = Checkpoint.TakeResume(checkpoints);
            if (resumedAt != null) letter.transform.position = resumedAt.respawnFeet;
        }

        void Start()
        {
            shooter = letter.GetComponent<LetterShooter>();
            transition = GetComponent<LevelTransition>();
            ShootKey.RewriteHints();
            shootHint = $"* {ShootKey.Name} to shoot";
            SetSentence(false);
            // Inactive ones too, because those taken before a death are hidden as the scene loads.
            collectibleCount = FindObjectsByType<Collectible>(FindObjectsInactive.Include).Length;
        }

        void OnEnable()
        {
            letter.Slotted += OnSlotted;
            letter.Burned += letter.FallOffScreen;
        }

        void OnDisable()
        {
            letter.Slotted -= OnSlotted;
            letter.Burned -= letter.FallOffScreen;
        }

        void Update()
        {
            // The game over screen reads its own keys, including the name typed into the high scores.
            if (IsGameOver || (gameOver != null && gameOver.IsShowing)) return;
            Keyboard keyboard = Keyboard.current;

            // The popup reads its own keys, and the level waits underneath it.
            if (pauseMenu != null && pauseMenu.IsOpen) return;

            bool inTransition = transition != null && transition.IsPlaying;
            bool pausePressed = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (pausePressed)
            {
                if (pauseMenu == null) LoadScene(MenuBuildIndex);
                else if (CanPause(inTransition))
                {
                    pauseMenu.Open();
                    return;
                }
            }

            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame && !inTransition && !RunIsLost)
                {
                    LevelRun.AbandonLevel();
                    Restart();
                }
            }

            // Scaled time, so the intro doesn't use up the clock. It waits during a fall, so a death can't run it out.
            if (!practice && !letter.IsSlotted && !letter.IsFallingOff)
            {
                LevelRun.Tick(Time.deltaTime);
                TickLowTime();
                if (LevelRun.TimeIsUp)
                {
                    Sfx.Play(Sound.TimeUp);
                    letter.FallOffScreen();
                }
            }

            // Falling into a pit, as opposed to the death fall, which has already made its sound.
            if (!letter.IsFallingOff && !letter.IsSlotted && !fallSoundPlayed && HasLeftTheScreen())
            {
                fallSoundPlayed = true;
                Sfx.Play(Sound.FallOff);
            }

            if (letter.IsFallingOff ? HasLeftTheScreen() : letter.transform.position.y < killY) LoseLife();

            ShowClockAndScore();

            bool lower = letter.Form == LetterForm.Lowercase;
            lowerChip.color = lower ? activeChip : idleChip;
            upperChip.color = lower ? idleChip : activeChip;
            lowerChipLabel.color = lower ? activeLabel : idleLabel;
            upperChipLabel.color = lower ? idleLabel : activeLabel;

            string caps = letter.IsCapsLocked ? "CAPS LOCK" : "";
            string asterisks = shooter != null && !shooter.startsArmed && shooter.IsArmed ? shootHint : "";
            readout.text = caps.Length > 0 && asterisks.Length > 0 ? $"{caps}   {asterisks}" : caps + asterisks;
        }

        /// <summary>One tick a second through the last seconds of the clock.</summary>
        void TickLowTime()
        {
            int second = Mathf.CeilToInt(LevelRun.TimeLeft);
            if (second != lastTickSecond && second > 0 && second <= LowTimeSeconds && lastTickSecond != -1)
                Sfx.Play(Sound.TimerTick);
            lastTickSecond = second;
        }

        // Not while the level animates in or out, and not during a death fall, which is already on its way to a reload.
        bool CanPause(bool inTransition) => !inTransition && !letter.IsSlotted && !letter.IsFallingOff;

        bool HasLeftTheScreen()
        {
            Camera view = Camera.main;
            float top = letter.transform.position.y + letter.Current.colliderSize.y + OffscreenMargin;
            return top < view.transform.position.y - view.orthographicSize;
        }

        void LoseLife()
        {
            if (!letter.IsFallingOff && !fallSoundPlayed) Sfx.Play(Sound.FallOff);
            if (practice) LevelRun.HasAsterisks = false;
            else if (endlessLives)
            {
                if (RunIsLost)
                {
                    // Out of time: a fresh attempt from the start, with the clock refilled.
                    Sfx.Play(Sound.LifeLost);
                    LevelRun.AbandonLevel();
                    Restart();
                    return;
                }
                LevelRun.HasAsterisks = false;
            }
            else if (RunIsLost || !LevelRun.LoseLife())
            {
                ShowLives();
                ShowGameOver();
                return;
            }

            Sfx.Play(Sound.LifeLost);

            Checkpoint furthest = Checkpoint.Furthest(checkpoints);
            if (furthest != null) furthest.ResumeAfterReload();
            LevelRun.ContinueAfterDeath();
            Restart();
        }

        void ShowGameOver()
        {
            IsGameOver = true;
            // Letters typed into the high scores must not move or fire the letter.
            var input = letter.GetComponent<LetterInput>();
            if (input != null) input.enabled = false;
            letter.SetInput(0f, false, false);
            gameOver.Show(LevelRun.Score, gameOver: true);
        }

        void OnSlotted()
        {
            Sfx.Play(Sound.LevelComplete);
            SetSentence(true);
            timeAtSlot = LevelRun.TimeLeft;
            scoreAtSlot = LevelRun.Score;
            if (!practice) LevelRun.BankTimeLeft();
        }

        /// <summary>Once the letter has slotted, the time left visibly drains into the score. The points are already banked.</summary>
        void ShowClockAndScore()
        {
            float timeShown = LevelRun.TimeLeft;
            int scoreShown = LevelRun.Score;
            if (letter.IsSlotted)
            {
                bankProgress = Mathf.MoveTowards(bankProgress, 1f, Time.unscaledDeltaTime / BankSeconds);
                timeShown = Mathf.Lerp(timeAtSlot, 0f, bankProgress);
                scoreShown = Mathf.RoundToInt(Mathf.Lerp(scoreAtSlot, LevelRun.Score, bankProgress));
            }

            timer.text = practice ? "" : $"{Mathf.CeilToInt(timeShown)}s";
            timer.color = !letter.IsSlotted && timeShown <= LowTimeSeconds ? lowTimeColor : timerColor;
            score.text = $"score {scoreShown}";
            ShowLives();
            if (collectibles != null)
                collectibles.text = collectibleCount > 0
                    ? $"<color={MissingLetterColor}>#</color> {LevelRun.CollectedThisLevel}/{collectibleCount}"
                    : "";
        }

        void ShowLives()
        {
            if (lives == null) return;
            if (practice || endlessLives)
            {
                lives.text = "";
                return;
            }
            string on = ColorUtility.ToHtmlStringRGBA(lifeColor);
            string off = ColorUtility.ToHtmlStringRGBA(lostLifeColor);
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < LevelRun.StartingLives; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append($"<color=#{(i < LevelRun.Lives ? on : off)}>{lifeGlyph}</color>");
            }
            lives.text = text.ToString();
        }

        void SetSentence(bool filled)
        {
            string blank = filled ? missingLetter : "_";
            sentence.text = $"{sentenceBefore}<color={MissingLetterColor}>{blank}</color>{sentenceAfter}";
        }

        void Restart()
        {
            if (transition != null) LevelTransition.SkipNextIntro = true;
            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0) SceneManager.LoadScene(active.buildIndex);
            else ReloadOutsideTheBuild(active.path);
        }

        /// <summary>
        /// Test rooms such as the Enemy Room aren't in the build settings, so they have no build index to reload by.
        /// The editor can still load them by path; a player build never contains them.
        /// </summary>
        static void ReloadOutsideTheBuild(string path)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
        }

        static void LoadScene(int index)
        {
            if (index < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(index);
        }
    }
}
