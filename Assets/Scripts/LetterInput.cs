using UnityEngine;
using UnityEngine.InputSystem;

namespace LowercaseUppercase
{
    /// <summary>Reads keyboard and gamepad and feeds the LetterController.</summary>
    [RequireComponent(typeof(LetterController))]
    public class LetterInput : MonoBehaviour
    {
        LetterController letter;
        LetterShooter shooter;
        InputAction move;
        InputAction jump;
        InputAction shift;
        InputAction shoot;

        /// <summary>Whether the letter was last steered with a gamepad rather than the keyboard, so screens after
        /// the run can offer the gamepad's way of doing things first.</summary>
        public static bool LastUsedGamepad { get; private set; }

        void Awake()
        {
            letter = GetComponent<LetterController>();
            shooter = GetComponent<LetterShooter>();

            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Axis");
            move.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            move.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick/x");

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Keyboard>/upArrow");
            jump.AddBinding("<Keyboard>/w");
            jump.AddBinding("<Gamepad>/buttonSouth");

            shift = new InputAction("Shift", InputActionType.Button);
            shift.AddBinding("<Keyboard>/leftShift");
            shift.AddBinding("<Keyboard>/rightShift");
            shift.AddBinding("<Gamepad>/rightTrigger");
            shift.AddBinding("<Gamepad>/leftTrigger");

            shoot = new InputAction("Shoot", InputActionType.Button);
            shoot.AddBinding("<Keyboard>/leftCtrl");
            shoot.AddBinding("<Keyboard>/rightCtrl");
#if UNITY_WEBGL && !UNITY_EDITOR
            // In a browser, Ctrl+W (W is also jump) closes the tab and the page can't stop it, so F shoots there too.
            shoot.AddBinding("<Keyboard>/f");
#endif
            shoot.AddBinding("<Gamepad>/buttonWest");
        }

        void OnEnable()
        {
            move.Enable();
            jump.Enable();
            shift.Enable();
            shoot.Enable();
        }

        void OnDisable()
        {
            move.Disable();
            jump.Disable();
            shift.Disable();
            shoot.Disable();
        }

        void OnDestroy()
        {
            move.Dispose();
            jump.Dispose();
            shift.Dispose();
            shoot.Dispose();
        }

        void Update()
        {
            InputControl used = jump.WasPressedThisFrame() ? jump.activeControl
                : shoot.WasPressedThisFrame() ? shoot.activeControl
                : Mathf.Abs(move.ReadValue<float>()) > 0.5f ? move.activeControl : null;
            if (used != null) LastUsedGamepad = used.device is Gamepad;

            if (jump.WasPressedThisFrame()) letter.PressJump();
            if (shoot.WasPressedThisFrame() && shooter != null) shooter.Fire();
            letter.SetInput(move.ReadValue<float>(), jump.IsPressed(), shift.IsPressed());
        }
    }
}
