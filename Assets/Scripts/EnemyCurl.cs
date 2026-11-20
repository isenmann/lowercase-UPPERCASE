using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Walks as an @. An UPPERCASE letter landing on it curls it into a still o. Touching the still o kicks it,
    /// and the rolling o knocks out other enemies, smashes cracked words and turns at walls, so it can come back
    /// and hurt the letter. Landing on a rolling o stops it again, and a still o uncurls into an @ after a while.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyCurl : MonoBehaviour, IEnemyTouchRule
    {
        const float LedgeProbeStart = 0.05f;
        const float LedgeProbeLength = 0.15f;
        const float KickGraceSeconds = 0.3f;

        enum State { Walking, Still, Rolling }

        [Tooltip("Units per second walking as an @.")]
        public float walkSpeed = 1.5f;
        [Tooltip("Units per second rolling as an o.")]
        public float rollSpeed = 7f;
        [Tooltip("Seconds a still o waits before it uncurls and walks again.")]
        public float uncurlSeconds = 6f;
        [Tooltip("1 starts walking right, -1 left.")]
        public float direction = 1f;
        public string walkingGlyph = "@";
        public string curledGlyph = "o";

        public bool IsRolling => state == State.Rolling;
        public bool IsCurled => state != State.Walking;

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        readonly Collider2D[] touching = new Collider2D[8];
        Enemy enemy;
        ContactFilter2D solidFilter;
        ContactFilter2D enemyFilter;
        State state = State.Walking;
        float stillLeft;
        float graceLeft;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            solidFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            solidFilter.SetLayerMask(enemy.levelMask != 0 ? enemy.levelMask : (LayerMask)LayerMask.GetMask("Level"));
            enemyFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
            enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
        }

        public EnemyTouch Touch(LetterController letter, bool fromAbove)
        {
            bool stomp = fromAbove && letter.Form == LetterForm.Uppercase;
            switch (state)
            {
                case State.Walking:
                    if (!stomp) return EnemyTouch.Hurts;
                    Curl();
                    return EnemyTouch.Stomped;

                case State.Still:
                    Kick(Mathf.Sign(enemy.Body.position.x - letter.transform.position.x));
                    return stomp ? EnemyTouch.Stomped : EnemyTouch.Harmless;

                default:
                    if (stomp)
                    {
                        Stop();
                        return EnemyTouch.Stomped;
                    }
                    return graceLeft > 0f ? EnemyTouch.Harmless : EnemyTouch.Hurts;
            }
        }

        // The letter plays the stomp sound.
        void Curl()
        {
            SetGlyph(curledGlyph);
            Stop();
        }

        void Stop()
        {
            state = State.Still;
            stillLeft = uncurlSeconds;
        }

        void Kick(float towards)
        {
            direction = towards == 0f ? direction : towards;
            state = State.Rolling;
            graceLeft = KickGraceSeconds;
            Sfx.Play(Sound.ShellKick);
        }

        void SetGlyph(string glyphText)
        {
            enemy.glyph.text = glyphText;
            enemy.FitCollider();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            graceLeft -= dt;
            switch (state)
            {
                case State.Walking:
                    Move(walkSpeed * dt, smashes: false);
                    break;
                case State.Still:
                    // A still o curled in the ink goes on sinking.
                    float sunk = EnemyInk.SinkStep(enemy, enemy.Body.position, dt, solidFilter);
                    if (sunk > 0f) enemy.Body.MovePosition(enemy.Body.position + Vector2.down * sunk);
                    stillLeft -= dt;
                    if (stillLeft > 0f) return;
                    SetGlyph(walkingGlyph);
                    state = State.Walking;
                    break;
                case State.Rolling:
                    KnockOutOthers();
                    Move(rollSpeed * dt, smashes: true);
                    break;
            }
        }

        // Walking or rolling, the @ sinks in ink like a walker and goes on along the bottom.
        void Move(float step, bool smashes)
        {
            Vector2 feet = enemy.Body.position;
            float sunk = EnemyInk.SinkStep(enemy, feet, Time.fixedDeltaTime, solidFilter);
            if (EnemyInk.IsIn(feet)) step *= EnemyInk.MoveFactor;
            BoxCollider2D box = enemy.Box;
            float reach = box.size.x * 0.5f + step;
            Vector2 centre = enemy.Body.position + box.offset;
            Vector2 ahead = new Vector2(direction, 0f);

            if (Physics2D.Raycast(centre, ahead, solidFilter, hits, reach) > 0)
            {
                var block = hits[0].collider.GetComponent<WordBlock>();
                if (smashes && block != null && block.kind == WordKind.Cracked) block.Smash();
                else direction = -direction;
            }
            else if (sunk <= 0f)
            {
                Vector2 groundAhead = new Vector2(centre.x + direction * reach, feet.y + LedgeProbeStart);
                if (Physics2D.Raycast(groundAhead, Vector2.down, solidFilter, hits, LedgeProbeLength) == 0
                    && !EnemyInk.BeyondLedge(new Vector2(groundAhead.x, feet.y)))
                    direction = -direction;
            }

            enemy.Body.MovePosition(feet + new Vector2(direction * step, -sunk));
        }

        /// <summary>A rolling o knocks out any enemy it meets that can be stomped or shot. Fire is left alone.</summary>
        void KnockOutOthers()
        {
            BoxCollider2D box = enemy.Box;
            int count = Physics2D.OverlapBox(enemy.Body.position + box.offset, box.size, 0f, enemyFilter, touching);
            for (int i = 0; i < count; i++)
            {
                var other = touching[i].GetComponent<Enemy>();
                if (other == null || other == enemy) continue;
                if (!other.stompable && !other.shootable) continue;
                Sfx.Play(Sound.EnemyShot);
                other.Kill();
            }
        }
    }
}
