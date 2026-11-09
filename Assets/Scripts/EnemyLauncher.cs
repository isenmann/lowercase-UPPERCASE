using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Sits on a launcher word and fires a copy of its arrow on a fixed beat, but only while the launcher is on
    /// screen, so arrows don't pile up out of sight. The arrow template is an inactive enemy built with the level.
    /// </summary>
    public class EnemyLauncher : MonoBehaviour
    {
        const float ScreenMargin = 1f;

        [Tooltip("An inactive arrow enemy to copy. Its position is where every arrow starts.")]
        public EnemyArrow arrowTemplate;
        [Tooltip("Seconds between arrows.")]
        public float interval = 2f;
        [Tooltip("Seconds before the first arrow once the launcher is on screen.")]
        public float firstDelay = 0.6f;

        float wait;
        bool wasOnScreen;

        void FixedUpdate()
        {
            if (!IsOnScreen())
            {
                wasOnScreen = false;
                return;
            }

            if (!wasOnScreen)
            {
                wasOnScreen = true;
                wait = firstDelay;
            }

            wait -= Time.fixedDeltaTime;
            if (wait > 0f) return;
            wait = interval;
            Fire();
        }

        void Fire()
        {
            Transform template = arrowTemplate.transform;
            EnemyArrow arrow = Instantiate(arrowTemplate, template.position, template.rotation, template.parent);
            arrow.name = arrowTemplate.name.Replace(" template", "");
            arrow.gameObject.SetActive(true);
            Sfx.Play(Sound.ArrowFire);
        }

        bool IsOnScreen()
        {
            Camera view = Camera.main;
            if (view == null) return false;
            float halfWidth = view.orthographicSize * view.aspect + ScreenMargin;
            return Mathf.Abs(transform.position.x - view.transform.position.x) < halfWidth;
        }
    }
}
