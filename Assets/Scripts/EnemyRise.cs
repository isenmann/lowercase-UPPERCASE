using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Hides under a hole between loose letters, rises out of it, waits and sinks back, on a timer.
    /// While it is down it can't be seen or touched, so a letter can drop through the hole.
    /// It stays down while the letter stands right next to the hole, which keeps it fair.
    /// The transform's resting position is the risen one: feet on the letters' surface.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyRise : MonoBehaviour
    {
        enum State { Down, Rising, Up, Sinking }

        [Tooltip("Seconds hidden between rises.")]
        public float downSeconds = 1.6f;
        [Tooltip("Seconds standing up out of the hole.")]
        public float upSeconds = 1.2f;
        [Tooltip("Seconds to rise or to sink.")]
        public float moveSeconds = 0.25f;
        [Tooltip("It won't rise while the letter is this close sideways, in units.")]
        public float safeDistance = 0.9f;
        [Tooltip("Seconds to wait before the first rise, so neighbours can rise out of step.")]
        public float startDelay = 0f;

        public bool IsUp => state == State.Up;

        Enemy enemy;
        Renderer glyphRenderer;
        LetterController letter;
        State state = State.Down;
        Vector2 upPosition;
        Vector2 downPosition;
        float timer;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            glyphRenderer = enemy.glyph.GetComponent<Renderer>();
        }

        // Not in Awake: a random level sets the fields only after adding the component, which wakes it at once.
        void Start()
        {
            upPosition = transform.position;
            downPosition = upPosition + Vector2.down * (enemy.Box.size.y + 0.05f);
            timer = startDelay > 0f ? startDelay : downSeconds;
            enemy.Body.position = downPosition;
            transform.position = downPosition;
            Show(false);
        }

        void FixedUpdate()
        {
            timer -= Time.fixedDeltaTime;
            switch (state)
            {
                case State.Down:
                    if (timer > 0f) return;
                    if (LetterIsClose())
                    {
                        timer = 0.2f;
                        return;
                    }
                    Show(true);
                    state = State.Rising;
                    timer = moveSeconds;
                    if (IsOnScreen()) Sfx.Play(Sound.RiserUp);
                    break;
                case State.Rising:
                    MoveBetween(downPosition, upPosition);
                    if (timer <= 0f)
                    {
                        state = State.Up;
                        timer = upSeconds;
                    }
                    break;
                case State.Up:
                    if (timer > 0f) return;
                    state = State.Sinking;
                    timer = moveSeconds;
                    break;
                case State.Sinking:
                    MoveBetween(upPosition, downPosition);
                    if (timer <= 0f)
                    {
                        Show(false);
                        state = State.Down;
                        timer = downSeconds;
                    }
                    break;
            }
        }

        void MoveBetween(Vector2 from, Vector2 to)
        {
            float t = 1f - Mathf.Clamp01(timer / moveSeconds);
            enemy.Body.MovePosition(Vector2.Lerp(from, to, t));
        }

        void Show(bool shown)
        {
            enemy.Box.enabled = shown;
            glyphRenderer.enabled = shown;
        }

        bool LetterIsClose()
        {
            if (letter == null) letter = FindAnyObjectByType<LetterController>();
            return letter != null && Mathf.Abs(letter.transform.position.x - upPosition.x) < safeDistance;
        }

        bool IsOnScreen()
        {
            Camera view = Camera.main;
            return view != null && Mathf.Abs(transform.position.x - view.transform.position.x) < view.orthographicSize * view.aspect;
        }
    }
}
