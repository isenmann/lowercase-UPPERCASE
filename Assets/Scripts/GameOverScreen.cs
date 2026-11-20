using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>
    /// GAME OVER (left out when the run was finished instead), the run's score and the ten best runs. A score good enough for the table appears in it straight away
    /// with a blinking cursor, and whatever the player types goes into that row; Enter or Esc saves it. Without a new
    /// entry, or once the name is saved, the screen goes back to the menu after a while, or at once on Enter or Esc.
    /// A run that went online (<see cref="OnlineScores"/>) always asks for a name, offered as the one typed last, and
    /// sends it to the world list. ← → or Tab switch between the player's own table and the world list, which shows the
    /// top runs and this run's place.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        const int MenuBuildIndex = 0;
        const float ShowSeconds = 6f;
        const float AfterNameSeconds = 4f;
        const float CursorBlinkSeconds = 0.4f;
        const int ScoreWidth = 7;
        // The row of the world list that says why the list isn't there.
        const int NoteRow = 4;

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

        public enum View { Local, World }

        public bool IsShowing { get; private set; }
        public View Showing { get; private set; }
        public bool IsEnteringName { get; private set; }
        /// <summary>The run's place in the table, or -1 if it didn't make it.</summary>
        public int Place { get; private set; } = -1;
        public string TypedName => typed.ToString();

        readonly StringBuilder typed = new StringBuilder();
        Keyboard listeningTo;
        // Some native players (the Linux one on X11) send no text input events. Until one arrives,
        // the name is read from the key presses instead.
        static bool textInputSeen;
        float leavesAt;
        int shownOnFrame = -1;
        int runScore;
        // Whether this run went online as it ended, so its name goes to the world list.
        bool onlineRun;
        TMP_Text listHeading;
        OnlineScores.ScoresPage worldPage;
        OnlineScores.Result worldResult;
        bool worldLoading;

        public void Show(int runScore, bool gameOver)
        {
            if (heading != null) heading.SetActive(gameOver);
            IsShowing = true;
            shownOnFrame = Time.frameCount;
            score.text = $"score {runScore}";
            this.runScore = runScore;
            typed.Clear();
            // A game over stops in a level that was never reported; after the last level everything was.
            OnlineScores.RunOver(gameOver ? LevelRun.PointsThisLevel : 0);
            Place = recordsHighScores ? HighScores.Insert(runScore) : -1;
            onlineRun = recordsHighScores && OnlineScores.State == OnlineScores.RunState.Online;
            IsEnteringName = Place >= 0 || onlineRun;
            if (IsEnteringName) typed.Append(HighScores.LastName);
            // Not good enough for the player's own table, the run is shown where it does count.
            Showing = Place < 0 && onlineRun ? View.World : View.Local;
            if (listHeading == null)
            {
                Transform found = transform.Find("High Scores Heading");
                if (found != null) listHeading = found.GetComponent<TMP_Text>();
            }
            worldPage = null;
            worldResult = null;
            if (recordsHighScores) FetchWorld();
            Sound ending = gameOver ? Sound.GameOver : Sound.GameFinished;
            Sfx.Play(ending);
            // The high score jingle follows the ending instead of playing over it.
            AudioClip endingClip = Sfx.Clip(ending);
            if (Place >= 0) Sfx.Play(Sound.HighScore, endingClip != null ? endingClip.length : 0f);
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
            if (Place >= 0) HighScores.Rename(Place, TypedName);
            if (typed.Length > 0) HighScores.LastName = TypedName;
            Sfx.Play(Sound.NameSaved);
            leavesAt = Time.unscaledTime + AfterNameSeconds;
            if (onlineRun) OnlineScores.Finish(TypedName, OnFinished);
            Refresh();
        }

        /// <summary>Between the player's own table and the world list. Waiting to leave starts again, so there's time to look.</summary>
        public void SwitchView()
        {
            if (!recordsHighScores) return;
            Showing = Showing == View.Local ? View.World : View.Local;
            Sfx.Play(Sound.MenuMove);
            if (!IsEnteringName) leavesAt = Mathf.Max(leavesAt, Time.unscaledTime + ShowSeconds);
            Refresh();
        }

        void OnFinished(OnlineScores.Result result)
        {
            // The answer can come after the menu has replaced this level.
            if (this == null) return;
            worldResult = result;
            if (result == null) return;
            leavesAt = Mathf.Max(leavesAt, Time.unscaledTime + AfterNameSeconds);
            FetchWorld();
        }

        void FetchWorld()
        {
            worldLoading = true;
            OnlineScores.FetchScores(rows.Length, page =>
            {
                if (this == null) return;
                worldLoading = false;
                if (page != null) worldPage = page;
            });
        }

        void OnDisable() => Listen(null);

        void Listen(Keyboard keyboard)
        {
            if (listeningTo != null)
            {
                listeningTo.onTextInput -= OnTextInput;
                listeningTo.SetIMEEnabled(false);
            }
            listeningTo = keyboard;
            if (listeningTo != null)
            {
                listeningTo.SetIMEEnabled(true);
                listeningTo.onTextInput += OnTextInput;
            }
        }

        void OnTextInput(char c)
        {
            textInputSeen = true;
            Type(c);
        }

        /// <summary>Types the characters of this frame's key presses, for players that send no text input events.
        /// Letters and digits follow the keyboard layout through the keys' display names.</summary>
        void TypeFromKeyPresses(Keyboard keyboard)
        {
            bool shift = keyboard.shiftKey.isPressed;
            foreach (KeyControl key in keyboard.allKeys)
            {
                if (key == null || !key.wasPressedThisFrame) continue;
                if (key.keyCode == Key.Space) { Type(' '); continue; }
                string label = key.displayName;
                if (string.IsNullOrEmpty(label) || label.Length != 1) continue;
                char c = label[0];
                if (char.IsLetter(c)) c = shift ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                else if (shift) continue;
                Type(c);
            }
        }

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
            bool switchView = fresh && ((keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame
                    || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame))
                || (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame)));
            if (switchView) SwitchView();

            if (IsEnteringName)
            {
                if (fresh && keyboard != null && !textInputSeen) TypeFromKeyPresses(keyboard);
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

            if (Showing == View.World) ShowWorld();
            else ShowLocal();
            hint.text = Hint();
        }

        void ShowLocal()
        {
            if (listHeading != null) listHeading.text = "your high scores";
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
        }

        string NameWithCursor()
        {
            bool cursorOn = Mathf.Repeat(Time.unscaledTime, CursorBlinkSeconds * 2f) < CursorBlinkSeconds;
            return TypedName + (cursorOn && typed.Length < HighScores.MaxNameLength ? "_" : "");
        }

        /// <summary>
        /// The top runs, and this run below them unless it is among them. While the name is typed, or when the run
        /// didn't count online, the list is shown as it stands.
        /// </summary>
        void ShowWorld()
        {
            OnlineScores.ScoresPage page = worldPage;
            bool ranked = worldResult != null;
            string heading = "world high scores";
            if (ranked) heading += $"  ·  #{worldResult.rank} of {worldResult.count}";
            if (listHeading != null) listHeading.text = heading;

            foreach (TMP_Text row in rows)
            {
                row.text = "";
                row.color = rowColor;
            }

            string note = WorldNote();
            if (page == null)
            {
                rows[NoteRow].text = worldLoading ? "loading…" : note ?? "offline";
                rows[NoteRow].color = emptyRowColor;
                return;
            }

            var shown = new List<OnlineScores.ScoreRow>(page.scores ?? new OnlineScores.ScoreRow[0]);
            OnlineScores.ScoreRow own = null;
            bool ownBelow = false;
            if (onlineRun && note == null || ranked)
            {
                own = new OnlineScores.ScoreRow
                {
                    rank = ranked ? worldResult.rank : 0,
                    name = IsEnteringName ? NameWithCursor() : ranked ? worldResult.name : TypedName,
                    total = ranked ? worldResult.total : runScore
                };
                ownBelow = !ranked || own.rank > rows.Length || own.rank > shown.Count;
                if (ownBelow && shown.Count > rows.Length - 1) shown.RemoveRange(rows.Length - 1, shown.Count - rows.Length + 1);
            }

            int last = Mathf.Max(rows.Length, own != null ? own.rank : 0);
            int rankWidth = Mathf.Max(2, last.ToString().Length);
            for (int i = 0; i < rows.Length; i++)
            {
                OnlineScores.ScoreRow entry = i < shown.Count ? shown[i] : null;
                bool isOwn = false;
                if (own != null && ownBelow && i == Mathf.Min(shown.Count, rows.Length - 1))
                {
                    entry = own;
                    isOwn = true;
                }
                else if (own != null && !ownBelow && entry != null && entry.rank == own.rank) isOwn = true;

                if (entry == null)
                {
                    rows[i].text = $"{(i + 1).ToString().PadLeft(rankWidth)}  {new string('.', HighScores.MaxNameLength)}  {"-".PadLeft(ScoreWidth)}";
                    rows[i].color = emptyRowColor;
                    continue;
                }
                string rank = entry.rank > 0 ? entry.rank.ToString() : "?";
                string name = entry.name ?? "";
                if (name.Length > HighScores.MaxNameLength && !(isOwn && IsEnteringName)) name = name.Substring(0, HighScores.MaxNameLength);
                rows[i].text = $"{rank.PadLeft(rankWidth)}  {name.PadRight(HighScores.MaxNameLength)}  {entry.total.ToString().PadLeft(ScoreWidth)}";
                rows[i].color = isOwn ? newRowColor : rowColor;
            }
        }

        /// <summary>Why this run isn't on the world list, or null when it is (or is on its way).</summary>
        string WorldNote()
        {
            if (!OnlineScores.Enabled) return "online scores are off (Options)";
            if (!OnlineScores.CanGoOnline) return "offline";
            if (worldResult != null) return worldResult.nameRefused ? "name not allowed online" : null;
            if (OnlineScores.State == OnlineScores.RunState.Offline)
                return OnlineScores.LastError == "OldVersion" ? "online scores need the latest version" : "offline, this run stays local";
            if (!onlineRun) return "only runs from the first level go online";
            return null;
        }

        string Hint()
        {
            if (IsEnteringName)
                return Place >= 0 ? "new high score!  type your name  ·  Enter  save" : "type your name for the world list  ·  Enter  send";
            string note = Showing == View.World ? WorldNote() : null;
            string views = Showing == View.World ? "←→  your scores" : "←→  world";
            return (note != null ? note + "  ·  " : "") + views + "  ·  Enter  continue";
        }
    }
}
