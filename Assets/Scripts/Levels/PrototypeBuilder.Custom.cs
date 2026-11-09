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

            // Terrain first, because plates, keys and enemies find what they belong to among it. Checkpoints are
            // numbered in the order they are placed, and the letter meets them left to right.
            float right = 1280f;
            List<CustomPiece> pieces = data.pieces.Where(p => p != null).OrderBy(p => p.x).ToList();
            foreach (CustomPiece piece in pieces.Where(p => BuildPhase(p) == 0))
                right = Mathf.Max(right, BuildPiece(level, piece));
            if (data.ceiling) Ceiling(level, right + CustomCeilingOverhangPx);
            foreach (CustomPiece piece in pieces.Where(p => BuildPhase(p) == 1))
                right = Mathf.Max(right, BuildPiece(level, piece));

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

                case CustomPieceType.Tube:
                    Stacked(level, text.Trim().Length > 0 ? text.Trim() : "tube", piece.x, Count(piece, 3), piece.y);
                    return piece.x + Mathf.Max(4, text.Trim().Length) * TubeFontPx * 0.6f;

                case CustomPieceType.Kerned:
                    return CustomKerned(level, text, piece.x, piece.y);

                case CustomPieceType.Spring:
                    Spring(level, piece.x, piece.y, Value(piece, 80f));
                    return piece.x + 40f;

                case CustomPieceType.Door:
                    return RightEdge(Door(level, piece.x, piece.y));

                case CustomPieceType.Plate:
                    WordBlock plate = Plate(level, piece.x, piece.y);
                    plate.linkedDoor = Nearest(level, piece.x, piece.y, w => w.kind == WordKind.Door);
                    return piece.x + 40f;

                case CustomPieceType.Lift:
                    float top = piece.y2 != 0f ? piece.y2 : piece.y - 300f;
                    return Lift(level, piece.x, top, piece.y);

                case CustomPieceType.Hooks:
                    int hooks = Count(piece, 1);
                    for (int i = 0; i < hooks; i++)
                        Hook(level, $"Hook {i + 1}", piece.x + i * HookSpacingPx, piece.y);
                    return piece.x + (hooks - 1) * HookSpacingPx + 60f;

                case CustomPieceType.CapsLock:
                    CustomCapsLock(level, piece.x, piece.x2 > piece.x ? piece.x2 : piece.x + 800f);
                    return Mathf.Max(piece.x, piece.x2);

                case CustomPieceType.CapsKey:
                    CustomCapsKey(level, piece.x, piece.y);
                    return piece.x + 90f;

                case CustomPieceType.Hill:
                    Hill(level, piece.x, Count(piece, 3));
                    return piece.x;

                case CustomPieceType.Cloud:
                    Cloud(level, piece.x, piece.y);
                    return piece.x;

                case CustomPieceType.Enemy:
                    return CustomEnemy(level, piece);

                default:
                    Debug.LogWarning($"Custom level: skipped a piece of unknown type '{piece.type}'.");
                    return piece.x;
            }
        }

        const float ColumnWidthPx = 46f * 0.6f;
        const string RiserMark = "\u00a1";

        /// <summary>0: terrain and things, built first. 1: pieces that find terrain built before them.</summary>
        static int BuildPhase(CustomPiece piece)
        {
            switch (piece.type)
            {
                case CustomPieceType.Plate:
                case CustomPieceType.CapsKey:
                case CustomPieceType.Enemy:
                    return 1;
                default:
                    return 0;
            }
        }

        static int Count(CustomPiece piece, int fallback) => piece.count > 0 ? piece.count : fallback;
        static float Value(CustomPiece piece, float fallback) => piece.value != 0f ? piece.value : fallback;

        /// <summary>Loose letters, with a riser in every hole marked by an inverted exclamation mark.</summary>
        static float CustomKerned(Transform level, string text, float leftPx, float surfacePx)
        {
            var holes = new List<int>();
            var letters = new System.Text.StringBuilder();
            foreach (char c in text.Replace(" ", ""))
            {
                if (c.ToString() == RiserMark)
                {
                    if (letters.Length > 0) holes.Add(letters.Length - 1);
                }
                else letters.Append(c);
            }
            if (letters.Length == 0) return leftPx;
            // A mark after the last letter has no hole to sit in.
            holes.RemoveAll(h => h >= letters.Length - 1);
            return KernedWithRisers(level, letters.ToString(), leftPx, surfacePx, holes.Distinct().ToArray());
        }

        /// <summary>The Caps Lock stretch without its key, which is a piece of its own.</summary>
        static void CustomCapsLock(Transform level, float leftPx, float rightPx)
        {
            var zoneObject = new GameObject("Caps Lock Zone");
            zoneObject.transform.SetParent(level, false);
            zoneObject.layer = LayerMask.NameToLayer("Level");
            float widthPx = rightPx - leftPx;
            zoneObject.transform.position = new Vector3(X(leftPx + widthPx * 0.5f), Y(352f), 0f);
            var trigger = zoneObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(Size(widthPx), Size(576f));

            var zone = zoneObject.AddComponent<CapsLockZone>();
            zone.tint = Rect(zoneObject.transform, "Tint", Vector2.zero, trigger.size,
                new Color(Hot.r, Hot.g, Hot.b, 0.03f), -5);
            zone.label = Text(level, "LOCK", "LOCK", leftPx + 20f, 300f, 96f, black,
                new Color(Hot.r, Hot.g, Hot.b, 0.3f), -4);
        }

        /// <summary>The key, switching off the Caps Lock stretch nearest to it.</summary>
        static void CustomCapsKey(Transform level, float leftPx, float baselinePx)
        {
            TextMeshPro key = Text(level, "Key", "key", leftPx, baselinePx, 40f, monoBold, Player, 5);
            key.gameObject.layer = LayerMask.NameToLayer("Level");
            var keyTrigger = key.gameObject.AddComponent<BoxCollider2D>();
            keyTrigger.isTrigger = true;
            keyTrigger.size = new Vector2(Size(90f), Size(70f));
            keyTrigger.offset = new Vector2(Size(36f), Size(10f));

            CapsLockZone nearest = null;
            float best = float.MaxValue;
            foreach (CapsLockZone zone in level.GetComponentsInChildren<CapsLockZone>())
            {
                float distance = Mathf.Abs(zone.transform.position.x - X(leftPx));
                if (distance < best)
                {
                    best = distance;
                    nearest = zone;
                }
            }
            key.gameObject.AddComponent<CapsLockKey>().zone = nearest;
        }

        static float CustomEnemy(Transform level, CustomPiece piece)
        {
            float x = piece.x;
            float direction = piece.flip ? -1f : 1f;
            WordBlock ground = GroundUnder(level, x, piece.y);
            switch (piece.kind)
            {
                case CustomPieceType.Walker:
                    if (ground == null) break;
                    BuildEnemy(level, ground, x).GetComponent<EnemyWalk>().direction = direction;
                    return x + 40f;
                case CustomPieceType.Spike:
                    if (ground == null) break;
                    BuildSpike(level, ground, x).GetComponent<EnemyWalk>().direction = direction;
                    return x + 40f;
                case CustomPieceType.Armour:
                    if (ground == null) break;
                    BuildArmour(level, ground, x).GetComponent<EnemyWalk>().direction = direction;
                    return x + 40f;
                case CustomPieceType.Swapper:
                    if (ground == null) break;
                    BuildCaseSwapper(level, ground, x).GetComponent<EnemyWalk>().direction = direction;
                    return x + 40f;
                case CustomPieceType.Mimic:
                    if (ground == null) break;
                    BuildMimic(level, ground, x).GetComponent<EnemyWalk>().direction = direction;
                    return x + 40f;
                case CustomPieceType.Curl:
                    if (ground == null) break;
                    BuildCurl(level, ground, x);
                    return x + 40f;
                case CustomPieceType.Hopper:
                    if (ground == null) break;
                    BuildHopper(level, ground, x, piece.value * (piece.flip ? -1f : 1f), piece.delay);
                    return x + 40f;
                case CustomPieceType.Spark:
                    WordBlock hot = Nearest(level, x, piece.y, w => w.kind == WordKind.Hot, below: true);
                    if (hot == null) break;
                    BuildSpark(level, hot, x, piece.delay);
                    return x + 40f;
                case CustomPieceType.Flier:
                    BuildFlier(level, x, piece.y, piece.value).direction = direction;
                    return x + Mathf.Max(0f, piece.value) + 40f;
                case CustomPieceType.Launcher:
                    return RightEdge(BuildLauncher(level, x, piece.y, piece.flip, Count(piece, 2), Value(piece, 2f)));
                case CustomPieceType.Drip:
                    BuildEnemyAt(level, new Vector3(X(x), Y(piece.y), 0f), "'", 72f, stompable: false, shootable: true)
                        .gameObject.AddComponent<EnemyDrip>();
                    return x + 40f;
                case CustomPieceType.FireBar:
                    BuildFireBar(level, x, piece.y, Count(piece, 4), Value(piece, -90f));
                    return x + 60f;
                case CustomPieceType.Eye:
                    BuildEye(level, x, piece.y);
                    return x + 40f;
                case CustomPieceType.Cursor:
                    float end = piece.x2 > x ? piece.x2 : x + 1500f;
                    BuildCursorChase(level, x, end, piece.y, Value(piece, 3.5f));
                    return end;
                default:
                    Debug.LogWarning($"Custom level: skipped an enemy of unknown kind '{piece.kind}'.");
                    return x;
            }

            Debug.LogWarning($"Custom level: skipped the {piece.kind} at {x:0}, {piece.y:0}, which has no "
                + (piece.kind == CustomPieceType.Spark ? "hot word" : "word") + " under it.");
            return x;
        }

        /// <summary>The word whose top is nearest below a point (or a little above it), with the point over it.</summary>
        static WordBlock GroundUnder(Transform level, float xPx, float feetPx) =>
            Nearest(level, xPx, feetPx, w => w.kind != WordKind.Door, below: true);

        /// <summary>
        /// The nearest word that matches. Below: only words under the point (tops at most 16 px above it), by how far
        /// down their top is. Otherwise any such word, by distance across.
        /// </summary>
        static WordBlock Nearest(Transform level, float xPx, float yPx, System.Func<WordBlock, bool> match,
            bool below = false)
        {
            WordBlock best = null;
            float bestDistance = float.MaxValue;
            foreach (WordBlock word in level.GetComponentsInChildren<WordBlock>())
            {
                if (!match(word)) continue;
                var box = word.GetComponent<BoxCollider2D>();
                if (box == null || box.isTrigger) continue;
                float left = LeftEdge(word);
                float right = RightEdge(word);
                float topPx = 360f - (word.transform.TransformPoint(box.offset).y + box.size.y * 0.5f) * PixelsPerUnit;

                float distance;
                if (below)
                {
                    if (xPx < left || xPx > right || topPx < yPx - 16f) continue;
                    distance = topPx - yPx;
                }
                else distance = Mathf.Abs((left + right) * 0.5f - xPx);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = word;
                }
            }
            return best;
        }

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
