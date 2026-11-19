using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A heavy word that only A can push. A walking into its side on the ground slides it along, slower than A walks;
    /// to a it is a wall. It stops at walls, falls off edges, and comes back to where it started if it falls out of the
    /// level, so a level can never lose it for good. It is too tall for A's hop, so only a can climb onto it.
    /// </summary>
    // Moves before the letter in each physics step, as a floating word does.
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(WordBlock), typeof(Rigidbody2D))]
    public class HeavyWord : MonoBehaviour
    {
        [Tooltip("How fast A pushes it, units per second.")]
        public float pushSpeed = 1.8f;
        public float gravity = 30f;
        public float maxFallSpeed = 15f;
        [Tooltip("How far under where it started it may fall before it comes back, units.")]
        public float lostBelow = 12f;
        public LayerMask levelMask;
        [Tooltip("Seconds between scrapes while it keeps sliding.")]
        public float scrapeSeconds = 0.4f;

        /// <summary>Whether A pushed it in the last physics step.</summary>
        public bool IsPushed => Time.fixedTime - lastPushed <= Time.fixedDeltaTime * 1.5f;
        public Vector2 StartPosition { get; private set; }

        const float Skin = 0.01f;

        Rigidbody2D body;
        ContactFilter2D filter;
        readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        float lastPushed = float.NegativeInfinity;
        float pushDirection;
        float fallSpeed;
        float nextScrape;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.freezeRotation = true;
            filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(levelMask.value != 0 ? levelMask : LayerMask.GetMask("Level"));
            StartPosition = body.position;
        }

        /// <summary>Called by the letter on every physics step A walks into its side. It moves on the next step.</summary>
        public void Push(float direction)
        {
            lastPushed = Time.fixedTime;
            pushDirection = Mathf.Sign(direction);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 at = body.position;

            bool sliding = false;
            if (IsPushed && pushDirection != 0f)
            {
                float step = Free(new Vector2(pushDirection, 0f), pushSpeed * dt);
                at.x += pushDirection * step;
                sliding = step > 0.0001f;
            }

            // It scrapes as soon as it starts sliding, and again while it keeps going; against a wall it is quiet.
            if (!sliding) nextScrape = 0f;
            else if (Time.fixedTime >= nextScrape)
            {
                Sfx.Play(Sound.Push);
                nextScrape = Time.fixedTime + scrapeSeconds;
            }

            // Falls when nothing holds it up, and lands on whatever it falls onto.
            float below = Free(Vector2.down, Mathf.Max(fallSpeed * dt, Skin * 2f));
            if (below > Skin)
            {
                fallSpeed = Mathf.Min(fallSpeed + gravity * dt, maxFallSpeed);
                at.y -= Mathf.Min(below, fallSpeed * dt);
            }
            else
            {
                fallSpeed = 0f;
            }

            if (at.y < StartPosition.y - lostBelow)
            {
                at = StartPosition;
                fallSpeed = 0f;
            }
            body.MovePosition(at);
        }

        /// <summary>How far it can move this way before it touches something, up to the distance asked for.</summary>
        float Free(Vector2 direction, float distance)
        {
            int count = body.Cast(direction, filter, hits, distance + Skin);
            float free = distance;
            // Only what faces it counts: sliding along the floor it rests on, the floor is touched but not in the way.
            for (int i = 0; i < count; i++)
                if (Vector2.Dot(hits[i].normal, -direction) > 0.5f)
                    free = Mathf.Min(free, Mathf.Max(0f, hits[i].distance - Skin));
            return free;
        }
    }
}
