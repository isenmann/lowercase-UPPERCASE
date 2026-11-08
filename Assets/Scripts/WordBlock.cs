using System.Collections;
using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    // New kinds go at the end: scenes store these as numbers.
    public enum WordKind { Solid, Ice, Cracked, Spring, Hot, Sticky, Up, Comma, Hyphen, Period, Door, Power }

    /// <summary>
    /// A typed word that is also level geometry. A one-line word gets one flat collider from baseline
    /// to x-height, so the letter never catches on the uneven tops of individual glyphs.
    /// Several lines, or punctuation, use the tight box around the glyphs themselves.
    /// The collider is refitted on entering Play mode, so retyping a word in the Inspector just works.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro), typeof(BoxCollider2D))]
    public class WordBlock : MonoBehaviour
    {
        const float OpenDoorAlpha = 0.2f;

        public WordKind kind = WordKind.Solid;

        [Header("Surface")]
        [Tooltip("Multiplies acceleration and braking for a letter standing on this word. Low values slide.")]
        public float grip = 1f;
        [Tooltip("Multiplies run speed for a letter standing on this word.")]
        public float moveFactor = 1f;
        [Tooltip("Multiplies jump speed for a letter standing on this word.")]
        public float jumpFactor = 1f;

        [Header("Punctuation")]
        [Tooltip("The width of the collider. Zero uses the width of the glyph.")]
        public float padWidth;
        [Tooltip("The height of the collider. Zero uses the height of the glyph.")]
        public float padHeight;

        [Header("Hot")]
        [Tooltip("Seconds a letter can stand on this word before it burns.")]
        public float burnDelay = 0.35f;

        [Header("Up")]
        [Tooltip("How far above the baseline the lift reaches.")]
        public float liftHeight = 4f;
        public float liftSpeedLower = 6f;
        public float liftSpeedUpper = 2.5f;

        [Header("Period and door")]
        public WordBlock linkedDoor;
        public Color pressedColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        [Tooltip("Seconds a door stays open after the last press.")]
        public float doorOpenSeconds = 2.5f;

        [Header("Breaking")]
        public Color impactColor = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        public TMP_FontAsset impactFont;
        public string impactText = "CRACK";
        [Tooltip("Seconds until a broken word returns. Zero means it stays broken until a restart.")]
        public float respawnSeconds;

        [Header("Power block")]
        public string spentText = "[ ]";
        public Color spentColor = new Color32(0x6B, 0x71, 0x80, 0xFF);
        public Color powerColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);

        public bool IsSpent { get; private set; }
        public Vector2 WorldCentre => transform.TransformPoint(box.offset);

        TextMeshPro text;
        BoxCollider2D box;
        Color restingColor;
        float standingTime;
        float lastStoodTime = -1f;
        float doorTimer;
        float pressedUntil;

        void Awake()
        {
            FitCollider();
            restingColor = text.color;
        }

        [ContextMenu("Fit Collider")]
        public void FitCollider()
        {
            text = GetComponent<TextMeshPro>();
            box = GetComponent<BoxCollider2D>();
            text.ForceMeshUpdate();
            box.isTrigger = kind == WordKind.Comma || kind == WordKind.Up;

            float left;
            float right;
            float bottom;
            float top;

            if (text.text.Contains("\n") || IsPunctuation)
            {
                MeasureInk(out left, out right, out bottom, out top);
                if (padWidth > 0f)
                {
                    float centre = (left + right) * 0.5f;
                    left = centre - padWidth * 0.5f;
                    right = centre + padWidth * 0.5f;
                }
                if (padHeight > 0f)
                {
                    float centre = (bottom + top) * 0.5f;
                    bottom = centre - padHeight * 0.5f;
                    top = centre + padHeight * 0.5f;
                }
            }
            else
            {
                var face = text.font.faceInfo;
                float pointsToUnits = text.fontSize / face.pointSize * face.scale * 0.1f;
                left = 0f;
                right = text.GetPreferredValues(text.text).x;
                bottom = 0f;
                top = kind == WordKind.Up ? liftHeight : face.meanLine * pointsToUnits;
            }

            box.size = new Vector2(right - left, top - bottom);
            box.offset = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        }

        bool IsPunctuation =>
            kind == WordKind.Spring || kind == WordKind.Comma || kind == WordKind.Hyphen || kind == WordKind.Period
            || kind == WordKind.Power;

        /// <summary>The tight box around the visible glyphs, unlike textBounds, which includes line height.</summary>
        void MeasureInk(out float left, out float right, out float bottom, out float top)
        {
            left = bottom = float.MaxValue;
            right = top = float.MinValue;
            TMP_TextInfo info = text.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                left = Mathf.Min(left, character.bottomLeft.x);
                bottom = Mathf.Min(bottom, character.bottomLeft.y);
                right = Mathf.Max(right, character.topRight.x);
                top = Mathf.Max(top, character.topRight.y);
            }
        }

        void Update()
        {
            if (kind == WordKind.Period)
            {
                text.color = Time.time < pressedUntil ? pressedColor : restingColor;
            }
            else if (kind == WordKind.Door && doorTimer > 0f)
            {
                doorTimer -= Time.deltaTime;
                if (doorTimer <= 0f)
                {
                    if (LetterIsInside()) doorTimer = 0.1f;
                    else SetDoorOpen(false);
                }
            }
        }

        /// <summary>Called by the letter on every physics step it stands on this word.</summary>
        public void OnStood(LetterController letter, float dt)
        {
            bool heavy = letter.Form == LetterForm.Uppercase;

            switch (kind)
            {
                case WordKind.Period:
                    if (!heavy) break;
                    pressedUntil = Time.time + 0.1f;
                    if (linkedDoor != null) linkedDoor.HoldOpen();
                    break;

                case WordKind.Hyphen:
                    if (heavy) Smash();
                    break;

                case WordKind.Hot:
                    if (Time.time - lastStoodTime > 0.1f) standingTime = 0f;
                    lastStoodTime = Time.time;
                    standingTime += dt;
                    if (standingTime >= burnDelay)
                    {
                        standingTime = 0f;
                        if (!letter.IsFallingOff) Sfx.Play(Sound.Burn);
                        letter.Burn();
                    }
                    break;
            }
        }

        public void HoldOpen()
        {
            doorTimer = doorOpenSeconds;
            SetDoorOpen(true);
        }

        void SetDoorOpen(bool open)
        {
            if (open && box.enabled) Sfx.Play(Sound.DoorOpen);
            box.enabled = !open;
            text.alpha = open ? OpenDoorAlpha : 1f;
        }

        /// <summary>Called by the letter when its head hits this word. A power block gives the letter its asterisks, once.</summary>
        public void HitFromBelow(LetterController letter)
        {
            if (kind != WordKind.Power || IsSpent) return;
            IsSpent = true;
            Sfx.Play(Sound.PowerBlock);

            var shooter = letter.GetComponent<LetterShooter>();
            if (shooter != null) shooter.Arm();

            text.text = spentText;
            text.color = spentColor;
            SpawnFlyingText("*", (Vector3)WorldCentre + Vector3.up * 0.3f, text.font, text.fontSize, powerColor,
                Vector2.up * 6f, 360f);
            StartCoroutine(BumpRoutine());
        }

        // Moves up and away from the head that hit it, so it never pushes into the letter.
        IEnumerator BumpRoutine()
        {
            const float duration = 0.2f;
            const float height = 0.2f;
            Vector3 rest = transform.position;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.position = rest + Vector3.up * (Mathf.Sin(t / duration * Mathf.PI) * height);
                yield return null;
            }
            transform.position = rest;
        }

        public void Bounce()
        {
            StopAllCoroutines();
            StartCoroutine(BounceRoutine());
        }

        IEnumerator BounceRoutine()
        {
            const float duration = 0.3f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float wobble = Mathf.Sin(t / duration * Mathf.PI * 3f) * (1f - t / duration);
                transform.localScale = new Vector3(1f, 1f + wobble * 0.45f, 1f);
                yield return null;
            }
            transform.localScale = Vector3.one;
        }

        public void Smash()
        {
            if (!box.enabled) return;
            box.enabled = false;
            Sfx.Play(Sound.Smash);

            text.ForceMeshUpdate();
            TMP_TextInfo info = text.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                Vector3 position = transform.TransformPoint(new Vector3(character.origin, character.baseLine, 0f));
                SpawnFlyingText(character.character.ToString(), position, text.font, text.fontSize, text.color,
                    new Vector2(Random.Range(1.5f, 7f), Random.Range(-1f, 6f)), Random.Range(-600f, 600f));
            }

            text.enabled = false;

            if (impactFont != null)
                SpawnFlyingText(impactText, (Vector3)WorldCentre + Vector3.left, impactFont, 4.5f, impactColor,
                    Vector2.up * 1.5f, 0f);

            if (respawnSeconds > 0f) StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnSeconds);
            while (LetterIsInside()) yield return null;
            box.enabled = true;
            text.enabled = true;
        }

        bool LetterIsInside()
        {
            return Physics2D.OverlapBox(WorldCentre, box.size, 0f, LayerMask.GetMask("Player")) != null;
        }

        void SpawnFlyingText(string content, Vector3 position, TMP_FontAsset font, float size, Color color,
            Vector2 velocity, float spin)
        {
            var shard = new GameObject($"Shard {content}");
            shard.transform.position = position;
            var label = shard.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.text = content;
            label.alignment = TextAlignmentOptions.BaselineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(4f, 0f);
            label.sortingOrder = 20;
            shard.AddComponent<FlyingText>().Launch(velocity, spin);
        }
    }

    /// <summary>A loose letter from a smashed word.</summary>
    public class FlyingText : MonoBehaviour
    {
        const float Lifetime = 0.8f;
        const float Gravity = 14f;

        TMP_Text label;
        Vector2 velocity;
        float spin;
        float age;

        public void Launch(Vector2 startVelocity, float startSpin)
        {
            label = GetComponent<TMP_Text>();
            velocity = startVelocity;
            spin = startSpin;
        }

        void Update()
        {
            age += Time.deltaTime;
            velocity.y -= Gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, spin * Time.deltaTime);
            label.alpha = 1f - age / Lifetime;
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
