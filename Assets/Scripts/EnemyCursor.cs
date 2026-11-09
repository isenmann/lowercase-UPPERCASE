using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A tall blinking text cursor that backspaces through a stretch of the level. It waits hidden and harmless at
    /// the start of the stretch, so the letter can walk in past it, and appears behind the letter once the letter is
    /// a little way in. Then it sweeps right at a steady speed, erasing every word it passes, so there is no way back.
    /// Touching it costs a life, and nothing can stop it. It stops and fades at the end of the stretch.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyCursor : MonoBehaviour
    {
        const float BlinkSeconds = 0.5f;
        const float BlinkAlpha = 0.45f;
        const float TraceAlpha = 0.12f;

        [Tooltip("Units per second. The letter runs at 7.")]
        public float speed = 3.5f;
        [Tooltip("World x where the stretch ends. The cursor stops here.")]
        public float endX = 10f;
        [Tooltip("It appears and starts once the letter is this far past it, in units.")]
        public float startLead = 2.5f;

        public bool IsRunning { get; private set; }
        public bool IsDone { get; private set; }

        // Each word with its right edge, read once at the start: a disabled collider reports empty bounds.
        readonly List<KeyValuePair<float, WordBlock>> toErase = new List<KeyValuePair<float, WordBlock>>();
        Enemy enemy;
        LetterController letter;
        float age;

        void Awake() => enemy = GetComponent<Enemy>();

        /// <summary>Collects the words that start inside the stretch, ordered by where they end.</summary>
        void Start()
        {
            float startX = transform.position.x;
            foreach (WordBlock word in FindObjectsByType<WordBlock>())
            {
                Bounds bounds = word.GetComponent<BoxCollider2D>().bounds;
                // The cursor stands a little way into the first word, so that word counts too.
                if (bounds.min.x >= startX - 0.5f && bounds.min.x < endX)
                    toErase.Add(new KeyValuePair<float, WordBlock>(bounds.max.x, word));
            }
            toErase.Sort((a, b) => a.Key.CompareTo(b.Key));
            Show(false);
        }

        void FixedUpdate()
        {
            if (IsDone) return;
            if (letter == null) letter = FindAnyObjectByType<LetterController>();

            if (!IsRunning)
            {
                if (letter == null || letter.transform.position.x < transform.position.x + startLead) return;
                IsRunning = true;
                Show(true);
            }

            float x = Mathf.Min(enemy.Body.position.x + speed * Time.fixedDeltaTime, endX);
            enemy.Body.MovePosition(new Vector2(x, enemy.Body.position.y));
            EraseUpTo(x);

            if (x >= endX)
            {
                IsDone = true;
                Show(false);
            }
        }

        void Show(bool shown)
        {
            enemy.Box.enabled = shown;
            enemy.glyph.alpha = shown ? 1f : 0f;
        }

        void Update()
        {
            if (!IsRunning || IsDone) return;
            age += Time.deltaTime;
            enemy.glyph.alpha = Mathf.Repeat(age, BlinkSeconds * 2f) < BlinkSeconds ? 1f : BlinkAlpha;
        }

        /// <summary>A word goes once the cursor has passed all of it, so it never drops a letter still ahead of it.</summary>
        void EraseUpTo(float x)
        {
            while (toErase.Count > 0 && toErase[0].Key < x)
            {
                WordBlock word = toErase[0].Value;
                toErase.RemoveAt(0);
                if (word == null || word.IsErased) continue;
                word.Erase(TraceAlpha);
                Sfx.Play(Sound.CursorErase);
            }
        }
    }
}
