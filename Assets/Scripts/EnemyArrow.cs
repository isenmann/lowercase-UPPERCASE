using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Flies straight along its line, ignoring the ground, until it meets a solid word or runs out of range.
    /// Fired by an <see cref="EnemyLauncher"/>; it is an enemy like any other, so it can be stomped or shot.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyArrow : MonoBehaviour
    {
        [Tooltip("Units per second.")]
        public float speed = 5f;
        [Tooltip("1 flies right, -1 left.")]
        public float direction = -1f;
        [Tooltip("Units it flies before it disappears.")]
        public float range = 16f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        Enemy enemy;
        ContactFilter2D solidFilter;
        float flown;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(enemy.levelMask != 0 ? enemy.levelMask : (LayerMask)LayerMask.GetMask("Level"));
        }

        void FixedUpdate()
        {
            float step = speed * Time.fixedDeltaTime;
            BoxCollider2D box = enemy.Box;
            Vector2 centre = enemy.Body.position + box.offset;
            bool hitWord = Physics2D.BoxCast(centre, box.size, 0f, new Vector2(direction, 0f), solidFilter, hits, step) > 0;
            flown += step;
            if (hitWord || flown >= range)
            {
                Destroy(gameObject);
                return;
            }

            enemy.Body.MovePosition(enemy.Body.position + new Vector2(direction * step, 0f));
        }
    }
}
