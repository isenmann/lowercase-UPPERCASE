#if UNITY_EDITOR
using TMPro;
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// The menu's My levels panel: a heading, a picture of the current row's level, a scrolling list of rows copied from an inactive template (the level's
    /// name, which plays it, then Edit, Share and Delete), a line for when there are none, and New level, Import and
    /// Back. Share and Import open over it, each with a box for the level's code.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const float MyLevelsListWidth = 760f;
        const float CodeBoxHeight = 132f;
        const float ShareStatusHeight = 48f;
        const float MyLevelsRowHeight = 44f;
        const float MyLevelsRowSpacing = 8f;
        const int MyLevelsRowsShown = 5;
        const float PreviewHeight = 720f * LevelPreviewStrip.Scale;

        static RectTransform BuildMyLevels(Transform ui, MainMenu menu, Sprite outline, TMP_FontAsset medium,
            TMP_FontAsset regular)
        {
            float listHeight = MyLevelsRowsShown * MyLevelsRowHeight + (MyLevelsRowsShown - 1) * MyLevelsRowSpacing;

            UnityEngine.UI.Image cover = UiImage(ui, "MyLevelsPanel", white, Background);
            cover.raycastTarget = true; // Keeps clicks off the hidden menu behind it.
            RectTransform panel = cover.rectTransform;
            Stretch(panel);
            var myLevels = panel.gameObject.AddComponent<MyLevelsMenu>();
            myLevels.menu = menu;

            var columnObject = new GameObject("Column", typeof(RectTransform));
            columnObject.transform.SetParent(panel, false);
            var column = (RectTransform)columnObject.transform;
            Stretch(column);
            var stack = columnObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.spacing = ResolutionStackSpacing;
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = false;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = false;
            stack.childForceExpandHeight = false;

            TextMeshProUGUI heading = UiText(column, "Heading", "My levels", medium, 28f, Paper, TextAlignmentOptions.Center);
            heading.rectTransform.sizeDelta = new Vector2(MenuColumnWidth, ResolutionHeadingHeight);
            Fixed(heading.gameObject, MenuColumnWidth, ResolutionHeadingHeight);

            myLevels.preview = BuildPreviewStrip(column, outline, regular);

            var listObject = new GameObject("LevelList", typeof(RectTransform));
            listObject.transform.SetParent(column, false);
            var list = (RectTransform)listObject.transform;
            list.sizeDelta = new Vector2(MyLevelsListWidth, listHeight);
            Fixed(listObject, MyLevelsListWidth, listHeight);

            UnityEngine.UI.Image viewportImage = UiImage(list, "Viewport", white, Color.clear);
            viewportImage.raycastTarget = true; // So the wheel works between the rows too.
            RectTransform viewport = viewportImage.rectTransform;
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

            var contentObject = new GameObject("Rows", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            var content = (RectTransform)contentObject.transform;
            var top = new Vector2(0.5f, 1f);
            Place(content, top, top, Vector2.zero, new Vector2(MyLevelsListWidth, MyLevelsRowHeight));
            var rowsLayout = contentObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            rowsLayout.spacing = MyLevelsRowSpacing;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = false;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            var fitter = contentObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            myLevels.rowTemplate = BuildMyLevelsRow(content, outline, medium);

            var scroll = listObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = MyLevelsRowHeight + MyLevelsRowSpacing; // One row per wheel notch.
            myLevels.scroll = scroll;

            TextMeshProUGUI empty = UiText(column, "Empty", "No levels yet. New level starts one.", regular, 17f, Muted,
                TextAlignmentOptions.Center);
            empty.rectTransform.sizeDelta = new Vector2(MyLevelsListWidth, listHeight);
            Fixed(empty.gameObject, MyLevelsListWidth, listHeight);
            myLevels.emptyText = empty;

            RectTransform buttons = OverlayButtonRow(column);
            myLevels.newButton = OutlineButton(buttons, "NewButton", "New level", outline, medium, out _);
            myLevels.importButton = OutlineButton(buttons, "ImportButton", "Import", outline, medium, out _);
            myLevels.backButton = OutlineButton(buttons, "BackButton", "Back", outline, medium, out _);

            BuildSharePanel(panel, myLevels, outline, medium, regular);
            BuildImportPanel(panel, myLevels, outline, medium, regular);

            menu.myLevelsPanel = panel.gameObject;
            menu.myLevels = myLevels;
            panel.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>One level's row: its name, which plays it, then Edit and Delete. Inactive; the menu copies it.</summary>
        static GameObject BuildMyLevelsRow(RectTransform content, Sprite outline, TMP_FontAsset medium)
        {
            var rowObject = new GameObject("LevelRow", typeof(RectTransform));
            rowObject.transform.SetParent(content, false);
            var row = (RectTransform)rowObject.transform;
            row.sizeDelta = new Vector2(MyLevelsListWidth, MyLevelsRowHeight);
            var layout = rowObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            UnityEngine.UI.Button play = OutlineButton(row, "PlayButton", "level name", outline, medium,
                out TextMeshProUGUI name);
            name.alignment = TextAlignmentOptions.Left;
            name.margin = new Vector4(16f, 0f, 16f, 0f);
            name.overflowMode = TextOverflowModes.Ellipsis;
            var playSize = play.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            playSize.flexibleWidth = 1f;
            playSize.minWidth = 200f;

            UnityEngine.UI.Button edit = OutlineButton(row, "EditButton", "Edit", outline, medium, out _);
            edit.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 110f;
            UnityEngine.UI.Button share = OutlineButton(row, "ShareButton", "Share", outline, medium, out _);
            share.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 110f;
            UnityEngine.UI.Button delete = OutlineButton(row, "DeleteButton", "Delete", outline, medium, out _);
            delete.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 120f;

            foreach (UnityEngine.UI.Button button in new[] { play, edit, share, delete })
                button.gameObject.AddComponent<SelectOnHover>();

            rowObject.SetActive(false);
            return rowObject;
        }

        /// <summary>
        /// A window as wide as the list onto a picture of the whole level, scrolled sideways by the wheel or a drag, with
        /// a line on top of it for a level that can't be drawn.
        /// </summary>
        static LevelPreviewStrip BuildPreviewStrip(RectTransform column, Sprite outline, TMP_FontAsset regular)
        {
            var stripObject = new GameObject("Preview", typeof(RectTransform));
            stripObject.transform.SetParent(column, false);
            var strip = (RectTransform)stripObject.transform;
            strip.sizeDelta = new Vector2(MyLevelsListWidth, PreviewHeight);
            Fixed(stripObject, MyLevelsListWidth, PreviewHeight);

            UnityEngine.UI.Image viewportImage = UiImage(strip, "Viewport", white, Background);
            viewportImage.raycastTarget = true; // So the wheel and drags work on the picture.
            RectTransform viewport = viewportImage.rectTransform;
            Stretch(viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

            UnityEngine.UI.RawImage picture = new GameObject("Picture", typeof(RectTransform))
                .AddComponent<UnityEngine.UI.RawImage>();
            picture.transform.SetParent(viewport, false);
            picture.raycastTarget = false;
            picture.enabled = false;
            var left = new Vector2(0f, 0.5f);
            Place(picture.rectTransform, left, left, Vector2.zero, new Vector2(MyLevelsListWidth, PreviewHeight));

            UnityEngine.UI.Image frame = UiImage(strip, "Frame", outline, MenuOutline);
            frame.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(frame.rectTransform);

            TextMeshProUGUI label = UiText(strip, "Label", "", regular, 17f, Muted, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            var scroll = stripObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = picture.rectTransform;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40f;

            var preview = stripObject.AddComponent<LevelPreviewStrip>();
            preview.scroll = scroll;
            preview.image = picture;
            preview.label = label;
            preview.pageColor = Background;
            preview.mono = mono;
            preview.monoBold = monoBold;
            preview.black = black;
            preview.white = white;
            return preview;
        }

        /// <summary>Share, over the list: a heading, whether the code was copied and the maker finished the level, the
        /// code in a box to select from, and Copy, Shared levels and Back.</summary>
        static void BuildSharePanel(RectTransform panel, MyLevelsMenu myLevels, Sprite outline, TMP_FontAsset medium,
            TMP_FontAsset regular)
        {
            RectTransform column = OverlayColumn(panel, "SharePanel", out GameObject cover);
            myLevels.sharePanel = cover;
            myLevels.shareHeading = OverlayHeading(column, "Share", medium);
            myLevels.shareStatus = OverlayStatus(column, regular);
            myLevels.shareCode = CodeBox(column, "Code", outline, "");
            myLevels.shareCode.readOnly = true;
            RectTransform buttons = OverlayButtonRow(column);
            myLevels.shareCopy = OutlineButton(buttons, "CopyButton", "Copy again", outline, medium, out _);
            myLevels.shareSite = OutlineButton(buttons, "SharedLevelsButton", "Shared levels", outline, medium, out _);
            myLevels.shareBack = OutlineButton(buttons, "BackButton", "Back", outline, medium, out _);
        }

        /// <summary>Import, over the list: a heading, a box to paste a code into, what went wrong, and Paste, Import,
        /// Shared levels and Back.</summary>
        static void BuildImportPanel(RectTransform panel, MyLevelsMenu myLevels, Sprite outline, TMP_FontAsset medium,
            TMP_FontAsset regular)
        {
            RectTransform column = OverlayColumn(panel, "ImportPanel", out GameObject cover);
            myLevels.importPanel = cover;
            OverlayHeading(column, "Import a level", medium);
            myLevels.importStatus = OverlayStatus(column, regular);
            myLevels.importCode = CodeBox(column, "Code", outline, "luc1:...");
            RectTransform buttons = OverlayButtonRow(column);
            myLevels.importPaste = OutlineButton(buttons, "PasteButton", "Paste", outline, medium, out _);
            myLevels.importConfirm = OutlineButton(buttons, "ImportButton", "Import", outline, medium, out _);
            myLevels.importSite = OutlineButton(buttons, "SharedLevelsButton", "Shared levels", outline, medium, out _);
            myLevels.importBack = OutlineButton(buttons, "BackButton", "Back", outline, medium, out _);
        }

        static RectTransform OverlayColumn(RectTransform panel, string name, out GameObject cover)
        {
            UnityEngine.UI.Image image = UiImage(panel, name, white, Background);
            image.raycastTarget = true; // Keeps clicks off the list behind it.
            Stretch(image.rectTransform);
            cover = image.gameObject;

            var columnObject = new GameObject("Column", typeof(RectTransform));
            columnObject.transform.SetParent(image.rectTransform, false);
            var column = (RectTransform)columnObject.transform;
            Stretch(column);
            var stack = columnObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.spacing = ResolutionStackSpacing;
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = false;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = false;
            stack.childForceExpandHeight = false;
            return column;
        }

        static TextMeshProUGUI OverlayHeading(RectTransform column, string text, TMP_FontAsset medium)
        {
            TextMeshProUGUI heading = UiText(column, "Heading", text, medium, 28f, Paper, TextAlignmentOptions.Center);
            heading.rectTransform.sizeDelta = new Vector2(MyLevelsListWidth, ResolutionHeadingHeight);
            heading.overflowMode = TextOverflowModes.Ellipsis;
            Fixed(heading.gameObject, MyLevelsListWidth, ResolutionHeadingHeight);
            return heading;
        }

        static TextMeshProUGUI OverlayStatus(RectTransform column, TMP_FontAsset regular)
        {
            TextMeshProUGUI status = UiText(column, "Status", "", regular, 17f, Muted, TextAlignmentOptions.Center);
            status.textWrappingMode = TextWrappingModes.Normal;
            status.rectTransform.sizeDelta = new Vector2(MyLevelsListWidth, ShareStatusHeight);
            Fixed(status.gameObject, MyLevelsListWidth, ShareStatusHeight);
            return status;
        }

        static RectTransform OverlayButtonRow(RectTransform column)
        {
            var buttonsObject = new GameObject("ButtonRow", typeof(RectTransform));
            buttonsObject.transform.SetParent(column, false);
            var buttons = (RectTransform)buttonsObject.transform;
            buttons.sizeDelta = new Vector2(MyLevelsListWidth, ResolutionBackHeight);
            Fixed(buttonsObject, MyLevelsListWidth, ResolutionBackHeight);
            // Its own layout expands its buttons, which would otherwise make the whole row stretch down the column.
            buttonsObject.GetComponent<UnityEngine.UI.LayoutElement>().flexibleHeight = 0f;
            var layout = buttonsObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return buttons;
        }

        /// <summary>An outlined box of mono text that wraps, for a level's code; Enter in it doesn't add lines.</summary>
        static TMP_InputField CodeBox(RectTransform column, string name, Sprite outline, string placeholder)
        {
            UnityEngine.UI.Image box = UiImage(column, name, outline, Color.white);
            box.type = UnityEngine.UI.Image.Type.Sliced;
            box.raycastTarget = true;
            box.rectTransform.sizeDelta = new Vector2(MyLevelsListWidth, CodeBoxHeight);
            Fixed(box.gameObject, MyLevelsListWidth, CodeBoxHeight);

            var areaObject = new GameObject("Text Area", typeof(RectTransform));
            areaObject.transform.SetParent(box.rectTransform, false);
            var area = (RectTransform)areaObject.transform;
            Stretch(area);
            area.offsetMin = new Vector2(14f, 10f);
            area.offsetMax = new Vector2(-14f, -10f);
            areaObject.AddComponent<UnityEngine.UI.RectMask2D>();

            TextMeshProUGUI text = UiText(area, "Text", "", mono, 14f, Paper, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            Stretch(text.rectTransform);
            TextMeshProUGUI hint = UiText(area, "Placeholder", placeholder, mono, 14f, Muted, TextAlignmentOptions.TopLeft);
            Stretch(hint.rectTransform);

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.fontAsset = mono;
            field.pointSize = 14f;
            field.lineType = TMP_InputField.LineType.MultiLineSubmit;
            field.targetGraphic = box;
            SetSelectableColors(field, MenuOutline, Player, Paper);
            return field;
        }
    }
}
#endif
