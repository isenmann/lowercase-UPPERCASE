using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>
    /// GAME OVER (left out when the run was finished instead), the run's score and the ten best runs. A score good enough for the table appears in it straight away
    /// with a blinking cursor, and whatever the player types goes into that row; Enter or Esc saves it. Without a new
    /// entry, or once the name is saved, the screen goes back to the menu after a while, or at once on Enter or Esc.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        const int MenuBuildIndex = 0;
        const float ShowSeconds = 6f;
        const float AfterNameSeconds = 4f;
        const float CursorBlinkSeconds = 0.4f;
        const int ScoreWidth = 7;

        [Tooltip("The GAME OVER text, hidden when the screen follows the last level.")]
        public GameObject heading;
        public TMP_Text score;
        [Tooltip("One line per place, best first.")]
        public TMP_Text[] rows;
        public TMP_Text hint;
        public Color rowColor = new Color32(0xF2, 0xF0, 0xEA, 0xFF);
        public Color emptyRowColor = new Color32(0x6B, 0x71, 0x80, 0xFF);
        public Color newRowColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        [Tooltip("Off for random levels: the score is shown, but the table is neither changed nor shown.")]
        public bool recordsHighScores = true;

        public bool IsShowing { get; private set; }
        public bool IsEnteringName { get; private set; }
        /// <summary>The run's place in the table, or -1 if it didn't make it.</summary>
        public int Place { get; private set; } = -1;
        public string TypedName => typed.ToString();

        readonly StringBuilder typed = new StringBuilder();
        Keyboard listeningTo;
        float leavesAt;
        int shownOnFrame = -1;

        public void Show(int runScore, bool gameOver)
        {
            if (heading != null) heading.SetActive(gameOver);
            IsShowing = true;
            shownOnFrame = Time.frameCount;
            score.text = $"score {runScore}";
            typed.Clear();
            Place = recordsHighScores ? HighScores.Insert(runScore) : -1;
            IsEnteringName = Place >= 0;
            Sound ending = gameOver ? Sound.GameOver : Sound.GameFinished;
            Sfx.Play(ending);
            // The high score jingle follows the ending instead of playing over it.
            AudioClip endingClip = Sfx.Clip(ending);
            if (IsEnteringName) Sfx.Play(Sound.HighScore, endingClip != null ? endingClip.length : 0f);
            if (IsEnteringName) Listen(Keyboard.current);
            else leavesAt = Time.unscaledTime + ShowSeconds;
            gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>Saves what has been typed so far as the name for the new row.</summary>
        public void ConfirmName()
        {
            if (!IsEnteringName) return;
            IsEnteringName = false;
            Listen(null);
            HighScores.Rename(Place, TypedName);
            Sfx.Play(Sound.NameSaved);
            leavesAt = Time.unscaledTime + AfterNameSeconds;
            Refresh();
        }

        void OnDisable() => Listen(null);

        void Listen(Keyboard keyboard)
        {
            if (listeningTo != null) listeningTo.onTextInput -= OnTextInput;
            listeningTo = keyboard;
            if (listeningTo != null) listeningTo.onTextInput += OnTextInput;
        }

        void OnTextInput(char c) => Type(c);

        /// <summary>One typed character for the new row's name; anything not allowed in a name is ignored.</summary>
        public void Type(char c)
        {
            if (!IsEnteringName || typed.Length >= HighScores.MaxNameLength || !HighScores.IsAllowedInName(c)) return;
            // A name doesn't start with a space.
            if (c == ' ' && typed.Length == 0) return;
            typed.Append(c);
            Sfx.Play(Sound.TypeLetter);
        }

        void Update()
        {
            if (!IsShowing) return;
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            // The key press that ended the run, if any, isn't an answer to this screen.
            bool fresh = Time.frameCount != shownOnFrame;

            bool enter = fresh && keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
            bool escape = fresh && keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool button = fresh && gamepad != null
                && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);

            if (IsEnteringName)
            {
                if (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame && typed.Length > 0)
                {
                    typed.Length--;
                    Sfx.Play(Sound.TypeLetter);
                }
                if (enter || escape || button) ConfirmName();
            }
            else
            {
                bool space = fresh && keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
                if (enter || escape || space || button || Time.unscaledTime >= leavesAt)
                {
                    IsShowing = false;
                    SceneManager.LoadScene(MenuBuildIndex);
                    return;
                }
            }

            Refresh();
        }

        void Refresh()
        {
            if (!recordsHighScores)
            {
                foreach (TMP_Text row in rows) row.text = "";
                hint.text = "random levels don't count for high scores  ·  Enter  continue";
                return;
            }

            var entries = HighScores.Load();
            for (int i = 0; i < rows.Length; i++)
            {
                string rank = (i + 1).ToString().PadLeft(2);
                if (i >= entries.Count)
                {
                    rows[i].text = $"{rank}  {new string('.', HighScores.MaxNameLength)}  {"-".PadLeft(ScoreWidth)}";
                    rows[i].color = emptyRowColor;
                    continue;
                }

                string name = entries[i].name;
                if (i == Place && IsEnteringName)
                {
                    bool cursorOn = Mathf.Repeat(Time.unscaledTime, CursorBlinkSeconds * 2f) < CursorBlinkSeconds;
                    name = TypedName + (cursorOn && typed.Length < HighScores.MaxNameLength ? "_" : "");
                }
                if (name.Length > HighScores.MaxNameLength) name = name.Substring(0, HighScores.MaxNameLength);
                rows[i].text = $"{rank}  {name.PadRight(HighScores.MaxNameLength)}  {entries[i].score.ToString().PadLeft(ScoreWidth)}";
                rows[i].color = i == Place ? newRowColor : rowColor;
            }

            if (IsEnteringName) hint.text = "new high score!  type your name  ·  Enter  save";
            else hint.text = "Enter  continue";
        }
    }
}
