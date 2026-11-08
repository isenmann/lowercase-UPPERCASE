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
    /// The title screen: Play starts the first level, Chapters picks a level, Options holds the settings, and Exit
    /// quits. The web build hides Exit, since a page can't close itself, and the window mode and resolution, since the
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
        public UnityEngine.UI.Button optionsButton;
        [Tooltip("Quits the game. Hidden in the web build.")]
        public UnityEngine.UI.Button exitButton;

        [Header("Chapters")]
        public UnityEngine.UI.Button[] chapterButtons;
        [Tooltip("The scene each chapter button loads. Play loads the first.")]
        public string[] chapterScenes;
        public UnityEngine.UI.Button chaptersBack;

        [Header("Options")]
        [Tooltip("Switches between borderless fullscreen and a window.")]
        public UnityEngine.UI.Button fullscreenButton;
        public TMP_Text fullscreenLabel;
        [Tooltip("Steps through the monitor's resolutions.")]
        public UnityEngine.UI.Button resolutionButton;
        public TMP_Text resolutionLabel;
        [Tooltip("Switches between the Chip and Type sound sets.")]
        public UnityEngine.UI.Button soundButton;
        public TMP_Text soundLabel;
        public UnityEngine.UI.Button optionsBack;

        public UnityEngine.UI.Image fade;

        [Tooltip("Filled in with the version from Player Settings when the menu opens.")]
        public TMP_Text version;
        public string versionFormat = "v{0} by dev.play.repeat";

        static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        GameObject openPanel;
        UnityEngine.UI.Button firstButton;
        GameObject lastSelected;
        bool loading;

        void Awake()
        {
            Time.timeScale = 1f;
            LevelRun.NewGame();
            CreateEventSystem();
            if (version != null) version.text = string.Format(versionFormat, Application.version);

            playButton.onClick.AddListener(() => Load(chapterScenes[0]));
            chaptersButton.onClick.AddListener(() => Confirm(() => Open(chaptersPanel, chapterButtons[0])));
            UnityEngine.UI.Button firstOption = IsWeb ? soundButton : fullscreenButton;
            optionsButton.onClick.AddListener(() => Confirm(() => Open(optionsPanel, firstOption)));
            chaptersBack.onClick.AddListener(Back);
            optionsBack.onClick.AddListener(Back);
            if (IsWeb)
            {
                // Both display buttons share a row; hiding the row lets the panel close the gap.
                fullscreenButton.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                fullscreenButton.onClick.AddListener(() => Confirm(DisplaySettings.ToggleFullscreen));
                if (resolutionButton != null) resolutionButton.onClick.AddListener(() => Confirm(DisplaySettings.NextResolution));
            }
            if (soundButton != null) soundButton.onClick.AddListener(SwitchSoundSet);
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
            ShowMain();
            StartCoroutine(Fade(1f, 0f));
        }

        void Update()
        {
            fullscreenLabel.text = DisplaySettings.ModeLabel;
            if (resolutionLabel != null) resolutionLabel.text = DisplaySettings.ResolutionLabel;
            if (soundLabel != null) soundLabel.text = $"Sounds: {Sfx.Set}";

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

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && openPanel != mainPanel) Back();
        }

        void ShowMain() => Open(mainPanel, playButton);

        void Back()
        {
            Sfx.Play(Sound.MenuBack);
            ShowMain();
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
            openPanel = panel;
            firstButton = first;
            // Opening a panel selects its first button; that isn't a move.
            lastSelected = first.gameObject;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void Load(string scene)
        {
            if (loading) return;
            loading = true;
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
