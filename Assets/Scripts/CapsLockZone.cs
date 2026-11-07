using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>While the letter is inside this zone, Shift is inverted: released means A, held means a.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CapsLockZone : MonoBehaviour
    {
        public SpriteRenderer tint;
        public Behaviour label;

        public bool IsActive { get; private set; } = true;

        public void Unlock()
        {
            IsActive = false;
            if (tint != null) tint.enabled = false;
            if (label != null) label.enabled = false;
        }
    }
}
