using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Generates the title screen from the "Title screen" board of the design canvas. Positions are that
    /// board's CSS pixels, on a 1280x720 canvas. The rounded corners and the Shift icon are drawn into PNGs here.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string MenuScene = SceneFolder + "/Menu.unity";
        const float MenuColumnTop = 433f;
        const float MenuColumnWidth = 280f;
        const int ButtonRadius = 10;
        const int KeycapRadius = 8;
        const int SpriteScale = 2; // Sprites hold twice the canvas's pixels, so they stay sharp above 720p.

        static readonly Color MenuOutline = new Color32(0x3A, 0x3F, 0x4B, 0xFF);
        static readonly Color MenuBackgroundText = new Color32(0x22, 0x25, 0x2D, 0xFF);
        static readonly Color MenuDim = new Color32(0x6B, 0x71, 0x80, 0xFF);
        static readonly Color PlayerLight = new Color32(0xFF, 0xE2, 0x7A, 0xFF);

        const string MenuBackgroundLines =
            "the cat sat on the mat     and the mat was cold\n" +
            "     a quick brown fox     jumps over     the lazy dog\n" +
            "the ice was thin            so the dog stayed home\n" +
            "        once upon a time     there was a small letter\n" +
            "it wanted to be bigger      but only for a moment\n" +
            "     the end              the end         the end";

        [MenuItem("lowercase UPPERCASE/Rebuild Main Menu")]
        public static void RebuildMainMenu()
        {
            PrepareAssets(out _, out _);
            BuildMenu();
            SetBuildScenes();
            EditorSceneManager.OpenScene(MenuScene);
        }

        static string SceneName(string path) => Path.GetFileNameWithoutExtension(path);

        static void BuildMenu()
        {
            Transform root = BeginScene(MenuScene, "Menu", ruledLines: false);
            TMP_FontAsset regular = FontAsset("SpaceGrotesk-Regular");
            TMP_FontAsset medium = FontAsset("SpaceGrotesk-Medium");
            TMP_FontAsset bold = FontAsset("SpaceGrotesk-Bold");

            Sprite filled = RoundedSprite("RoundedFilled", ButtonRadius, 0);
            Sprite outline = RoundedSprite("RoundedOutline", ButtonRadius, 1);
            Sprite keycap = RoundedSprite("KeycapOutline", KeycapRadius, 1);
            Sprite shiftArrow = ShiftArrowSprite();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(root, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Transform ui = canvasObject.transform;

            // Background text.
            TextMeshProUGUI lines = UiText(ui, "BackgroundText", MenuBackgroundLines, mono, 28f, MenuBackgroundText,
                TextAlignmentOptions.TopLeft);
            lines.characterSpacing = 7.1f;
            lines.lineSpacing = 80f;
            Place(lines.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(1184f, 640f));

            // Tagline and title.
            TextMeshProUGUI tagline = UiText(ui, "Tagline", "A ONE-KEY PLATFORMER · GAME OFF 2026", mono, 15f, Muted,
                TextAlignmentOptions.Center);
            tagline.characterSpacing = 20f;
            PlaceFromTop(tagline.rectTransform, 111f, 900f, 22f);

            TextMeshProUGUI lower = UiText(ui, "TitleLowercase", "lowercase", mono, 88f, Paper, TextAlignmentOptions.Center);
            lower.characterSpacing = -2.3f;
            PlaceFromTop(lower.rectTransform, 161f, 900f, 88f);

            TextMeshProUGUI upper = UiText(ui, "TitleUppercase", "UPPERCASE", black, 132f, Player, TextAlignmentOptions.Center);
            upper.characterSpacing = -2.3f;
            PlaceFromTop(upper.rectTransform, 253f, 1100f, 132f);

            // Buttons.
            RectTransform mainPanel = MenuPanel(ui, "MainPanel");
            UnityEngine.UI.Button play = PrimaryButton(mainPanel, "PlayButton", "Play", filled, bold);
            UnityEngine.UI.Button chapters = OutlineButton(mainPanel, "ChaptersButton", "Chapters", outline, medium, out _);
            UnityEngine.UI.Button options = OutlineButton(mainPanel, "OptionsButton", "Options", outline, medium, out _);

            string[] scenes = System.Array.ConvertAll(PlayOrder, SceneName);
            RectTransform chaptersPanel = MenuPanel(ui, "ChaptersPanel");
            RectTransform chapterGrid = ChapterGrid(chaptersPanel);
            var chapterButtons = new UnityEngine.UI.Button[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                chapterButtons[i] = OutlineButton(chapterGrid, $"{scenes[i]}Button", $"{i + 1} · {scenes[i]}", outline,
                    medium, out _);
            }
            UnityEngine.UI.Button chaptersBack = OutlineButton(chaptersPanel, "BackButton", "Back", outline, medium, out _);
            chaptersPanel.gameObject.SetActive(false);

            RectTransform optionsPanel = MenuPanel(ui, "OptionsPanel");
            UnityEngine.UI.Button fullscreen = OutlineButton(optionsPanel, "FullscreenButton", "Fullscreen: off", outline,
                medium, out TextMeshProUGUI fullscreenLabel);
            UnityEngine.UI.Button sound = OutlineButton(optionsPanel, "SoundButton", "Sounds: Chip", outline,
                medium, out TextMeshProUGUI soundLabel);
            TextMeshProUGUI controls = UiText(optionsPanel, "Controls",
                "← →  move\nSpace  jump\nShift  grow\nF  shoot", mono, 15f, Muted, TextAlignmentOptions.Center);
            controls.rectTransform.sizeDelta = new Vector2(MenuColumnWidth, 88f);
            UnityEngine.UI.Button optionsBack = OutlineButton(optionsPanel, "BackButton", "Back", outline, medium, out _);
            optionsPanel.gameObject.SetActive(false);

            BuildKeyHint(ui, keycap, shiftArrow, regular);
            BuildFormMark(ui);
            TextMeshProUGUI version = BuildVersion(ui, regular);

            UnityEngine.UI.Image fade = UiImage(ui, "Fade", white, Color.black);
            fade.raycastTarget = false;
            fade.rectTransform.anchorMin = Vector2.zero;
            fade.rectTransform.anchorMax = Vector2.one;
            fade.rectTransform.offsetMin = Vector2.zero;
            fade.rectTransform.offsetMax = Vector2.zero;

            var menu = root.gameObject.AddComponent<MainMenu>();
            menu.mainPanel = mainPanel.gameObject;
            menu.chaptersPanel = chaptersPanel.gameObject;
            menu.optionsPanel = optionsPanel.gameObject;
            menu.playButton = play;
            menu.chaptersButton = chapters;
            menu.optionsButton = options;
            menu.chapterButtons = chapterButtons;
            menu.chapterScenes = scenes;
            menu.chaptersBack = chaptersBack;
            menu.fullscreenButton = fullscreen;
            menu.fullscreenLabel = fullscreenLabel;
            menu.soundButton = sound;
            menu.soundLabel = soundLabel;
            menu.optionsBack = optionsBack;
            menu.fade = fade;
            menu.version = version;

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene, MenuScene);
        }

        static void BuildKeyHint(Transform ui, Sprite keycap, Sprite shiftArrow, TMP_FontAsset regular)
        {
            const float keyWidth = 118f;
            const float keyHeight = 44f;
            var bottomLeft = new Vector2(0f, 0f);

            UnityEngine.UI.Image key = UiImage(ui, "ShiftKey", keycap, MenuOutline);
            key.type = UnityEngine.UI.Image.Type.Sliced;
            Place(key.rectTransform, bottomLeft, bottomLeft, new Vector2(48f, 40f), new Vector2(keyWidth, keyHeight));

            // The design's key has a thicker bottom edge.
            UnityEngine.UI.Image edge = UiImage(key.rectTransform, "ShiftKeyEdge", white, MenuOutline);
            Place(edge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(keyWidth - 10f, 3f));

            UnityEngine.UI.Image arrow = UiImage(key.rectTransform, "ShiftIcon", shiftArrow, Paper);
            Place(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(18f, 18f));

            TextMeshProUGUI label = UiText(key.rectTransform, "ShiftLabel", "Shift", mono, 16f, Paper, TextAlignmentOptions.Left);
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(60f, 24f));

            TextMeshProUGUI hint = UiText(ui, "ShiftHint", "hold to grow · release to shrink", regular, 15f, Muted, TextAlignmentOptions.Left);
            Place(hint.rectTransform, bottomLeft, bottomLeft, new Vector2(48f + keyWidth + 16f, 40f), new Vector2(300f, keyHeight));
        }

        static void BuildFormMark(Transform ui)
        {
            var bottomRight = new Vector2(1f, 0f);

            TextMeshProUGUI upperMark = UiText(ui, "MarkUpper", "A", black, 88f, Player, TextAlignmentOptions.BottomRight);
            Place(upperMark.rectTransform, bottomRight, bottomRight, new Vector2(-64f, 34f), new Vector2(90f, 100f));

            TextMeshProUGUI arrow = UiText(ui, "MarkArrow", "→", mono, 22f, MenuDim, TextAlignmentOptions.BottomRight);
            Place(arrow.rectTransform, bottomRight, bottomRight, new Vector2(-142f, 58f), new Vector2(30f, 30f));

            TextMeshProUGUI lowerMark = UiText(ui, "MarkLower", "a", monoBold, 40f, Player, TextAlignmentOptions.BottomRight);
            Place(lowerMark.rectTransform, bottomRight, bottomRight, new Vector2(-180f, 48f), new Vector2(30f, 50f));
        }

        /// <summary>
        /// The version and studio, small in the bottom right corner, lined up with the right edge of the A. The menu
        /// fills in the version from Player Settings when it opens; the text here is only what the editor shows.
        /// </summary>
        static TextMeshProUGUI BuildVersion(Transform ui, TMP_FontAsset font)
        {
            var bottomRight = new Vector2(1f, 0f);
            TextMeshProUGUI version = UiText(ui, "Version", $"v{PlayerSettings.bundleVersion} by dev.play.repeat", font, 13f,
                Muted, TextAlignmentOptions.BottomRight);
            Place(version.rectTransform, bottomRight, bottomRight, new Vector2(-64f, 14f), new Vector2(320f, 18f));
            return version;
        }

        // ---------------------------------------------------------------- ui pieces

        static RectTransform MenuPanel(Transform ui, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(ui, false);
            var rect = (RectTransform)go.transform;
            PlaceFromTop(rect, MenuColumnTop, MenuColumnWidth, 260f);

            var layout = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        /// <summary>
        /// One column per chapter, three levels down each, filled column by column so the levels read in play order.
        /// The panel's own layout stacks the grid and the Back button under it.
        /// </summary>
        static RectTransform ChapterGrid(RectTransform panel)
        {
            const int chapters = 5;
            const int levelsPerChapter = 3;
            const float spacing = 10f;
            const float cellWidth = 200f;
            const float cellHeight = 40f;
            float width = chapters * cellWidth + (chapters - 1) * spacing;
            float height = levelsPerChapter * cellHeight + (levelsPerChapter - 1) * spacing;

            PlaceFromTop(panel, MenuColumnTop, width, 260f);
            var stack = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.childControlWidth = false; // Keeps Back at its usual width instead of the grid's.
            stack.spacing = 16f;

            var go = new GameObject("ChapterGrid", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            var grid = go.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(cellWidth, cellHeight);
            grid.spacing = new Vector2(spacing, spacing);
            grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Vertical;
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = levelsPerChapter;
            grid.childAlignment = TextAnchor.UpperCenter;
            return rect;
        }

        static UnityEngine.UI.Button PrimaryButton(RectTransform panel, string name, string label, Sprite sprite, TMP_FontAsset font)
        {
            UnityEngine.UI.Button button = MenuButton(panel, name, label, sprite, font, 20f, 56f, Background, out _);
            SetButtonColors(button, Player, PlayerLight, Paper);

            // It starts selected, and the design shows it in the plain accent colour.
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.selectedColor = Player;
            button.colors = colors;
            return button;
        }

        static UnityEngine.UI.Button OutlineButton(RectTransform panel, string name, string label, Sprite sprite,
            TMP_FontAsset font, out TextMeshProUGUI text)
        {
            UnityEngine.UI.Button button = MenuButton(panel, name, label, sprite, font, 17f, 48f, Paper, out text);
            SetButtonColors(button, MenuOutline, Player, Paper);
            return button;
        }

        static UnityEngine.UI.Button MenuButton(RectTransform panel, string name, string label, Sprite sprite, TMP_FontAsset font,
            float fontPx, float height, Color textColor, out TextMeshProUGUI text)
        {
            UnityEngine.UI.Image image = UiImage(panel, name, sprite, Color.white);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.raycastTarget = true;
            image.rectTransform.sizeDelta = new Vector2(MenuColumnWidth, height);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            text = UiText(image.rectTransform, "Label", label, font, fontPx, textColor, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        static void SetButtonColors(UnityEngine.UI.Button button, Color normal, Color active, Color pressed)
        {
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = active;
            colors.selectedColor = active;
            colors.pressedColor = pressed;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        static UnityEngine.UI.Image UiImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI UiText(Transform parent, string name, string content, TMP_FontAsset font, float sizePx,
            Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = sizePx;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Horizontally centred, at a CSS top in the board, keeping the column together on any aspect ratio.</summary>
        static void PlaceFromTop(RectTransform rect, float topPx, float width, float height)
        {
            var centre = new Vector2(0.5f, 0.5f);
            Place(rect, centre, new Vector2(0.5f, 1f), new Vector2(0f, 360f - topPx), new Vector2(width, height));
        }

        // ---------------------------------------------------------------- sprites

        static Sprite RoundedSprite(string assetName, int radius, int border)
        {
            int size = (radius * 2 + 4) * SpriteScale;
            float r = radius * SpriteScale;
            float half = size * 0.5f;
            float line = border * SpriteScale;

            return SaveSprite(assetName, size, (x, y) =>
            {
                float dx = Mathf.Max(Mathf.Abs(x - half) - (half - r), 0f);
                float dy = Mathf.Max(Mathf.Abs(y - half) - (half - r), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy) - r;
                float outer = Mathf.Clamp01(0.5f - distance);
                return border > 0 ? outer * Mathf.Clamp01(0.5f + distance + line) : outer;
            }, (radius + 1) * SpriteScale);
        }

        /// <summary>The design's outlined Shift arrow: Space Mono has no such glyph.</summary>
        static Sprite ShiftArrowSprite()
        {
            const int scale = 4;
            Vector2[] points =
            {
                new Vector2(12f, 3f), new Vector2(3f, 12f), new Vector2(8f, 12f), new Vector2(8f, 20f),
                new Vector2(16f, 20f), new Vector2(16f, 12f), new Vector2(21f, 12f)
            };

            return SaveSprite("ShiftArrow", 24 * scale, (x, y) =>
            {
                var p = new Vector2(x / scale, 24f - y / scale);
                float nearest = float.MaxValue;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[(i + 1) % points.Length];
                    float along = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                    nearest = Mathf.Min(nearest, Vector2.Distance(p, a + (b - a) * along));
                }
                return Mathf.Clamp01(0.5f - (nearest - 1f) * scale);
            }, 0);
        }

        static Sprite SaveSprite(string assetName, int size, System.Func<float, float, float> alpha, int sliceBorder)
        {
            string path = $"{ArtFolder}/{assetName}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha(x + 0.5f, y + 0.5f) * 255f));
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f * SpriteScale;
            importer.spriteBorder = Vector4.one * sliceBorder;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
