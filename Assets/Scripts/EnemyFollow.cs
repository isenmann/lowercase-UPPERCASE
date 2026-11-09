using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Drifts toward the letter through words, slower than the letter can run, so standing still is what gets you
    /// caught. It wakes only once the letter comes within range, so it doesn't wait at the letter's start.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyFollow : MonoBehaviour
    {
        [Tooltip("Units per second at most. The letter runs at 7.")]
        public float maxSpeed = 2f;
        [Tooltip("Units per second added each second while turning toward the letter.")]
        public float acceleration = 3f;
        [Tooltip("It wakes once the letter is this close, in units.")]
        public float wakeDistance = 8f;

        public bool IsAwake { get; private set; }

        Enemy enemy;
        LetterController letter;
        Vector2 velocity;

        void Awake() => enemy = GetComponent<Enemy>();

        void FixedUpdate()
        {
            if (letter == null) letter = FindAnyObjectByType<LetterController>();
            if (letter == null || letter.IsFallingOff) return;

            // Aims at the middle of the letter, not its feet.
            Vector2 target = (Vector2)letter.transform.position + Vector2.up * 0.3f;
            Vector2 centre = enemy.Body.position + enemy.Box.offset;
            Vector2 toLetter = target - centre;
            if (!IsAwake)
            {
                if (toLetter.magnitude > wakeDistance) return;
                IsAwake = true;
            }

            float dt = Time.fixedDeltaTime;
            velocity = Vector2.MoveTowards(velocity, toLetter.normalized * maxSpeed, acceleration * dt);
            enemy.Body.MovePosition(enemy.Body.position + velocity * dt);
        }
    }
}
