using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>Restart keys, scene switching, falling out of the world, and the HUD.</summary>
    public class PrototypeFlow : MonoBehaviour
    {
        const string MissingLetterColor = "#FFD23F";

        public LetterController letter;
        public float killY = -6f;

        [Tooltip("Falling or burning returns the letter to the last solid word instead of restarting the level.")]
        public bool respawnAtLastSafe;

        [Header("Sentence")]
        public TMP_Text sentence;
        public string sentenceBefore = "the c";
        public string missingLetter = "a";
        public string sentenceAfter = "t sat on the mat";

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

        Vector2 lastSafePosition;
        LetterShooter shooter;

        void Start()
        {
            shooter = letter.GetComponent<LetterShooter>();
            SetSentence(false);
            lastSafePosition = letter.transform.position;
        }

        void OnEnable()
        {
            letter.Slotted += OnSlotted;
            letter.Burned += LoseLife;
        }

        void OnDisable()
        {
            letter.Slotted -= OnSlotted;
            letter.Burned -= LoseLife;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.digit1Key.wasPressedThisFrame) LoadScene(0);
                if (keyboard.digit2Key.wasPressedThisFrame) LoadScene(1);
                if (keyboard.digit3Key.wasPressedThisFrame) LoadScene(2);
                if (keyboard.digit4Key.wasPressedThisFrame) LoadScene(3);
            }

            if (letter.IsGrounded && letter.Surface != null && letter.Surface.kind == WordKind.Solid)
                lastSafePosition = letter.transform.position;

            if (letter.transform.position.y < killY) LoseLife();

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

        void LoseLife()
        {
            if (respawnAtLastSafe) letter.Respawn(lastSafePosition);
            else Restart();
        }

        void OnSlotted() => SetSentence(true);

        void SetSentence(bool filled)
        {
            string blank = filled ? missingLetter : "_";
            sentence.text = $"{sentenceBefore}<color={MissingLetterColor}>{blank}</color>{sentenceAfter}";
        }

        static void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        static void LoadScene(int index)
        {
            if (index < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(index);
        }
    }
}
