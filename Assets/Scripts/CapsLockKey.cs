using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>The key that switches a Caps Lock zone off.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CapsLockKey : MonoBehaviour
    {
        public CapsLockZone zone;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<LetterController>() == null) return;
            zone.Unlock();
            gameObject.SetActive(false);
        }
    }
}
