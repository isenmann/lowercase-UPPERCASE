using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The custom level being played, handed from whatever starts it (the editor, the My levels list) to the
    /// custom level's scene, which builds it as it loads. Null means the scene was opened directly and shows its
    /// sample level. A test play from the editor also carries where to start and what to go back to.
    /// </summary>
    public static class CustomRun
    {
        public const string Scene = "CustomLevel";
        public const string EditorScene = "LevelEditor";

        public static CustomLevelData Level { get; set; }

        /// <summary>The level is being tried out from the editor: Esc and the exit both go back to it.</summary>
        public static bool Testing { get; private set; }
        /// <summary>Where a test play starts the letter, in page pixels, instead of the level's start.</summary>
        public static Vector2? TestStartPx { get; private set; }
        /// <summary>Whether the last test play reached the exit.</summary>
        public static bool TestFinished { get; set; }

        /// <summary>What the editor showed when the test began, so it can come back the same.</summary>
        public static float EditorCameraX { get; private set; }
        public static CustomPiece EditorSelected { get; private set; }
        public static int EditorPalettePage { get; private set; }
        public static PaletteItem EditorTool { get; private set; }
        /// <summary>The editor's undo history, so a test play doesn't forget it.</summary>
        public static EditorHistory EditorHistory { get; private set; }

        // Domain reload is off in this project's Play mode settings, so statics survive from one session to the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Level = null;
            EndTest();
        }

        /// <summary>Remembers the editor's state and the level to try out. Load <see cref="Scene"/> after this.</summary>
        public static void BeginTest(CustomLevelData level, Vector2? startPx, float cameraX, CustomPiece selected,
            int palettePage, PaletteItem tool, EditorHistory history = null)
        {
            Level = level;
            Testing = true;
            TestStartPx = startPx;
            TestFinished = false;
            EditorCameraX = cameraX;
            EditorSelected = selected;
            EditorPalettePage = palettePage;
            EditorTool = tool;
            EditorHistory = history;
        }

        /// <summary>Called by the editor once it has taken back its state.</summary>
        public static void EndTest()
        {
            Testing = false;
            TestStartPx = null;
            EditorSelected = null;
            EditorTool = null;
            EditorHistory = null;
        }
    }
}
