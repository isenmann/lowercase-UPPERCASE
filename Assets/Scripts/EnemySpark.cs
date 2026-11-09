using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Leaps out of a hot word in an arc on a timer and drops back into it, hidden in between.
    /// Each leap leans the other way, so it keeps returning to the same spot.
    /// The transform's resting position is where it leaves the word.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemySpark : MonoBehaviour
    {
        const float Gravity = 20f;

        [Tooltip("Units above the word at the top of a leap.")]
        public float leapHeight = 1.8f;
        [Tooltip("Units per second sideways during a leap.")]
        public float sideSpeed = 0.8f;
        [Tooltip("Seconds hidden in the word between leaps.")]
        public float restSeconds = 1.4f;
        [Tooltip("Seconds to wait before the first leap, so neighbours can leap out of step.")]
        public float startDelay = 0f;

        public bool IsLeaping { get; private set; }

        Enemy enemy;
        Renderer glyphRenderer;
        Vector2 home;
        Vector2 velocity;
        float lean = 1f;
        float restLeft;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            glyphRenderer = enemy.glyph.GetComponent<Renderer>();
        }

        void Start()
        {
            home = transform.position;
            restLeft = startDelay > 0f ? startDelay : restSeconds;
            Show(false);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (!IsLeaping)
            {
                restLeft -= dt;
                if (restLeft > 0f) return;
                IsLeaping = true;
                velocity = new Vector2(lean * sideSpeed, Mathf.Sqrt(2f * Gravity * leapHeight));
                lean = -lean;
                enemy.Body.position = home;
                Show(true);
                return;
            }

            velocity.y -= Gravity * dt;
            Vector2 next = enemy.Body.position + velocity * dt;
            if (velocity.y < 0f && next.y <= home.y)
            {
                IsLeaping = false;
                restLeft = restSeconds;
                Show(false);
                next = new Vector2(next.x, home.y);
                home = new Vector2(next.x, home.y);
            }
            enemy.Body.MovePosition(next);
        }

        void Show(bool shown)
        {
            enemy.Box.enabled = shown;
            glyphRenderer.enabled = shown;
        }
    }
}
