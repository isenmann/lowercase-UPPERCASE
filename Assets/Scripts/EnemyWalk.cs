using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>Walks an enemy along its word, turning at walls and at the end of the word.</summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyWalk : MonoBehaviour
    {
        const float LedgeProbeStart = 0.05f;
        const float LedgeProbeLength = 0.15f;

        [Tooltip("Units per second.")]
        public float speed = 1.5f;
        [Tooltip("1 starts walking right, -1 left.")]
        public float direction = 1f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        Enemy enemy;
        ContactFilter2D solidFilter;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(enemy.levelMask != 0 ? enemy.levelMask : (LayerMask)LayerMask.GetMask("Level"));
        }

        void FixedUpdate()
        {
            float step = speed * Time.fixedDeltaTime;
            if (MustTurn(step)) direction = -direction;
            enemy.Body.MovePosition(enemy.Body.position + new Vector2(direction * step, 0f));
        }

        bool MustTurn(float step)
        {
            BoxCollider2D box = enemy.Box;
            float reach = box.size.x * 0.5f + step;
            Vector2 centre = enemy.Body.position + box.offset;
            Vector2 ahead = new Vector2(direction, 0f);
            if (Physics2D.Raycast(centre, ahead, solidFilter, hits, reach) > 0) return true;

            Vector2 groundAhead = new Vector2(centre.x + direction * reach, enemy.Body.position.y + LedgeProbeStart);
            return Physics2D.Raycast(groundAhead, Vector2.down, solidFilter, hits, LedgeProbeLength) == 0;
        }
    }
}
