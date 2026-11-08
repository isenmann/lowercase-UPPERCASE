#if UNITY_EDITOR
using TMPro;
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// The menu's "How to play" popup: two pages taken from the design canvas's "The two forms" and "Level vocabulary"
    /// boards, rewritten to match what the game does now. Positions are CSS pixels from the top left of a 1280x720 board.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        static readonly Color GuideCard = new Color32(0x18, 0x1B, 0x22, 0xFF);
        static readonly Color GuideBody = new Color32(0xD8, 0xD6, 0xD0, 0xFF);
        static readonly Color GuideRule = new Color32(0xB8, 0xBC, 0xC6, 0xFF);
        static readonly Color GuideOrange = new Color32(0xFF, 0x8A, 0x3D, 0xFF);

        struct GuideTile
        {
            public string glyph, tag, name, rule;
            public Color color;

            public GuideTile(string glyph, Color color, string tag, string name, string rule)
            {
                this.glyph = glyph;
                this.color = color;
                this.tag = tag;
                this.name = name;
                this.rule = rule;
            }
        }

        static GuideTile[] VocabularyTiles() => new[]
        {
            new GuideTile(".", Interactive, "A", "Period",
                "A pressure plate. Only UPPERCASE is heavy enough to hold it down and open the door."),
            new GuideTile("!", Interactive, "both", "Exclamation",
                "A spring. Launches anyone; lowercase flies twice as high."),
            new GuideTile(",", Interactive, "a", "Comma",
                "A hook to hang from. Jump to let go. Breaks under a capital."),
            new GuideTile("a-b", Interactive, "a", "Hyphen bridge",
                "Joins two words over a gap. Snaps under UPPERCASE."),
            new GuideTile("ice", Interactive, "both", "Literal words",
                "Words do what they say: ice slides, up lifts, sticky holds you to walls, hot burns."),
            new GuideTile("brick", Breakable, "A", "Cracked word",
                "A wall made of a word. Run into it as a capital to smash through."),
            new GuideTile("g a p", Paper, "a", "Kerning gap",
                "Loosely spaced letters leave holes only a small letter fits through."),
            new GuideTile("LOCK", GuideOrange, "both", "Caps Lock zone",
                "Shift is inverted inside: you are A unless you hold it. Find the key to switch it off."),
            new GuideTile(";", Muted, "both", "Checkpoint",
                "Pass it, and dying brings you back here instead of to the start."),
            new GuideTile("[<color=#FFD23F>*</color>]", Interactive, "both", "Power block",
                "Hit it from below to get asterisks, then Ctrl shoots them. Dying loses them."),
            new GuideTile("&", Danger, "both", "Enemy",
                "Touching one costs a life. Shoot it, or land on it as UPPERCASE."),
            new GuideTile("#", Player, "both", "Hash",
                "Worth 50 points. Each one can be taken once per level."),
        };

        /// <summary>
        /// A full-screen panel that covers the title: one page per tab, and the tabs and Back in a row at the bottom.
        /// Selecting a tab shows its page, so arrow keys and gamepads flip through the pages.
        /// </summary>
        static RectTransform BuildGuide(Transform ui, Sprite filled, Sprite outline, Sprite keycap, Sprite shiftArrow,
            TMP_FontAsset regular, TMP_FontAsset medium, TMP_FontAsset bold,
            out UnityEngine.UI.Button[] tabs, out TextMeshProUGUI[] tabLabels, out GameObject[] pages,
            out UnityEngine.UI.Button back)
        {
            UnityEngine.UI.Image cover = UiImage(ui, "GuidePanel", white, Background);
            cover.raycastTarget = true; // Keeps clicks off the hidden menu behind it.
            RectTransform panel = cover.rectTransform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            // The pages keep the board's size and stay centred on wider screens.
            var boardObject = new GameObject("Board", typeof(RectTransform));
            boardObject.transform.SetParent(panel, false);
            var board = (RectTransform)boardObject.transform;
            Place(board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280f, 720f));

            pages = new[]
            {
                FormsPage(board, filled, outline, keycap, shiftArrow, regular, bold).gameObject,
                VocabularyPage(board, filled, outline, keycap, regular, bold).gameObject
            };

            var rowObject = new GameObject("GuideTabs", typeof(RectTransform));
            rowObject.transform.SetParent(board, false);
            var row = (RectTransform)rowObject.transform;
            Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(3 * 210f + 2 * 12f, 44f));
            var layout = rowObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            tabs = new UnityEngine.UI.Button[2];
            tabLabels = new TextMeshProUGUI[2];
            tabs[0] = OutlineButton(row, "FormsTab", "The two forms", outline, medium, out tabLabels[0]);
            tabs[1] = OutlineButton(row, "VocabularyTab", "Level vocabulary", outline, medium, out tabLabels[1]);
            back = OutlineButton(row, "BackButton", "Back", outline, medium, out _);

            panel.gameObject.SetActive(false);
            return panel;
        }

        // ---------------------------------------------------------------- the two forms

        static RectTransform FormsPage(RectTransform board, Sprite filled, Sprite outline, Sprite keycap, Sprite shiftArrow,
            TMP_FontAsset regular, TMP_FontAsset bold)
        {
            RectTransform page = GuidePage(board, "FormsPage", "The two forms",
                "One key. Every puzzle is a question of which form, and when.", regular, bold);

            const float cardWidth = (1168f - 24f) / 2f;
            FormCard(page, 56f, cardWidth, "a", monoBold, 120f, "lowercase", mono, "default · Shift released",
                new[] { 1, 1, 5, 5 },
                "Fast and light, with a high, floaty jump\n" +
                "Slips through the gaps in spaced-out words\n" +
                "Hangs from commas and crosses hyphens\n" +
                "Springs launch it twice as high\n" +
                "Too light to hold a period down or break a word",
                filled, outline, regular);
            FormCard(page, 56f + cardWidth + 24f, cardWidth, "A", black, 130f, "UPPERCASE", black, "while Shift is held",
                new[] { 5, 5, 2, 1 },
                "Heavy and slow, with a short hop\n" +
                "Smashes cracked words, presses periods\n" +
                "Squashes an enemy by landing on it\n" +
                "Shoots bigger asterisks\n" +
                "Snaps hyphen bridges and breaks commas",
                filled, outline, regular);

            RectTransform note = Strip(page, "ShiftNote", 562f, 52f, outline, MenuOutline);
            UnityEngine.UI.Image key = UiImage(note, "ShiftKey", keycap, MenuOutline);
            key.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(key.rectTransform, 20f, 10f, 92f, 32f);
            UnityEngine.UI.Image arrow = UiImage(key.rectTransform, "ShiftIcon", shiftArrow, Paper);
            Place(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(15f, 15f));
            TextMeshProUGUI keyLabel = UiText(key.rectTransform, "ShiftLabel", "Shift", mono, 13f, Paper, TextAlignmentOptions.Left);
            Place(keyLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(50f, 20f));

            TextMeshProUGUI text = UiText(note, "Text",
                "Hold to grow, release to shrink. Growing needs headroom: under a low ceiling, Shift waits until there is room.",
                regular, 16f, GuideBody, TextAlignmentOptions.Left);
            PlaceTopLeft(text.rectTransform, 128f, 0f, 1168f - 148f, 52f);
            return page;
        }

        static void FormCard(RectTransform page, float x, float width, string glyph, TMP_FontAsset glyphFont, float glyphPx,
            string name, TMP_FontAsset nameFont, string when, int[] stats, string traits, Sprite filled, Sprite outline,
            TMP_FontAsset regular)
        {
            RectTransform card = Card(page, $"Card {name}", x, 104f, width, 442f, filled, outline);

            TextMeshProUGUI big = UiText(card, "Glyph", glyph, glyphFont, glyphPx, Player, TextAlignmentOptions.BottomLeft);
            // Space Mono leaves room for descenders under the a, so it sits lower than the A to share a baseline.
            PlaceTopLeft(big.rectTransform, 32f, glyphFont == black ? 20f : 44f, 110f, 130f);

            TextMeshProUGUI title = UiText(card, "Name", name, nameFont, 26f, Paper, TextAlignmentOptions.BottomLeft);
            PlaceTopLeft(title.rectTransform, 162f, 84f, 300f, 36f);
            TextMeshProUGUI subtitle = UiText(card, "When", when, regular, 14f, Muted, TextAlignmentOptions.TopLeft);
            PlaceTopLeft(subtitle.rectTransform, 162f, 124f, 300f, 20f);

            string[] labels = { "Size", "Weight", "Speed", "Jump" };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 170f + i * 24f;
                TextMeshProUGUI label = UiText(card, labels[i], labels[i], regular, 14f, Muted, TextAlignmentOptions.Left);
                PlaceTopLeft(label.rectTransform, 32f, y, 72f, 14f);
                for (int s = 0; s < 5; s++)
                {
                    UnityEngine.UI.Image segment = UiImage(card, $"{labels[i]} {s + 1}", white, s < stats[i] ? Player : BarLine);
                    PlaceTopLeft(segment.rectTransform, 120f + s * 46f, y + 2f, 40f, 10f);
                }
            }

            TextMeshProUGUI list = UiText(card, "Traits", Bullets(traits), regular, 16f, GuideBody, TextAlignmentOptions.TopLeft);
            list.lineSpacing = 20f;
            PlaceTopLeft(list.rectTransform, 32f, 282f, width - 64f, 140f);
        }

        /// <summary>One bullet per line, with wrapped lines indented under the text.</summary>
        static string Bullets(string lines)
        {
            var result = new System.Text.StringBuilder();
            foreach (string line in lines.Split('\n'))
            {
                if (result.Length > 0) result.Append('\n');
                result.Append("•<indent=20>").Append(line).Append("</indent>");
            }
            return result.ToString();
        }

        // ---------------------------------------------------------------- level vocabulary

        static RectTransform VocabularyPage(RectTransform board, Sprite filled, Sprite outline, Sprite keycap,
            TMP_FontAsset regular, TMP_FontAsset bold)
        {
            RectTransform page = GuidePage(board, "VocabularyPage", "Level vocabulary",
                "The world is text. Punctuation is machinery, and words do what they say.", regular, bold);

            const int columns = 4;
            const float gap = 12f;
            const float tileWidth = (1168f - (columns - 1) * gap) / columns;
            const float tileHeight = 146f;
            GuideTile[] tiles = VocabularyTiles();
            for (int i = 0; i < tiles.Length; i++)
            {
                float x = 56f + (i % columns) * (tileWidth + gap);
                float y = 100f + (i / columns) * (tileHeight + gap);
                VocabularyTile(page, tiles[i], x, y, tileWidth, tileHeight, filled, outline, keycap, regular, bold);
            }

            RectTransform goal = Strip(page, "Goal", 576f, 52f, filled, GuideCard);
            UnityEngine.UI.Image goalLine = UiImage(goal, "Outline", outline, BarLine);
            goalLine.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(goalLine.rectTransform);

            TextMeshProUGUI c = UiText(goal, "C", "c", mono, 26f, Paper, TextAlignmentOptions.Center);
            PlaceTopLeft(c.rectTransform, 20f, 6f, 18f, 40f);
            UnityEngine.UI.Image blank = UiImage(goal, "Blank", keycap, Player);
            blank.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(blank.rectTransform, 40f, 15f, 18f, 24f);
            TextMeshProUGUI t = UiText(goal, "T", "t", mono, 26f, Paper, TextAlignmentOptions.Center);
            PlaceTopLeft(t.rectTransform, 60f, 6f, 18f, 40f);

            TextMeshProUGUI text = UiText(goal, "Text",
                "Goal: every level is a sentence missing one letter: you. Reach the blank in the form it wants to slot in. " +
                "Some blanks want <color=#FFD23F>a</color>, some want <color=#FFD23F><b>A</b></color>.",
                regular, 16f, GuideBody, TextAlignmentOptions.Left);
            PlaceTopLeft(text.rectTransform, 100f, 0f, 1168f - 120f, 52f);
            return page;
        }

        static void VocabularyTile(RectTransform page, GuideTile tile, float x, float y, float width, float height,
            Sprite filled, Sprite outline, Sprite keycap, TMP_FontAsset regular, TMP_FontAsset bold)
        {
            RectTransform card = Card(page, $"Tile {tile.name}", x, y, width, height, filled, outline);

            TextMeshProUGUI glyph = UiText(card, "Glyph", tile.glyph, monoBold, 34f, tile.color, TextAlignmentOptions.TopLeft);
            PlaceTopLeft(glyph.rectTransform, 16f, 10f, width - 90f, 44f);

            Color tagColor = tile.tag == "both" ? Muted : Player;
            float tagWidth = tile.tag == "both" ? 48f : 28f;
            UnityEngine.UI.Image pill = UiImage(card, "Tag", keycap, tagColor);
            pill.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(pill.rectTransform, width - 16f - tagWidth, 16f, tagWidth, 22f);
            TextMeshProUGUI tag = UiText(pill.rectTransform, "Label", tile.tag, mono, 12f, tagColor, TextAlignmentOptions.Center);
            Stretch(tag.rectTransform);

            TextMeshProUGUI name = UiText(card, "Name", tile.name, bold, 17f, Paper, TextAlignmentOptions.TopLeft);
            PlaceTopLeft(name.rectTransform, 16f, 60f, width - 32f, 22f);

            TextMeshProUGUI rule = UiText(card, "Rule", tile.rule, regular, 13f, GuideRule, TextAlignmentOptions.TopLeft);
            rule.textWrappingMode = TextWrappingModes.Normal;
            rule.lineSpacing = 4f;
            PlaceTopLeft(rule.rectTransform, 16f, 86f, width - 32f, height - 96f);
        }

        // ---------------------------------------------------------------- pieces

        static RectTransform GuidePage(RectTransform board, string name, string title, string subtitle,
            TMP_FontAsset regular, TMP_FontAsset bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(board, false);
            var page = (RectTransform)go.transform;
            Stretch(page);

            TextMeshProUGUI heading = UiText(page, "Title", title, bold, 36f, Paper, TextAlignmentOptions.BottomLeft);
            heading.characterSpacing = -1.5f;
            PlaceTopLeft(heading.rectTransform, 56f, 36f, 500f, 46f);
            TextMeshProUGUI line = UiText(page, "Subtitle", subtitle, regular, 16f, Muted, TextAlignmentOptions.BottomRight);
            PlaceTopLeft(line.rectTransform, 1224f - 700f, 36f, 700f, 40f);
            return page;
        }

        static RectTransform Card(RectTransform parent, string name, float x, float y, float width, float height,
            Sprite filled, Sprite outline)
        {
            UnityEngine.UI.Image fill = UiImage(parent, name, filled, GuideCard);
            fill.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(fill.rectTransform, x, y, width, height);
            UnityEngine.UI.Image line = UiImage(fill.rectTransform, "Outline", outline, BarLine);
            line.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(line.rectTransform);
            return fill.rectTransform;
        }

        /// <summary>A full-width band under the page's main content.</summary>
        static RectTransform Strip(RectTransform page, string name, float y, float height, Sprite sprite, Color color)
        {
            UnityEngine.UI.Image strip = UiImage(page, name, sprite, color);
            strip.type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTopLeft(strip.rectTransform, 56f, y, 1168f, height);
            return strip.rectTransform;
        }

        static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            var topLeft = new Vector2(0f, 1f);
            Place(rect, topLeft, topLeft, new Vector2(x, -y), new Vector2(width, height));
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
