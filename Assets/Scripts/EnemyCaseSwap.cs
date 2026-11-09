using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Touching it doesn't hurt: it turns Shift around, as if Caps Lock had been pressed, and then vanishes for a
    /// moment so it can't swap back at once. Walk it with an <see cref="EnemyWalk"/> next to it.
    /// An UPPERCASE letter landing on it still kills it.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyCaseSwap : MonoBehaviour, IEnemyTouchRule
    {
        [Tooltip("Seconds it stays gone after a swap.")]
        public float goneSeconds = 1.5f;

        Enemy enemy;
        Renderer glyphRenderer;
        EnemyWalk walk;
        float goneLeft;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            glyphRenderer = enemy.glyph.GetComponent<Renderer>();
        }

        public EnemyTouch Touch(LetterController letter, bool fromAbove)
        {
            if (goneLeft > 0f) return EnemyTouch.Harmless;

            if (enemy.stompable && fromAbove && letter.Form == LetterForm.Uppercase)
            {
                enemy.Kill();
                return EnemyTouch.Stomped;
            }

            letter.SwapCase();
            Sfx.Play(Sound.CaseSwap);
            goneLeft = goneSeconds;
            Show(false);
            return EnemyTouch.Harmless;
        }

        void FixedUpdate()
        {
            if (goneLeft <= 0f) return;
            goneLeft -= Time.fixedDeltaTime;
            if (goneLeft <= 0f) Show(true);
        }

        void Show(bool shown)
        {
            if (walk == null) walk = GetComponent<EnemyWalk>();
            enemy.Box.enabled = shown;
            glyphRenderer.enabled = shown;
            if (walk != null) walk.enabled = shown;
        }
    }
}
