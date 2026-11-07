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
        const float GameOverSeconds = 2.5f;

        public LetterController letter;
        public float killY = -6f;
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
        public Color timerColor = new Color32(0xF2, 0xF0, 0xEA, 0xFF);
        public Color lowTimeColor = new Color32(0xFF, 0x4D, 0x5E, 0xFF);

        [Header("Game over")]
        public GameObject gameOverScreen;
        public TMP_Text gameOverScore;

        [Header("Form chips")]
        public SpriteRenderer lowerChip;
        public SpriteRenderer upperChip;
        public TMP_Text lowerChipLabel;
        public TMP_Text upperChipLabel;
        public Color activeChip = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        public Color idleChip = new Color32(0x1A, 0x1D, 0x24, 0xFF);
        public Color activeLabel = new Color32(0x11, 0x13, 0x18, 0xFF);
        public Color idleLabel = new Color32(0x6B, 0x71, 0x80, 0xFF);

        [Header("Tuning readout")]
        public TMP_Text readout;

        LetterShooter shooter;
        LevelTransition transition;
        float timeAtSlot;
        int scoreAtSlot;
        float bankProgress;
        float gameOverEnds;
        int collectibleCount;
        Checkpoint resumedAt;

        public bool IsGameOver { get; private set; }

        // The clock also reads zero once the time left has been banked at the exit.
        bool RunIsLost => LevelRun.TimeIsUp && !letter.IsSlotted;

        void Awake()
        {
            LevelRun.EnterLevel();
            resumedAt = Checkpoint.TakeResume(checkpoints);
            if (resumedAt != null) letter.transform.position = resumedAt.respawnFeet;
        }

        void Start()
        {
            shooter = letter.GetComponent<LetterShooter>();
            transition = GetComponent<LevelTransition>();
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
            Keyboard keyboard = Keyboard.current;
            if (IsGameOver)
            {
                bool skipped = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
                if (skipped || Time.unscaledTime >= gameOverEnds) LoadScene(MenuBuildIndex);
                return;
            }

            if (keyboard != null)
            {
                bool inTransition = transition != null && transition.IsPlaying;
                if (keyboard.rKey.wasPressedThisFrame && !inTransition && !RunIsLost)
                {
                    LevelRun.AbandonLevel();
                    Restart();
                }
                if (keyboard.escapeKey.wasPressedThisFrame) LoadScene(MenuBuildIndex);
                if (keyboard.digit1Key.wasPressedThisFrame) LoadScene(1);
                if (keyboard.digit2Key.wasPressedThisFrame) LoadScene(2);
                if (keyboard.digit3Key.wasPressedThisFrame) LoadScene(3);
                if (keyboard.digit4Key.wasPressedThisFrame) LoadScene(4);
                if (keyboard.digit5Key.wasPressedThisFrame) LoadScene(5);
                if (keyboard.digit6Key.wasPressedThisFrame) LoadScene(6);
                if (keyboard.digit7Key.wasPressedThisFrame) LoadScene(7);
                if (keyboard.digit8Key.wasPressedThisFrame) LoadScene(8);
            }

            // Scaled time, so the intro doesn't use up the clock. It waits during a fall, so a death can't run it out.
            if (!letter.IsSlotted && !letter.IsFallingOff)
            {
                LevelRun.Tick(Time.deltaTime);
                if (LevelRun.TimeIsUp) letter.FallOffScreen();
            }

            if (letter.IsFallingOff ? HasLeftTheScreen() : letter.transform.position.y < killY) LoseLife();

            ShowClockAndScore();

            bool lower = letter.Form == LetterForm.Lowercase;
            lowerChip.color = lower ? activeChip : idleChip;
            upperChip.color = lower ? idleChip : activeChip;
            lowerChipLabel.color = lower ? activeLabel : idleLabel;
            upperChipLabel.color = lower ? idleLabel : activeLabel;

            Vector2 velocity = letter.IsSlotted ? Vector2.zero : letter.Velocity;
            string caps = letter.IsCapsLocked ? "   CAPS LOCK" : "";
            string asterisks = shooter != null && !shooter.startsArmed && shooter.IsArmed ? "   * F to shoot" : "";
            readout.text = $"speed {Mathf.Abs(velocity.x):0.0}   fall {velocity.y:0.0}{caps}{asterisks}";
        }

        bool HasLeftTheScreen()
        {
            Camera view = Camera.main;
            float top = letter.transform.position.y + letter.Current.colliderSize.y + OffscreenMargin;
            return top < view.transform.position.y - view.orthographicSize;
        }

        void LoseLife()
        {
            if (RunIsLost)
            {
                ShowGameOver();
                return;
            }

            Checkpoint furthest = Checkpoint.Furthest(checkpoints);
            if (furthest != null) furthest.ResumeAfterReload();
            LevelRun.ContinueAfterDeath();
            Restart();
        }

        void ShowGameOver()
        {
            IsGameOver = true;
            gameOverEnds = Time.unscaledTime + GameOverSeconds;
            gameOverScore.text = $"score {LevelRun.Score}";
            gameOverScreen.SetActive(true);
        }

        void OnSlotted()
        {
            SetSentence(true);
            timeAtSlot = LevelRun.TimeLeft;
            scoreAtSlot = LevelRun.Score;
            LevelRun.BankTimeLeft();
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

            timer.text = $"{Mathf.CeilToInt(timeShown)}s";
            timer.color = !letter.IsSlotted && timeShown <= LowTimeSeconds ? lowTimeColor : timerColor;
            score.text = $"score {scoreShown}";
            if (collectibles != null)
                collectibles.text = collectibleCount > 0
                    ? $"<color={MissingLetterColor}>#</color> {LevelRun.CollectedThisLevel}/{collectibleCount}"
                    : "";
        }

        void SetSentence(bool filled)
        {
            string blank = filled ? missingLetter : "_";
            sentence.text = $"{sentenceBefore}<color={MissingLetterColor}>{blank}</color>{sentenceAfter}";
        }

        void Restart()
        {
            if (transition != null) LevelTransition.SkipNextIntro = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static void LoadScene(int index)
        {
            if (index < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(index);
        }
    }
}
