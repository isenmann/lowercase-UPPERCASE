#if UNITY_EDITOR
using TMPro;
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// The menu's My levels panel: a heading, a scrolling list of rows copied from an inactive template (the level's
    /// name, which plays it, then Edit and Delete), a line for when there are none, and New level and Back.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const float MyLevelsListWidth = 640f;
        const float MyLevelsRowHeight = 44f;
        const float MyLevelsRowSpacing = 8f;
        const int MyLevelsRowsShown = 7;

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

            var buttonsObject = new GameObject("ButtonRow", typeof(RectTransform));
            buttonsObject.transform.SetParent(column, false);
            var buttons = (RectTransform)buttonsObject.transform;
            float buttonsWidth = MenuColumnWidth * 2f + 12f;
            buttons.sizeDelta = new Vector2(buttonsWidth, ResolutionBackHeight);
            Fixed(buttonsObject, buttonsWidth, ResolutionBackHeight);
            // Its own layout expands its buttons, which would otherwise make the whole row stretch down the column.
            buttonsObject.GetComponent<UnityEngine.UI.LayoutElement>().flexibleHeight = 0f;
            var buttonsLayout = buttonsObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            buttonsLayout.spacing = 12f;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;
            buttonsLayout.childForceExpandHeight = true;
            myLevels.newButton = OutlineButton(buttons, "NewButton", "New level", outline, medium, out _);
            myLevels.backButton = OutlineButton(buttons, "BackButton", "Back", outline, medium, out _);

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
            UnityEngine.UI.Button delete = OutlineButton(row, "DeleteButton", "Delete", outline, medium, out _);
            delete.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 120f;

            rowObject.SetActive(false);
            return rowObject;
        }
    }
}
#endif
