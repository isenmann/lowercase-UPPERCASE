using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Hops straight up from its word on a fixed beat, and can drift along the word while in the air,
    /// turning at walls and at the end of the word like a walker. It squashes a little on landing so the beat reads.
    /// In ink it sinks to the bottom and hops on from there, lower and slower (see <see cref="EnemyInk"/>).
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyHop : MonoBehaviour
    {
        const float Gravity = 30f;
        const float LedgeProbeStart = 0.05f;
        const float LedgeProbeLength = 0.15f;
        const float SquashSeconds = 0.15f;
        const float SquashScale = 0.7f;

        [Tooltip("Units above the word at the top of a hop.")]
        public float hopHeight = 1.2f;
        [Tooltip("Seconds standing on the word between hops.")]
        public float restSeconds = 0.6f;
        [Tooltip("Units per second sideways while in the air. 0 hops in place.")]
        public float sideSpeed = 0f;
        [Tooltip("1 starts drifting right, -1 left.")]
        public float direction = 1f;
        [Tooltip("Seconds to wait before the first hop, so neighbours can hop out of step.")]
        public float startDelay = 0f;

        public bool IsAirborne => height > 0f || verticalSpeed > 0f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        Enemy enemy;
        ContactFilter2D solidFilter;
        float groundY;
        float height;
        float verticalSpeed;
        float restLeft;
        float squashAge = SquashSeconds;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(enemy.levelMask != 0 ? enemy.levelMask : (LayerMask)LayerMask.GetMask("Level"));
        }

        // Not in Awake: a random level sets the fields only after adding the component, which wakes it at once.
        void Start()
        {
            groundY = transform.position.y;
            restLeft = startDelay > 0f ? startDelay : restSeconds;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 position = enemy.Body.position;

            // The word it hops from sinks with it through ink, down to whatever is under it.
            var feet = new Vector2(position.x, groundY);
            float sunk = EnemyInk.SinkStep(enemy, feet, dt, solidFilter);
            groundY -= sunk;
            bool inInk = EnemyInk.IsIn(feet);

            if (!IsAirborne)
            {
                restLeft -= dt;
                float hop = hopHeight * (inInk ? EnemyInk.MoveFactor : 1f);
                if (restLeft <= 0f) verticalSpeed = Mathf.Sqrt(2f * Gravity * hop);
            }

            if (IsAirborne)
            {
                verticalSpeed -= Gravity * dt;
                height += verticalSpeed * dt;
                if (height <= 0f)
                {
                    height = 0f;
                    verticalSpeed = 0f;
                    restLeft = restSeconds;
                    squashAge = 0f;
                }
                else if (sideSpeed > 0f)
                {
                    float step = sideSpeed * dt * (inInk ? EnemyInk.MoveFactor : 1f);
                    if (MustTurn(position, step, sinking: sunk > 0f)) direction = -direction;
                    position.x += direction * step;
                }
            }

            enemy.Body.MovePosition(new Vector2(position.x, groundY + height));
        }

        void Update()
        {
            if (squashAge >= SquashSeconds) return;
            squashAge += Time.deltaTime;
            float t = Mathf.Clamp01(squashAge / SquashSeconds);
            float y = Mathf.Lerp(SquashScale, 1f, t);
            enemy.glyph.transform.localScale = new Vector3(2f - y, y, 1f);
        }

        /// <summary>Like a walker, but the ledge is looked for at the word's height, since the hopper is usually above it.</summary>
        bool MustTurn(Vector2 position, float step, bool sinking)
        {
            BoxCollider2D box = enemy.Box;
            float reach = box.size.x * 0.5f + step;
            Vector2 centre = position + box.offset;
            Vector2 ahead = new Vector2(direction, 0f);
            if (Physics2D.Raycast(centre, ahead, solidFilter, hits, reach) > 0) return true;

            if (sinking) return false;

            Vector2 groundAhead = new Vector2(centre.x + direction * reach, groundY + LedgeProbeStart);
            if (Physics2D.Raycast(groundAhead, Vector2.down, solidFilter, hits, LedgeProbeLength) > 0) return false;
            return !EnemyInk.BeyondLedge(new Vector2(groundAhead.x, groundY));
        }
    }
}
