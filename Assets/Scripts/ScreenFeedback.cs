using System.Collections;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>Camera shake, a short freeze on heavy events, and optional sideways follow. Lives on the camera.</summary>
    public class ScreenFeedback : MonoBehaviour
    {
        public LetterController letter;

        [Header("Shake")]
        public float landShakePerSpeed = 0.012f;
        public float smashShake = 0.25f;
        public float shakeDecay = 7f;

        [Header("Freeze")]
        public float smashFreezeSeconds = 0.06f;

        [Header("Follow (for levels wider than one screen)")]
        public bool followLetter;
        public float minX;
        public float maxX;
        public float followSharpness = 8f;
        [Tooltip("Moves with the camera but without the shake.")]
        public Transform hud;

        Vector3 home;
        float shake;

        void Awake()
        {
            home = transform.position;
            if (followLetter) home.x = Mathf.Clamp(letter.transform.position.x, minX, maxX);
            PlaceHud();
        }

        void OnEnable()
        {
            letter.Landed += OnLanded;
            letter.Smashed += OnSmashed;
        }

        void OnDisable()
        {
            letter.Landed -= OnLanded;
            letter.Smashed -= OnSmashed;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (followLetter)
            {
                float target = Mathf.Clamp(letter.transform.position.x, minX, maxX);
                home.x = Mathf.Lerp(home.x, target, 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
                PlaceHud();
            }

            shake = Mathf.MoveTowards(shake, 0f, shakeDecay * shake * Time.unscaledDeltaTime + 0.001f);
            transform.position = home + (Vector3)(Random.insideUnitCircle * shake);
        }

        void PlaceHud()
        {
            if (hud != null) hud.position = new Vector3(home.x, home.y, 0f);
        }

        void OnLanded(float impactSpeed)
        {
            if (letter.Form != LetterForm.Uppercase) return;
            shake = Mathf.Max(shake, impactSpeed * landShakePerSpeed);
        }

        void OnSmashed()
        {
            shake = Mathf.Max(shake, smashShake);
            StartCoroutine(Freeze());
        }

        IEnumerator Freeze()
        {
            Time.timeScale = 0.02f;
            yield return new WaitForSecondsRealtime(smashFreezeSeconds);
            Time.timeScale = 1f;
        }
    }
}
