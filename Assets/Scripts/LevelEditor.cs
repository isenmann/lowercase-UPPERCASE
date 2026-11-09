using System.Collections.Generic;
using LowercaseUppercase.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LowercaseUppercase
{
    /// <summary>
    /// The level editor. It keeps the level as <see cref="CustomLevelData"/> and shows it as a frozen preview, built by
    /// the same <see cref="PrototypeBuilder.BuildCustomLevel"/> that plays it, and rebuilt whole after every change.
    /// Clicking empty page starts a new word there; clicking a word edits it. Positions snap to an 8 px grid.
    /// </summary>
    public class LevelEditor : MonoBehaviour
    {
        public const float PixelsPerUnit = 72f;
        public const float GridPx = 8f;
        public const int MaxWordLength = 40;
        const float GridLinePx = 40f;
        const float ArrowPanPxPerSecond = 1200f;
        const float WheelPanPx = 160f;
        const float CaretAbovePx = 34f;  // From the surface up to the top of a tall letter.
        const float CaretBelowPx = 42f;  // From the surface down to the baseline and a little past it.
        const float CaretWidthPx = 3f;
        const float HitPaddingPx = 6f;
        const float TopBarPx = 64f;
        const float BottomBarPx = 72f;

        public Camera view;
        public Transform grid;
        public SpriteRenderer caret;
        public Transform cursorMark;
        public TMP_Text startMarker;

        [Header("HUD")]
        public TMP_Text readout;
        public TMP_Text sentence;
        public TMP_Text levelName;

        [Header("Assets the preview is built with")]
        public TMP_FontAsset mono;
        public TMP_FontAsset monoBold;
        public TMP_FontAsset black;
        public Sprite white;

        public CustomLevelData Level { get; private set; }
        /// <summary>The word being typed, or null.</summary>
        public CustomPiece Typing { get; private set; }
        public Transform Preview { get; private set; }
        public bool GridShown => grid != null && grid.gameObject.activeSelf;

        readonly Dictionary<CustomPiece, List<Transform>> built = new Dictionary<CustomPiece, List<Transform>>();
        string typingOriginal;
        bool typingIsNew;
        Keyboard listeningTo;
        bool textInputSeen;
        bool dragging;
        float dragStartMouseX;
        float dragStartCameraX;

        /// <summary>A fresh level: one word to stand on where the letter starts.</summary>
        public static CustomLevelData NewLevel()
        {
            var data = new CustomLevelData { name = "untitled" };
            data.pieces.Add(new CustomPiece(CustomPieceType.Word, 40f, CustomLevelData.GroundPx, "start here",
                CustomPieceType.Solid));
            return data;
        }

        void Awake()
        {
            Open(NewLevel());
        }

        void OnDisable() => Listen(null);

        /// <summary>Shows a level for editing, dropping whatever was being typed.</summary>
        public void Open(CustomLevelData level)
        {
            Typing = null;
            Listen(null);
            Level = level;
            Rebuild();
        }

        // ---------------------------------------------------------------- pixels

        public static float WorldX(float px) => (px - 640f) / PixelsPerUnit;
        public static float WorldY(float px) => (360f - px) / PixelsPerUnit;
        public static float PxX(float worldX) => worldX * PixelsPerUnit + 640f;
        public static float PxY(float worldY) => 360f - worldY * PixelsPerUnit;
        public static float Snap(float px) => Mathf.Round(px / GridPx) * GridPx;

        Vector2 MousePx(Mouse mouse)
        {
            Vector2 screen = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -view.transform.position.z));
            return new Vector2(PxX(world.x), PxY(world.y));
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            Pan(keyboard, mouse);

            Vector2 mousePx = MousePx(mouse);
            var snapped = new Vector2(Snap(mousePx.x), Snap(mousePx.y));
            cursorMark.position = new Vector3(WorldX(snapped.x), WorldY(snapped.y), 0f);

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsOverBars(mouse.position.ReadValue().y))
                Click(mousePx, snapped);

            if (Typing != null) TypeKeys(keyboard);
            else if (keyboard != null && keyboard.gKey.wasPressedThisFrame) grid.gameObject.SetActive(!GridShown);

            ShowReadout(snapped);
            FollowCameraWithGrid();
            PlaceCaret();
        }

        void Pan(Keyboard keyboard, Mouse mouse)
        {
            float x = view.transform.position.x;

            if (keyboard != null)
            {
                float direction = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
                x += direction * ArrowPanPxPerSecond / PixelsPerUnit * Time.unscaledDeltaTime;
            }

            if (mouse != null)
            {
                // Some platforms report 120 per notch, others 1.
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 10f) wheel /= 120f;
                x -= wheel * WheelPanPx / PixelsPerUnit;

                bool dragButton = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                float mouseX = mouse.position.ReadValue().x;
                if (dragButton && !dragging)
                {
                    dragging = true;
                    dragStartMouseX = mouseX;
                    dragStartCameraX = x;
                }
                else if (!dragButton) dragging = false;

                if (dragging)
                {
                    float unitsPerScreenPixel = 2f * view.orthographicSize / Mathf.Max(1, Screen.height);
                    x = dragStartCameraX - (mouseX - dragStartMouseX) * unitsPerScreenPixel;
                }
            }

            // The page starts at the left edge of the first screen.
            x = Mathf.Max(0f, x);
            view.transform.position = new Vector3(x, view.transform.position.y, view.transform.position.z);
        }

        /// <summary>The HUD bars stay at the screen's top and bottom edges, 64 and 72 px tall in the 720 px frame.</summary>
        bool IsOverBars(float screenY)
        {
            float screenPxPerPagePx = Screen.height / (2f * view.orthographicSize * PixelsPerUnit);
            return screenY > Screen.height - TopBarPx * screenPxPerPagePx || screenY < BottomBarPx * screenPxPerPagePx;
        }

        /// <summary>A left click at a page position: edits the word there, or starts a new one on the grid.</summary>
        public void ClickAt(Vector2 px) => Click(px, new Vector2(Snap(px.x), Snap(px.y)));

        void Click(Vector2 mousePx, Vector2 snapped)
        {
            if (Typing != null) Commit();

            CustomPiece word = WordAt(mousePx);
            if (word != null)
            {
                BeginTyping(word, isNew: false);
                return;
            }

            var piece = new CustomPiece(CustomPieceType.Word, snapped.x, snapped.y, "", CustomPieceType.Solid);
            Level.pieces.Add(piece);
            BeginTyping(piece, isNew: true);
        }

        /// <summary>The word whose letters are under a point, if any.</summary>
        public CustomPiece WordAt(Vector2 px)
        {
            var point = new Vector2(WorldX(px.x), WorldY(px.y));
            float padding = HitPaddingPx / PixelsPerUnit;
            foreach (KeyValuePair<CustomPiece, List<Transform>> entry in built)
            {
                if (entry.Key.type != CustomPieceType.Word) continue;
                foreach (Transform part in entry.Value)
                {
                    if (part == null) continue;
                    Renderer shape = part.GetComponent<Renderer>();
                    if (shape == null) continue;
                    Bounds bounds = shape.bounds;
                    bounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
                    if (bounds.Contains(new Vector3(point.x, point.y, bounds.center.z))) return entry.Key;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- typing

        void BeginTyping(CustomPiece piece, bool isNew)
        {
            Typing = piece;
            typingOriginal = piece.text;
            typingIsNew = isNew;
            Listen(Keyboard.current);
            Rebuild();
        }

        /// <summary>Places the word being typed. An emptied word is removed.</summary>
        public void Commit()
        {
            if (Typing == null) return;
            if (Typing.text.Trim().Length == 0) Level.pieces.Remove(Typing);
            EndTyping();
        }

        /// <summary>Drops the typing: a new word goes, an edited one gets its old text back.</summary>
        public void Cancel()
        {
            if (Typing == null) return;
            if (typingIsNew) Level.pieces.Remove(Typing);
            else Typing.text = typingOriginal;
            EndTyping();
        }

        void EndTyping()
        {
            Typing = null;
            Listen(null);
            Rebuild();
        }

        /// <summary>One typed character for the word being typed. Control characters are ignored.</summary>
        public void Type(char c)
        {
            if (Typing == null || char.IsControl(c) || Typing.text.Length >= MaxWordLength) return;
            // A word doesn't start with a space.
            if (c == ' ' && Typing.text.Length == 0) return;
            Typing.text += c;
            Rebuild();
        }

        public void Backspace()
        {
            if (Typing == null || Typing.text.Length == 0) return;
            Typing.text = Typing.text.Substring(0, Typing.text.Length - 1);
            Rebuild();
        }

        void TypeKeys(Keyboard keyboard)
        {
            if (keyboard == null) return;
            if (!textInputSeen) TypeFromKeyPresses(keyboard);
            if (keyboard.backspaceKey.wasPressedThisFrame) Backspace();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Commit();
            else if (keyboard.escapeKey.wasPressedThisFrame) Cancel();
        }

        void Listen(Keyboard keyboard)
        {
            if (listeningTo == keyboard) return;
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

        // ---------------------------------------------------------------- preview

        /// <summary>Builds the level again from its data, and stops everything in it from moving.</summary>
        public void Rebuild()
        {
            // A new root, because a destroyed one lingers until the end of the frame, and the builder looks for
            // grounds and doors among the root's children.
            if (Preview != null) Destroy(Preview.gameObject);
            Preview = new GameObject("Preview").transform;
            built.Clear();

            PrototypeBuilder.BuildCustomLevel(Preview, Level, mono, monoBold, black, white, (piece, part) =>
            {
                if (!built.TryGetValue(piece, out List<Transform> parts)) built[piece] = parts = new List<Transform>();
                parts.Add(part);
            });
            Freeze(Preview);
            // Meshes are made lazily, and finding the word under the mouse needs their bounds in this same frame.
            foreach (TMP_Text text in Preview.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();

            if (startMarker != null)
                startMarker.transform.position = new Vector3(WorldX(Level.startX - 12f), WorldY(Level.startY), 0f);
            if (sentence != null)
                sentence.text = $"{Level.sentenceBefore}<color=#FFD23F>_</color>{Level.sentenceAfter}";
            if (levelName != null) levelName.text = Level.name;
        }

        /// <summary>
        /// Switches off every behaviour in the preview before it starts, so enemies don't walk, springs don't
        /// bounce and nothing hides itself. TextMesh Pro's own components stay on, or the words wouldn't show.
        /// </summary>
        static void Freeze(Transform root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour.GetType().Namespace != "TMPro") behaviour.enabled = false;
            foreach (Rigidbody2D body in root.GetComponentsInChildren<Rigidbody2D>(true))
                body.simulated = false;
        }

        // ---------------------------------------------------------------- marks

        void PlaceCaret()
        {
            if (caret == null) return;
            bool blinkOn = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
            caret.enabled = Typing != null && blinkOn;
            if (Typing == null) return;

            float rightX = WorldX(Typing.x);
            if (built.TryGetValue(Typing, out List<Transform> parts))
                foreach (Transform part in parts)
                {
                    Renderer shape = part != null ? part.GetComponent<Renderer>() : null;
                    if (shape != null) rightX = Mathf.Max(rightX, shape.bounds.max.x);
                }

            float gap = 4f / PixelsPerUnit;
            float top = WorldY(Typing.y - CaretAbovePx);
            float bottom = WorldY(Typing.y + CaretBelowPx);
            caret.transform.position = new Vector3(rightX + gap, (top + bottom) * 0.5f, 0f);
            caret.transform.localScale = new Vector3(CaretWidthPx / PixelsPerUnit, top - bottom, 1f);
        }

        void FollowCameraWithGrid()
        {
            if (!GridShown) return;
            float leftPx = PxX(view.transform.position.x - view.orthographicSize * view.aspect);
            float firstLine = Mathf.Floor(leftPx / GridLinePx) * GridLinePx;
            grid.position = new Vector3(firstLine / PixelsPerUnit, 0f, 0f);
        }

        void ShowReadout(Vector2 snapped)
        {
            if (readout == null) return;
            string where = $"x {snapped.x:0}  y {snapped.y:0}";
            readout.text = Typing != null ? $"{where}   typing: Enter places it, Esc cancels" : where;
        }
    }
}
