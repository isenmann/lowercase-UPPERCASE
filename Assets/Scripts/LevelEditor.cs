using System.Collections.Generic;
using LowercaseUppercase.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>
    /// The level editor. It keeps the level as <see cref="CustomLevelData"/> and shows it as a frozen preview, built by
    /// the same <see cref="PrototypeBuilder.BuildCustomLevel"/> that plays it, and rebuilt whole after every change.
    /// Clicking empty page places the palette's tool there (words start typing); clicking a piece selects it, dragging
    /// moves it, and the panel changes its settings. Positions snap to an 8 px grid.
    /// </summary>
    public class LevelEditor : MonoBehaviour
    {
        public const float PixelsPerUnit = 72f;
        public const float GridPx = 8f;
        public const int MaxWordLength = 40;
        public const char RiserMark = '¡';
        const float GridLinePx = 40f;
        const float ArrowPanPxPerSecond = 1200f;
        const float WheelPanPx = 160f;
        const float CaretAbovePx = 34f;  // From the surface up to the top of a tall letter.
        const float CaretBelowPx = 42f;  // From the surface down to the baseline and a little past it.
        const float CaretWidthPx = 3f;
        const float HitPaddingPx = 6f;
        const float SelectionPaddingPx = 6f;
        const float SelectionLinePx = 2f;
        const float TopBarPx = 64f;
        const float BottomBarPx = 72f;
        const float DoubleClickSeconds = 0.35f;
        // Faint see-through parts, such as the Caps Lock stretch's tint, don't count for clicking.
        const float FaintAlpha = 0.1f;
        const float HandleReachPx = 14f;
        const float LinkLinePx = 2f;
        static readonly Color DoorLinkColor = new Color(0.36f, 0.83f, 0.96f, 0.5f);
        static readonly Color KeyLinkColor = new Color(1f, 0.54f, 0.24f, 0.5f);
        static readonly Color HandleLineColor = new Color(1f, 0.82f, 0.25f, 0.6f);
        const float NoticeSeconds = 4f;

        public Camera view;
        public Transform grid;
        public SpriteRenderer caret;
        public Transform cursorMark;
        public TMP_Text startMarker;
        [Tooltip("Four lines, top, bottom, left and right, drawn around the selection.")]
        public SpriteRenderer[] selectionLines;
        public LevelEditorPalette palette;
        public LevelEditorPanel panel;
        [Tooltip("The square drawn on the selected piece's handle.")]
        public SpriteRenderer handleMark;

        [Header("HUD")]
        public TMP_Text playButton;
        public TMP_Text readout;
        public TMP_Text sentence;
        public TMP_Text levelName;

        [Header("Assets the preview is built with")]
        public TMP_FontAsset mono;
        public TMP_FontAsset monoBold;
        public TMP_FontAsset black;
        public Sprite white;

        public CustomLevelData Level { get; private set; }
        /// <summary>The piece whose text is being typed, or null. It is also the selected piece.</summary>
        public CustomPiece Typing { get; private set; }
        /// <summary>The selected piece, or null.</summary>
        public CustomPiece Selected { get; private set; }
        /// <summary>Whether the letter's start marker is selected instead of a piece.</summary>
        public bool StartSelected { get; private set; }
        public Transform Preview { get; private set; }
        public bool GridShown => grid != null && grid.gameObject.activeSelf;

        readonly Dictionary<CustomPiece, List<Transform>> built = new Dictionary<CustomPiece, List<Transform>>();
        string typingOriginal;
        bool typingIsNew;
        Keyboard listeningTo;
        bool textInputSeen;

        bool panning;
        float panStartMouseX;
        float panStartCameraX;

        bool dragging;
        Vector2 dragFrom;
        Vector2 dragDelta;
        Vector4 dragOrigin; // x, y, x2, y2 of the piece, or the start, when the drag began.

        CustomPiece lastClicked;
        float lastClickTime = -1f;

        string notice;
        float noticeUntil;

        bool draggingHandle;
        readonly List<SpriteRenderer> lines = new List<SpriteRenderer>();
        int linesUsed;

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
            // A level transition or a pause can leave the clock stopped on the way here.
            Time.timeScale = 1f;
            Open(CustomRun.Testing && CustomRun.Level != null ? CustomRun.Level : NewLevel());
        }

        /// <summary>Back from a test play: the camera, the selection and the tool as they were.</summary>
        void Start()
        {
            if (!CustomRun.Testing) return;
            Vector3 camera = view.transform.position;
            view.transform.position = new Vector3(CustomRun.EditorCameraX, camera.y, camera.z);
            if (CustomRun.EditorTool != null) palette.Select(CustomRun.EditorTool);
            palette.ShowPage(CustomRun.EditorPalettePage);
            if (CustomRun.EditorSelected != null && Level.pieces.Contains(CustomRun.EditorSelected))
                Select(CustomRun.EditorSelected);
            ShowNotice(CustomRun.TestFinished ? "test play: reached the exit" : "test play: stopped before the exit");
            CustomRun.EndTest();
        }

        void ShowNotice(string text)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        // ---------------------------------------------------------------- test play

        /// <summary>
        /// Tries the level out in the custom level scene, from its start or from a page position. Esc there, or
        /// reaching the exit, comes back here as things were.
        /// </summary>
        public void Play(Vector2? startPx = null)
        {
            if (Typing != null) Commit();
            dragging = draggingHandle = false;
            LevelRun.NewGame();
            CustomRun.BeginTest(Level, startPx, view.transform.position.x, Selected, palette.Page, palette.Current);
            LevelTransition.SkipNextIntro = true;
            SceneManager.LoadScene(CustomRun.Scene);
        }

        void OnDisable() => Listen(null);

        /// <summary>Shows a level for editing, dropping whatever was being typed or selected.</summary>
        public void Open(CustomLevelData level)
        {
            Typing = null;
            Listen(null);
            Level = level;
            Deselect();
            Rebuild();
        }

        // ---------------------------------------------------------------- pixels

        public static float WorldX(float px) => (px - 640f) / PixelsPerUnit;
        public static float WorldY(float px) => (360f - px) / PixelsPerUnit;
        public static float PxX(float worldX) => worldX * PixelsPerUnit + 640f;
        public static float PxY(float worldY) => 360f - worldY * PixelsPerUnit;
        public static float Snap(float px) => Mathf.Round(px / GridPx) * GridPx;
        public static Vector2 World(Vector2 px) => new Vector2(WorldX(px.x), WorldY(px.y));

        Vector2 MouseWorld(Mouse mouse)
        {
            Vector2 screen = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -view.transform.position.z));
            return world;
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            Pan(keyboard, mouse);

            Vector2 world = MouseWorld(mouse);
            var px = new Vector2(PxX(world.x), PxY(world.y));
            var snapped = new Vector2(Snap(px.x), Snap(px.y));
            bool overHud = mouse != null && IsOverHud(mouse.position.ReadValue().y, world);
            cursorMark.gameObject.SetActive(!overHud);
            cursorMark.position = new Vector3(WorldX(snapped.x), WorldY(snapped.y), 0f);

            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) Press(world, px, snapped, overHud);
                if (dragging && mouse.leftButton.isPressed) DragTo(snapped);
                if (draggingHandle && mouse.leftButton.isPressed) DragHandleTo(snapped);
                if (!mouse.leftButton.isPressed) dragging = draggingHandle = false;
            }

            if (Typing != null) TypeKeys(keyboard);
            else ShortcutKeys(keyboard);

            ShowReadout(world, px, snapped, overHud);
            FollowCameraWithGrid();
            PlaceCaret();
            DrawSelection();
            DrawMarks();
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

                bool panButton = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                float mouseX = mouse.position.ReadValue().x;
                if (panButton && !panning)
                {
                    panning = true;
                    panStartMouseX = mouseX;
                    panStartCameraX = x;
                }
                else if (!panButton) panning = false;

                if (panning)
                {
                    float unitsPerScreenPixel = 2f * view.orthographicSize / Mathf.Max(1, Screen.height);
                    x = panStartCameraX - (mouseX - panStartMouseX) * unitsPerScreenPixel;
                }
            }

            // The page starts at the left edge of the first screen.
            x = Mathf.Max(0f, x);
            view.transform.position = new Vector3(x, view.transform.position.y, view.transform.position.z);
        }

        /// <summary>
        /// The HUD bars stay at the screen's top and bottom edges, 64 and 72 px tall in the 720 px frame; the panel
        /// floats below the top bar.
        /// </summary>
        bool IsOverHud(float screenY, Vector2 world)
        {
            float screenPxPerPagePx = Screen.height / (2f * view.orthographicSize * PixelsPerUnit);
            bool overBars = screenY > Screen.height - TopBarPx * screenPxPerPagePx || screenY < BottomBarPx * screenPxPerPagePx;
            return overBars || (panel != null && panel.Contains(world));
        }

        // ---------------------------------------------------------------- clicks

        /// <summary>A left click at a page position, as if the mouse were there: see <see cref="Press"/>.</summary>
        public void ClickAt(Vector2 px)
        {
            Vector2 world = World(px);
            Press(world, px, new Vector2(Snap(px.x), Snap(px.y)), overHud: false);
            dragging = draggingHandle = false;
        }

        /// <summary>A left click in the HUD at a world position: the palette or the panel.</summary>
        public void ClickHud(Vector2 world) => Press(world, Vector2.zero, Vector2.zero, overHud: true);

        void Press(Vector2 world, Vector2 px, Vector2 snapped, bool overHud)
        {
            if (overHud)
            {
                PressHud(world);
                return;
            }

            if (Typing != null) Commit();

            if (HandleAt(px) != null)
            {
                draggingHandle = true;
                return;
            }

            bool doubleClick = false;
            if (StartAt(world))
            {
                SelectStart();
            }
            else
            {
                // With a word tool, words can be picked; with anything else, a click on a word puts the thing there.
                CustomPiece piece = PieceAt(px, includeGround: palette.Current.Typed);
                if (piece != null)
                {
                    doubleClick = piece == lastClicked && Time.unscaledTime - lastClickTime < DoubleClickSeconds;
                    lastClicked = piece;
                    lastClickTime = Time.unscaledTime;
                    if (doubleClick && EditorPalette.HasText(piece))
                    {
                        BeginTyping(piece, isNew: false);
                        return;
                    }
                    Select(piece);
                }
                else
                {
                    Place(palette.Current, snapped);
                    return;
                }
            }

            dragging = true;
            dragFrom = snapped;
            dragDelta = Vector2.zero;
            dragOrigin = StartSelected
                ? new Vector4(Level.startX, Level.startY, 0f, 0f)
                : new Vector4(Selected.x, Selected.y, Selected.x2, Selected.y2);
        }

        void PressHud(Vector2 world)
        {
            if (playButton != null)
            {
                Bounds bounds = playButton.GetComponent<Renderer>().bounds;
                bounds.Expand(HitPaddingPx * 2f / PixelsPerUnit);
                if (bounds.Contains(new Vector3(world.x, world.y, bounds.center.z)))
                {
                    Play();
                    return;
                }
            }

            if (panel != null && panel.Contains(world))
            {
                PressPanel(world);
                return;
            }

            int tab = palette.TabAt(world);
            if (tab >= 0)
            {
                palette.ShowPage(tab);
                return;
            }

            PaletteItem item = palette.ItemAt(world);
            if (item != null)
            {
                if (Typing != null) Commit();
                palette.Select(item);
            }
        }

        void PressPanel(Vector2 world)
        {
            LevelEditorPanel.Hit hit = panel.HitAt(world, out int row);
            CustomPiece piece = panel.Piece;
            if (piece == null) return;

            switch (hit)
            {
                case LevelEditorPanel.Hit.Delete:
                    DeleteSelected();
                    break;
                case LevelEditorPanel.Hit.Less:
                case LevelEditorPanel.Hit.More:
                case LevelEditorPanel.Hit.Value:
                    PieceProperty property = panel.Properties[row];
                    if (property.IsText)
                    {
                        if (Typing != piece) BeginTyping(piece, isNew: false);
                    }
                    else
                    {
                        if (Typing != null) Commit();
                        property.Step(piece, hit == LevelEditorPanel.Hit.Less ? -1 : 1);
                        Rebuild();
                    }
                    break;
            }
        }

        /// <summary>Places a new piece of a tool. Words start typing; anything else is selected.</summary>
        public CustomPiece Place(PaletteItem item, Vector2 snapped)
        {
            CustomPiece piece = EditorPalette.NewPiece(item, snapped.x, snapped.y);
            if (EditorPalette.Stands(piece)) DropOntoWord(piece);
            Level.pieces.Add(piece);
            if (item.Typed) BeginTyping(piece, isNew: true);
            else
            {
                Select(piece);
                Rebuild();
            }
            return piece;
        }

        /// <summary>Moves the selection by an offset in page pixels, as one drag would.</summary>
        public void MoveSelectedBy(Vector2 delta)
        {
            if (Selected == null && !StartSelected) return;
            dragOrigin = StartSelected
                ? new Vector4(Level.startX, Level.startY, 0f, 0f)
                : new Vector4(Selected.x, Selected.y, Selected.x2, Selected.y2);
            MoveBy(delta);
        }

        void DragTo(Vector2 snapped)
        {
            Vector2 delta = snapped - dragFrom;
            if (delta == dragDelta) return;
            dragDelta = delta;
            MoveBy(delta);
        }

        /// <summary>Moves the selection to where it was when the drag began, plus an offset in page pixels.</summary>
        void MoveBy(Vector2 delta)
        {
            if (StartSelected)
            {
                Level.startX = dragOrigin.x + delta.x;
                Level.startY = dragOrigin.y + delta.y;
            }
            else if (Selected != null)
            {
                Selected.x = dragOrigin.x + delta.x;
                Selected.y = dragOrigin.y + delta.y;
                // Second positions are kept relative, so a lift keeps its height and a stretch its width.
                if (dragOrigin.z != 0f) Selected.x2 = dragOrigin.z + delta.x;
                if (dragOrigin.w != 0f) Selected.y2 = dragOrigin.w + delta.y;
                if (EditorPalette.Stands(Selected)) DropOntoWord(Selected);
            }
            Rebuild();
        }

        // ---------------------------------------------------------------- handles

        /// <summary>The selected piece's handle if a page position is on it, or null.</summary>
        public PieceHandle HandleAt(Vector2 px)
        {
            if (Selected == null) return null;
            PieceHandle handle = PieceHandles.For(Selected);
            return handle != null && Vector2.Distance(handle.At, px) <= HandleReachPx ? handle : null;
        }

        /// <summary>Drags the selected piece's handle to a page position, snapped to the grid.</summary>
        public void DragHandleTo(Vector2 snapped)
        {
            if (Selected == null) return;
            PieceHandle handle = PieceHandles.For(Selected);
            if (handle == null) return;
            var before = new Vector4(Selected.x2, Selected.y2, Selected.count, Selected.value);
            handle.MoveTo(Selected, snapped);
            if (before != new Vector4(Selected.x2, Selected.y2, Selected.count, Selected.value)) Rebuild();
        }

        /// <summary>
        /// The lines the editor draws over the level: which door each plate opens and which stretch each key
        /// unlocks, always; and the selected piece's handle.
        /// </summary>
        void DrawMarks()
        {
            linesUsed = 0;
            if (Preview != null)
            {
                foreach (WordBlock plate in Preview.GetComponentsInChildren<WordBlock>())
                    if (plate.kind == WordKind.Period && plate.linkedDoor != null)
                        DrawLine(Centre(plate.transform), Centre(plate.linkedDoor.transform), DoorLinkColor);
                foreach (CapsLockKey key in Preview.GetComponentsInChildren<CapsLockKey>(true))
                    if (key.zone != null && key.zone.label != null)
                        DrawLine(Centre(key.transform), Centre(key.zone.label.transform), KeyLinkColor);
            }

            PieceHandle handle = Selected != null ? PieceHandles.For(Selected) : null;
            if (handle != null)
            {
                DrawLine(World(handle.From), World(handle.At), HandleLineColor);
                handleMark.transform.position = World(handle.At);
            }
            if (handleMark != null) handleMark.enabled = handle != null;

            for (int i = linesUsed; i < lines.Count; i++) lines[i].enabled = false;
        }

        static Vector2 Centre(Transform part)
        {
            Renderer shape = part.GetComponent<Renderer>();
            return shape != null && shape.bounds.size != Vector3.zero ? (Vector2)shape.bounds.center : (Vector2)part.position;
        }

        void DrawLine(Vector2 from, Vector2 to, Color color)
        {
            if (linesUsed == lines.Count)
            {
                var go = new GameObject("Mark Line");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<SpriteRenderer>();
                line.sprite = white;
                line.sortingOrder = 19;
                lines.Add(line);
            }
            SpriteRenderer mark = lines[linesUsed++];
            mark.enabled = true;
            mark.color = color;
            Vector2 along = to - from;
            mark.transform.position = (from + to) * 0.5f;
            mark.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            mark.transform.localScale = new Vector3(along.magnitude, LinkLinePx / PixelsPerUnit, 1f);
        }

        // ---------------------------------------------------------------- selection

        public void Select(CustomPiece piece)
        {
            Selected = piece;
            StartSelected = false;
            if (panel != null)
            {
                if (piece != null) panel.Show(piece, EditorPalette.NameOf(piece));
                else panel.Hide();
            }
        }

        void SelectStart()
        {
            Select(null);
            StartSelected = true;
            if (panel != null) panel.Show(null, "letter start");
        }

        public void Deselect()
        {
            Select(null);
            StartSelected = false;
        }

        public void DeleteSelected()
        {
            if (Selected == null) return;
            if (Typing == Selected)
            {
                Typing = null;
                Listen(null);
            }
            Level.pieces.Remove(Selected);
            Deselect();
            Rebuild();
        }

        bool StartAt(Vector2 world)
        {
            if (startMarker == null) return false;
            Bounds bounds = startMarker.GetComponent<Renderer>().bounds;
            bounds.Expand(HitPaddingPx * 2f / PixelsPerUnit);
            return bounds.Contains(new Vector3(world.x, world.y, bounds.center.z));
        }

        /// <summary>How far below a word's surface a click still counts as putting something on that word.</summary>
        const float DropReachPx = 40f;

        /// <summary>
        /// Moves a standing piece's surface onto the top of the word under it, if a word's top is near or below it.
        /// Its own parts don't count, so a dragged piece doesn't land on itself.
        /// </summary>
        void DropOntoWord(CustomPiece piece)
        {
            built.TryGetValue(piece, out List<Transform> own);
            float x = WorldX(piece.x);
            float best = float.MaxValue;
            foreach (WordBlock word in Preview.GetComponentsInChildren<WordBlock>())
            {
                if (own != null && own.Exists(part => part != null && word.transform.IsChildOf(part))) continue;
                var box = word.GetComponent<BoxCollider2D>();
                if (box == null || box.isTrigger) continue;
                Vector3 centre = word.transform.TransformPoint(box.offset);
                if (x < centre.x - box.size.x * 0.5f || x > centre.x + box.size.x * 0.5f) continue;
                float topPx = PxY(centre.y + box.size.y * 0.5f);
                if (topPx < piece.y - DropReachPx || topPx >= best) continue;
                best = topPx;
            }
            if (best < float.MaxValue)
            {
                float lift = piece.y - best;
                piece.y = best;
                if (piece.y2 != 0f) piece.y2 -= lift;
            }
        }

        /// <summary>
        /// The piece drawn under a page position; where pieces overlap, the smallest one. Words and loose letters only
        /// count when asked for.
        /// </summary>
        public CustomPiece PieceAt(Vector2 px, bool includeGround = true)
        {
            Vector2 point = World(px);
            CustomPiece best = null;
            float bestArea = float.MaxValue;
            foreach (KeyValuePair<CustomPiece, List<Transform>> entry in built)
            {
                if (!includeGround && EditorPalette.IsGround(entry.Key)) continue;
                if (!TryGetBounds(entry.Value, out Bounds bounds)) continue;
                bounds.Expand(HitPaddingPx * 2f / PixelsPerUnit);
                if (!bounds.Contains(new Vector3(point.x, point.y, bounds.center.z))) continue;
                float area = bounds.size.x * bounds.size.y;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = entry.Key;
                }
            }
            return best;
        }

        /// <summary>The outline of what a piece draws, leaving out faint see-through parts.</summary>
        static bool TryGetBounds(List<Transform> parts, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Transform part in parts)
            {
                if (part == null) continue;
                foreach (Renderer shape in part.GetComponentsInChildren<Renderer>())
                {
                    if (!shape.enabled || shape.bounds.size == Vector3.zero) continue;
                    if (shape is SpriteRenderer sprite && sprite.color.a < FaintAlpha) continue;
                    var text = shape.GetComponent<TMP_Text>();
                    if (text != null && (text.alpha < FaintAlpha || text.color.a < FaintAlpha)) continue;
                    if (!any) bounds = shape.bounds;
                    else bounds.Encapsulate(shape.bounds);
                    any = true;
                }
            }
            return any;
        }

        void DrawSelection()
        {
            if (selectionLines == null || selectionLines.Length < 4) return;
            Bounds bounds = default;
            bool shown = false;
            if (StartSelected && startMarker != null)
            {
                bounds = startMarker.GetComponent<Renderer>().bounds;
                shown = true;
            }
            else if (Selected != null && built.TryGetValue(Selected, out List<Transform> parts))
                shown = TryGetBounds(parts, out bounds);

            foreach (SpriteRenderer line in selectionLines) line.enabled = shown;
            if (!shown) return;

            bounds.Expand(SelectionPaddingPx * 2f / PixelsPerUnit);
            float thick = SelectionLinePx / PixelsPerUnit;
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            SetLine(selectionLines[0], new Vector3(c.x, c.y + e.y), new Vector3(e.x * 2f + thick, thick));
            SetLine(selectionLines[1], new Vector3(c.x, c.y - e.y), new Vector3(e.x * 2f + thick, thick));
            SetLine(selectionLines[2], new Vector3(c.x - e.x, c.y), new Vector3(thick, e.y * 2f + thick));
            SetLine(selectionLines[3], new Vector3(c.x + e.x, c.y), new Vector3(thick, e.y * 2f + thick));
        }

        static void SetLine(SpriteRenderer line, Vector3 position, Vector3 size)
        {
            line.transform.position = new Vector3(position.x, position.y, 0f);
            line.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        // ---------------------------------------------------------------- keys

        void ShortcutKeys(Keyboard keyboard)
        {
            if (keyboard == null) return;
            if (keyboard.pKey.wasPressedThisFrame)
            {
                Play();
                return;
            }
            if (keyboard.hKey.wasPressedThisFrame)
            {
                Vector2 world = MouseWorld(Mouse.current);
                Play(new Vector2(Snap(PxX(world.x)), Snap(PxY(world.y))));
                return;
            }
            if (keyboard.gKey.wasPressedThisFrame) grid.gameObject.SetActive(!GridShown);
            if (keyboard.digit1Key.wasPressedThisFrame) palette.ShowPage(0);
            if (keyboard.digit2Key.wasPressedThisFrame) palette.ShowPage(1);
            if (keyboard.digit3Key.wasPressedThisFrame) palette.ShowPage(2);
            if (keyboard.escapeKey.wasPressedThisFrame) Deselect();
            if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) DeleteSelected();
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if (enter && Selected != null && EditorPalette.HasText(Selected)) BeginTyping(Selected, isNew: false);
        }

        // ---------------------------------------------------------------- typing

        void BeginTyping(CustomPiece piece, bool isNew)
        {
            Select(piece);
            Typing = piece;
            typingOriginal = piece.text;
            typingIsNew = isNew;
            Listen(Keyboard.current);
            Rebuild();
        }

        /// <summary>Places the text being typed and keeps the piece selected. An emptied piece is removed.</summary>
        public void Commit()
        {
            if (Typing == null) return;
            CustomPiece piece = Typing;
            if (piece.text.Trim().Length == 0)
            {
                Level.pieces.Remove(piece);
                Deselect();
            }
            EndTyping();
        }

        /// <summary>Drops the typing: a new piece goes, an edited one gets its old text back.</summary>
        public void Cancel()
        {
            if (Typing == null) return;
            if (typingIsNew)
            {
                Level.pieces.Remove(Typing);
                Deselect();
            }
            else Typing.text = typingOriginal;
            EndTyping();
        }

        void EndTyping()
        {
            Typing = null;
            Listen(null);
            Rebuild();
        }

        /// <summary>
        /// One typed character for the text being typed. Control characters are ignored. In loose letters, "!" marks a
        /// riser's hole.
        /// </summary>
        public void Type(char c)
        {
            if (Typing == null || char.IsControl(c) || Typing.text.Length >= MaxWordLength) return;
            // Text doesn't start with a space.
            if (c == ' ' && Typing.text.Length == 0) return;
            if (c == '!' && Typing.type == CustomPieceType.Kerned) c = RiserMark;
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
            // Meshes are made lazily, and finding the piece under the mouse needs their bounds in this same frame.
            foreach (TMP_Text text in Preview.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();

            if (startMarker != null)
            {
                startMarker.transform.position = new Vector3(WorldX(Level.startX - 12f), WorldY(Level.startY), 0f);
                startMarker.ForceMeshUpdate();
            }
            if (sentence != null)
                sentence.text = $"{Level.sentenceBefore}<color=#FFD23F>_</color>{Level.sentenceAfter}";
            if (levelName != null) levelName.text = Level.name;
            if (panel != null && panel.gameObject.activeSelf) panel.Refresh();
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
            if (built.TryGetValue(Typing, out List<Transform> parts) && TryGetBounds(parts, out Bounds bounds))
                rightX = Mathf.Max(rightX, bounds.max.x);

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

        void ShowReadout(Vector2 world, Vector2 px, Vector2 snapped, bool overHud)
        {
            if (readout == null) return;
            string where = $"x {snapped.x:0}  y {snapped.y:0}";
            string what;
            PaletteItem hovered = overHud ? palette.ItemAt(world) : null;
            if (Time.unscaledTime < noticeUntil) what = notice;
            else if (Typing != null) what = "typing  ·  Enter places  ·  Esc cancels";
            else if (hovered != null) what = hovered.Name;
            else if (Selected != null && PieceHandles.For(Selected) != null)
                what = $"{EditorPalette.NameOf(Selected)}  ·  drag to move  ·  the square stretches  ·  Del removes";
            else if (Selected != null) what = $"{EditorPalette.NameOf(Selected)}  ·  drag to move  ·  Del removes";
            else if (palette.Current.Typed) what = $"click: type a {palette.Current.Name}  ·  click a word to pick it";
            else what = $"click: place a {palette.Current.Name}";
            readout.text = $"{where}   {what}";
        }
    }
}
