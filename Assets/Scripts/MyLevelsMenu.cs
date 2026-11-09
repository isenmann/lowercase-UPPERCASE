using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LowercaseUppercase
{
    /// <summary>
    /// The menu's My levels panel: one row per level made in the editor, with Play (the level's name), Edit, Share and
    /// Delete, and New level, Import and Back under the list. <see cref="MainMenu"/> opens and closes it; this fills it
    /// in, and opens Share and Import over it. Both of those link to the shared levels page. Delete asks for a second press, like Reset progress. The strip above the
    /// list shows a picture of the row last pointed at or moved to; clicking the name still plays it.
    /// </summary>
    public class MyLevelsMenu : MonoBehaviour
    {
        public MainMenu menu;
        [Tooltip("Inactive row inside the list, copied once per level: a name button that plays it, Edit and Delete.")]
        public GameObject rowTemplate;
        public ScrollRect scroll;
        [Tooltip("The picture of the current row's level, above the list.")]
        public LevelPreviewStrip preview;
        [Tooltip("Shown instead of the list when there are no levels yet.")]
        public TMP_Text emptyText;
        public Button newButton;
        public Button importButton;
        public Button backButton;

        [Header("Share")]
        [Tooltip("Covers the list while open: the level's code, already copied, and whether its maker finished it.")]
        public GameObject sharePanel;
        public TMP_Text shareHeading;
        public TMP_Text shareStatus;
        public TMP_InputField shareCode;
        public Button shareCopy;
        [Tooltip("Opens the shared levels page, where finished levels can be uploaded.")]
        public Button shareSite;
        public Button shareBack;

        [Header("Import")]
        [Tooltip("Covers the list while open: a box to paste a code into.")]
        public GameObject importPanel;
        public TMP_InputField importCode;
        public TMP_Text importStatus;
        public Button importPaste;
        public Button importConfirm;
        [Tooltip("Opens the shared levels page, to find codes to import.")]
        public Button importSite;
        public Button importBack;

        [Header("Shared levels page")]
        [Tooltip("Where players share their codes: the page in Web/, put online with Cloudflare Workers.")]
        public string sharedLevelsUrl = "https://lowercase-uppercase-levels.devplayrepeat.workers.dev";

        [Header("Words")]
        public string deleteText = "Delete";
        public string deleteConfirmText = "Really?";
        public string finishedMark = "  <color=#6B7180>finished</color>";
        public string importHint = "Paste a level code, then Import. Shared levels has more to find.";

        const float UnreadableAlpha = 0.4f;

        readonly List<Row> rows = new List<Row>();
        Button armedDelete;
        GameObject shown;
        int overlayRow;

        sealed class Row
        {
            public StoredLevel Level;
            public Button Play;
            public Button Edit;
            public Button Share;
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
            importButton.onClick.AddListener(OpenImport);
            shareCopy.onClick.AddListener(CopyShareCode);
            shareSite.onClick.AddListener(OpenSharedLevels);
            importSite.onClick.AddListener(OpenSharedLevels);
            shareBack.onClick.AddListener(() => CloseOverlay());
            importPaste.onClick.AddListener(PasteImportCode);
            importConfirm.onClick.AddListener(() => Import());
            importBack.onClick.AddListener(() => CloseOverlay());
            sharePanel.SetActive(false);
            importPanel.SetActive(false);
        }

        public bool OverlayOpen => sharePanel.activeSelf || importPanel.activeSelf;

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
            sharePanel.SetActive(false);
            importPanel.SetActive(false);

            foreach (StoredLevel level in LevelStore.List()) rows.Add(MakeRow(content, level));

            bool any = rows.Count > 0;
            if (preview != null) preview.Show(any ? rows[Mathf.Clamp(focusRow, 0, rows.Count - 1)].Level : null);
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
                Share = go.transform.Find("ShareButton").GetComponent<Button>(),
                Delete = go.transform.Find("DeleteButton").GetComponent<Button>(),
            };
            row.DeleteLabel = row.Delete.GetComponentInChildren<TMP_Text>();
            // Pointing at a row picks it, as moving there with keys does, so the picture follows the mouse too.
            foreach (Button button in new[] { row.Play, row.Edit, row.Share, row.Delete })
            {
                var hover = button.GetComponent<SelectOnHover>();
                if (hover != null) hover.Hovered = menu.Focus;
            }
            row.DeleteLabel.text = deleteText;

            TMP_Text name = row.Play.GetComponentInChildren<TMP_Text>();
            bool readable = level.Level != null;
            name.text = !readable ? $"can't read {level.Name}"
                : level.Level.FinishedByMaker ? level.Name + finishedMark : level.Name;
            row.Play.interactable = readable;
            row.Edit.interactable = readable;
            row.Share.interactable = readable;
            if (!readable)
            {
                name.alpha = UnreadableAlpha;
                row.Edit.GetComponentInChildren<TMP_Text>().alpha = UnreadableAlpha;
                row.Share.GetComponentInChildren<TMP_Text>().alpha = UnreadableAlpha;
            }

            row.Play.onClick.AddListener(() => Play(level));
            row.Edit.onClick.AddListener(() => Edit(level));
            row.Share.onClick.AddListener(() => Share(row));
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

        // ---------------------------------------------------------------- share and import

        void Share(Row row)
        {
            if (row.Level.Level == null) return;
            overlayRow = rows.IndexOf(row);
            Sfx.Play(Sound.MenuConfirm);
            Show(row.Level.Level);
        }

        /// <summary>Opens Share for a level: its code, copied to the clipboard, and whether its maker finished it.</summary>
        public void Show(CustomLevelData level)
        {
            shareHeading.text = $"Share {level.name}";
            shareCode.text = LevelShareCode.Encode(level);
            string finished = level.FinishedByMaker
                ? "Finished by its maker, so it can go on Shared levels too."
                : "Not finished yet: reach the exit in a test play from the start. Shared levels only takes finished ones.";
            shareStatus.text = (Copy(shareCode.text) ? "Copied to the clipboard. " : "Select the code to copy it. ") + finished;
            sharePanel.SetActive(true);
            menu.Focus(shareCopy);
        }

        void CopyShareCode()
        {
            Sfx.Play(Sound.MenuConfirm);
            if (Copy(shareCode.text)) shareStatus.text = "Copied to the clipboard again.";
        }

        static bool Copy(string text)
        {
            // The web build can't reach the clipboard this way.
            if (Application.platform == RuntimePlatform.WebGLPlayer) return false;
            GUIUtility.systemCopyBuffer = text;
            return true;
        }

        /// <summary>Opens the shared levels page in the browser, or in a new tab in the web build.</summary>
        public void OpenSharedLevels()
        {
            Sfx.Play(Sound.MenuConfirm);
            Application.OpenURL(sharedLevelsUrl);
        }

        void OpenImport()
        {
            overlayRow = 0;
            Sfx.Play(Sound.MenuConfirm);
            importCode.text = "";
            importStatus.text = importHint;
            importPanel.SetActive(true);
            menu.Focus(importPaste);
        }

        void PasteImportCode()
        {
            Sfx.Play(Sound.MenuConfirm);
            importCode.text = GUIUtility.systemCopyBuffer ?? "";
            menu.Focus(importConfirm);
        }

        /// <summary>
        /// Reads the pasted code and saves it as a new level at the top of the list, or says what is wrong with it.
        /// </summary>
        public bool Import()
        {
            CustomLevelData level;
            try
            {
                level = LevelShareCode.Decode(importCode.text);
            }
            catch (System.FormatException e)
            {
                Sfx.Play(Sound.MenuBack);
                importStatus.text = e.Message;
                return false;
            }

            try
            {
                LevelStore.Save(level);
            }
            catch (System.Exception e) when (e is System.IO.IOException || e is System.UnauthorizedAccessException)
            {
                importStatus.text = "Couldn't save it: " + e.Message;
                return false;
            }
            Sfx.Play(Sound.MenuStart);
            menu.OpenMyLevels(0);
            return true;
        }

        /// <summary>Closes Share or Import, back to the list where it was. False when neither was open.</summary>
        public bool CloseOverlay()
        {
            if (!OverlayOpen) return false;
            Sfx.Play(Sound.MenuBack);
            menu.OpenMyLevels(overlayRow);
            return true;
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
            {
                ShowInList(selected.transform);
                Row row = rows.Find(r => r.Play.transform.parent == selected.transform.parent);
                if (row != null && preview != null) preview.Show(row.Level);
            }
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
