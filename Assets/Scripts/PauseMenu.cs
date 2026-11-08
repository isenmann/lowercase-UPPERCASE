using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>
    /// The small popup that Esc opens during a level: Resume or Main menu. Time stops while it is open, and the
    /// letter ignores its controls, so nothing pressed in the popup reaches the level.
    /// Arrows or W/S choose, Enter or Space confirms, Esc resumes. The mouse can point and click too.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        const int MenuBuildIndex = 0;
        const int Resume = 0;
        const int MainMenu = 1;

        public GameObject popup;
        public LetterInput letterInput;
        public LetterController letter;
        [Tooltip("Resume, then Main menu.")]
        public TMP_Text[] labels;
        [Tooltip("The box behind each label, which also takes the mouse.")]
        public SpriteRenderer[] boxes;
        public Color selectedBox = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        public Color idleBox = new Color32(0x1A, 0x1D, 0x24, 0xFF);
        public Color selectedLabel = new Color32(0x11, 0x13, 0x18, 0xFF);
        public Color idleLabel = new Color32(0xF2, 0xF0, 0xEA, 0xFF);

        public bool IsOpen { get; private set; }
        public int Selected { get; private set; }

        float timeScaleBefore = 1f;
        bool inputWasEnabled;
        int openedOnFrame = -1;
        Vector2 lastMouse;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            openedOnFrame = Time.frameCount;
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;

            inputWasEnabled = letterInput != null && letterInput.enabled;
            if (letterInput != null) letterInput.enabled = false;
            letter.SetInput(0f, false, false);

            if (Mouse.current != null) lastMouse = Mouse.current.position.ReadValue();
            popup.SetActive(true);
            Sfx.Play(Sound.PauseOpen);
            Selected = -1;
            Select(Resume);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Sfx.Play(Sound.PauseClose);
            Time.timeScale = timeScaleBefore;
            if (letterInput != null) letterInput.enabled = inputWasEnabled;
            popup.SetActive(false);
        }

        public void Choose(int option)
        {
            if (option == Resume)
            {
                Close();
                return;
            }

            Sfx.Play(Sound.MenuConfirm);
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuBuildIndex);
        }

        void Awake() => popup.SetActive(false);

        void Update()
        {
            // The same Esc press that opened the popup must not close it again.
            if (!IsOpen || Time.frameCount == openedOnFrame) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Close();
                    return;
                }
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Select(Resume);
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Select(MainMenu);
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame)
                {
                    Choose(Selected);
                    return;
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.startButton.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame)
                {
                    Close();
                    return;
                }
                if (gamepad.dpad.up.wasPressedThisFrame) Select(Resume);
                if (gamepad.dpad.down.wasPressedThisFrame) Select(MainMenu);
                if (gamepad.buttonSouth.wasPressedThisFrame)
                {
                    Choose(Selected);
                    return;
                }
            }

            UpdateMouse();
        }

        void UpdateMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Camera.main == null) return;

            Vector2 screen = mouse.position.ReadValue();
            Vector2 world = Camera.main.ScreenToWorldPoint(screen);
            int under = -1;
            for (int i = 0; i < boxes.Length; i++)
                if (boxes[i].bounds.Contains(new Vector3(world.x, world.y, boxes[i].bounds.center.z))) under = i;

            // Only a moving mouse takes over the selection, so a resting pointer doesn't fight the keys.
            if (under >= 0 && screen != lastMouse) Select(under);
            lastMouse = screen;
            if (under >= 0 && mouse.leftButton.wasPressedThisFrame) Choose(under);
        }

        void Select(int option)
        {
            if (Selected >= 0 && option != Selected) Sfx.Play(Sound.MenuMove);
            Selected = option;
            for (int i = 0; i < labels.Length; i++)
            {
                bool on = i == option;
                boxes[i].color = on ? selectedBox : idleBox;
                labels[i].color = on ? selectedLabel : idleLabel;
            }
        }
    }
}
