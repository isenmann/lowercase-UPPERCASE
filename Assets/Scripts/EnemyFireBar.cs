using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Turns a row of enemy dots around a centre point at a steady speed, like the hand of a clock.
    /// The dots are enemies of their own, placed with their bodies so the physics sees them move.
    /// </summary>
    public class EnemyFireBar : MonoBehaviour
    {
        [Tooltip("The dots, nearest the centre first.")]
        public Enemy[] dots;
        [Tooltip("Units between one dot and the next, and between the centre and the first.")]
        public float spacing = 0.45f;
        [Tooltip("Degrees per second. Negative turns clockwise.")]
        public float degreesPerSecond = -90f;
        [Tooltip("Where the bar points at the start, in degrees from pointing right.")]
        public float startAngle = 90f;

        float angle;

        void Start()
        {
            angle = startAngle;
            Place(teleport: true);
        }

        void FixedUpdate()
        {
            angle += degreesPerSecond * Time.fixedDeltaTime;
            Place(teleport: false);
        }

        void Place(bool teleport)
        {
            Vector2 centre = transform.position;
            Vector2 along = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            for (int i = 0; i < dots.Length; i++)
            {
                Enemy dot = dots[i];
                if (dot == null) continue;
                // Each dot's ink centre goes on the bar, not its feet.
                Vector2 inkCentre = dot.Box.offset;
                Vector2 target = centre + along * (spacing * (i + 1)) - inkCentre;
                if (teleport)
                {
                    dot.Body.position = target;
                    dot.transform.position = target;
                }
                else
                {
                    dot.Body.MovePosition(target);
                }
            }
        }
    }
}
