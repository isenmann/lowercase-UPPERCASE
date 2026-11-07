using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A glyph that patrols a word, turning at walls and at the end of the word. Touching it costs the letter a life.
    /// One asterisk kills it, and so does an UPPERCASE letter landing on it. The transform's origin is at its feet.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class Enemy : MonoBehaviour
    {
        const float LedgeProbeStart = 0.05f;
        const float LedgeProbeLength = 0.15f;

        public TMP_Text glyph;
        public LayerMask levelMask;
        [Tooltip("Units per second.")]
        public float speed = 1.5f;
        [Tooltip("1 starts walking right, -1 left.")]
        public float direction = 1f;

        public float TopY => rb.position.y + box.offset.y + box.size.y * 0.5f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        Rigidbody2D rb;
        BoxCollider2D box;
        ContactFilter2D solidFilter;
        Vector2 spawnPosition;

        void Awake()
        {
            spawnPosition = transform.position;
            rb = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            box.isTrigger = true;

            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(levelMask);

            FitCollider();
        }

        void FitCollider()
        {
            glyph.ForceMeshUpdate();
            TMP_CharacterInfo ink = glyph.textInfo.characterInfo[0];
            Vector2 min = ink.bottomLeft;
            Vector2 max = ink.topRight;
            box.size = max - min;
            box.offset = (min + max) * 0.5f;
        }

        void FixedUpdate()
        {
            float step = speed * Time.fixedDeltaTime;
            if (MustTurn(step)) direction = -direction;
            rb.MovePosition(rb.position + new Vector2(direction * step, 0f));
        }

        bool MustTurn(float step)
        {
            float reach = box.size.x * 0.5f + step;
            Vector2 centre = rb.position + box.offset;
            Vector2 ahead = new Vector2(direction, 0f);
            if (Physics2D.Raycast(centre, ahead, solidFilter, hits, reach) > 0) return true;

            Vector2 groundAhead = new Vector2(centre.x + direction * reach, rb.position.y + LedgeProbeStart);
            return Physics2D.Raycast(groundAhead, Vector2.down, solidFilter, hits, LedgeProbeLength) == 0;
        }

        public void Kill()
        {
            if (!box.enabled) return;
            box.enabled = false;
            LevelRun.EnemyKilled(spawnPosition);
            glyph.transform.SetParent(null, true);
            glyph.gameObject.AddComponent<FlyingText>().Launch(
                new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 6f)), Random.Range(-600f, 600f));
            Destroy(gameObject);
        }
    }
}
