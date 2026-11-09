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
        const string EditorControls = "click  type  ·  Enter  place  ·  Esc  cancel  ·  ← → wheel  scroll  ·  G  grid";

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
            editor.sentence = Text(hud, "Sentence", "", 500f, 40f, 22f, mono, Paper, order + 1);
            editor.levelName = Text(hud, "Level Name", "", 1070f, 38f, 14f, mono, Muted, order + 1);
            editor.readout = Text(hud, "Readout", "", 32f, 690f, 14f, mono, Muted, order + 1);
            Text(hud, "Controls", EditorControls, 600f, 690f, 14f, mono, Muted, order + 1);
            return hud;
        }
    }
}
#endif
