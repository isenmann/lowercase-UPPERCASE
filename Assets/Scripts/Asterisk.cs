using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>A flying asterisk. It spins along a straight line, kills the first enemy it meets, and pops on that, on a solid word, or at the end of its range.</summary>
    public class Asterisk : MonoBehaviour
    {
        const float SpinDegreesPerSecond = 540f;
        const float PopSeconds = 0.1f;
        const float PopScale = 1.6f;
        const float MinRadius = 0.02f;

        public float Radius { get; private set; }
        public bool HasHit { get; private set; }

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        ContactFilter2D solidFilter;
        ContactFilter2D enemyFilter;
        TextMeshPro label;
        Vector2 direction;
        float speed;
        float remainingRange;
        float popAge;

        public void Launch(TMP_FontAsset font, float fontSize, Color color, int sortingOrder, Vector2 flightDirection,
            float flightSpeed, float range, LayerMask levelMask)
        {
            direction = flightDirection;
            speed = flightSpeed;
            remainingRange = range;
            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(levelMask);
            // Enemies are triggers, so they need their own filter: a solid-only one would miss them,
            // and including triggers for the level would stop asterisks on commas and lifts.
            enemyFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
            enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));

            var glyph = new GameObject("Glyph *");
            glyph.transform.SetParent(transform, false);
            label = glyph.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(4f, 0f);
            label.sortingOrder = sortingOrder;
            label.text = "*";
            CentreOnInk();
        }

        /// <summary>An asterisk sits high in its line, so the glyph is moved until its ink is on the spin and collision centre.</summary>
        void CentreOnInk()
        {
            label.ForceMeshUpdate();
            TMP_CharacterInfo ink = label.textInfo.characterInfo[0];
            Vector2 min = ink.bottomLeft;
            Vector2 max = ink.topRight;
            label.transform.localPosition = -(min + max) * 0.5f;
            Radius = Mathf.Max(Mathf.Min(max.x - min.x, max.y - min.y) * 0.5f, MinRadius);
        }

        void Update()
        {
            if (HasHit) Pop();
            else Fly();
        }

        void Fly()
        {
            float step = Mathf.Min(speed * Time.deltaTime, remainingRange);
            bool hitWord = Physics2D.CircleCast(transform.position, Radius, direction, solidFilter, hits, step) > 0;
            Vector2 stopPoint = hits[0].centroid;
            float reach = hitWord ? hits[0].distance : step;

            if (Physics2D.CircleCast(transform.position, Radius, direction, enemyFilter, hits, reach) > 0)
            {
                var enemy = hits[0].collider.GetComponent<Enemy>();
                if (enemy.shootable)
                {
                    Sfx.Play(Sound.EnemyShot);
                    enemy.Kill();
                }
                else
                {
                    Sfx.Play(Sound.AsteriskBlocked);
                }
                transform.position = hits[0].centroid;
                HasHit = true;
                return;
            }

            if (hitWord)
            {
                transform.position = stopPoint;
                Sfx.Play(Sound.AsteriskPop);
                HasHit = true;
                return;
            }

            transform.position += (Vector3)(direction * step);
            transform.Rotate(0f, 0f, -Mathf.Sign(direction.x) * SpinDegreesPerSecond * Time.deltaTime);
            remainingRange -= step;
            if (remainingRange <= 0f)
            {
                Sfx.Play(Sound.AsteriskPop);
                HasHit = true;
            }
        }

        void Pop()
        {
            popAge += Time.deltaTime;
            float t = popAge / PopSeconds;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, PopScale, t);
            label.alpha = 1f - t;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
