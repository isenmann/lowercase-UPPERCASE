using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>The blank in the sentence. Slots in the letter when it arrives in the wanted form.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class GoalBlank : MonoBehaviour
    {
        public LetterForm wantedForm = LetterForm.Lowercase;
        public SpriteRenderer glow;

        void OnTriggerStay2D(Collider2D other)
        {
            var letter = other.GetComponent<LetterController>();
            if (letter == null || letter.IsSlotted || letter.Form != wantedForm) return;

            Bounds blank = GetComponent<BoxCollider2D>().bounds;
            letter.SlotInto(new Vector2(blank.center.x, blank.min.y + 0.06f));
            if (glow != null) glow.enabled = true;
        }
    }
}
