using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Generates the prototype scenes. Layout is written in the pixel coordinates of the 1280x720
    /// mockups, where a "surface" is the height a letter stands at. Form settings are only created
    /// when missing, so rebuilding never discards movement tuning.
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
        const float PixelsPerUnit = 72f;

        static readonly Color Background = new Color32(0x11, 0x13, 0x18, 0xFF);
        static readonly Color RuledLine = new Color32(0x1A, 0x1D, 0x24, 0xFF);
        static readonly Color BarLine = new Color32(0x2A, 0x2E, 0x38, 0xFF);
        static readonly Color Paper = new Color32(0xF2, 0xF0, 0xEA, 0xFF);
        static readonly Color Muted = new Color32(0x9A, 0xA0, 0xAE, 0xFF);
        static readonly Color Player = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        static readonly Color Interactive = new Color32(0x5C, 0xD3, 0xF5, 0xFF);
        static readonly Color Breakable = new Color32(0x6B, 0x71, 0x80, 0xFF);

        static TMP_FontAsset mono;
        static TMP_FontAsset monoBold;
        static TMP_FontAsset black;
        static Sprite white;

        [MenuItem("lowercase UPPERCASE/Rebuild Prototype Scenes")]
        public static void Rebuild()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);

            BuildChapter1(lower, upper);
            BuildPlayground(lower, upper);
            BuildVocabulary(lower, upper);

            SetBuildScenes();
            EditorSceneManager.OpenScene(ChapterScene);
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

        static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ChapterScene, true),
                new EditorBuildSettingsScene(PlaygroundScene, true),
                new EditorBuildSettingsScene(VocabularyScene, true)
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

            Word(level, "the quick brown", 8f, floor, 44f);
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

            Cracked(level, "b\nr\ni\nc\nk", 830f, floor);
            Word(level, "ice ice ice", 950f, 470f, 48f, WordKind.Ice, Interactive, italic: true);

            FinishScene(PlaygroundScene, level, "Playground · 1 chapter  2 playground", lower, upper,
                new Vector2(905f, floor), "try every ", "move", " here");
        }

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

        // ---------------------------------------------------------------- scene shell

        static Transform BeginScene(string path, string levelName)
        {
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
            Transform lines = new GameObject("Ruled Lines").transform;
            for (float y = 89f; y < 720f; y += 90f)
                PixelRect(lines, "Line", -2000f, y, 14000f, 1f, RuledLine, -10);

            return new GameObject(levelName).transform;
        }

        static void FinishScene(string path, Transform level, string title, FormSettings lower, FormSettings upper,
            Vector2 startPx, string before, string missing, string after, float scrollEndPx = 0f)
        {
            LetterController letter = BuildLetter(lower, upper, startPx);

            var feedback = Camera.main.gameObject.AddComponent<ScreenFeedback>();
            feedback.letter = letter;

            PrototypeFlow flow = BuildHud(letter, title, before, missing, after, out Transform hud);

            if (scrollEndPx > 0f)
            {
                feedback.followLetter = true;
                feedback.minX = 0f;
                feedback.maxX = X(scrollEndPx - 640f);
                feedback.hud = hud;
                flow.respawnAtLastSafe = true;
            }

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

            go.AddComponent<LetterInput>();
            return letter;
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
            TextMeshPro sentence = Text(hud, "Sentence", "", 500f, 40f, 22f, mono, Paper, order + 1);
            Text(hud, "Restart", "R restart", 1160f, 38f, 14f, mono, Muted, order + 1);

            Text(hud, "Form", "Form", 32f, 690f, 14f, mono, Muted, order + 1);
            SpriteRenderer lowerChip = PixelRect(hud, "Chip a", 80f, 662f, 44f, 44f, Player, order + 1);
            SpriteRenderer upperChip = PixelRect(hud, "Chip A", 136f, 662f, 44f, 44f, RuledLine, order + 1);
            TextMeshPro lowerLabel = Text(hud, "Chip a Label", "a", 95f, 692f, 24f, monoBold, Background, order + 2);
            TextMeshPro upperLabel = Text(hud, "Chip A Label", "A", 149f, 692f, 22f, black, Breakable, order + 2);
            TextMeshPro readout = Text(hud, "Readout", "", 210f, 690f, 14f, mono, Muted, order + 1);
            Text(hud, "Controls", "← →  move  ·  Space  jump  ·  Shift  grow", 790f, 690f, 14f, mono, Muted, order + 1);

            var flow = new GameObject("Prototype Flow").AddComponent<PrototypeFlow>();
            flow.letter = letter;
            flow.sentence = sentence;
            flow.sentenceBefore = before;
            flow.missingLetter = missing;
            flow.sentenceAfter = after;
            flow.lowerChip = lowerChip;
            flow.upperChip = upperChip;
            flow.lowerChipLabel = lowerLabel;
            flow.upperChipLabel = upperLabel;
            flow.readout = readout;
            return flow;
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
    }
}
