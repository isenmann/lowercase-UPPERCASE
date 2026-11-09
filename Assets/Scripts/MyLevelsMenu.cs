using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LowercaseUppercase
{
    /// <summary>
    /// The menu's My levels panel: one row per level made in the editor, with Play (the level's name), Edit and Delete,
    /// and New level and Back under the list. <see cref="MainMenu"/> opens and closes it; this fills it in.
    /// Delete asks for a second press, like Reset progress.
    /// </summary>
    public class MyLevelsMenu : MonoBehaviour
    {
        public MainMenu menu;
        [Tooltip("Inactive row inside the list, copied once per level: a name button that plays it, Edit and Delete.")]
        public GameObject rowTemplate;
        public ScrollRect scroll;
        [Tooltip("Shown instead of the list when there are no levels yet.")]
        public TMP_Text emptyText;
        public Button newButton;
        public Button backButton;
        public string deleteText = "Delete";
        public string deleteConfirmText = "Really?";

        const float UnreadableAlpha = 0.4f;

        readonly List<Row> rows = new List<Row>();
        Button armedDelete;
        GameObject shown;

        sealed class Row
        {
            public StoredLevel Level;
            public Button Play;
            public Button Edit;
            public Button Delete;
            public TMP_Text DeleteLabel;
        }

        public IReadOnlyList<StoredLevel> Levels
        {
            get
            {
                var levels = new List<StoredLevel>();
                foreach (Row row in rows) levels.Add(row.Level);
                return levels;
            }
        }

        void Awake()
        {
            newButton.onClick.AddListener(NewLevel);
        }

        /// <summary>
        /// Lists the levels again, and returns the button to start on: the Play of the row at focusRow (or the last
        /// row, if there are fewer), or New level when there are none.
        /// </summary>
        public Button Refresh(int focusRow = 0)
        {
            Transform content = scroll.content;
            foreach (Transform child in content)
            {
                if (child == rowTemplate.transform) continue;
                child.gameObject.SetActive(false); // Destroy waits for the frame's end; this keeps it out of the layout now.
                Destroy(child.gameObject);
            }
            rows.Clear();
            armedDelete = null;

            foreach (StoredLevel level in LevelStore.List()) rows.Add(MakeRow(content, level));

            bool any = rows.Count > 0;
            scroll.gameObject.SetActive(any);
            emptyText.gameObject.SetActive(!any);
            content.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);

            if (!any) return newButton;
            Row focus = rows[Mathf.Clamp(focusRow, 0, rows.Count - 1)];
            return focus.Play.interactable ? focus.Play : focus.Delete;
        }

        Row MakeRow(Transform content, StoredLevel level)
        {
            GameObject go = Instantiate(rowTemplate, content);
            go.name = $"Level {level.Name}";
            go.SetActive(true);
            var row = new Row
            {
                Level = level,
                Play = go.transform.Find("PlayButton").GetComponent<Button>(),
                Edit = go.transform.Find("EditButton").GetComponent<Button>(),
                Delete = go.transform.Find("DeleteButton").GetComponent<Button>(),
            };
            row.DeleteLabel = row.Delete.GetComponentInChildren<TMP_Text>();
            row.DeleteLabel.text = deleteText;

            TMP_Text name = row.Play.GetComponentInChildren<TMP_Text>();
            bool readable = level.Level != null;
            name.text = readable ? level.Name : $"can't read {level.Name}";
            row.Play.interactable = readable;
            row.Edit.interactable = readable;
            if (!readable)
            {
                name.alpha = UnreadableAlpha;
                row.Edit.GetComponentInChildren<TMP_Text>().alpha = UnreadableAlpha;
            }

            row.Play.onClick.AddListener(() => Play(level));
            row.Edit.onClick.AddListener(() => Edit(level));
            row.Delete.onClick.AddListener(() => Delete(row));
            return row;
        }

        public void Play(StoredLevel level)
        {
            if (level.Level == null) return;
            LevelRun.NewGame();
            CustomRun.Level = level.Level;
            CustomRun.EditPath = null;
            CustomRun.ReturnToMyLevels = true;
            menu.Load(CustomRun.Scene);
        }

        public void Edit(StoredLevel level)
        {
            if (level.Level == null) return;
            CustomRun.Level = level.Level;
            CustomRun.EditPath = level.Path;
            menu.Load(CustomRun.EditorScene);
        }

        public void NewLevel()
        {
            CustomRun.Level = null;
            CustomRun.EditPath = null;
            menu.Load(CustomRun.EditorScene);
        }

        void Delete(Row row)
        {
            if (armedDelete != row.Delete)
            {
                armedDelete = row.Delete;
                row.DeleteLabel.text = deleteConfirmText;
                Sfx.Play(Sound.MenuConfirm);
                return;
            }
            int index = rows.IndexOf(row);
            LevelStore.Delete(row.Level.Path);
            Sfx.Play(Sound.MenuBack);
            menu.OpenMyLevels(index);
        }

        void Update()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            // Moving off an armed Delete takes the question back.
            if (armedDelete != null && selected != armedDelete.gameObject)
            {
                armedDelete.GetComponentInChildren<TMP_Text>().text = deleteText;
                armedDelete = null;
            }

            if (selected != shown && selected != null && selected.transform.parent != null
                && selected.transform.parent.parent == scroll.content)
                ShowInList(selected.transform);
            shown = selected;
        }

        /// <summary>Scrolls just enough to show a row's button, so keys and the d-pad never move onto a hidden one.</summary>
        void ShowInList(Transform button)
        {
            RectTransform viewport = scroll.viewport;
            RectTransform content = scroll.content;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, button);
            Rect view = viewport.rect;
            float shift = 0f;
            if (bounds.max.y > view.yMax) shift = bounds.max.y - view.yMax;
            else if (bounds.min.y < view.yMin) shift = bounds.min.y - view.yMin;
            if (shift == 0f) return;

            scroll.StopMovement();
            float limit = Mathf.Max(0f, content.rect.height - view.height);
            Vector2 position = content.anchoredPosition;
            position.y = Mathf.Clamp(position.y - shift, 0f, limit);
            content.anchoredPosition = position;
        }
    }
}
