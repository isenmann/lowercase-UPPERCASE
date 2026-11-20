using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Walks an enemy along its word, turning at walls and at the end of the word. A ledge with ink beyond it is
    /// walked off: the enemy sinks to the bottom of the ink and walks along it (see <see cref="EnemyInk"/>).
    /// </summary>
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
            float dt = Time.fixedDeltaTime;
            Vector2 feet = enemy.Body.position;
            float sunk = EnemyInk.SinkStep(enemy, feet, dt, solidFilter);
            float step = speed * dt * (EnemyInk.IsIn(feet) ? EnemyInk.MoveFactor : 1f);
            if (MustTurn(step, sinking: sunk > 0f)) direction = -direction;
            enemy.Body.MovePosition(feet + new Vector2(direction * step, -sunk));
        }

        // While it sinks there is no ground ahead to look for; only walls turn it.
        bool MustTurn(float step, bool sinking)
        {
            BoxCollider2D box = enemy.Box;
            float reach = box.size.x * 0.5f + step;
            Vector2 centre = enemy.Body.position + box.offset;
            Vector2 ahead = new Vector2(direction, 0f);
            if (Physics2D.Raycast(centre, ahead, solidFilter, hits, reach) > 0) return true;

            if (sinking) return false;

            Vector2 groundAhead = new Vector2(centre.x + direction * reach, enemy.Body.position.y + LedgeProbeStart);
            if (Physics2D.Raycast(groundAhead, Vector2.down, solidFilter, hits, LedgeProbeLength) > 0) return false;
            return !EnemyInk.BeyondLedge(new Vector2(groundAhead.x, enemy.Body.position.y));
        }
    }
}
