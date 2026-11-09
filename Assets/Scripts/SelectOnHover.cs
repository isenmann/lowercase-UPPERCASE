using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LowercaseUppercase
{
    /// <summary>
    /// Selects its button when the mouse moves onto it, so pointing at something does what moving there with keys or a
    /// gamepad does. Whoever owns the button says what selecting means through <see cref="Hovered"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SelectOnHover : MonoBehaviour, IPointerEnterHandler
    {
        public Action<Button> Hovered;

        public void OnPointerEnter(PointerEventData eventData)
        {
            var button = GetComponent<Button>();
            if (button.interactable) Hovered?.Invoke(button);
        }
    }
}
