using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A semicolon partway through a level. Its trigger is a full-height strip, so passing it at any height counts.
    /// Dying after that reloads the level with the letter at the furthest one reached. Like every death, it costs the
    /// asterisks: the power block has to be hit again. A level can have several, numbered in the order they are met.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Checkpoint : MonoBehaviour
    {
        const float PopScale = 1.6f;
        const float PopStiffness = 260f;
        const float PopDamping = 15f;

        // Only set in the moment before a reload, so any other way into a level starts it from the beginning.
        static int resumeOrder = -1;

        public TMP_Text glyph;
        public Color reachedColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        [Tooltip("Where the letter's feet go when it returns here.")]
        public Vector2 respawnFeet;
        [Tooltip("Its place among the level's checkpoints, counting from 0 at the start.")]
        public int order;

        public bool IsReached { get; private set; }

        Vector2 scale = Vector2.one;
        Vector2 scaleVelocity;

        /// <summary>
        /// Called by the flow as the scene loads. Returns the checkpoint this load returns to, or null for a fresh
        /// start. The ones before it are shown as reached too.
        /// </summary>
        public static Checkpoint TakeResume(Checkpoint[] all)
        {
            if (resumeOrder < 0) return null;
            int order = resumeOrder;
            resumeOrder = -1;

            Checkpoint resumed = null;
            foreach (Checkpoint checkpoint in all)
            {
                if (checkpoint.order > order) continue;
                checkpoint.MarkReached();
                if (checkpoint.order == order) resumed = checkpoint;
            }
            return resumed;
        }

        /// <summary>The furthest one the letter has reached, or null.</summary>
        public static Checkpoint Furthest(Checkpoint[] all)
        {
            Checkpoint furthest = null;
            foreach (Checkpoint checkpoint in all)
                if (checkpoint.IsReached && (furthest == null || checkpoint.order > furthest.order))
                    furthest = checkpoint;
            return furthest;
        }

        public void ResumeAfterReload()
        {
            resumeOrder = order;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (IsReached) return;
            var letter = other.GetComponent<LetterController>();
            if (letter == null) return;

            MarkReached();
            scale = Vector2.one * PopScale;
        }

        void MarkReached()
        {
            IsReached = true;
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
