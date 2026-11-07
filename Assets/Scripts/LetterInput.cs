using UnityEngine;
using UnityEngine.InputSystem;

namespace LowercaseUppercase
{
    /// <summary>Reads keyboard and gamepad and feeds the LetterController.</summary>
    [RequireComponent(typeof(LetterController))]
    public class LetterInput : MonoBehaviour
    {
        LetterController letter;
        InputAction move;
        InputAction jump;
        InputAction shift;

        void Awake()
        {
            letter = GetComponent<LetterController>();

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
        }

        void OnEnable()
        {
            move.Enable();
            jump.Enable();
            shift.Enable();
        }

        void OnDisable()
        {
            move.Disable();
            jump.Disable();
            shift.Disable();
        }

        void OnDestroy()
        {
            move.Dispose();
            jump.Dispose();
            shift.Dispose();
        }

        void Update()
        {
            if (jump.WasPressedThisFrame()) letter.PressJump();
            letter.SetInput(move.ReadValue<float>(), jump.IsPressed(), shift.IsPressed());
        }
    }
}
