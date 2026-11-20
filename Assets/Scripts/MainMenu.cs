using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>
    /// The title screen: Play starts the first level, Chapters picks a level already reached (see LevelProgress), Random starts an endless run of random levels, Options holds the settings and resets progress, Editor opens My levels (see MyLevelsMenu), How to play
    /// explains the two forms and the level pieces, and Exit quits. The web build hides Exit, since a page can't close itself, and the window mode and resolution, since the
    /// page's canvas sets the size there.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        const float FadeSeconds = 0.35f;

        [Header("Panels")]
        public GameObject mainPanel;
        public GameObject chaptersPanel;
        public GameObject optionsPanel;

        [Header("Main")]
        public UnityEngine.UI.Button playButton;
        public UnityEngine.UI.Button chaptersButton;
        [Tooltip("Starts an endless run of random levels, which don't count for high scores.")]
        public UnityEngine.UI.Button randomButton;
        public string randomScene = "Random";
        public UnityEngine.UI.Button optionsButton;
        [Tooltip("Opens My levels, the levels made in the level editor.")]
        public UnityEngine.UI.Button editorButton;
        [Tooltip("Quits the game. Hidden in the web build.")]
        public UnityEngine.UI.Button exitButton;

        [Header("Chapters")]
        public UnityEngine.UI.Button[] chapterButtons;
        [Tooltip("The scene each chapter button loads. Play loads the first.")]
        public string[] chapterScenes;
        public UnityEngine.UI.Button chaptersBack;
        [Tooltip("Label color of levels not reached yet. Only the first level is open at the start; finishing one opens the next.")]
        public Color lockedLabelColor = new Color32(0xF2, 0xF0, 0xEA, 0x40);

        [Header("Options")]
        [Tooltip("Switches between borderless fullscreen and a window.")]
        public UnityEngine.UI.Button fullscreenButton;
        public TMP_Text fullscreenLabel;
        [Tooltip("Opens the list of the monitor's resolutions.")]
        public UnityEngine.UI.Button resolutionButton;
        public TMP_Text resolutionLabel;
        [Tooltip("Switches between the Chip and Type sound sets.")]
        public UnityEngine.UI.Button soundButton;
        public TMP_Text soundLabel;
        [Tooltip("Switches sending runs to the world high scores on and off.")]
        public UnityEngine.UI.Button onlineButton;
        public TMP_Text onlineLabel;
        [Tooltip("Locks every level but the first again. Asks for a second press first.")]
        public UnityEngine.UI.Button resetProgressButton;
        public TMP_Text resetProgressLabel;
        public string resetProgressText = "Reset progress";
        public string resetConfirmText = "Really reset?";
        public string resetDoneText = "Progress reset";
        public UnityEngine.UI.Button optionsBack;

        [Header("Resolutions")]
        [Tooltip("Covers the whole menu while open. Lists every resolution; clicking one applies it and goes back to Options.")]
        public GameObject resolutionPanel;
        [Tooltip("Inactive button inside the list's grid, copied once per resolution when the list opens.")]
        public UnityEngine.UI.Button resolutionOption;
        [Tooltip("Scrolls the grid when there are more resolutions than fit on the screen.")]
        public UnityEngine.UI.ScrollRect resolutionScroll;
        public UnityEngine.UI.Button resolutionBack;
        [Tooltip("Canvas units kept clear round the list on every side.")]
        public float resolutionMargin = 40f;
        [Tooltip("Canvas units the heading, Back and the gaps between them take from the list's height.")]
        public float resolutionReservedHeight = 132f;
        [Tooltip("Most columns the list uses, however wide the screen.")]
        public int maxResolutionColumns = 8;

        [Header("My levels")]
        [Tooltip("Covers the whole menu while open.")]
        public GameObject myLevelsPanel;
        public MyLevelsMenu myLevels;

        [Header("How to play")]
        public UnityEngine.UI.Button guideButton;
        [Tooltip("Covers the whole menu while open.")]
        public GameObject guidePanel;
        [Tooltip("One per page. Selecting a tab shows its page.")]
        public UnityEngine.UI.Button[] guideTabs;
        public TMP_Text[] guideTabLabels;
        public GameObject[] guidePages;
        public UnityEngine.UI.Button guideBack;
        public Color currentTabColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        public Color otherTabColor = new Color32(0xF2, 0xF0, 0xEA, 0xFF);

        public UnityEngine.UI.Image fade;

        [Tooltip("Filled in with the version from Player Settings when the menu opens.")]
        public TMP_Text version;
        public string versionFormat = "v{0} by dev.play.repeat";

        static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        GameObject openPanel;
        UnityEngine.UI.Button firstButton;
        GameObject lastSelected;
        bool loading;
        bool resetArmed;
        Vector2 resolutionArea;

        void Awake()
        {
            Time.timeScale = 1f;
            LevelRun.NewGame();
            // Back at the menu, a run that was going on is over without a name.
            OnlineScores.Abandon();
            CreateEventSystem();
            if (version != null) version.text = string.Format(versionFormat, Application.version);
            ShootKey.RewriteHints();

            playButton.onClick.AddListener(() => Load(chapterScenes[0]));
            chaptersButton.onClick.AddListener(() => Confirm(() => Open(chaptersPanel, chapterButtons[0])));
            if (randomButton != null) randomButton.onClick.AddListener(StartRandomRun);
            UnityEngine.UI.Button firstOption = IsWeb ? soundButton : fullscreenButton;
            optionsButton.onClick.AddListener(() => Confirm(() => Open(optionsPanel, firstOption)));
            chaptersBack.onClick.AddListener(Back);
            optionsBack.onClick.AddListener(Back);
            if (myLevelsPanel != null)
            {
                editorButton.onClick.AddListener(() => Confirm(() => OpenMyLevels()));
                myLevels.backButton.onClick.AddListener(Back);
            }
            if (guidePanel != null)
            {
                guideButton.onClick.AddListener(() => Confirm(OpenGuide));
                guideBack.onClick.AddListener(Back);
            }
            if (IsWeb)
            {
                // Both display buttons share a row; hiding the row lets the panel close the gap.
                fullscreenButton.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                fullscreenButton.onClick.AddListener(() => Confirm(DisplaySettings.ToggleFullscreen));
                if (resolutionButton != null) resolutionButton.onClick.AddListener(() => Confirm(OpenResolutions));
                if (resolutionBack != null) resolutionBack.onClick.AddListener(Back);
            }
            if (soundButton != null) soundButton.onClick.AddListener(SwitchSoundSet);
            if (onlineButton != null) onlineButton.onClick.AddListener(() => Confirm(() => OnlineScores.Enabled = !OnlineScores.Enabled));
            if (resetProgressButton != null) resetProgressButton.onClick.AddListener(ResetProgress);
            if (exitButton != null)
            {
                if (IsWeb) exitButton.gameObject.SetActive(false);
                else exitButton.onClick.AddListener(Exit);
            }

            for (int i = 0; i < chapterButtons.Length; i++)
            {
                string scene = chapterScenes[i];
                chapterButtons[i].onClick.AddListener(() => Load(scene));
            }
            UpdateChapterLocks();
        }

        // A locked level can't be clicked or reached with keys or the d-pad; its label is dimmed along with the outline.
        void UpdateChapterLocks()
        {
            for (int i = 0; i < chapterButtons.Length; i++)
            {
                UnityEngine.UI.Button button = chapterButtons[i];
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null && button.interactable) unlockedLabelColor = label.color;
                bool open = i == 0 || LevelProgress.IsUnlocked(chapterScenes[i]);
                button.interactable = open;
                if (label != null) label.color = open ? unlockedLabelColor : lockedLabelColor;
            }
        }

        Color unlockedLabelColor = Color.white;

        // The first press only asks, so a stray click or key can't wipe the player's progress.
        void ResetProgress()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                Sfx.Play(Sound.MenuConfirm);
                resetProgressLabel.text = resetConfirmText;
                return;
            }
            resetArmed = false;
            LevelProgress.Clear();
            UpdateChapterLocks();
            Sfx.Play(Sound.MenuBack);
            resetProgressLabel.text = resetDoneText;
        }

        // Created here because the default UI actions are made in memory and wouldn't survive being saved in a scene.
        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void Start()
        {
            // Back from a custom level or the editor, the menu opens where they were started from.
            if (CustomRun.ReturnToMyLevels && myLevelsPanel != null) OpenMyLevels();
            else ShowMain();
            CustomRun.ReturnToMyLevels = false;
            StartCoroutine(Fade(1f, 0f));
        }

        void Update()
        {
            fullscreenLabel.text = DisplaySettings.ModeLabel;
            if (resolutionLabel != null) resolutionLabel.text = DisplaySettings.ResolutionLabel;
            if (soundLabel != null) soundLabel.text = $"Sounds: {Sfx.Set}";
            if (onlineLabel != null) onlineLabel.text = OnlineScores.Enabled ? "Online scores: On" : "Online scores: Off";

            if (loading) return;

            // Clicking empty space clears the selection, and keys and gamepads can't move from nothing.
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null)
            {
                events.SetSelectedGameObject(firstButton.gameObject);
                lastSelected = firstButton.gameObject;
            }

            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected != lastSelected)
            {
                if (selected != null && lastSelected != null) Sfx.Play(Sound.MenuMove);
                lastSelected = selected;
            }

            if (openPanel == guidePanel) FollowGuideTabs(selected);
            if (openPanel == resolutionPanel) UpdateResolutionList(selected);

            // Moving off the button or leaving Options takes the question back.
            if (resetProgressButton != null && selected != resetProgressButton.gameObject
                && resetProgressLabel.text != resetProgressText)
            {
                resetArmed = false;
                resetProgressLabel.text = resetProgressText;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && openPanel != mainPanel) Back();
        }

        void ShowMain() => Open(mainPanel, playButton);

        void Back()
        {
            if (openPanel == myLevelsPanel && myLevels.CloseOverlay()) return;
            Sfx.Play(Sound.MenuBack);
            if (openPanel == resolutionPanel) Open(optionsPanel, resolutionButton);
            else if (openPanel == myLevelsPanel) Open(mainPanel, editorButton);
            else ShowMain();
        }

        static void Confirm(System.Action action)
        {
            Sfx.Play(Sound.MenuConfirm);
            action();
        }

        // The confirm sound plays in the newly chosen set, so the switch can be heard.
        void SwitchSoundSet()
        {
            Sfx.Set = Sfx.Set == SoundSet.Chip ? SoundSet.Type : SoundSet.Chip;
            Sfx.Play(Sound.MenuConfirm);
        }

        void Open(GameObject panel, UnityEngine.UI.Button first)
        {
            mainPanel.SetActive(panel == mainPanel);
            chaptersPanel.SetActive(panel == chaptersPanel);
            optionsPanel.SetActive(panel == optionsPanel);
            if (guidePanel != null) guidePanel.SetActive(panel == guidePanel);
            if (resolutionPanel != null) resolutionPanel.SetActive(panel == resolutionPanel);
            if (myLevelsPanel != null) myLevelsPanel.SetActive(panel == myLevelsPanel);
            openPanel = panel;
            firstButton = first;
            // Opening a panel selects its first button; that isn't a move.
            lastSelected = first.gameObject;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        /// <summary>
        /// Fills the list with one button per resolution, the current one highlighted and selected, so keys and the
        /// d-pad start from it. The list is made fresh each time, as the monitor or window size may have changed.
        /// </summary>
        void OpenResolutions()
        {
            Transform grid = resolutionOption.transform.parent;
            foreach (Transform child in grid)
            {
                if (child == resolutionOption.transform) continue;
                child.gameObject.SetActive(false); // Destroy waits for the frame's end; this keeps it out of the layout now.
                Destroy(child.gameObject);
            }

            Vector2Int current = DisplaySettings.Current;
            UnityEngine.UI.Button selected = null;
            foreach (Vector2Int size in DisplaySettings.Sizes())
            {
                UnityEngine.UI.Button option = Instantiate(resolutionOption, grid);
                option.name = $"Resolution {DisplaySettings.Label(size)}";
                option.gameObject.SetActive(true);
                TMP_Text label = option.GetComponentInChildren<TMP_Text>();
                label.text = DisplaySettings.Label(size);
                if (size == current)
                {
                    label.color = currentTabColor;
                    selected = option;
                }
                Vector2Int chosen = size;
                option.onClick.AddListener(() => ChooseResolution(chosen));
            }

            Open(resolutionPanel, selected != null ? selected : resolutionBack);
            LayoutResolutions();
            resolutionScroll.content.anchoredPosition = Vector2.zero;
            ShowResolution(selected);
        }

        /// <summary>
        /// Fits the list to the screen: as many columns as the width allows, up to maxResolutionColumns, and as tall as
        /// the rows need, or as the screen allows, with the rest reached by scrolling. Done again if the window is resized.
        /// </summary>
        void LayoutResolutions()
        {
            var panel = (RectTransform)resolutionPanel.transform;
            Vector2 area = panel.rect.size;
            resolutionArea = area;

            RectTransform content = resolutionScroll.content;
            var grid = content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            int count = 0;
            foreach (Transform child in content)
                if (child.gameObject.activeSelf) count++;

            float cellWidth = grid.cellSize.x, cellHeight = grid.cellSize.y;
            float gapX = grid.spacing.x, gapY = grid.spacing.y;
            // The scrollbar sits outside the list on the right, so a second margin's worth keeps it on screen.
            float usableWidth = area.x - 3f * resolutionMargin;
            int columns = Mathf.FloorToInt((usableWidth + gapX) / (cellWidth + gapX));
            columns = Mathf.Clamp(columns, 1, Mathf.Max(1, maxResolutionColumns));
            columns = Mathf.Min(columns, Mathf.Max(1, count));
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            grid.constraintCount = columns;

            float width = columns * cellWidth + (columns - 1) * gapX;
            float contentHeight = rows * cellHeight + (rows - 1) * gapY;
            // At least one row shows, even on a screen too short for everything else.
            float usableHeight = Mathf.Max(cellHeight, area.y - 2f * resolutionMargin - resolutionReservedHeight);
            float height = Mathf.Min(contentHeight, usableHeight);

            var list = (RectTransform)resolutionScroll.transform;
            list.sizeDelta = new Vector2(width, height);
            var element = list.GetComponent<UnityEngine.UI.LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
            content.sizeDelta = new Vector2(width, contentHeight);

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)list.parent);
        }

        GameObject shownResolution;

        void UpdateResolutionList(GameObject selected)
        {
            var panel = (RectTransform)resolutionPanel.transform;
            if (panel.rect.size != resolutionArea)
            {
                LayoutResolutions();
                shownResolution = null;
            }
            if (selected != shownResolution && selected != null
                && selected.transform.parent == resolutionScroll.content)
                ShowResolution(selected.GetComponent<UnityEngine.UI.Button>());
        }

        /// <summary>
        /// Scrolls just enough to show the given button, so keys and the d-pad never move onto a hidden one, and points
        /// Back's up at it, so leaving Back returns to the same place in the list.
        /// </summary>
        void ShowResolution(UnityEngine.UI.Button option)
        {
            if (option == null) return;
            shownResolution = option.gameObject;

            UnityEngine.UI.Navigation backNavigation = resolutionBack.navigation;
            backNavigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            backNavigation.selectOnUp = option;
            resolutionBack.navigation = backNavigation;

            RectTransform viewport = resolutionScroll.viewport;
            RectTransform content = resolutionScroll.content;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, option.transform);
            Rect view = viewport.rect;
            float shift = 0f;
            if (bounds.max.y > view.yMax) shift = bounds.max.y - view.yMax;
            else if (bounds.min.y < view.yMin) shift = bounds.min.y - view.yMin;
            if (shift == 0f) return;

            resolutionScroll.StopMovement();
            float limit = Mathf.Max(0f, content.rect.height - view.height);
            Vector2 position = content.anchoredPosition;
            position.y = Mathf.Clamp(position.y - shift, 0f, limit);
            content.anchoredPosition = position;
        }

        void ChooseResolution(Vector2Int size)
        {
            Sfx.Play(Sound.MenuConfirm);
            DisplaySettings.SetResolution(size);
            Open(optionsPanel, resolutionButton);
        }

        void OpenGuide()
        {
            ShowGuidePage(0);
            Open(guidePanel, guideTabs[0]);
        }

        // Moving onto a tab turns the page, so arrow keys, the d-pad and clicks all work the same way.
        void FollowGuideTabs(GameObject selected)
        {
            for (int i = 0; i < guideTabs.Length; i++)
                if (selected == guideTabs[i].gameObject) ShowGuidePage(i);
        }

        void ShowGuidePage(int index)
        {
            for (int i = 0; i < guidePages.Length; i++)
            {
                guidePages[i].SetActive(i == index);
                guideTabLabels[i].color = i == index ? currentTabColor : otherTabColor;
            }
        }

        void StartRandomRun()
        {
            if (loading) return;
            RandomRun.Begin();
            Load(randomScene);
        }

        /// <summary>Selects a button in the open panel, and makes it the one keys come back to.</summary>
        public void Focus(UnityEngine.UI.Button button)
        {
            if (button == null || loading) return;
            firstButton = button;
            lastSelected = button.gameObject;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        /// <summary>Lists the levels made in the editor, starting on the given row.</summary>
        public void OpenMyLevels(int focusRow = 0)
        {
            myLevelsPanel.SetActive(true); // Its list lays itself out only while active.
            Open(myLevelsPanel, myLevels.Refresh(focusRow));
        }

        /// <summary>Fades out and loads a scene, once.</summary>
        public void Load(string scene)
        {
            if (loading) return;
            loading = true;
            // Only a run from the first level goes online, whether started with Play or from Chapters.
            if (scene == RunLimits.LevelScenes[0]) OnlineScores.BeginRun();
            Sfx.Play(Sound.MenuStart);
            StartCoroutine(FadeThenLoad(scene));
        }

        void Exit()
        {
            if (loading) return;
            loading = true;
            Sfx.Play(Sound.MenuBack);
            StartCoroutine(FadeThenQuit());
        }

        IEnumerator FadeThenQuit()
        {
            yield return Fade(0f, 1f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator FadeThenLoad(string scene)
        {
            yield return Fade(0f, 1f);
            SceneManager.LoadScene(scene);
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, to, t / FadeSeconds));
                yield return null;
            }
            SetFade(to);
        }

        void SetFade(float alpha)
        {
            Color color = fade.color;
            color.a = alpha;
            fade.color = color;
        }
    }
}
