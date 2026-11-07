using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>
    /// Forms a level out of letters rising from below, then shows the player. When the letter slots into the
    /// exit, the level's letters fall away, the screen goes black and the next scene loads. Time is stopped
    /// during both, so enemies and physics wait, and only the text meshes move: the colliders stay in place.
    /// </summary>
    public class LevelTransition : MonoBehaviour
    {
        const float FadeInSeconds = 0.3f;
        const float RiseStagger = 0.9f;
        const float RiseSeconds = 0.7f;
        const float AppearSeconds = 0.35f;
        const float SlotBeatSeconds = 0.6f;
        const float CollapseStagger = 0.7f;
        const float FallGravity = 26f;
        const float FadeOutDelay = 1.1f;
        const float FadeOutSeconds = 0.4f;
        const float BlackHoldSeconds = 0.15f;

        /// <summary>A restart after dying uses this, so the level isn't assembled again every time.</summary>
        public static bool SkipNextIntro;

        public Transform level;
        public LetterController letter;
        public SpriteRenderer fade;
        [Tooltip("The scene that loads after this level collapses.")]
        public string nextScene;

        public bool IsPlaying { get; private set; }

        LetterVisual visual;

        void Awake()
        {
            visual = letter.GetComponent<LetterVisual>();
            if (SkipNextIntro) return;

            IsPlaying = true;
            visual.Hidden = true;
            Time.timeScale = 0f;
        }

        void Start()
        {
            if (SkipNextIntro)
            {
                SkipNextIntro = false;
                SetFade(0f);
                return;
            }
            StartCoroutine(Intro());
        }

        void OnEnable() => letter.Slotted += OnSlotted;
        void OnDisable() => letter.Slotted -= OnSlotted;
        void OnDestroy() => Time.timeScale = 1f;

        void OnSlotted()
        {
            if (IsPlaying) return;
            IsPlaying = true;
            StartCoroutine(Collapse());
        }

        void SetFade(float alpha)
        {
            Color color = fade.color;
            color.a = alpha;
            fade.color = color;
        }

        IEnumerator Intro()
        {
            SetFade(1f);
            GoalBlank goal = level.GetComponentInChildren<GoalBlank>();
            Vector3 goalRest = goal != null ? goal.transform.position : Vector3.zero;

            Camera view = Camera.main;
            float bottom = view.transform.position.y - view.orthographicSize;
            var field = new GlyphField(level.GetComponentsInChildren<TMP_Text>(), view);
            foreach (GlyphField.Glyph glyph in field.Glyphs)
            {
                glyph.Delay = field.ScreenFraction(glyph.WorldCentre.x) * 0.6f + UnityEngine.Random.value * 0.3f;
                glyph.Travel = bottom - 1f - glyph.WorldCentre.y;
                glyph.Spin = UnityEngine.Random.Range(-90f, 90f);
                glyph.Drift = UnityEngine.Random.Range(-1.2f, 1.2f);
            }

            Func<GlyphField.Glyph, float, (Vector2, float)> rise = (glyph, time) =>
            {
                float left = RemainingRise(time - glyph.Delay);
                return (new Vector2(glyph.Drift * left, glyph.Travel * left), glyph.Spin * left);
            };

            field.Apply(rise, 0f);
            float goalTravel = goal != null ? bottom - 1f - goalRest.y : 0f;
            if (goal != null) goal.transform.position = goalRest + Vector3.up * goalTravel;

            for (float t = 0f; t < FadeInSeconds; t += Time.unscaledDeltaTime)
            {
                SetFade(1f - t / FadeInSeconds);
                yield return null;
            }
            SetFade(0f);

            for (float t = 0f; t < RiseStagger + RiseSeconds; t += Time.unscaledDeltaTime)
            {
                field.Apply(rise, t);
                if (goal != null)
                    goal.transform.position = goalRest + Vector3.up * (goalTravel * RemainingRise(t - RiseStagger));
                yield return null;
            }

            field.Restore();
            if (goal != null) goal.transform.position = goalRest;

            visual.Appear();
            yield return new WaitForSecondsRealtime(AppearSeconds);

            Time.timeScale = 1f;
            IsPlaying = false;
        }

        /// <summary>How much of a glyph's rise is left, starting fast and settling gently.</summary>
        static float RemainingRise(float time)
        {
            float left = 1f - Mathf.Clamp01(time / RiseSeconds);
            return left * left * left;
        }

        IEnumerator Collapse()
        {
            yield return new WaitForSecondsRealtime(SlotBeatSeconds);
            Time.timeScale = 0f;

            GoalBlank goal = level.GetComponentInChildren<GoalBlank>();
            Vector3 goalRest = goal != null ? goal.transform.position : Vector3.zero;

            var texts = new List<TMP_Text>(level.GetComponentsInChildren<TMP_Text>());
            texts.Add(letter.Form == LetterForm.Lowercase ? visual.lowerGlyph : visual.upperGlyph);

            // It spreads outward from the finished word, so the letter's arrival seems to bring the level down.
            Camera view = Camera.main;
            Vector2 origin = letter.transform.position;
            float reach = view.orthographicSize * view.aspect * 2f;
            var field = new GlyphField(texts, view);
            foreach (GlyphField.Glyph glyph in field.Glyphs)
            {
                float distance = Vector2.Distance(glyph.WorldCentre, origin);
                glyph.Delay = Mathf.Clamp01(distance / reach) * CollapseStagger * 0.7f
                    + UnityEngine.Random.value * CollapseStagger * 0.3f;
                glyph.Spin = UnityEngine.Random.Range(-300f, 300f);
                glyph.Drift = UnityEngine.Random.Range(-1.5f, 1.5f);
            }

            Func<GlyphField.Glyph, float, (Vector2, float)> fall = (glyph, time) =>
            {
                float falling = Mathf.Max(0f, time - glyph.Delay);
                return (new Vector2(glyph.Drift * falling, -0.5f * FallGravity * falling * falling), glyph.Spin * falling);
            };

            float end = FadeOutDelay + FadeOutSeconds;
            for (float t = 0f; t < end; t += Time.unscaledDeltaTime)
            {
                field.Apply(fall, t);
                if (goal != null)
                {
                    float falling = Mathf.Max(0f, t - CollapseStagger * 0.5f);
                    goal.transform.position = goalRest + Vector3.down * (0.5f * FallGravity * falling * falling);
                }
                SetFade(Mathf.Clamp01((t - FadeOutDelay) / FadeOutSeconds));
                yield return null;
            }

            SetFade(1f);
            yield return new WaitForSecondsRealtime(BlackHoldSeconds);
            SceneManager.LoadScene(nextScene);
        }

        /// <summary>The characters currently on screen, each posed separately by moving its four mesh vertices.</summary>
        sealed class GlyphField
        {
            public sealed class Glyph
            {
                public int MaterialIndex;
                public int VertexIndex;
                public Vector3 LocalCentre;
                public Vector2 WorldCentre;
                public float Delay;
                public float Travel;
                public float Spin;
                public float Drift;
            }

            sealed class Entry
            {
                public TMP_Text Text;
                public TMP_MeshInfo[] Rest;
                public readonly List<Glyph> Glyphs = new List<Glyph>();
            }

            const float OffscreenMargin = 1f;

            public readonly List<Glyph> Glyphs = new List<Glyph>();

            readonly List<Entry> entries = new List<Entry>();
            readonly float minX;
            readonly float maxX;

            public GlyphField(IEnumerable<TMP_Text> texts, Camera view)
            {
                float halfWidth = view.orthographicSize * view.aspect + OffscreenMargin;
                minX = view.transform.position.x - halfWidth;
                maxX = view.transform.position.x + halfWidth;
                foreach (TMP_Text text in texts) Add(text);
            }

            public float ScreenFraction(float worldX) => Mathf.InverseLerp(minX, maxX, worldX);

            void Add(TMP_Text text)
            {
                if (!text.isActiveAndEnabled) return;

                text.ForceMeshUpdate();
                TMP_TextInfo info = text.textInfo;
                var entry = new Entry { Text = text, Rest = info.CopyMeshInfoVertexData() };

                for (int i = 0; i < info.characterCount; i++)
                {
                    TMP_CharacterInfo character = info.characterInfo[i];
                    if (!character.isVisible) continue;

                    Vector3[] rest = entry.Rest[character.materialReferenceIndex].vertices;
                    Vector3 centre = (rest[character.vertexIndex] + rest[character.vertexIndex + 2]) * 0.5f;
                    Vector2 world = text.transform.TransformPoint(centre);
                    if (world.x < minX || world.x > maxX) continue;

                    var glyph = new Glyph
                    {
                        MaterialIndex = character.materialReferenceIndex,
                        VertexIndex = character.vertexIndex,
                        LocalCentre = centre,
                        WorldCentre = world
                    };
                    entry.Glyphs.Add(glyph);
                    Glyphs.Add(glyph);
                }

                if (entry.Glyphs.Count > 0) entries.Add(entry);
            }

            public void Apply(Func<Glyph, float, (Vector2 offset, float degrees)> pose, float time)
            {
                foreach (Entry entry in entries)
                {
                    TMP_MeshInfo[] live = entry.Text.textInfo.meshInfo;
                    foreach (Glyph glyph in entry.Glyphs)
                    {
                        (Vector2 offset, float degrees) = pose(glyph, time);
                        Quaternion rotation = Quaternion.Euler(0f, 0f, degrees);
                        Vector3[] rest = entry.Rest[glyph.MaterialIndex].vertices;
                        Vector3[] vertices = live[glyph.MaterialIndex].vertices;
                        for (int corner = 0; corner < 4; corner++)
                        {
                            int index = glyph.VertexIndex + corner;
                            vertices[index] = glyph.LocalCentre + rotation * (rest[index] - glyph.LocalCentre)
                                + (Vector3)offset;
                        }
                    }
                    entry.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                }
            }

            public void Restore()
            {
                foreach (Entry entry in entries) entry.Text.ForceMeshUpdate();
            }
        }
    }
}
