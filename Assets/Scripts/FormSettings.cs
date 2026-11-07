using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// All tuning for one form of the letter. These are assets, so edits made during
    /// Play mode are kept when you stop playing.
    /// </summary>
    [CreateAssetMenu(menuName = "lowercase UPPERCASE/Form Settings")]
    public class FormSettings : ScriptableObject
    {
        [Header("Body")]
        public Vector2 colliderSize = new Vector2(0.42f, 0.4f);
        public float mass = 1f;

        [Header("Run (units per second; the screen is about 17.8 units wide)")]
        public float runSpeed = 7f;
        public float groundAcceleration = 60f;
        public float groundDeceleration = 70f;
        public float airAcceleration = 40f;
        public float airDeceleration = 8f;

        [Tooltip("How fast speed above Run Speed bleeds off on the ground.")]
        public float overspeedDecayGround = 12f;

        [Tooltip("How fast speed above Run Speed bleeds off in the air. Low values make glides long.")]
        public float overspeedDecayAir = 2f;

        [Header("Jump")]
        public float jumpHeight = 2.5f;
        public float timeToApex = 0.38f;
        public float fallGravityMultiplier = 0.8f;
        public float maxFallSpeed = 12f;

        [Tooltip("Upward speed kept when Jump is released early. 1 turns off variable jump height.")]
        [Range(0f, 1f)] public float jumpCutMultiplier = 0.45f;

        [Tooltip("Below this vertical speed, holding Jump counts as hanging at the apex.")]
        public float apexHangThreshold = 1.2f;

        [Range(0.1f, 1f)] public float apexHangGravityMultiplier = 0.55f;
        public float coyoteTime = 0.09f;
        public float jumpBuffer = 0.12f;

        [Header("Spring")]
        [Tooltip("Height reached from an exclamation mark. The concept wants lowercase at twice UPPERCASE.")]
        public float springHeight = 6f;

        [Header("Entering this form in mid-air")]
        [Tooltip("Share of the downward speed converted into forward speed. This is what makes the glide.")]
        public float fallToForward = 0f;

        [Tooltip("Share of the downward speed kept after entering this form.")]
        [Range(0f, 1f)] public float fallKept = 1f;

        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
        public float JumpVelocity => 2f * jumpHeight / timeToApex;
        public float VelocityToReach(float height) => Mathf.Sqrt(2f * Gravity * height);
    }
}
