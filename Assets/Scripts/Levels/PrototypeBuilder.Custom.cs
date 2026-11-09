using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LowercaseUppercase.Levels
{
    /// <summary>What the custom level's scene needs to know about the level just built into it.</summary>
    public struct CustomLevelLayout
    {
        public Checkpoint[] Checkpoints;
        /// <summary>The camera's furthest x, in world units.</summary>
        public float CameraMaxX;
        /// <summary>Where the letter's feet start, in world units.</summary>
        public Vector3 StartFeet;
        /// <summary>The right edge of the furthest piece, in pixels.</summary>
        public float RightPx;
    }

    /// <summary>
    /// Custom levels from the level editor, built while their scene loads the way random levels are. Each piece record
    /// becomes the same builder call the hand-made levels use, so a word made in the editor behaves exactly like one in
    /// the chapters.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string CustomScene = SceneFolder + "/CustomLevel.unity";
        const string SampleCustomLevel = "Assets/Levels/SampleCustomLevel.json";
        const float CustomLevelPlaceholderEndPx = 4000f;
        const float CustomTailPx = 200f; // Room past the last piece before the camera stops.
        const float CustomCeilingOverhangPx = 400f;

        /// <summary>
        /// Builds a custom level under the level root. Pieces of an unknown type are skipped with a warning, so a level
        /// made by a newer version still opens.
        /// </summary>
        public static CustomLevelLayout BuildCustomLevel(Transform level, CustomLevelData data, TMP_FontAsset monoFont,
            TMP_FontAsset monoBoldFont, TMP_FontAsset blackFont, Sprite whiteSprite)
        {
            mono = monoFont;
            monoBold = monoBoldFont;
            black = blackFont;
            white = whiteSprite;
            levelCheckpoints.Clear();

            // Checkpoints are numbered in the order they are placed, and the letter meets them left to right.
            float right = 1280f;
            IEnumerable<CustomPiece> ordered = data.pieces
                .Where(p => p != null)
                .OrderBy(p => p.type == CustomPieceType.Checkpoint ? 1 : 0)
                .ThenBy(p => p.x);
            foreach (CustomPiece piece in ordered)
                right = Mathf.Max(right, BuildPiece(level, piece));

            if (data.ceiling) Ceiling(level, right + CustomCeilingOverhangPx);

            var layout = new CustomLevelLayout
            {
                Checkpoints = levelCheckpoints.ToArray(),
                CameraMaxX = CameraMaxX(right + CustomTailPx),
                StartFeet = new Vector3(X(data.startX), Y(data.startY), 0f),
                RightPx = right
            };
            levelCheckpoints.Clear();
            return layout;
        }

        /// <summary>Builds one piece and returns its right edge in pixels.</summary>
        static float BuildPiece(Transform level, CustomPiece piece)
        {
            string text = piece.text ?? "";
            switch (piece.type)
            {
                case CustomPieceType.Word:
                    if (text.Trim().Length == 0) return piece.x;
                    return RightEdge(CustomWord(level, text, piece.x, piece.y, piece.kind));

                case CustomPieceType.Brick:
                    BrickWall(level, piece.x, ColumnLetters(text, BrickLetters), piece.y);
                    return piece.x + ColumnWidthPx;

                case CustomPieceType.StickyWall:
                    return RightEdge(StickyWall(level, piece.x, ColumnLetters(text, "s\nt\ni\nc\nk\ny"), piece.y));

                case CustomPieceType.Checkpoint:
                    PlaceCheckpoint(level, piece.x, piece.y);
                    return piece.x + 44f;

                case CustomPieceType.Power:
                    PowerBlock(level, piece.x, piece.y);
                    return piece.x + 130f;

                case CustomPieceType.Hash:
                    Hash(level, piece.x, piece.y);
                    return piece.x + HashSpacingPx * 0.5f;

                case CustomPieceType.Exit:
                    return CustomExit(level, text, piece.x, piece.y, piece.kind);

                default:
                    Debug.LogWarning($"Custom level: skipped a piece of unknown type '{piece.type}'.");
                    return piece.x;
            }
        }

        const float ColumnWidthPx = 46f * 0.6f;

        static WordBlock CustomWord(Transform level, string text, float leftPx, float surfacePx, string kind)
        {
            switch (kind)
            {
                case CustomPieceType.Ice: return Ice(level, text, leftPx, surfacePx);
                case CustomPieceType.Hot: return HotWord(level, text, leftPx, surfacePx);
                case CustomPieceType.Sticky: return StickyFloor(level, text, leftPx, surfacePx);
                case CustomPieceType.Plank: return Word(level, text, leftPx, surfacePx, 72f, WordKind.Hyphen, Interactive);
                default: return Word(level, text, leftPx, surfacePx, 72f);
            }
        }

        /// <summary>A column's letters, one per line. Typed as a plain word, it is stood on end.</summary>
        static string ColumnLetters(string text, string fallback)
        {
            string letters = text.Replace(" ", "").Replace("\r", "");
            if (letters.Length == 0) return fallback;
            return letters.Contains("\n") ? letters : string.Join("\n", letters.ToCharArray());
        }

        static float CustomExit(Transform level, string text, float leftPx, float baselinePx, string kind)
        {
            int blank = text.IndexOf('_');
            string before = blank < 0 ? text : text.Substring(0, blank);
            string after = blank < 0 ? "" : text.Substring(blank + 1);
            LetterForm wanted = kind == "A" ? LetterForm.Uppercase : LetterForm.Lowercase;
            ExitWord(level, leftPx, baselinePx, before, after, wanted);

            float charPx = wanted == LetterForm.Uppercase ? 88f * 0.6f : 72f * 0.6f;
            float blankPx = wanted == LetterForm.Uppercase ? 96f : 52f;
            return leftPx + (before.Length + after.Length) * charPx + blankPx;
        }

#if UNITY_EDITOR
        /// <summary>Builds only the custom level's scene, so edits made to the other scenes are left alone.</summary>
        [MenuItem("lowercase UPPERCASE/Rebuild Custom Level Scene")]
        public static void RebuildCustomScene()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildCustomScene(lower, upper);
            SetBuildScenes();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CustomScene);
        }

        /// <summary>
        /// The letter, HUD and camera with an empty level. Finishing it goes back to the menu without the end screen,
        /// and nothing in it touches the high scores: custom levels have endless lives, so there is no game over.
        /// </summary>
        static void BuildCustomScene(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(CustomScene, "Custom");
            FinishScene(CustomScene, level, "Custom", lower, upper, new Vector2(110f, Ground),
                "", "a", "", scrollEndPx: CustomLevelPlaceholderEndPx, startsArmed: false,
                nextScene: SceneName(MenuScene), configure: (flow, hud) =>
                {
                    flow.gameOver.recordsHighScores = false;
                    flow.gameOver.transform.Find("High Scores Heading").gameObject.SetActive(false);
                    flow.GetComponent<LevelTransition>().endScreen = null;

                    var custom = flow.gameObject.AddComponent<CustomLevel>();
                    custom.level = level;
                    custom.flow = flow;
                    custom.feedback = Camera.main.GetComponent<ScreenFeedback>();
                    custom.title = hud.Find("Title").GetComponent<TMP_Text>();
                    custom.sampleLevel = AssetDatabase.LoadAssetAtPath<TextAsset>(SampleCustomLevel);
                    custom.mono = mono;
                    custom.monoBold = monoBold;
                    custom.black = black;
                    custom.white = white;
                });
        }
#endif
    }
}
