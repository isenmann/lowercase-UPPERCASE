using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A glyph that costs the letter a life when touched. How it moves is up to a movement component next to it,
    /// such as <see cref="EnemyWalk"/>. An UPPERCASE letter landing on it kills it if it is stompable,
    /// and an asterisk kills it if it is shootable. The transform's origin is at its feet.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class Enemy : MonoBehaviour
    {
        public TMP_Text glyph;
        public LayerMask levelMask;
        [Tooltip("An UPPERCASE letter landing on it kills it. Otherwise the landing burns the letter.")]
        public bool stompable = true;
        [Tooltip("An asterisk kills it. Otherwise the asterisk pops on it.")]
        public bool shootable = true;
        [Tooltip("Killing it adds to the score. Off for enemies that keep coming, such as arrows.")]
        public bool paysPoints = true;

        public float TopY => Body.position.y + Box.offset.y + Box.size.y * 0.5f;
        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Box { get; private set; }

        Vector2 spawnPosition;
        IEnemyTouchRule touchRule;
        bool lookedForTouchRule;

        void Awake()
        {
            // A random level adds enemies while the game runs, so this wakes before their fields are set.
            if (glyph == null) glyph = GetComponentInChildren<TMP_Text>();
            if (levelMask == 0) levelMask = LayerMask.GetMask("Level");

            spawnPosition = transform.position;
            Body = GetComponent<Rigidbody2D>();
            Box = GetComponent<BoxCollider2D>();
            Body.bodyType = RigidbodyType2D.Kinematic;
            Body.freezeRotation = true;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Box.isTrigger = true;

            FitCollider();
        }

        /// <summary>
        /// Moves the glyph so its ink stands on the enemy's feet, then fits the trigger to the ink.
        /// Glyphs such as ^ sit high in their line and would otherwise float above the word.
        /// Call it again after changing the glyph.
        /// </summary>
        public void FitCollider()
        {
            glyph.ForceMeshUpdate();
            TMP_CharacterInfo ink = glyph.textInfo.characterInfo[0];
            Vector3 lift = glyph.transform.localPosition;
            float inkBottom = lift.y + ink.bottomLeft.y;
            glyph.transform.localPosition = new Vector3(lift.x, lift.y - inkBottom, lift.z);

            Vector2 min = (Vector2)glyph.transform.localPosition + (Vector2)ink.bottomLeft;
            Vector2 max = (Vector2)glyph.transform.localPosition + (Vector2)ink.topRight;
            Box.size = max - min;
            Box.offset = (min + max) * 0.5f;
        }

        /// <summary>
        /// Decides what the letter touching it leads to. By default an UPPERCASE letter landing on a stompable enemy
        /// kills it, and any other touch hurts. A movement component can take this over with <see cref="IEnemyTouchRule"/>.
        /// </summary>
        public EnemyTouch Touch(LetterController letter, bool fromAbove)
        {
            // Looked up on first touch, because a random level adds the movement component after this one wakes.
            if (!lookedForTouchRule)
            {
                touchRule = GetComponent<IEnemyTouchRule>();
                lookedForTouchRule = true;
            }
            if (touchRule != null) return touchRule.Touch(letter, fromAbove);

            if (stompable && fromAbove && letter.Form == LetterForm.Uppercase)
            {
                Kill();
                return EnemyTouch.Stomped;
            }
            return EnemyTouch.Hurts;
        }

        public void Kill()
        {
            if (!Box.enabled) return;
            Box.enabled = false;
            if (paysPoints) LevelRun.EnemyKilled(spawnPosition);
            glyph.transform.SetParent(null, true);
            glyph.gameObject.AddComponent<FlyingText>().Launch(
                new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 6f)), Random.Range(-600f, 600f));
            Destroy(gameObject);
        }
    }
}
