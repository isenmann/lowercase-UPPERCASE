using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>Fires asterisks in the direction the letter faces. Their size comes from the current form's settings.</summary>
    [RequireComponent(typeof(LetterController), typeof(LetterVisual))]
    public class LetterShooter : MonoBehaviour
    {
        const int SortingOrder = 9;

        [Tooltip("Units per second. The screen is about 17.8 units wide.")]
        public float speed = 18f;
        [Tooltip("Units travelled before the asterisk pops by itself.")]
        public float range = 12f;
        [Tooltip("Minimum seconds between shots.")]
        public float cooldown = 0.2f;
        [Tooltip("Off means the letter can't shoot until it hits a power block.")]
        public bool startsArmed = true;

        public bool IsArmed { get; private set; }

        LetterController letter;
        LetterVisual visual;
        float nextShotTime;

        void Awake()
        {
            letter = GetComponent<LetterController>();
            visual = GetComponent<LetterVisual>();
            IsArmed = startsArmed;
        }

        public void Arm() => IsArmed = true;

        public void Fire()
        {
            if (!IsArmed || letter.IsSlotted || letter.IsFallingOff || Time.time < nextShotTime) return;
            nextShotTime = Time.time + cooldown;

            bool lower = letter.Form == LetterForm.Lowercase;
            var shot = new GameObject("Asterisk").AddComponent<Asterisk>();
            shot.transform.position = letter.BodyCentre;
            shot.Launch((lower ? visual.lowerGlyph : visual.upperGlyph).font, letter.Current.asteriskSize,
                visual.normalColor, SortingOrder, new Vector2(letter.Facing, 0f), speed, range, letter.levelMask);
        }
    }
}
