using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>Shows the current glyph and adds squash and stretch on a damped spring.</summary>
    [RequireComponent(typeof(LetterController))]
    public class LetterVisual : MonoBehaviour
    {
        public Transform scaler;
        public TMP_Text lowerGlyph;
        public TMP_Text upperGlyph;
        [Tooltip("Optional thin line drawn from a comma down to the hanging letter.")]
        public SpriteRenderer hookThread;
        public Color normalColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        public Color deniedColor = new Color32(0xFF, 0x8A, 0x3D, 0xFF);

        [Header("Squash spring")]
        public float stiffness = 260f;
        public float damping = 15f;

        [Header("Kicks (x and y scale at the moment of the event)")]
        public Vector2 growSquash = new Vector2(1.3f, 0.45f);
        public Vector2 shrinkPop = new Vector2(1.5f, 1.5f);
        public Vector2 jumpStretch = new Vector2(0.75f, 1.3f);
        public Vector2 launchStretch = new Vector2(0.6f, 1.6f);
        public Vector2 slamStretch = new Vector2(0.7f, 1.35f);
        public float landSquashPerSpeed = 0.035f;
        public float maxLandSquash = 0.5f;

        [Header("Slam dust")]
        public string slamDust = "~";
        public float slamDustSize = 5f;
        public float slamDustSpeed = 5f;

        [Header("Ink splash")]
        public string splashDrop = "\u2022";
        public float splashDropSize = 4.5f;
        public Color splashColor = new Color32(0x7B, 0x8B, 0xFF, 0xFF);
        [Tooltip("Sideways speeds of the drops on each side; the fan is mirrored to the other side.")]
        public float[] splashSideSpeeds = { 1.2f, 2.6f, 4f };
        [Tooltip("Upward speed of the drops, and how much more a faster fall throws them.")]
        public float splashRise = 4f;
        public float splashRisePerSpeed = 0.25f;
        [Tooltip("How much each drop's speed varies, as a share either way, so no two splashes look the same.")]
        public float splashSideJitter = 0.25f;
        public Vector2 splashRiseJitter = new Vector2(0.8f, 1.15f);

        [Header("Refused grow")]
        public float deniedShake = 0.06f;
        public float deniedDuration = 0.18f;

        [Header("Death")]
        [Tooltip("Degrees per second while falling off the screen, tipping in the direction the letter faces.")]
        public float deathSpinSpeed = 120f;

        /// <summary>Keeps the letter out of sight until Appear is called.</summary>
        public bool Hidden { get; set; }

        LetterController letter;
        Vector2 scale = Vector2.one;
        Vector2 scaleVelocity;
        float deniedTimer;
        float deathAngle;

        void Awake()
        {
            letter = GetComponent<LetterController>();
            ShowForm(letter.Form);
        }

        void OnEnable()
        {
            letter.FormChanged += OnFormChanged;
            letter.GrowDenied += OnGrowDenied;
            letter.Jumped += OnJumped;
            letter.Landed += OnLanded;
            letter.Launched += OnLaunched;
            letter.SlamStarted += OnSlamStarted;
            letter.Slammed += OnSlammed;
            letter.Splashed += OnSplashed;
        }

        void OnDisable()
        {
            letter.FormChanged -= OnFormChanged;
            letter.GrowDenied -= OnGrowDenied;
            letter.Jumped -= OnJumped;
            letter.Landed -= OnLanded;
            letter.Launched -= OnLaunched;
            letter.SlamStarted -= OnSlamStarted;
            letter.Slammed -= OnSlammed;
            letter.Splashed -= OnSplashed;
        }

        /// <summary>Pops the letter into view on the squash spring.</summary>
        public void Appear()
        {
            Hidden = false;
            Kick(Vector2.zero);
        }

        void Update()
        {
            if (Hidden)
            {
                scaler.localScale = Vector3.zero;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            StepSpring(ref scale, ref scaleVelocity, dt, stiffness, damping);
            scaler.localScale = new Vector3(scale.x, scale.y, 1f);

            deniedTimer = Mathf.Max(0f, deniedTimer - dt);
            float denied = deniedDuration > 0f ? deniedTimer / deniedDuration : 0f;
            Color color = Color.Lerp(normalColor, deniedColor, denied);
            lowerGlyph.color = color;
            upperGlyph.color = color;
            float shake = denied > 0f ? Mathf.Sin(Time.unscaledTime * 90f) * deniedShake * denied : 0f;
            deathAngle = letter.IsFallingOff ? deathAngle - letter.Facing * deathSpinSpeed * Time.deltaTime : 0f;
            Quaternion tilt = Quaternion.Euler(0f, 0f, deathAngle);
            Vector3 centre = Vector3.up * (letter.Current.colliderSize.y * 0.5f);
            scaler.localRotation = tilt;
            scaler.localPosition = new Vector3(shake, 0f, 0f) + centre - tilt * centre;

            UpdateThread();
        }

        void UpdateThread()
        {
            if (hookThread == null) return;

            hookThread.enabled = letter.IsHanging;
            if (!letter.IsHanging) return;

            Vector2 from = letter.HookAnchor;
            Vector2 to = (Vector2)transform.position + Vector2.up * lowerGlyph.fontSize * 0.04f;
            Vector2 line = to - from;
            Transform thread = hookThread.transform;
            thread.position = (from + to) * 0.5f;
            thread.rotation = Quaternion.FromToRotation(Vector3.up, line);
            thread.localScale = new Vector3(0.03f, line.magnitude, 1f);
        }

        /// <summary>
        /// Advances the spring in small steps, because one long step makes it diverge on a slow frame.
        /// Time beyond MaxSpringTime is dropped, so a long hitch only pauses the animation.
        /// </summary>
        public static void StepSpring(ref Vector2 value, ref Vector2 velocity, float dt, float stiffness, float damping)
        {
            const float StepTime = 1f / 240f;
            const float MaxSpringTime = 0.1f;

            for (float remaining = Mathf.Min(dt, MaxSpringTime); remaining > 0f; remaining -= StepTime)
            {
                float step = Mathf.Min(remaining, StepTime);
                velocity += ((Vector2.one - value) * stiffness - velocity * damping) * step;
                value += velocity * step;
            }
        }

        void ShowForm(LetterForm form)
        {
            lowerGlyph.gameObject.SetActive(form == LetterForm.Lowercase);
            upperGlyph.gameObject.SetActive(form == LetterForm.Uppercase);
        }

        void Kick(Vector2 startScale)
        {
            scale = startScale;
            scaleVelocity = Vector2.zero;
        }

        void OnFormChanged(LetterForm form)
        {
            ShowForm(form);
            Kick(form == LetterForm.Uppercase ? growSquash : shrinkPop);
        }

        void OnGrowDenied() => deniedTimer = deniedDuration;
        void OnJumped() => Kick(jumpStretch);
        void OnLaunched() => Kick(launchStretch);
        void OnSlamStarted() => Kick(slamStretch);

        /// <summary>Two puffs thrown out sideways from the feet, as far as the shockwave reaches.</summary>
        void OnSlammed(int knockedOut)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var puff = new GameObject("Slam dust");
                puff.transform.position = transform.position + new Vector3(side * 0.4f, 0.15f, 0f);
                var label = puff.AddComponent<TextMeshPro>();
                label.font = upperGlyph.font;
                label.fontSize = slamDustSize;
                label.color = normalColor;
                label.text = slamDust;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.sizeDelta = new Vector2(2f, 1f);
                label.sortingOrder = 20;
                puff.AddComponent<FlyingText>().Launch(new Vector2(side * slamDustSpeed, 1.5f), 0f);
            }
        }

        /// <summary>A fan of drops thrown up from where the letter broke the ink's surface, higher for a harder fall.</summary>
        void OnSplashed(Vector2 at, float speed)
        {
            float rise = splashRise + speed * splashRisePerSpeed;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < splashSideSpeeds.Length; i++)
                {
                    // The outer drops fly lower, so the fan opens like a splash.
                    float up = rise * (1f - 0.18f * i) * Random.Range(splashRiseJitter.x, splashRiseJitter.y);
                    float across = splashSideSpeeds[i] * Random.Range(1f - splashSideJitter, 1f + splashSideJitter);
                    Throw(splashDrop, splashDropSize, splashColor, at + new Vector2(side * 0.1f, 0f),
                        new Vector2(side * across, up));
                }
            Throw(splashDrop, splashDropSize, splashColor, at,
                new Vector2(Random.Range(-0.5f, 0.5f), rise * 1.15f * Random.Range(splashRiseJitter.x, splashRiseJitter.y)));
        }

        void Throw(string glyph, float size, Color color, Vector2 from, Vector2 velocity)
        {
            var drop = new GameObject("Ink drop");
            drop.transform.position = new Vector3(from.x, from.y, 0f);
            var label = drop.AddComponent<TextMeshPro>();
            label.font = upperGlyph.font;
            label.fontSize = size;
            label.color = color;
            label.text = glyph;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(1f, 1f);
            label.sortingOrder = 20;
            drop.AddComponent<FlyingText>().Launch(velocity, 0f);
        }

        void OnLanded(float impactSpeed)
        {
            float squash = Mathf.Min(impactSpeed * landSquashPerSpeed, maxLandSquash);
            if (squash < 0.04f) return;
            Kick(new Vector2(1f + squash, 1f - squash));
        }
    }
}
