using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Hangs under a ceiling word. When the letter passes underneath it shakes for a moment, then falls,
    /// splashes on whatever solid word it meets, and grows back on its ceiling a few seconds later.
    /// The trigger area is as wide as an UPPERCASE letter, so a small letter slips by more easily.
    /// Shooting it while it hangs pays points; shooting it as it falls doesn't.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyDrip : MonoBehaviour
    {
        const float Gravity = 30f;
        const float MaxFallSpeed = 14f;
        const float CeilingSearch = 4f;
        const float ShakeWidth = 0.03f;

        enum State { Hanging, Shaking, Falling, Gone }

        [Tooltip("Half the width, in units, of the strip under it that sets it off.")]
        public float triggerHalfWidth = 0.5f;
        [Tooltip("How far below it, in units, the strip reaches.")]
        public float triggerDepth = 6f;
        [Tooltip("Seconds it shakes before it lets go.")]
        public float shakeSeconds = 0.3f;
        [Tooltip("Seconds after splashing before it hangs from the ceiling again.")]
        public float regrowSeconds = 3f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        Enemy enemy;
        Renderer glyphRenderer;
        LetterController letter;
        ContactFilter2D solidFilter;
        State state = State.Hanging;
        Vector2 hangPosition;
        float timer;
        float fallSpeed;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            glyphRenderer = enemy.glyph.GetComponent<Renderer>();
            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(enemy.levelMask != 0 ? enemy.levelMask : (LayerMask)LayerMask.GetMask("Level"));
        }

        /// <summary>Moves up until the top of its ink touches the ceiling above it.</summary>
        void Start()
        {
            hangPosition = transform.position;
            Vector2 top = hangPosition + Vector2.up * enemy.Box.size.y;
            if (Physics2D.Raycast(top, Vector2.up, solidFilter, hits, CeilingSearch) > 0)
                hangPosition = new Vector2(hangPosition.x, hits[0].point.y - enemy.Box.size.y);
            enemy.Body.position = hangPosition;
            transform.position = hangPosition;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            switch (state)
            {
                case State.Hanging:
                    if (!LetterIsBelow()) return;
                    state = State.Shaking;
                    timer = shakeSeconds;
                    break;
                case State.Shaking:
                    timer -= dt;
                    float shake = (Mathf.Repeat(timer * 40f, 2f) < 1f ? 1f : -1f) * ShakeWidth;
                    enemy.Body.MovePosition(hangPosition + new Vector2(shake, 0f));
                    if (timer > 0f) return;
                    state = State.Falling;
                    fallSpeed = 0f;
                    enemy.paysPoints = false;
                    Sfx.Play(Sound.DripFall);
                    break;
                case State.Falling:
                    Fall(dt);
                    break;
                case State.Gone:
                    timer -= dt;
                    if (timer > 0f) return;
                    enemy.Body.position = hangPosition;
                    transform.position = hangPosition;
                    enemy.paysPoints = true;
                    Show(true);
                    state = State.Hanging;
                    break;
            }
        }

        void Fall(float dt)
        {
            fallSpeed = Mathf.Min(fallSpeed + Gravity * dt, MaxFallSpeed);
            float step = fallSpeed * dt;
            // From the drip's lowest point, because a box cast would start out touching the ceiling it hung from.
            Vector2 bottom = enemy.Body.position + new Vector2(enemy.Box.offset.x, 0.01f);
            if (Physics2D.Raycast(bottom, Vector2.down, solidFilter, hits, step + 0.01f) > 0)
            {
                Show(false);
                state = State.Gone;
                timer = regrowSeconds;
                return;
            }
            enemy.Body.MovePosition(enemy.Body.position + Vector2.down * step);
        }

        void Show(bool shown)
        {
            enemy.Box.enabled = shown;
            glyphRenderer.enabled = shown;
        }

        bool LetterIsBelow()
        {
            if (letter == null) letter = FindAnyObjectByType<LetterController>();
            if (letter == null) return false;
            Vector2 feet = letter.transform.position;
            return Mathf.Abs(feet.x - hangPosition.x) < triggerHalfWidth
                && feet.y < hangPosition.y && feet.y > hangPosition.y - triggerDepth;
        }
    }
}
