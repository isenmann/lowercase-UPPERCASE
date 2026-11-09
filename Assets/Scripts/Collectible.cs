using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A gold # to pick up for points. Once taken it stays taken for the rest of the level, even through the reload
    /// that follows a death, so dying can't earn it twice.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Collectible : MonoBehaviour
    {
        const float BobUnits = 0.05f;
        const float BobSpeed = 3f;
        const float BobPhasePerUnit = 0.8f; // A row ripples instead of moving as one.
        const float PopSeconds = 0.25f;
        const float PopScale = 1.8f;
        const float PopRiseUnits = 0.35f;

        public TextMeshPro glyph;

        Vector2 home;

        /// <summary>Where it was placed. It bobs and rises away from here, but is scored by this spot.</summary>
        public Vector2 Home => home;
        Vector3 inkOffset;
        float popAge;

        public bool IsTaken { get; private set; }

        void Awake()
        {
            // A random level adds these while the game runs, so this wakes before the glyph is assigned.
            if (glyph == null) glyph = GetComponentInChildren<TextMeshPro>();
            home = transform.position;
            CentreOnInk();
        }

        // In Start, after every Awake, because a random level is built before the flow wakes and clears what the last
        // attempt took.
        void Start()
        {
            if (LevelRun.IsCollected(home)) gameObject.SetActive(false);
        }

        /// <summary>A # sits above the baseline, so the glyph is moved until its ink is on the trigger's centre.</summary>
        public void CentreOnInk()
        {
            glyph.ForceMeshUpdate();
            TMP_CharacterInfo ink = glyph.textInfo.characterInfo[0];
            inkOffset = -(Vector3)((Vector2)(ink.bottomLeft + ink.topRight) * 0.5f);
            glyph.transform.localPosition = inkOffset;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (IsTaken || other.GetComponent<LetterController>() == null) return;
            IsTaken = true;
            Sfx.Play(Sound.CollectHash);
            LevelRun.Collect(home);
        }

        void Update()
        {
            if (IsTaken)
            {
                Pop();
                return;
            }

            float bob = Mathf.Sin(Time.time * BobSpeed + home.x * BobPhasePerUnit) * BobUnits;
            glyph.transform.localPosition = inkOffset + Vector3.up * bob;
        }

        void Pop()
        {
            popAge += Time.deltaTime;
            float t = Mathf.Clamp01(popAge / PopSeconds);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, PopScale, t);
            transform.position = home + Vector2.up * (PopRiseUnits * t);
            glyph.alpha = 1f - t;
            if (t >= 1f) gameObject.SetActive(false);
        }
    }
}
