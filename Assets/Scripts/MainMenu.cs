using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>The title screen: Play starts the first level, Chapters picks a level, and Options holds the settings.</summary>
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

        [Header("Chapters")]
        public UnityEngine.UI.Button[] chapterButtons;
        [Tooltip("The scene each chapter button loads. Play loads the first.")]
        public string[] chapterScenes;
        public UnityEngine.UI.Button chaptersBack;

        [Header("Options")]
        public UnityEngine.UI.Button fullscreenButton;
        public TMP_Text fullscreenLabel;
        public UnityEngine.UI.Button optionsBack;

        public UnityEngine.UI.Image fade;

        GameObject openPanel;
        UnityEngine.UI.Button firstButton;
        bool loading;

        void Awake()
        {
            Time.timeScale = 1f;
            LevelRun.NewGame();
            CreateEventSystem();

            playButton.onClick.AddListener(() => Load(chapterScenes[0]));
            chaptersButton.onClick.AddListener(() => Open(chaptersPanel, chapterButtons[0]));
            optionsButton.onClick.AddListener(() => Open(optionsPanel, fullscreenButton));
            chaptersBack.onClick.AddListener(ShowMain);
            optionsBack.onClick.AddListener(ShowMain);
            fullscreenButton.onClick.AddListener(() => Screen.fullScreen = !Screen.fullScreen);

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
            fullscreenLabel.text = Screen.fullScreen ? "Fullscreen: on" : "Fullscreen: off";

            if (loading) return;

            // Clicking empty space clears the selection, and keys and gamepads can't move from nothing.
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null)
                events.SetSelectedGameObject(firstButton.gameObject);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && openPanel != mainPanel) ShowMain();
        }

        void ShowMain() => Open(mainPanel, playButton);

        void Open(GameObject panel, UnityEngine.UI.Button first)
        {
            mainPanel.SetActive(panel == mainPanel);
            chaptersPanel.SetActive(panel == chaptersPanel);
            optionsPanel.SetActive(panel == optionsPanel);
            openPanel = panel;
            firstButton = first;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void Load(string scene)
        {
            if (loading) return;
            loading = true;
            StartCoroutine(FadeThenLoad(scene));
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
