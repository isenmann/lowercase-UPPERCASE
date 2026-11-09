using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Flies back and forth between two points without touching the ground, bobbing on a sine wave.
    /// With no travel it only bobs, which suits guarding a gap. It turns early at a wall.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyWave : MonoBehaviour
    {
        [Tooltip("Units it flies to either side of where it starts. 0 bobs in place.")]
        public float travel = 2f;
        [Tooltip("Units per second along the travel.")]
        public float speed = 1.5f;
        [Tooltip("1 starts flying right, -1 left.")]
        public float direction = 1f;
        [Tooltip("Units up and down from its line of flight.")]
        public float amplitude = 0.4f;
        [Tooltip("Seconds for one bob up and down.")]
        public float period = 1.4f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        Enemy enemy;
        ContactFilter2D solidFilter;
        Vector2 home;
        float x;
        float age;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(enemy.levelMask != 0 ? enemy.levelMask : (LayerMask)LayerMask.GetMask("Level"));
        }

        void Start()
        {
            home = transform.position;
            x = home.x;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            age += dt;

            if (travel > 0f)
            {
                float step = speed * dt;
                bool pastEnd = direction > 0f ? x + step > home.x + travel : x - step < home.x - travel;
                if (pastEnd || HitsWall(step)) direction = -direction;
                x += direction * step;
            }

            float y = home.y + amplitude * Mathf.Sin(age * 2f * Mathf.PI / period);
            enemy.Body.MovePosition(new Vector2(x, y));
        }

        bool HitsWall(float step)
        {
            BoxCollider2D box = enemy.Box;
            Vector2 centre = enemy.Body.position + box.offset;
            return Physics2D.Raycast(centre, new Vector2(direction, 0f), solidFilter, hits, box.size.x * 0.5f + step) > 0;
        }
    }
}
