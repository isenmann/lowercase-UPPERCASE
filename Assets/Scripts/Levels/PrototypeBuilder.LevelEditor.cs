#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// The level editor's scene: the ruled page, a faint grid, the cursor and caret, the HUD, and a
    /// <see cref="LevelEditor"/> that builds the level being edited as a frozen preview.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string LevelEditorScene = SceneFolder + "/LevelEditor.unity";
        const float EditorGridPx = 40f;
        const string EditorControls = "1 2 3  pages  ·  ← → wheel  scroll  ·  G  grid";
        const float ChipPx = 40f;
        const float ChipStepPx = 44f;
        const float FirstChipPx = 290f;
        const float PanelLeftPx = 928f;
        const float PanelTopPx = 76f;
        const float PanelWidthPx = 320f;
        const float PanelHeightPx = 200f;
        const float PanelRowPx = 30f;
        static readonly Color ChipColor = new Color32(0x1A, 0x1D, 0x24, 0xFF);
        static readonly Color ActiveChipColor = new Color32(0x3A, 0x40, 0x50, 0xFF);
        static readonly Color PanelColor = new Color32(0x16, 0x18, 0x1E, 0xFF);

        [MenuItem("lowercase UPPERCASE/Rebuild Level Editor Scene")]
        public static void RebuildLevelEditorScene()
        {
            PrepareAssets(out _, out _);
            BuildLevelEditorScene();
            SetBuildScenes();
            EditorSceneManager.OpenScene(LevelEditorScene);
        }

        static void BuildLevelEditorScene()
        {
            Transform unused = BeginScene(LevelEditorScene, "Level Editor");
            Object.DestroyImmediate(unused.gameObject);
            Camera camera = Camera.main;

            var editor = new GameObject("Level Editor").AddComponent<LevelEditor>();
            editor.view = camera;
            editor.mono = mono;
            editor.monoBold = monoBold;
            editor.black = black;
            editor.white = white;

            editor.grid = BuildEditorGrid();

            var cursor = new GameObject("Cursor").transform;
            var mark = new Color(Player.r, Player.g, Player.b, 0.9f);
            Rect(cursor, "Across", Vector2.zero, new Vector2(Size(20f), Size(2f)), mark, 20);
            Rect(cursor, "Down", Vector2.zero, new Vector2(Size(2f), Size(20f)), mark, 20);
            editor.cursorMark = cursor;

            editor.caret = Rect(null, "Caret", Vector2.zero, Vector2.one * Size(4f), Player, 21);
            editor.caret.enabled = false;

            editor.startMarker = Text(null, "Start", "a", 110f, Ground, 72f, mono,
                new Color(Player.r, Player.g, Player.b, 0.45f), 4);

            var selection = new GameObject("Selection").transform;
            var outline = new Color(Player.r, Player.g, Player.b, 0.9f);
            editor.selectionLines = new[]
            {
                Rect(selection, "Top", Vector2.zero, Vector2.one, outline, 22),
                Rect(selection, "Bottom", Vector2.zero, Vector2.one, outline, 22),
                Rect(selection, "Left", Vector2.zero, Vector2.one, outline, 22),
                Rect(selection, "Right", Vector2.zero, Vector2.one, outline, 22),
            };
            foreach (SpriteRenderer line in editor.selectionLines) line.enabled = false;

            Transform hud = BuildEditorHud(editor);
            hud.SetParent(camera.transform, true);
            camera.gameObject.AddComponent<ScreenFit>().hud = hud;

            EditorSceneManager.MarkSceneDirty(editor.gameObject.scene);
            EditorSceneManager.SaveScene(editor.gameObject.scene, LevelEditorScene);
        }

        /// <summary>Faint lines every 40 px over a little more than a screen; the editor slides it along with the camera.</summary>
        static Transform BuildEditorGrid()
        {
            var grid = new GameObject("Grid").transform;
            // Opaque and just lighter than the page, under the ruled lines: see-through white came out far too bright.
            Color line = new Color32(0x15, 0x17, 0x1D, 0xFF);
            for (float x = 0f; x <= 1280f + 400f; x += EditorGridPx)
                PixelRect(grid, "Down", x, -400f, 1f, 1520f, line, -11);
            for (float y = -400f; y <= 1120f; y += EditorGridPx)
                PixelRect(grid, "Across", 0f, y, 1680f, 1f, line, -11);
            return grid;
        }

        static Transform BuildEditorHud(LevelEditor editor)
        {
            var hud = new GameObject("HUD").transform;
            const int order = 30;

            PixelRect(hud, "Top Bar", 0f, 0f, 1280f, 64f, Background, order);
            PixelRect(hud, "Top Line", 0f, 63f, 1280f, 1f, BarLine, order);
            PixelRect(hud, "Bottom Bar", 0f, 648f, 1280f, 72f, Background, order);
            PixelRect(hud, "Bottom Line", 0f, 648f, 1280f, 1f, BarLine, order);

            Text(hud, "Title", "Level editor", 32f, 38f, 14f, mono, Muted, order + 1);
            editor.readout = Text(hud, "Editor Readout", "", 168f, 37f, 12f, mono, Muted, order + 1);
            editor.sentence = Text(hud, "Sentence", "", 860f, 40f, 22f, mono, Paper, order + 1);
            editor.levelName = Text(hud, "Level Name", "", 1120f, 38f, 14f, mono, Muted, order + 1);
            Text(hud, "Controls", EditorControls, 940f, 690f, 12f, mono, Muted, order + 1);

            editor.palette = BuildEditorPalette(hud, order);
            editor.panel = BuildEditorPanel(hud, order);
            return hud;
        }

        /// <summary>The tabs and, for each page, a row of chips showing the glyph of what each tool places.</summary>
        static LevelEditorPalette BuildEditorPalette(Transform hud, int order)
        {
            var root = new GameObject("Palette");
            root.transform.SetParent(hud, false);
            var palette = root.AddComponent<LevelEditorPalette>();
            palette.tabColor = Breakable;
            palette.activeTabColor = Paper;
            palette.chipColor = ChipColor;
            palette.activeChipColor = ActiveChipColor;

            float[] tabLefts = { 32f, 96f, 168f };
            palette.tabs = new TMP_Text[EditorPalette.PageNames.Length];
            palette.pages = new GameObject[EditorPalette.PageNames.Length];
            var chips = new System.Collections.Generic.List<SpriteRenderer>();
            for (int page = 0; page < EditorPalette.Pages.Length; page++)
            {
                palette.tabs[page] = Text(root.transform, $"Tab {EditorPalette.PageNames[page]}",
                    EditorPalette.PageNames[page], tabLefts[page], 690f, 14f, mono, Breakable, order + 1);

                var pageRoot = new GameObject($"Page {EditorPalette.PageNames[page]}");
                pageRoot.transform.SetParent(root.transform, false);
                palette.pages[page] = pageRoot;

                PaletteItem[] items = EditorPalette.Pages[page];
                for (int i = 0; i < items.Length; i++)
                {
                    float left = FirstChipPx + i * ChipStepPx;
                    SpriteRenderer chip = PixelRect(pageRoot.transform, $"Chip {items[i].Name}", left, 664f, ChipPx, ChipPx,
                        ChipColor, order + 1);
                    chips.Add(chip);

                    float fontPx = items[i].Glyph.Length > 2 ? 16f : 24f;
                    TextMeshPro glyph = Text(pageRoot.transform, $"Glyph {items[i].Name}", items[i].Glyph,
                        left + ChipPx * 0.5f - 360f, 692f, fontPx, items[i].Look == PaletteLook.Danger ? monoBold : mono,
                        LookColor(items[i].Look), order + 2);
                    glyph.alignment = TextAlignmentOptions.Baseline;
                }
            }
            palette.chips = chips.ToArray();
            return palette;
        }

        static Color LookColor(PaletteLook look)
        {
            switch (look)
            {
                case PaletteLook.Interactive: return Interactive;
                case PaletteLook.Hot: return Hot;
                case PaletteLook.Breakable: return Breakable;
                case PaletteLook.Danger: return Danger;
                case PaletteLook.Player: return Player;
                case PaletteLook.Scenery: return Muted;
                default: return Paper;
            }
        }

        /// <summary>The selected piece's panel, under the top bar on the right. Hidden until something is selected.</summary>
        static LevelEditorPanel BuildEditorPanel(Transform hud, int order)
        {
            var root = new GameObject("Panel");
            root.transform.SetParent(hud, false);
            var panel = root.AddComponent<LevelEditorPanel>();

            panel.background = PixelRect(root.transform, "Box", PanelLeftPx, PanelTopPx, PanelWidthPx, PanelHeightPx,
                PanelColor, order);
            PixelRect(root.transform, "Box Line", PanelLeftPx, PanelTopPx + PanelHeightPx - 1f, PanelWidthPx, 1f,
                BarLine, order + 1);
            float left = PanelLeftPx + 16f;
            panel.title = Text(root.transform, "Title", "", left, PanelTopPx + 28f, 14f, mono, Paper, order + 1);

            int rows = LevelEditorPanel.Rows;
            panel.labels = new TMP_Text[rows];
            panel.values = new TMP_Text[rows];
            panel.lessButtons = new TMP_Text[rows];
            panel.moreButtons = new TMP_Text[rows];
            for (int i = 0; i < rows; i++)
            {
                float baseline = PanelTopPx + 62f + i * PanelRowPx;
                panel.labels[i] = Text(root.transform, $"Label {i + 1}", "", left, baseline, 12f, mono, Muted, order + 1);
                panel.lessButtons[i] = Text(root.transform, $"Less {i + 1}", "", 1100f, baseline, 16f, monoBold, Player, order + 1);
                panel.values[i] = Text(root.transform, $"Value {i + 1}", "", 1122f, baseline, 14f, mono, Paper, order + 1);
                panel.moreButtons[i] = Text(root.transform, $"More {i + 1}", "", 1222f, baseline, 16f, monoBold, Player, order + 1);
            }
            panel.deleteButton = Text(root.transform, "Delete", "delete", left, PanelTopPx + PanelHeightPx - 16f, 14f,
                mono, Danger, order + 1);

            root.SetActive(false);
            return panel;
        }
    }
}
#endif
