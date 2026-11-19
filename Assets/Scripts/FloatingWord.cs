using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A word that floats: it rises while a stands on it and sinks while A does, between two heights. Left alone, it
    /// drifts back to where it started and bobs there, so a level can never strand the letter. A slam landing on it
    /// pushes it down with a jolt. The letter rides along through <see cref="Velocity"/>.
    /// </summary>
    // Moves before the letter in each physics step, so a letter riding it reads this step's speed.
    [DefaultExecutionOrder(-60)]
    [RequireComponent(typeof(WordBlock), typeof(Rigidbody2D))]
    public class FloatingWord : MonoBehaviour
    {
        [Tooltip("The highest and lowest world y the word's origin (its baseline) moves between.")]
        public float topY;
        public float bottomY;

        [Header("Speeds, units per second")]
        public float riseSpeed = 1.5f;
        public float sinkSpeed = 2f;
        public float returnSpeed = 0.7f;
        [Tooltip("How quickly the speed changes, units per second squared.")]
        public float acceleration = 12f;
        [Tooltip("Downward speed a slam landing on the word gives it.")]
        public float slamJolt = 7f;

        [Header("Bobbing while empty")]
        public float bobHeight = 0.04f;
        public float bobSeconds = 2.4f;

        /// <summary>Vertical speed this physics step, units per second.</summary>
        public float Velocity { get; private set; }
        public float RestY { get; private set; }
        /// <summary>The world y of the top of the word's collider, where it is in the physics before this step moves it.</summary>
        public float SurfaceY => body.position.y + box.offset.y + box.size.y * 0.5f;
        /// <summary>Who stood on it in the last physics step, if anyone.</summary>
        public bool IsStoodOn => Time.fixedTime - lastStood <= Time.fixedDeltaTime * 1.5f;

        Rigidbody2D body;
        BoxCollider2D box;
        float lastStood = float.NegativeInfinity;
        LetterForm stoodForm;
        float bobClock;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.freezeRotation = true;
            RestY = transform.position.y;
        }

        /// <summary>Called by <see cref="WordBlock.OnStood"/> on every physics step the letter stands on the word.</summary>
        public void Stood(LetterController letter)
        {
            lastStood = Time.fixedTime;
            stoodForm = letter.Form;
        }

        /// <summary>A slam landed on the word. It takes effect in this physics step, which the word has already planned.</summary>
        public void Jolt()
        {
            Velocity = Mathf.Min(Velocity, -slamJolt);
            Move(Time.fixedDeltaTime);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float y = body.position.y;
            float target;

            if (IsStoodOn)
            {
                bobClock = 0f;
                target = stoodForm == LetterForm.Uppercase ? -sinkSpeed : riseSpeed;
            }
            else
            {
                bobClock += dt;
                float aim = RestY + Mathf.Sin(bobClock * 2f * Mathf.PI / bobSeconds) * bobHeight;
                target = Mathf.Clamp((aim - y) * 4f, -returnSpeed, returnSpeed);
            }

            // A jolt is faster than any target, so it eases off at the usual rate.
            Velocity = Mathf.MoveTowards(Velocity, target, acceleration * dt);
            Move(dt);
        }

        /// <summary>Plans this step's move at <see cref="Velocity"/>, stopping at the ends of the travel.</summary>
        void Move(float dt)
        {
            float y = body.position.y;
            float next = Mathf.Clamp(y + Velocity * dt, Mathf.Min(bottomY, topY), Mathf.Max(bottomY, topY));
            Velocity = (next - y) / dt;
            body.MovePosition(new Vector2(body.position.x, next));
        }
    }
}
