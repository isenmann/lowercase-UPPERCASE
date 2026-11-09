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
    /// moves it, and the panel changes its settings, or the level's. Positions snap to an 8 px grid. Every finished
    /// change is kept for undo, and the level is checked for likely mistakes, which are listed under the top bar.
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
        static readonly Color WarningColor = new Color(1f, 0.3f, 0.37f, 0.8f);
        const float UngroundedMarkPx = 40f;
        const int WarningsListed = 3;

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
        [Tooltip("Goes back to My levels in the menu.")]
        public TMP_Text backButton;
        public TMP_Text playButton;
        public TMP_Text readout;
        public TMP_Text sentence;
        public TMP_Text settingsButton;
        [Tooltip("The level's warnings, under the top bar on the left. Clicking it shows the next piece warned about.")]
        public TMP_Text warningsText;

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
        /// <summary>Whether the panel shows the level's settings.</summary>
        public bool SettingsShown { get; private set; }
        /// <summary>The level setting whose text is being typed, or null.</summary>
        public LevelSetting TypingSetting { get; private set; }
        /// <summary>Whether a piece's text or a setting is being typed.</summary>
        public bool IsTyping => Typing != null || TypingSetting != null;
        public EditorHistory History { get; private set; }
        /// <summary>What looks wrong with the level, as of the last finished change.</summary>
        public IReadOnlyList<EditorWarning> Warnings => warnings;

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

        string typingSettingText;
        bool changed;
        List<EditorWarning> warnings = new List<EditorWarning>();
        int warningCursor = -1;

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
            // My levels hands over the level to edit and its file; a new level has neither until its first change.
            Open(CustomRun.Level ?? NewLevel());
        }

        /// <summary>Saves and goes back to My levels in the menu.</summary>
        public void Leave()
        {
            if (IsTyping) Commit();
            dragging = draggingHandle = false;
            Checkpoint();
            CustomRun.Level = null;
            CustomRun.EditPath = null;
            CustomRun.ReturnToMyLevels = true;
            SceneManager.LoadScene(CustomRun.MenuScene);
        }

        /// <summary>
        /// Writes the level to its file, starting one for a level that has none yet. Saving is quiet; only a failure
        /// is shown.
        /// </summary>
        void Save()
        {
            try
            {
                CustomRun.EditPath = LevelStore.Save(Level, CustomRun.EditPath);
            }
            catch (System.Exception e) when (e is System.IO.IOException || e is System.UnauthorizedAccessException)
            {
                ShowNotice("couldn't save: " + e.Message);
                Debug.LogWarning($"Level editor: couldn't save the level: {e}");
            }
        }

        /// <summary>Back from a test play: the camera, the selection and the tool as they were.</summary>
        void Start()
        {
            if (!CustomRun.Testing) return;
            Vector3 camera = view.transform.position;
            view.transform.position = new Vector3(CustomRun.EditorCameraX, camera.y, camera.z);
            if (CustomRun.EditorTool != null) palette.Select(CustomRun.EditorTool);
            palette.ShowPage(CustomRun.EditorPalettePage);
            if (CustomRun.EditorHistory != null) History = CustomRun.EditorHistory;
            if (CustomRun.EditorSelected != null && Level.pieces.Contains(CustomRun.EditorSelected))
                Select(CustomRun.EditorSelected);
            if (!CustomRun.TestFinished) ShowNotice("test play: stopped before the exit");
            else if (CustomRun.TestStartPx != null)
                ShowNotice("test play: reached the exit  ·  play from the start to finish the level");
            else
            {
                // Finishing it isn't an edit, so it isn't a step to undo; it is saved straight away.
                Level.MarkFinished();
                History.Amend(Level.ToJson(false));
                Save();
                ShowNotice("test play: reached the exit  ·  finished, ready to share");
            }
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
            if (IsTyping) Commit();
            dragging = draggingHandle = false;
            Checkpoint();
            LevelRun.NewGame();
            CustomRun.BeginTest(Level, startPx, view.transform.position.x, Selected, palette.Page, palette.Current, History);
            LevelTransition.SkipNextIntro = true;
            SceneManager.LoadScene(CustomRun.Scene);
        }

        void OnDisable() => Listen(null);

        /// <summary>Shows a level for editing, dropping whatever was being typed or selected.</summary>
        public void Open(CustomLevelData level)
        {
            Typing = null;
            TypingSetting = null;
            Listen(null);
            Level = level;
            Deselect();
            Rebuild();
            History = new EditorHistory(level.ToJson(false));
            changed = false;
            FindWarnings();
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

            if (IsTyping) TypeKeys(keyboard);
            else ShortcutKeys(keyboard);
            if (changed && !dragging && !draggingHandle && !IsTyping) Checkpoint();

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
            return overBars || (panel != null && panel.Contains(world)) || Over(warningsText, world);
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

            if (IsTyping) Commit();

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
            if (Over(playButton, world))
            {
                Play();
                return;
            }
            if (Over(backButton, world))
            {
                Leave();
                return;
            }
            if (Over(settingsButton, world) || Over(sentence, world))
            {
                ShowSettings();
                return;
            }
            if (Over(warningsText, world))
            {
                ShowNextWarning();
                return;
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
                if (IsTyping) Commit();
                palette.Select(item);
            }
        }

        /// <summary>Whether a world point is on a HUD text, with a little padding.</summary>
        static bool Over(TMP_Text text, Vector2 world)
        {
            if (text == null || !text.gameObject.activeInHierarchy || text.text.Length == 0) return false;
            Bounds bounds = text.GetComponent<Renderer>().bounds;
            if (bounds.size == Vector3.zero) return false;
            bounds.Expand(HitPaddingPx * 2f / PixelsPerUnit);
            return bounds.Contains(new Vector3(world.x, world.y, bounds.center.z));
        }

        void PressPanel(Vector2 world)
        {
            LevelEditorPanel.Hit hit = panel.HitAt(world, out int row);
            switch (hit)
            {
                case LevelEditorPanel.Hit.Delete:
                    DeleteSelected();
                    break;
                case LevelEditorPanel.Hit.Less:
                case LevelEditorPanel.Hit.More:
                case LevelEditorPanel.Hit.Value:
                    PanelRow shown = panel.Shown[row];
                    if (shown.IsText) shown.Edit?.Invoke();
                    else
                    {
                        if (IsTyping) Commit();
                        shown.Step(hit == LevelEditorPanel.Hit.Less ? -1 : 1);
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

            foreach (EditorWarning warning in warnings)
                if (warning.Piece != null && Level.pieces.Contains(warning.Piece)) DrawBox(WarningBounds(warning.Piece), WarningColor);

            PieceHandle handle = Selected != null ? PieceHandles.For(Selected) : null;
            if (handle != null)
            {
                DrawLine(World(handle.From), World(handle.At), HandleLineColor);
                handleMark.transform.position = World(handle.At);
            }
            if (handleMark != null) handleMark.enabled = handle != null;

            for (int i = linesUsed; i < lines.Count; i++) lines[i].enabled = false;
        }

        /// <summary>What a warned piece built, or a box where it stands if it built nothing, like an enemy in the air.</summary>
        Bounds WarningBounds(CustomPiece piece)
        {
            if (built.TryGetValue(piece, out List<Transform> parts) && TryGetBounds(parts, out Bounds bounds)) return bounds;
            float size = UngroundedMarkPx / PixelsPerUnit;
            return new Bounds(World(new Vector2(piece.x, piece.y - UngroundedMarkPx * 0.5f)), new Vector3(size, size, 0f));
        }

        void DrawBox(Bounds bounds, Color color)
        {
            Vector2 min = bounds.min;
            Vector2 max = bounds.max;
            DrawLine(new Vector2(min.x, min.y), new Vector2(max.x, min.y), color);
            DrawLine(new Vector2(min.x, max.y), new Vector2(max.x, max.y), color);
            DrawLine(new Vector2(min.x, min.y), new Vector2(min.x, max.y), color);
            DrawLine(new Vector2(max.x, min.y), new Vector2(max.x, max.y), color);
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
            SettingsShown = false;
            if (panel != null)
            {
                if (piece != null) panel.Show(EditorPalette.NameOf(piece), RowsFor(piece), deletable: true);
                else panel.Hide();
            }
        }

        List<PanelRow> RowsFor(CustomPiece piece)
        {
            var rows = new List<PanelRow>();
            foreach (PieceProperty property in PieceProperties.For(piece))
            {
                PieceProperty p = property;
                rows.Add(p.IsText
                    ? new PanelRow(p.Label, () => p.Show(piece), null, () =>
                    {
                        if (Typing != piece) BeginTyping(piece, isNew: false);
                    })
                    : new PanelRow(p.Label, () => p.Show(piece), direction => p.Step(piece, direction)));
            }
            return rows;
        }

        void SelectStart()
        {
            Select(null);
            StartSelected = true;
            if (panel != null) panel.Show("letter start", new PanelRow[0], deletable: false);
        }

        /// <summary>Shows the level's own settings in the panel instead of a piece's.</summary>
        public void ShowSettings()
        {
            if (IsTyping) Commit();
            Deselect();
            SettingsShown = true;
            if (panel == null) return;
            var rows = new List<PanelRow>();
            foreach (LevelSetting setting in LevelSettings.All)
            {
                LevelSetting s = setting;
                rows.Add(s.IsText
                    ? new PanelRow(s.Label, () => TypingSetting == s ? typingSettingText + "|" : s.Show(Level), null, () =>
                    {
                        if (TypingSetting != s) BeginTypingSetting(s);
                    })
                    : new PanelRow(s.Label, () => s.Show(Level), direction => s.Step(Level, direction)));
            }
            panel.Show("level settings", rows, deletable: false);
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
            warningCursor = -1;
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

        // ---------------------------------------------------------------- undo and warnings

        /// <summary>
        /// Keeps the level as it is now for undo, if it changed, and checks it again. The editor calls it once a change
        /// is finished: after a drag, not during it, and after typing is placed.
        /// </summary>
        public void Checkpoint()
        {
            changed = false;
            if (History == null || !History.Record(Level.ToJson(false))) return;
            FindWarnings();
            Save();
        }

        public void Undo() => GoTo(() => History.Undo());

        public void Redo() => GoTo(() => History.Redo());

        /// <summary>
        /// Swaps the level for one from the history. A piece at the same place in the list stays selected, so undoing a
        /// move keeps the moved piece picked.
        /// </summary>
        void GoTo(System.Func<string> step)
        {
            if (dragging || draggingHandle) return;
            if (IsTyping) Commit();
            Checkpoint();
            string json = step();
            if (json == null) return;

            int index = Selected != null ? Level.pieces.IndexOf(Selected) : -1;
            string type = Selected?.type;
            bool settings = SettingsShown;
            bool start = StartSelected;

            Level = CustomLevelData.FromJson(json);
            Rebuild();
            changed = false;
            FindWarnings();
            Save();

            if (settings) ShowSettings();
            else if (start) SelectStart();
            else if (index >= 0 && index < Level.pieces.Count && Level.pieces[index].type == type) Select(Level.pieces[index]);
            else Deselect();
        }

        void FindWarnings()
        {
            warnings = EditorWarnings.Find(Level, built);
            warningCursor = -1;
            ShowWarnings();
        }

        /// <summary>The warnings, the same ones counted together: "pieces overlap (2) · no exit word".</summary>
        void ShowWarnings()
        {
            if (warningsText == null) return;
            var groups = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (EditorWarning warning in warnings)
            {
                if (!counts.ContainsKey(warning.Text))
                {
                    counts[warning.Text] = 0;
                    groups.Add(warning.Text);
                }
                counts[warning.Text]++;
            }
            var parts = new List<string>();
            for (int i = 0; i < groups.Count && i < WarningsListed; i++)
                parts.Add(counts[groups[i]] > 1 ? $"{groups[i]} ({counts[groups[i]]})" : groups[i]);
            if (groups.Count > WarningsListed) parts.Add($"{groups.Count - WarningsListed} more");
            warningsText.text = parts.Count == 0 ? "" : "! " + string.Join("  ·  ", parts);
            warningsText.ForceMeshUpdate();
        }

        /// <summary>Selects the next piece a warning is about, and scrolls to it.</summary>
        public void ShowNextWarning()
        {
            var pieces = new List<CustomPiece>();
            foreach (EditorWarning warning in warnings)
                if (warning.Piece != null && !pieces.Contains(warning.Piece)) pieces.Add(warning.Piece);
            if (pieces.Count == 0)
            {
                if (warnings.Count > 0) ShowNotice(warnings[0].Text);
                return;
            }
            warningCursor = (warningCursor + 1) % pieces.Count;
            CustomPiece piece = pieces[warningCursor];
            if (IsTyping) Commit();
            Select(piece);
            Vector3 camera = view.transform.position;
            view.transform.position = new Vector3(Mathf.Max(0f, WorldX(piece.x)), camera.y, camera.z);
            foreach (EditorWarning warning in warnings)
                if (warning.Piece == piece)
                {
                    ShowNotice(warning.Text);
                    break;
                }
        }

        // ---------------------------------------------------------------- keys

        void ShortcutKeys(Keyboard keyboard)
        {
            if (keyboard == null) return;
            bool command = keyboard.ctrlKey.isPressed || keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed;
            if (command)
            {
                bool redo = keyboard.yKey.wasPressedThisFrame || (keyboard.zKey.wasPressedThisFrame && keyboard.shiftKey.isPressed);
                if (redo) Redo();
                else if (keyboard.zKey.wasPressedThisFrame) Undo();
                // Ctrl with a letter is never a tool shortcut.
                return;
            }
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
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                // Esc first lets go of what is picked; with nothing picked, it leaves.
                if (Selected != null || StartSelected || SettingsShown) Deselect();
                else Leave();
                return;
            }
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

        /// <summary>
        /// Places the text being typed and keeps the piece selected. An emptied piece is removed. A setting that
        /// can't take the text keeps what it had, and says why.
        /// </summary>
        public void Commit()
        {
            if (TypingSetting != null)
            {
                string problem = TypingSetting.Apply(Level, typingSettingText);
                if (problem != null) ShowNotice(problem);
                EndTypingSetting();
                return;
            }
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
            if (TypingSetting != null)
            {
                EndTypingSetting();
                return;
            }
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

        void BeginTypingSetting(LevelSetting setting)
        {
            if (IsTyping) Commit();
            TypingSetting = setting;
            typingSettingText = setting.Show(Level);
            Listen(Keyboard.current);
            panel.Refresh();
        }

        void EndTypingSetting()
        {
            TypingSetting = null;
            typingSettingText = null;
            Listen(null);
            Rebuild();
        }

        /// <summary>
        /// One typed character for the text being typed. Control characters are ignored. In loose letters, "!" marks a
        /// riser's hole.
        /// </summary>
        public void Type(char c)
        {
            if (TypingSetting != null)
            {
                if (char.IsControl(c) || typingSettingText.Length >= TypingSetting.MaxLength) return;
                typingSettingText += c;
                panel.Refresh();
                return;
            }
            if (Typing == null || char.IsControl(c) || Typing.text.Length >= MaxWordLength) return;
            // Text doesn't start with a space.
            if (c == ' ' && Typing.text.Length == 0) return;
            if (c == '!' && Typing.type == CustomPieceType.Kerned) c = RiserMark;
            Typing.text += c;
            Rebuild();
        }

        public void Backspace()
        {
            if (TypingSetting != null)
            {
                if (typingSettingText.Length == 0) return;
                typingSettingText = typingSettingText.Substring(0, typingSettingText.Length - 1);
                panel.Refresh();
                return;
            }
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

        /// <summary>Builds the level again from its data, frozen (see <see cref="FrozenLevel"/>).</summary>
        public void Rebuild()
        {
            // A new root, because a destroyed one lingers until the end of the frame, and the builder looks for
            // grounds and doors among the root's children.
            if (Preview != null) Destroy(Preview.gameObject);
            Preview = new GameObject("Preview").transform;
            built.Clear();
            changed = true;

            FrozenLevel.Build(Preview, Level, mono, monoBold, black, white, (piece, part) =>
            {
                if (!built.TryGetValue(piece, out List<Transform> parts)) built[piece] = parts = new List<Transform>();
                parts.Add(part);
            });

            if (startMarker != null)
            {
                startMarker.transform.position = new Vector3(WorldX(Level.startX - 12f), WorldY(Level.startY), 0f);
                startMarker.ForceMeshUpdate();
            }
            if (sentence != null)
                sentence.text = $"{Level.sentenceBefore}<color=#FFD23F>_</color>{Level.sentenceAfter}";
            if (panel != null && panel.gameObject.activeSelf) panel.Refresh();
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
            else if (IsTyping) what = "typing  ·  Enter places  ·  Esc cancels";
            else if (hovered != null) what = hovered.Name;
            else if (Selected != null && PieceHandles.For(Selected) != null)
                what = $"{EditorPalette.NameOf(Selected)}  ·  drag to move  ·  the square stretches  ·  Del removes";
            else if (Selected != null) what = $"{EditorPalette.NameOf(Selected)}  ·  drag to move  ·  Del removes";
            else if (SettingsShown) what = "level settings  ·  click a value to change it";
            else if (palette.Current.Typed) what = $"click: type a {palette.Current.Name}  ·  click a word to pick it";
            else what = $"click: place a {palette.Current.Name}";
            readout.text = $"{where}   {what}";
        }
    }
}
