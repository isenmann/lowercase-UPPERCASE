using System.Collections.Generic;
using System.IO;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// Generates the prototype scenes. Layout is written in the pixel coordinates of the 1280x720
    /// mockups, where a "surface" is the height a letter stands at. Form settings are only created
    /// when missing, so rebuilding never discards movement tuning.
    /// The level pieces and sections are part of the game, because the random level is built from them as it loads;
    /// everything that makes and saves scenes and assets is editor-only.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string FontFolder = "Assets/Fonts";
        const string ArtFolder = "Assets/Art";
        const string SettingsFolder = "Assets/Settings/Forms";
        const string SceneFolder = "Assets/Scenes";
        const string TemplateScene = SceneFolder + "/SampleScene.unity";
        const string ChapterScene = SceneFolder + "/Chapter1.unity";
        const string PlaygroundScene = SceneFolder + "/Playground.unity";
        const string VocabularyScene = SceneFolder + "/Vocabulary.unity";
        const string OverworldScene = SceneFolder + "/Overworld.unity";
        const float PixelsPerUnit = 72f;
        const string ControlsText = "← →  move  ·  Space  jump  ·  Shift  grow  ·  F  shoot  ·  Esc  pause";
        const float ControlsLeftPx = 670f;

        static readonly Color Background = new Color32(0x11, 0x13, 0x18, 0xFF);
        static readonly Color RuledLine = new Color32(0x1A, 0x1D, 0x24, 0xFF);
        static readonly Color BarLine = new Color32(0x2A, 0x2E, 0x38, 0xFF);
        static readonly Color Paper = new Color32(0xF2, 0xF0, 0xEA, 0xFF);
        static readonly Color Muted = new Color32(0x9A, 0xA0, 0xAE, 0xFF);
        static readonly Color Player = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        static readonly Color Interactive = new Color32(0x5C, 0xD3, 0xF5, 0xFF);
        static readonly Color Danger = new Color32(0xFF, 0x4D, 0x5E, 0xFF);
        static readonly Color Breakable = new Color32(0x6B, 0x71, 0x80, 0xFF);

        static TMP_FontAsset mono;
        static TMP_FontAsset monoBold;
        static TMP_FontAsset black;
        static Sprite white;
        static readonly List<Checkpoint> levelCheckpoints = new List<Checkpoint>();

#if UNITY_EDITOR
        [MenuItem("lowercase UPPERCASE/Rebuild Prototype Scenes")]
        public static void Rebuild()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);

            BuildMenu();
            BuildChapter1(lower, upper);
            BuildPlayground(lower, upper);
            BuildVocabulary(lower, upper);
            BuildOverworld(lower, upper);
            BuildCellar(lower, upper);
            BuildSkyline(lower, upper);
            BuildCrack(lower, upper);
            BuildWall(lower, upper);
            BuildPlank(lower, upper);
            BuildGate(lower, upper);
            BuildSpace(lower, upper);
            BuildSkate(lower, upper);
            BuildTrail(lower, upper);
            BuildChain(lower, upper);
            BuildLadder(lower, upper);
            BuildFlames(lower, upper);
            BuildTar(lower, upper);
            BuildFinal(lower, upper);
            BuildRandomScene(lower, upper);

            SetBuildScenes();
            EditorSceneManager.OpenScene(MenuScene);
        }

        static void PrepareAssets(out FormSettings lower, out FormSettings upper)
        {
            mono = FontAsset("SpaceMono-Regular");
            monoBold = FontAsset("SpaceMono-Bold");
            black = FontAsset("ArchivoBlack-Regular");
            white = WhiteSprite();
            lower = LowercaseForm();
            upper = UppercaseForm();
            AssetDatabase.SaveAssets();
        }

        /// <summary>The 15 levels in the order they are played, which is not their build order.</summary>
        static readonly string[] PlayOrder =
        {
            OverworldScene, CrackScene, WallScene,
            PlankScene, GateScene, CellarScene,
            SpaceScene, SkateScene, TrailScene,
            ChainScene, LadderScene, SkylineScene,
            FlamesScene, TarScene, FinalScene
        };

        // New scenes are appended, so older build indices, the number keys and the tests that use them don't move.
        static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(ChapterScene, true),
                new EditorBuildSettingsScene(PlaygroundScene, true),
                new EditorBuildSettingsScene(VocabularyScene, true),
                new EditorBuildSettingsScene(OverworldScene, true),
                new EditorBuildSettingsScene(CellarScene, true),
                new EditorBuildSettingsScene(SkylineScene, true),
                new EditorBuildSettingsScene(CrackScene, true),
                new EditorBuildSettingsScene(WallScene, true),
                new EditorBuildSettingsScene(PlankScene, true),
                new EditorBuildSettingsScene(GateScene, true),
                new EditorBuildSettingsScene(SpaceScene, true),
                new EditorBuildSettingsScene(SkateScene, true),
                new EditorBuildSettingsScene(TrailScene, true),
                new EditorBuildSettingsScene(ChainScene, true),
                new EditorBuildSettingsScene(LadderScene, true),
                new EditorBuildSettingsScene(FlamesScene, true),
                new EditorBuildSettingsScene(TarScene, true),
                new EditorBuildSettingsScene(FinalScene, true),
                new EditorBuildSettingsScene(RandomScene, true)
            };
        }

        // ---------------------------------------------------------------- levels

        static void BuildChapter1(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(ChapterScene, "Chapter 1");

            // Every word of the sentence is ground, in order: the, sat, on, the, mat.
            Word(level, "the", 56f, 547f, 72f);
            Word(level, "sat", 290f, 447f, 72f);
            Word(level, "ice", 520f, 587f, 72f, WordKind.Ice, Interactive, italic: true);
            PlaceCheckpoint(level, 565f, 587f);
            Word(level, "on", 666f, 587f, 72f);
            Word(level, "the", 768f, 587f, 72f);
            Word(level, "mat", 914f, 587f, 72f);
            Cracked(level, "b\nr\ni\nc\nk\nb\nr\ni\nc\nk", 698f, 587f);
            Spring(level, 1110f, 587f, padWidthPx: 80f);

            Text(level, "Word c", "c", 1040f, 232f, 72f, mono, Paper);
            Goal(level, 1090f, 180f, 40f, 52f);
            Text(level, "Word t", "t", 1136f, 232f, 72f, mono, Paper);

            FinishScene(ChapterScene, level, "Ch.1 · A simple sentence", lower, upper, new Vector2(130f, 547f),
                "the c", "a", "t sat on the mat");
        }

        static void BuildPlayground(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(PlaygroundScene, "Playground");
            const float floor = 600f;

            WordBlock enemyGround = Word(level, "the quick brown", 8f, floor, 44f);
            BuildEnemy(level, enemyGround, 230f);
            Spring(level, 453f, floor, padWidthPx: 80f);
            Word(level, "fox jumps over the lazy dog", 493f, floor, 44f);

            // Steps that measure jump height, each 90px above the last.
            Word(level, "one", 30f, 520f, 72f);
            Word(level, "two", 200f, 430f, 72f);
            Word(level, "three", 30f, 340f, 72f);

            // Drop off this one and release Shift on the way down to try the glide.
            Word(level, "drop", 590f, 200f, 72f);

            // The gap under this word fits a but not A, so it tests headroom.
            Word(level, "ceiling", 540f, 526f, 48f);

            // Between the ceiling and the brick. The enemy's word ends at the spring, so it can't walk here.
            PlaceCheckpoint(level, 770f, floor);

            Cracked(level, "b\nr\ni\nc\nk", 830f, floor);
            Word(level, "ice ice ice", 950f, 470f, 48f, WordKind.Ice, Interactive, italic: true);

            FinishScene(PlaygroundScene, level, "Playground · 1 chapter  2 playground", lower, upper,
                new Vector2(905f, floor), "try every ", "move", " here");
        }

#endif

        // ---------------------------------------------------------------- level pieces

        static float X(float pixels) => (pixels - 640f) / PixelsPerUnit;
        static float Y(float pixels) => (360f - pixels) / PixelsPerUnit;
        static float Size(float pixels) => pixels / PixelsPerUnit;
        static float FontSize(float pixels) => pixels / PixelsPerUnit * 10f;

        static TextMeshPro Text(Transform parent, string name, string content, float leftPx, float baselinePx,
            float fontPx, TMP_FontAsset font, Color color, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(X(leftPx), Y(baselinePx), 0f);
            var text = go.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontSize = FontSize(fontPx);
            text.color = color;
            text.alignment = TextAlignmentOptions.BaselineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(10f, 0f);
            text.sortingOrder = order;
            text.text = content;
            return text;
        }

        static WordBlock Word(Transform parent, string content, float leftPx, float surfacePx, float fontPx,
            WordKind kind = WordKind.Solid, Color? color = null, bool italic = false)
        {
            TextMeshPro text = Text(parent, $"Word {content}", content, leftPx, surfacePx, fontPx, mono, color ?? Paper);
            if (italic) text.fontStyle = FontStyles.Italic;
            WordBlock block = AddBlock(text, kind);
            if (kind == WordKind.Ice) block.grip = 0.12f;
            MoveTopTo(block, surfacePx);
            return block;
        }

        static void Cracked(Transform parent, string letters, float leftPx, float bottomPx)
        {
            TextMeshPro text = Text(parent, "Word brick", letters, leftPx, bottomPx, 46f, mono, Breakable);
            text.lineSpacing = -63f;
            WordBlock block = AddBlock(text, WordKind.Cracked);
            block.impactFont = black;
            block.FitCollider();
            var box = block.GetComponent<BoxCollider2D>();
            float localBottom = box.offset.y - box.size.y * 0.5f;
            SetHeight(block, Y(bottomPx) - localBottom);
        }

        static void Spring(Transform parent, float centrePx, float surfacePx, float padWidthPx, float fontPx = 84f)
        {
            float leftPx = centrePx - fontPx * 0.3f;
            TextMeshPro text = Text(parent, "Word !", "!", leftPx, surfacePx, fontPx, monoBold, Interactive);
            WordBlock block = AddBlock(text, WordKind.Spring);
            block.padWidth = Size(padWidthPx);
            MoveTopTo(block, surfacePx);
        }

        static WordBlock AddBlock(TextMeshPro text, WordKind kind)
        {
            text.gameObject.layer = LayerMask.NameToLayer("Level");
            var block = text.gameObject.AddComponent<WordBlock>();
            block.kind = kind;
            block.FitCollider();
            return block;
        }

        static void MoveTopTo(WordBlock block, float surfacePx)
        {
            block.FitCollider();
            var box = block.GetComponent<BoxCollider2D>();
            float localTop = box.offset.y + box.size.y * 0.5f;
            SetHeight(block, Y(surfacePx) - localTop);
        }

        static void SetHeight(WordBlock block, float y)
        {
            var rect = (RectTransform)block.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        static void Goal(Transform parent, float leftPx, float topPx, float widthPx, float heightPx,
            LetterForm wantedForm = LetterForm.Lowercase)
        {
            var go = new GameObject("Goal Blank");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(X(leftPx + widthPx * 0.5f), Y(topPx + heightPx * 0.5f), 0f);
            go.layer = LayerMask.NameToLayer("Level");

            float w = Size(widthPx);
            float h = Size(heightPx);
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(w, h);

            const float line = 0.04f;
            Rect(go.transform, "Top", new Vector2(0f, h * 0.5f), new Vector2(w, line), Player, 1);
            Rect(go.transform, "Bottom", new Vector2(0f, -h * 0.5f), new Vector2(w, line), Player, 1);
            Rect(go.transform, "Left", new Vector2(-w * 0.5f, 0f), new Vector2(line, h), Player, 1);
            Rect(go.transform, "Right", new Vector2(w * 0.5f, 0f), new Vector2(line, h), Player, 1);

            SpriteRenderer glow = Rect(go.transform, "Glow", Vector2.zero, new Vector2(w, h),
                new Color(Player.r, Player.g, Player.b, 0.25f), -1);
            glow.enabled = false;
            var blank = go.AddComponent<GoalBlank>();
            blank.glow = glow;
            blank.wantedForm = wantedForm;
        }

        /// <summary>
        /// Stands a checkpoint on a surface. Choose a spot no enemy can walk to, because the letter returns there.
        /// Call in the order the letter meets them.
        /// </summary>
        static void PlaceCheckpoint(Transform parent, float leftPx, float surfacePx)
        {
            const float fontPx = 72f;
            const float tailPx = 14f; // The semicolon's tail hangs below the baseline, so lift it to rest on the word.
            const float stripHeight = 40f;
            float centrePx = fontPx * 0.3f;

            TextMeshPro text = Text(parent, "Checkpoint", ";", leftPx, surfacePx - tailPx, fontPx, monoBold, Muted, -1);
            var trigger = text.gameObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(Size(centrePx), stripHeight);
            trigger.offset = new Vector2(Size(centrePx), 0f);

            var checkpoint = text.gameObject.AddComponent<Checkpoint>();
            checkpoint.glyph = text;
            checkpoint.reachedColor = Player;
            checkpoint.respawnFeet = new Vector2(X(leftPx + centrePx), Y(surfacePx) + 0.05f);
            checkpoint.order = levelCheckpoints.Count;
            levelCheckpoints.Add(checkpoint);
        }

        static SpriteRenderer Rect(Transform parent, string name, Vector2 localPosition, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = white;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        static SpriteRenderer PixelRect(Transform parent, string name, float leftPx, float topPx, float widthPx,
            float heightPx, Color color, int order)
        {
            var position = new Vector2(X(leftPx + widthPx * 0.5f), Y(topPx + heightPx * 0.5f));
            return Rect(parent, name, position, new Vector2(Size(widthPx), Size(heightPx)), color, order);
        }

#if UNITY_EDITOR
        // ---------------------------------------------------------------- scene shell

        static Transform BeginScene(string path, string levelName, bool ruledLines = true)
        {
            levelCheckpoints.Clear(); // In case an earlier build stopped part way.
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(TemplateScene, path);
            EditorSceneManager.OpenScene(path);

            Camera camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.allowMSAA = true;

            // Far wider than one screen, so they also cover levels where the camera scrolls.
            if (ruledLines)
            {
                Transform lines = new GameObject("Ruled Lines").transform;
                for (float y = 89f; y < 720f; y += 90f)
                    PixelRect(lines, "Line", -2000f, y, 14000f, 1f, RuledLine, -10);
            }

            return new GameObject(levelName).transform;
        }

        static void FinishScene(string path, Transform level, string title, FormSettings lower, FormSettings upper,
            Vector2 startPx, string before, string missing, string after, float scrollEndPx = 0f,
            bool startsArmed = true, string nextScene = null, System.Action<PrototypeFlow, Transform> configure = null)
        {
            // The random level places its checkpoints as it loads.
            if (levelCheckpoints.Count == 0 && configure == null)
                throw new System.InvalidOperationException($"{path} needs a checkpoint: call PlaceCheckpoint while building it.");

            LetterController letter = BuildLetter(lower, upper, startPx);
            letter.GetComponent<LetterShooter>().startsArmed = startsArmed;

            var feedback = Camera.main.gameObject.AddComponent<ScreenFeedback>();
            feedback.letter = letter;

            PrototypeFlow flow = BuildHud(letter, title, before, missing, after, out Transform hud);
            flow.checkpoints = levelCheckpoints.ToArray();
            levelCheckpoints.Clear();

            if (scrollEndPx > 0f)
            {
                feedback.followLetter = true;
                feedback.minX = 0f;
                feedback.maxX = X(scrollEndPx - 640f);
                feedback.hud = hud;
            }

            if (nextScene != null)
            {
                var transition = flow.gameObject.AddComponent<LevelTransition>();
                transition.level = level;
                transition.letter = letter;
                transition.nextScene = nextScene;
                // The last level ends the run, so its score goes to the high score table on the way to the menu.
                if (nextScene == SceneName(MenuScene)) transition.endScreen = flow.gameOver;
                // Larger than the screen, so camera shake can't show its edges.
                transition.fade = PixelRect(hud, "Fade", -100f, -100f, 1480f, 920f, Color.black, 100);
            }

            configure?.Invoke(flow, hud);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene, path);
        }

        static LetterController BuildLetter(FormSettings lower, FormSettings upper, Vector2 startPx)
        {
            var go = new GameObject("Letter");
            go.transform.position = new Vector3(X(startPx.x), Y(startPx.y), 0f);
            go.layer = LayerMask.NameToLayer("Player");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<BoxCollider2D>();

            var scaler = new GameObject("Scaler").transform;
            scaler.SetParent(go.transform, false);

            var letter = go.AddComponent<LetterController>();
            letter.lowercase = lower;
            letter.uppercase = upper;
            letter.levelMask = LayerMask.GetMask("Level");

            var visual = go.AddComponent<LetterVisual>();
            visual.scaler = scaler;
            visual.lowerGlyph = Glyph(scaler, "a", 56f, monoBold);
            visual.upperGlyph = Glyph(scaler, "A", 96f, black);
            visual.upperGlyph.gameObject.SetActive(false);

            visual.hookThread = Rect(null, "Hook Thread", Vector2.zero, Vector2.one, Interactive, 9);
            visual.hookThread.enabled = false;

            go.AddComponent<LetterShooter>();
            go.AddComponent<LetterInput>();
            return letter;
        }

#endif

        /// <summary>Stands an enemy on top of a word. Enemies have no gravity, so the height has to be exact.</summary>
        static void BuildEnemy(Transform parent, WordBlock ground, float leftPx)
        {
            var groundBox = ground.GetComponent<BoxCollider2D>();
            float surfaceY = ground.transform.TransformPoint(groundBox.offset).y + groundBox.size.y * 0.5f;

            var go = new GameObject("Enemy &");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(X(leftPx), surfaceY, 0f);
            go.layer = LayerMask.NameToLayer("Enemy");
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<BoxCollider2D>();

            // The glyph comes first, because in the game the enemy wakes the moment it is added.
            TextMeshPro glyph = Glyph(go.transform, "&", 48f, monoBold);
            glyph.color = Danger;
            glyph.sortingOrder = 8;
            var enemy = go.AddComponent<Enemy>();
            enemy.levelMask = LayerMask.GetMask("Level");
            enemy.glyph = glyph;
        }

        static TextMeshPro Glyph(Transform parent, string content, float fontPx, TMP_FontAsset font)
        {
            var go = new GameObject($"Glyph {content}");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontSize = FontSize(fontPx);
            text.color = Player;
            text.alignment = TextAlignmentOptions.Baseline;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(4f, 0f);
            text.sortingOrder = 10;
            text.text = content;
            return text;
        }

#if UNITY_EDITOR
        static PrototypeFlow BuildHud(LetterController letter, string title, string before, string missing,
            string after, out Transform hud)
        {
            hud = new GameObject("HUD").transform;
            const int order = 30;

            PixelRect(hud, "Top Bar", 0f, 0f, 1280f, 64f, Background, order);
            PixelRect(hud, "Top Line", 0f, 63f, 1280f, 1f, BarLine, order);
            PixelRect(hud, "Bottom Bar", 0f, 648f, 1280f, 72f, Background, order);
            PixelRect(hud, "Bottom Line", 0f, 648f, 1280f, 1f, BarLine, order);

            Text(hud, "Title", title, 32f, 38f, 14f, mono, Muted, order + 1);
            TextMeshPro lives = Text(hud, "Lives", "", 380f, 40f, 22f, monoBold, Player, order + 1);
            TextMeshPro sentence = Text(hud, "Sentence", "", 500f, 40f, 22f, mono, Paper, order + 1);
            TextMeshPro collectibles = Text(hud, "Collectibles", "", 800f, 38f, 14f, mono, Muted, order + 1);
            TextMeshPro score = Text(hud, "Score", "", 900f, 38f, 14f, mono, Muted, order + 1);
            TextMeshPro timer = Text(hud, "Timer", "", 1070f, 40f, 22f, monoBold, Paper, order + 1);
            Text(hud, "Restart", "R restart", 1160f, 38f, 14f, mono, Muted, order + 1);

            Text(hud, "Form", "Form", 32f, 690f, 14f, mono, Muted, order + 1);
            SpriteRenderer lowerChip = PixelRect(hud, "Chip a", 80f, 662f, 44f, 44f, Player, order + 1);
            SpriteRenderer upperChip = PixelRect(hud, "Chip A", 136f, 662f, 44f, 44f, RuledLine, order + 1);
            TextMeshPro lowerLabel = Text(hud, "Chip a Label", "a", 95f, 692f, 24f, monoBold, Background, order + 2);
            TextMeshPro upperLabel = Text(hud, "Chip A Label", "A", 149f, 692f, 22f, black, Breakable, order + 2);
            TextMeshPro readout = Text(hud, "Readout", "", 210f, 690f, 14f, mono, Muted, order + 1);
            Text(hud, "Controls", ControlsText, ControlsLeftPx, 690f, 14f, mono, Muted, order + 1);

            var flow = new GameObject("Prototype Flow").AddComponent<PrototypeFlow>();
            flow.letter = letter;
            flow.sentence = sentence;
            flow.sentenceBefore = before;
            flow.missingLetter = missing;
            flow.sentenceAfter = after;
            flow.timer = timer;
            flow.score = score;
            flow.collectibles = collectibles;
            flow.lives = lives;
            flow.lifeColor = Player;
            flow.lostLifeColor = Breakable;
            flow.timerColor = Paper;
            flow.lowTimeColor = Danger;

            // Above everything else in the HUD, including a level's fade.
            const int gameOverOrder = 110;
            var gameOver = new GameObject("Game Over");
            gameOver.transform.SetParent(hud, false);
            PixelRect(gameOver.transform, "Backdrop", -100f, -100f, 1480f, 920f, Background, gameOverOrder);
            TextMeshPro heading = Centred(gameOver.transform, "Heading", "GAME OVER", 150f, 72f, black, Danger, gameOverOrder + 1);
            TextMeshPro finalScore = Centred(gameOver.transform, "Final Score", "", 198f, 22f, mono, Paper, gameOverOrder + 1);
            Centred(gameOver.transform, "High Scores Heading", "high scores", 250f, 14f, mono, Muted, gameOverOrder + 1);
            var rows = new TMP_Text[HighScores.Size];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = Centred(gameOver.transform, $"High Score {i + 1}", "", 284f + i * 30f, 20f, mono, Paper, gameOverOrder + 1);
            TextMeshPro hint = Centred(gameOver.transform, "Hint", "", 610f, 14f, mono, Muted, gameOverOrder + 1);
            gameOver.SetActive(false);

            var screen = gameOver.AddComponent<GameOverScreen>();
            screen.heading = heading.gameObject;
            screen.score = finalScore;
            screen.rows = rows;
            screen.hint = hint;
            screen.rowColor = Paper;
            screen.emptyRowColor = Breakable;
            screen.newRowColor = Player;
            flow.gameOver = screen;

            BuildPauseMenu(hud, flow, letter);

            flow.lowerChip = lowerChip;
            flow.upperChip = upperChip;
            flow.lowerChipLabel = lowerLabel;
            flow.upperChipLabel = upperLabel;
            flow.readout = readout;
            return flow;
        }

        // Text boxes are 720px wide from their left edge, so 280 centres them on the 1280px screen.
        static TextMeshPro Centred(Transform parent, string name, string content, float baselinePx, float fontPx,
            TMP_FontAsset font, Color color, int order)
        {
            TextMeshPro text = Text(parent, name, content, 280f, baselinePx, fontPx, font, color, order);
            text.alignment = TextAlignmentOptions.Baseline;
            return text;
        }

        /// <summary>A small box in the middle of the screen over a dimmed level: Resume, then Main menu.</summary>
        static void BuildPauseMenu(Transform hud, PrototypeFlow flow, LetterController letter)
        {
            // Above the level's fade, below the game over screen.
            const int order = 104;
            const float boxLeft = 520f;
            const float boxWidth = 240f;
            const float boxHeight = 44f;

            var popup = new GameObject("Pause");
            popup.transform.SetParent(hud, false);
            PixelRect(popup.transform, "Dim", -100f, -100f, 1480f, 920f, new Color(Background.r, Background.g, Background.b, 0.75f), order);
            PixelRect(popup.transform, "Panel", 480f, 250f, 320f, 206f, Background, order + 1);
            PixelRect(popup.transform, "Panel Top", 480f, 250f, 320f, 1f, BarLine, order + 2);
            PixelRect(popup.transform, "Panel Bottom", 480f, 455f, 320f, 1f, BarLine, order + 2);
            PixelRect(popup.transform, "Panel Left", 480f, 250f, 1f, 206f, BarLine, order + 2);
            PixelRect(popup.transform, "Panel Right", 799f, 250f, 1f, 206f, BarLine, order + 2);

            // Text boxes are 720px wide from their left edge, so 280 centres them on the 1280px screen.
            Text(popup.transform, "Heading", "paused", 280f, 300f, 22f, monoBold, Muted, order + 3)
                .alignment = TextAlignmentOptions.Baseline;

            string[] options = { "Resume", "Main menu" };
            var labels = new TMP_Text[options.Length];
            var boxes = new SpriteRenderer[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                float top = 330f + i * 60f;
                boxes[i] = PixelRect(popup.transform, $"{options[i]} Box", boxLeft, top, boxWidth, boxHeight, RuledLine, order + 2);
                TextMeshPro label = Text(popup.transform, options[i], options[i], 280f, top + 29f, 20f, monoBold, Paper, order + 3);
                label.alignment = TextAlignmentOptions.Baseline;
                labels[i] = label;
            }
            popup.SetActive(false);

            var pause = flow.gameObject.AddComponent<PauseMenu>();
            pause.popup = popup;
            pause.letter = letter;
            pause.letterInput = letter.GetComponent<LetterInput>();
            pause.labels = labels;
            pause.boxes = boxes;
            pause.selectedBox = Player;
            pause.idleBox = RuledLine;
            pause.selectedLabel = Background;
            pause.idleLabel = Paper;
            flow.pauseMenu = pause;
        }

        // ---------------------------------------------------------------- assets

        static TMP_FontAsset FontAsset(string fontName)
        {
            string path = $"{FontFolder}/{fontName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{fontName}.ttf");
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic);
            asset.name = $"{fontName} SDF";
            AssetDatabase.CreateAsset(asset, path);

            asset.atlasTexture.name = $"{fontName} Atlas";
            asset.material.name = $"{fontName} Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static Sprite WhiteSprite()
        {
            string path = $"{ArtFolder}/White.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(ArtFolder);
                var texture = new Texture2D(4, 4);
                var pixels = new List<Color32>();
                for (int i = 0; i < 16; i++) pixels.Add(new Color32(255, 255, 255, 255));
                texture.SetPixels32(pixels.ToArray());
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 4f;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static FormSettings LowercaseForm()
        {
            return Form("Form_lowercase", s =>
            {
                s.colliderSize = new Vector2(0.42f, 0.4f);
                s.mass = 1f;
                s.runSpeed = 7f;
                s.groundAcceleration = 60f;
                s.groundDeceleration = 70f;
                s.airAcceleration = 40f;
                s.airDeceleration = 8f;
                s.overspeedDecayGround = 12f;
                s.overspeedDecayAir = 2f;
                s.jumpHeight = 2.5f;
                s.timeToApex = 0.38f;
                s.fallGravityMultiplier = 0.8f;
                s.maxFallSpeed = 12f;
                s.jumpCutMultiplier = 0.45f;
                s.apexHangThreshold = 1.2f;
                s.apexHangGravityMultiplier = 0.55f;
                s.springHeight = 6f;
                s.asteriskSize = 5f;
                s.fallToForward = 0.6f;
                s.fallKept = 0.35f;
            });
        }

        static FormSettings UppercaseForm()
        {
            return Form("Form_UPPERCASE", s =>
            {
                s.colliderSize = new Vector2(0.92f, 0.92f);
                s.mass = 5f;
                s.runSpeed = 3f;
                s.groundAcceleration = 18f;
                s.groundDeceleration = 22f;
                s.airAcceleration = 10f;
                s.airDeceleration = 3f;
                s.overspeedDecayGround = 10f;
                s.overspeedDecayAir = 4f;
                s.jumpHeight = 0.6f;
                s.timeToApex = 0.2f;
                s.fallGravityMultiplier = 1.5f;
                s.maxFallSpeed = 20f;
                s.jumpCutMultiplier = 0.7f;
                s.apexHangThreshold = 0f;
                s.apexHangGravityMultiplier = 1f;
                s.springHeight = 3f;
                s.asteriskSize = 14f;
                s.fallToForward = 0f;
                s.fallKept = 1f;
            });
        }

        static FormSettings Form(string assetName, System.Action<FormSettings> configure)
        {
            string path = $"{SettingsFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<FormSettings>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.ImportAsset(SettingsFolder);
            var settings = ScriptableObject.CreateInstance<FormSettings>();
            configure(settings);
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }
#endif
    }
}
