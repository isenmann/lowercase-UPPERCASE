using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The semicolon halfway through a level. Its trigger is a full-height strip, so passing it at any height counts.
    /// Dying after that reloads the level with the letter here, and with the asterisks it had when it passed.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Checkpoint : MonoBehaviour
    {
        const float PopScale = 1.6f;
        const float PopStiffness = 260f;
        const float PopDamping = 15f;

        // Only set in the moment before a reload, so any other way into a level starts it from the beginning.
        static bool resumeAfterReload;
        static bool resumeArmed;

        public TMP_Text glyph;
        public Color reachedColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        [Tooltip("Where the letter's feet go when it returns here.")]
        public Vector2 respawnFeet;

        public bool IsReached { get; private set; }
        public bool ArmedWhenReached { get; private set; }

        Vector2 scale = Vector2.one;
        Vector2 scaleVelocity;

        /// <summary>Called by the flow as the scene loads. True if this load is a return to the checkpoint.</summary>
        public bool TakeResume()
        {
            if (!resumeAfterReload) return false;
            resumeAfterReload = false;
            MarkReached(resumeArmed);
            return true;
        }

        public void ResumeAfterReload()
        {
            resumeAfterReload = true;
            resumeArmed = ArmedWhenReached;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (IsReached) return;
            var letter = other.GetComponent<LetterController>();
            if (letter == null) return;

            MarkReached(letter.GetComponent<LetterShooter>().IsArmed);
            scale = Vector2.one * PopScale;
        }

        void MarkReached(bool armed)
        {
            IsReached = true;
            ArmedWhenReached = armed;
            glyph.color = reachedColor;
        }

        void Update()
        {
            if (!IsReached) return;
            LetterVisual.StepSpring(ref scale, ref scaleVelocity, Time.unscaledDeltaTime, PopStiffness, PopDamping);
            transform.localScale = new Vector3(scale.x, scale.y, 1f);
        }
    }
}
