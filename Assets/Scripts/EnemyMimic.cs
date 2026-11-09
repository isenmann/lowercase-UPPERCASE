using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Takes on the letter's case a moment after the letter does: b while the letter is lowercase, B while it is
    /// UPPERCASE. Only UPPERCASE stomps, but a B can't be stomped, so the letter has to switch to A in the air
    /// just before landing, while the mimic is still a b. Asterisks kill it either way.
    /// Walk it with an <see cref="EnemyWalk"/> next to it.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class EnemyMimic : MonoBehaviour, IEnemyTouchRule
    {
        [Tooltip("Seconds it takes to copy the letter's case.")]
        public float delay = 0.2f;
        public string lowerGlyph = "b";
        public string upperGlyph = "B";
        [Tooltip("The B is drawn this much larger than the b, since in a monospaced font they stand almost as tall.")]
        public float upperScale = 1.3f;

        public bool IsUpper { get; private set; }

        Enemy enemy;
        LetterController letter;
        float changeAt = -1f;
        float lowerFontSize;

        void Awake()
        {
            enemy = GetComponent<Enemy>();
            lowerFontSize = enemy.glyph.fontSize;
        }

        public EnemyTouch Touch(LetterController toucher, bool fromAbove)
        {
            if (!IsUpper && fromAbove && toucher.Form == LetterForm.Uppercase)
            {
                enemy.Kill();
                return EnemyTouch.Stomped;
            }
            return EnemyTouch.Hurts;
        }

        void FixedUpdate()
        {
            if (letter == null) letter = FindAnyObjectByType<LetterController>();
            if (letter == null) return;

            bool letterIsUpper = letter.Form == LetterForm.Uppercase;
            if (letterIsUpper == IsUpper)
            {
                changeAt = -1f;
                return;
            }

            if (changeAt < 0f) changeAt = Time.time + delay;
            if (Time.time < changeAt) return;

            IsUpper = letterIsUpper;
            changeAt = -1f;
            enemy.glyph.text = IsUpper ? upperGlyph : lowerGlyph;
            enemy.glyph.fontSize = IsUpper ? lowerFontSize * upperScale : lowerFontSize;
            enemy.FitCollider();
        }
    }
}
