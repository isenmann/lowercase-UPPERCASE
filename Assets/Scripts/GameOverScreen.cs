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
    /// With a gamepad the name is entered the arcade way: ↑↓ choose the letter under the cursor, ←→ move the cursor,
    /// and the slot after the name holds an Enter sign (↵); A on it saves, A elsewhere goes one slot right, B deletes,
    /// Start saves at once, LB/RB switch the view. Keyboard typing works alongside, at the same cursor.
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
        // What ↑↓ step through on a gamepad, in order; the Enter sign comes before the first and after the last.
        public const string PadSymbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 -_.!?";
        // Holding a direction on the gamepad repeats it after a pause.
        const float RepeatDelaySeconds = 0.35f;
        const float RepeatSeconds = 0.08f;
        const float StickThreshold = 0.5f;

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
        /// <summary>Where the next letter goes: 0 to the name's length, where the length is the Enter sign's slot.</summary>
        public int Cursor { get; private set; }
        /// <summary>Whether the name is shown the gamepad's way, with the Enter sign after it.</summary>
        public bool PadMode { get; private set; }

        readonly StringBuilder typed = new StringBuilder();
        // The gamepad direction held last, and when it repeats next.
        int heldVertical, heldHorizontal;
        float verticalRepeatAt, horizontalRepeatAt;
        Keyboard listeningTo;
        // Some native players send no text input events. Until one arrives, the name is read from the key presses
        // instead.
        static bool textInputSeen;
        // The Linux player on X11 sends text input through its IME unreliably: after the last letter of an offered
        // name was deleted, nothing more arrived. There the name is only ever read from the key presses.
        static bool KeyPressesOnly => Application.platform == RuntimePlatform.LinuxPlayer;
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
            Cursor = typed.Length;
            PadMode = Gamepad.current != null && LetterInput.LastUsedGamepad;
            // A direction still held from the run waits to be let go before it counts.
            PadDirections(Gamepad.current, out heldVertical, out heldHorizontal);
            verticalRepeatAt = horizontalRepeatAt = float.PositiveInfinity;
            // Not good enough for the player's own table, the run is shown where it does count.
            Showing = Place < 0 && onlineRun ? View.World : View.Local;
            if (listHeading == null)
            {
                Transform found = transform.Find("High Scores Heading");
                if (found != null) listHeading = found.GetComponent<TMP_Text>();
            }
            worldPage = null;
            worldResult = null;
            foreach (TMP_Text row in rows) EnterSign.Use(row);
            EnterSign.Use(hint);
            if (recordsHighScores)
            {
                MakeRoomForOwnRow();
                FetchWorld();
            }
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
            // Spaces only count between letters.
            string name = TypedName.Trim();
            typed.Clear().Append(name);
            Cursor = typed.Length;
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
            if (listeningTo != null && !KeyPressesOnly)
            {
                listeningTo.SetIMEEnabled(true);
                listeningTo.onTextInput += OnTextInput;
            }
        }

        void OnTextInput(char c)
        {
            textInputSeen = true;
            PadMode = false;
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
                if (key.keyCode == Key.Space) { PadMode = false; Type(' '); continue; }
                string label = key.displayName;
                if (string.IsNullOrEmpty(label) || label.Length != 1) continue;
                char c = label[0];
                if (char.IsLetter(c)) c = shift ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                else if (shift) continue;
                PadMode = false;
                Type(c);
            }
        }

        /// <summary>One typed character for the new row's name, put in at the cursor; anything not allowed in a name
        /// is ignored.</summary>
        public void Type(char c)
        {
            if (!IsEnteringName || typed.Length >= HighScores.MaxNameLength || !HighScores.IsAllowedInName(c)) return;
            // A name doesn't start with a space.
            if (c == ' ' && Cursor == 0) return;
            typed.Insert(Cursor, c);
            Cursor++;
            Sfx.Play(Sound.TypeLetter);
        }

        /// <summary>Deletes the character before the cursor, as Backspace does.</summary>
        public void DeleteBack()
        {
            if (!IsEnteringName || Cursor == 0) return;
            typed.Remove(Cursor - 1, 1);
            Cursor--;
            Sfx.Play(Sound.TypeLetter);
        }

        /// <summary>Moves the cursor one slot, from the first letter up to the Enter sign after the last.</summary>
        public void MoveCursor(int step)
        {
            if (!IsEnteringName) return;
            int moved = Mathf.Clamp(Cursor + step, 0, typed.Length);
            if (moved == Cursor) return;
            Cursor = moved;
            Sfx.Play(Sound.MenuMove);
        }

        /// <summary>
        /// The gamepad's ↑ (+1) or ↓ (-1): the letter under the cursor steps through <see cref="PadSymbols"/>. On the
        /// Enter sign's slot it starts a new letter (if the name has room); stepping the last letter past either end
        /// turns it back into the Enter sign. Letters within the name just wrap around.
        /// </summary>
        public void StepLetter(int step)
        {
            if (!IsEnteringName || step == 0) return;
            step = step > 0 ? 1 : -1;
            int count = PadSymbols.Length;
            bool onEnter = Cursor == typed.Length;
            if (onEnter && typed.Length >= HighScores.MaxNameLength) return;
            bool last = Cursor == typed.Length - 1;

            int index = onEnter ? (step > 0 ? -1 : count) : PadSymbols.IndexOf(typed[Cursor]);
            // A letter that came from the keyboard and isn't in the list starts from its end.
            if (index < 0 && !onEnter) index = step > 0 ? -1 : count;
            for (int tries = 0; tries <= count + 1; tries++)
            {
                index += step;
                if (index < 0 || index >= count)
                {
                    if (onEnter || last)
                    {
                        // Back to the Enter sign: the slot is empty again.
                        if (last) typed.Length--;
                        Sfx.Play(Sound.TypeLetter);
                        return;
                    }
                    index = step > 0 ? 0 : count - 1;
                }
                // A name doesn't start with a space.
                if (PadSymbols[index] == ' ' && Cursor == 0) continue;
                break;
            }
            if (onEnter) typed.Append(PadSymbols[index]);
            else typed[Cursor] = PadSymbols[index];
            Sfx.Play(Sound.TypeLetter);
        }

        /// <summary>The gamepad's A: saves on the Enter sign, otherwise takes the letter and goes one slot right.</summary>
        public void PadAccept()
        {
            if (!IsEnteringName) return;
            if (Cursor >= typed.Length) ConfirmName();
            else MoveCursor(1);
        }

        /// <summary>The d-pad or left stick as -1, 0 or 1 up and right.</summary>
        static void PadDirections(Gamepad gamepad, out int up, out int right)
        {
            up = right = 0;
            if (gamepad == null) return;
            Vector2 stick = gamepad.leftStick.ReadValue();
            up = Sign(gamepad.dpad.down.isPressed || stick.y < -StickThreshold, gamepad.dpad.up.isPressed || stick.y > StickThreshold);
            right = Sign(gamepad.dpad.left.isPressed || stick.x < -StickThreshold, gamepad.dpad.right.isPressed || stick.x > StickThreshold);
        }

        static int Sign(bool negative, bool positive) => positive == negative ? 0 : positive ? 1 : -1;

        /// <summary>The direction to act on this frame: once on the press, then repeating while held.</summary>
        static int Repeating(int direction, ref int held, ref float repeatAt)
        {
            if (direction == 0) { held = 0; return 0; }
            if (direction != held)
            {
                held = direction;
                repeatAt = Time.unscaledTime + RepeatDelaySeconds;
                return direction;
            }
            if (Time.unscaledTime < repeatAt) return 0;
            repeatAt = Time.unscaledTime + RepeatSeconds;
            return direction;
        }

        /// <summary>The arcade name entry: ↑↓ the letter, ←→ the cursor, A take/save, B delete.</summary>
        void ReadPadName(Gamepad gamepad, bool fresh)
        {
            if (gamepad == null) { heldVertical = heldHorizontal = 0; return; }
            PadDirections(gamepad, out int up, out int right);
            int vertical = Repeating(up, ref heldVertical, ref verticalRepeatAt);
            int horizontal = Repeating(right, ref heldHorizontal, ref horizontalRepeatAt);
            if (!fresh) return;
            bool accept = gamepad.buttonSouth.wasPressedThisFrame;
            bool delete = gamepad.buttonEast.wasPressedThisFrame;
            if (vertical != 0 || horizontal != 0 || accept || delete) PadMode = true;
            if (vertical != 0) StepLetter(vertical);
            if (horizontal != 0) MoveCursor(horizontal);
            if (delete) DeleteBack();
            if (accept) PadAccept();
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
            // While a name is entered, the gamepad's ←→ move its cursor; the shoulder buttons switch views any time.
            bool switchView = fresh && ((keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame
                    || keyboard.rightArrowKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame))
                || (gamepad != null && (gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame))
                || (gamepad != null && !IsEnteringName
                    && (gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame)));
            if (switchView) SwitchView();

            if (IsEnteringName)
            {
                if (fresh && keyboard != null && (KeyPressesOnly || !textInputSeen)) TypeFromKeyPresses(keyboard);
                if (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame)
                {
                    PadMode = false;
                    DeleteBack();
                }
                ReadPadName(gamepad, fresh);
                bool start = fresh && gamepad != null && gamepad.startButton.wasPressedThisFrame;
                if (IsEnteringName && (enter || escape || start)) ConfirmName();
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
            if (ownRow != null) ownRow.text = "";
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
                if (i == Place && IsEnteringName) name = NameField();
                else if (name.Length > HighScores.MaxNameLength) name = name.Substring(0, HighScores.MaxNameLength);
                rows[i].text = $"{rank}  {name.PadRight(HighScores.MaxNameLength)}  {entries[i].score.ToString().PadLeft(ScoreWidth)}";
                rows[i].color = i == Place ? newRowColor : rowColor;
            }
        }

        /// <summary>
        /// The name being entered, as rich text padded to a name's width: the letter under the cursor underlined, and
        /// after the name a blinking "_" (keyboard) or the Enter sign (gamepad), which may stand one past the longest
        /// name.
        /// </summary>
        string NameField()
        {
            bool cursorOn = Mathf.Repeat(Time.unscaledTime, CursorBlinkSeconds * 2f) < CursorBlinkSeconds;
            var field = new StringBuilder();
            for (int i = 0; i < typed.Length; i++)
            {
                // Names hold no '<', so their letters can't open a tag.
                if (i == Cursor) field.Append("<u>").Append(typed[i]).Append("</u>");
                else field.Append(typed[i]);
            }
            int width = typed.Length;
            if (Cursor >= typed.Length)
            {
                if (PadMode)
                {
                    field.Append(cursorOn ? EnterSign.Text : "_");
                    width++;
                }
                else if (cursorOn && typed.Length < HighScores.MaxNameLength)
                {
                    field.Append('_');
                    width++;
                }
            }
            if (width < HighScores.MaxNameLength) field.Append(' ', HighScores.MaxNameLength - width);
            return field.ToString();
        }

        /// <summary>
        /// The world's top 10, and this run on a line of its own below them unless it is among them. While the name is
        /// typed, the run waits on that line with a "?" for its place.
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
            OwnRow.text = "";

            string note = WorldNote();
            if (page == null)
            {
                rows[NoteRow].text = worldLoading ? "loading…" : note ?? "offline";
                rows[NoteRow].color = emptyRowColor;
                return;
            }

            OnlineScores.ScoreRow[] top = page.scores ?? new OnlineScores.ScoreRow[0];
            OnlineScores.ScoreRow own = null;
            if (onlineRun && note == null || ranked)
            {
                own = new OnlineScores.ScoreRow
                {
                    rank = ranked ? worldResult.rank : 0,
                    name = IsEnteringName ? NameField() : ranked ? worldResult.name : TypedName,
                    total = ranked ? worldResult.total : runScore
                };
            }
            bool ownInTop = own != null && own.rank > 0 && own.rank <= Mathf.Min(rows.Length, top.Length);

            int rankWidth = Mathf.Max(2, Mathf.Max(rows.Length, own != null ? own.rank : 0).ToString().Length);
            for (int i = 0; i < rows.Length; i++)
            {
                if (i >= top.Length)
                {
                    rows[i].text = $"{(i + 1).ToString().PadLeft(rankWidth)}  {new string('.', HighScores.MaxNameLength)}  {"-".PadLeft(ScoreWidth)}";
                    rows[i].color = emptyRowColor;
                    continue;
                }
                bool isOwn = ownInTop && top[i].rank == own.rank;
                rows[i].text = WorldLine(top[i], rankWidth, isOwn);
                rows[i].color = isOwn ? newRowColor : rowColor;
            }

            if (own != null && !ownInTop)
            {
                OwnRow.text = WorldLine(own, rankWidth, true);
                OwnRow.color = newRowColor;
            }
        }

        string WorldLine(OnlineScores.ScoreRow entry, int rankWidth, bool isOwn)
        {
            string rank = entry.rank > 0 ? entry.rank.ToString() : "?";
            string name = entry.name ?? "";
            // The name being entered comes padded already, with its cursor and tags (see NameField).
            if (isOwn && IsEnteringName) return $"{rank.PadLeft(rankWidth)}  {name}  {entry.total.ToString().PadLeft(ScoreWidth)}";
            if (name.Length > HighScores.MaxNameLength) name = name.Substring(0, HighScores.MaxNameLength);
            return $"{rank.PadLeft(rankWidth)}  {name.PadRight(HighScores.MaxNameLength)}  {entry.total.ToString().PadLeft(ScoreWidth)}";
        }

        /// <summary>
        /// The line under the top 10 for a run that isn't among them. Levels are built with ten rows, so the eleventh is
        /// a copy of the last one, one row lower, made when first needed.
        /// </summary>
        public TMP_Text OwnRow
        {
            get
            {
                if (ownRow == null)
                {
                    TMP_Text last = rows[rows.Length - 1];
                    Vector3 step = last.transform.localPosition - rows[rows.Length - 2].transform.localPosition;
                    ownRow = Instantiate(last, last.transform.parent);
                    ownRow.name = "Own Row";
                    EnterSign.Use(ownRow);
                    ownRow.transform.localPosition = last.transform.localPosition + step;
                    ownRow.text = "";
                }
                return ownRow;
            }
        }

        TMP_Text ownRow;
        bool hintMoved;

        /// <summary>The hint goes one row lower, in both views, so the eleventh line has room above it.</summary>
        void MakeRoomForOwnRow()
        {
            if (hintMoved) return;
            hintMoved = true;
            Vector3 step = rows[rows.Length - 1].transform.localPosition - rows[rows.Length - 2].transform.localPosition;
            hint.transform.localPosition += step;
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
            if (IsEnteringName && PadMode)
                return (Place >= 0 ? "new high score!" : "your name for the world list")
                    + $"  ·  ↑↓  letter  ·  ←→  move  ·  A on {EnterSign.Text}  " + (Place >= 0 ? "save" : "send");
            if (IsEnteringName)
                return Place >= 0 ? "new high score!  type your name  ·  Enter  save" : "type your name for the world list  ·  Enter  send";
            string note = Showing == View.World ? WorldNote() : null;
            string views = Showing == View.World ? "←→  your scores" : "←→  world";
            return (note != null ? note + "  ·  " : "") + views + "  ·  Enter  continue";
        }
    }
}
