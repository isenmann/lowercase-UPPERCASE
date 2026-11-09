using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LowercaseUppercase
{
    /// <summary>
    /// The picture of a level above the My levels list. The level is built frozen, a second camera draws all of it into
    /// a texture at once, and the built level is thrown away again in the same call. Pictures are kept while the menu is
    /// open, so moving between rows draws each level only once. A level wider than the strip drifts slowly from end to
    /// end; the wheel or a drag scrolls it by hand, and the drift waits while the mouse is over it.
    /// </summary>
    public class LevelPreviewStrip : MonoBehaviour
    {
        /// <summary>Canvas units per page pixel: the whole 720 px page fits the strip's height.</summary>
        public const float Scale = 0.25f;
        /// <summary>Texture pixels per canvas unit, so the picture stays sharp on screens above 720p.</summary>
        const float Sharpness = 2f;
        const int MaxTextureWidth = 8192;
        const float PageHeightPx = 720f;
        const float TailPx = 160f;
        const int CacheSize = 8;
        const float DriftUnitsPerSecond = 50f;
        const float DriftPauseSeconds = 1.5f;

        public ScrollRect scroll;
        public RawImage image;
        [Tooltip("Shown instead of the picture for a level that can't be read, or when there is none.")]
        public TMP_Text label;
        public Color pageColor = new Color32(0x0F, 0x11, 0x16, 0xFF);

        [Header("Assets the level is built with")]
        public TMP_FontAsset mono;
        public TMP_FontAsset monoBold;
        public TMP_FontAsset black;
        public Sprite white;

        /// <summary>The level shown, or being drawn.</summary>
        public StoredLevel Shown { get; private set; }

        sealed class Picture
        {
            public RenderTexture Texture;
            public float WidthPx;
        }

        readonly Dictionary<string, Picture> pictures = new Dictionary<string, Picture>();
        readonly List<string> drawnOrder = new List<string>();
        Camera view;
        int viewMadeOnFrame;
        StoredLevel waiting;
        float driftDirection = 1f;
        float driftPausedUntil;

        void Awake()
        {
            var go = new GameObject("Level Preview Camera");
            go.transform.SetParent(transform, false);
            view = go.AddComponent<Camera>();
            view.orthographic = true;
            view.orthographicSize = PageHeightPx / LevelEditor.PixelsPerUnit * 0.5f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = pageColor;
            view.depth = -10f;
            view.enabled = false;
            viewMadeOnFrame = Time.frameCount;
        }

        void OnDestroy()
        {
            foreach (Picture picture in pictures.Values) Release(picture);
            pictures.Clear();
        }

        /// <summary>Shows a level's picture, drawing it first if this is the first time. Null shows nothing.</summary>
        public void Show(StoredLevel level)
        {
            if (level == Shown && (image.texture != null || waiting == level)) return;
            Shown = level;
            waiting = null;

            if (level == null || level.Level == null)
            {
                SetPicture(null, level == null ? "" : "can't read this level");
                return;
            }
            string key = Key(level);
            if (pictures.TryGetValue(key, out Picture picture))
            {
                SetPicture(picture, null);
                return;
            }
            // A camera made this frame draws nothing yet, as when My levels opens with the menu: draw next frame.
            if (Time.frameCount == viewMadeOnFrame)
            {
                SetPicture(null, "");
                waiting = level;
                return;
            }
            picture = Draw(level);
            Keep(key, picture);
            SetPicture(picture, null);
        }

        /// <summary>Forgets every picture, for when the levels may have changed.</summary>
        public void Forget()
        {
            if (image != null) image.texture = null;
            foreach (Picture picture in pictures.Values) Release(picture);
            pictures.Clear();
            drawnOrder.Clear();
            Shown = null;
            waiting = null;
        }

        // A level saved again is a different picture.
        static string Key(StoredLevel level) => level.Path + "|" + level.Saved.Ticks;

        Picture Draw(StoredLevel level)
        {
            var root = new GameObject("Level Preview").transform;
            Levels.CustomLevelLayout layout = FrozenLevel.Build(root, level.Level, mono, monoBold, black, white);

            float widthPx = Mathf.Max(layout.RightPx + TailPx, 1280f);
            float sharp = Mathf.Min(Scale * Sharpness, MaxTextureWidth / widthPx);
            var texture = new RenderTexture(Mathf.CeilToInt(widthPx * sharp), Mathf.CeilToInt(PageHeightPx * sharp), 16)
            {
                name = $"Preview {level.Name}",
            };
            view.aspect = widthPx / PageHeightPx;
            view.transform.position = new Vector3(LevelEditor.WorldX(widthPx * 0.5f), LevelEditor.WorldY(PageHeightPx * 0.5f), -10f);
            Render(texture);

            // Drawn and gone in the same call, so the menu never shows it.
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
            return new Picture { Texture = texture, WidthPx = widthPx };
        }

        /// <summary>
        /// Draws the camera into a texture now. Leaving a camera on for a frame draws nothing here, so the render
        /// pipeline is asked directly.
        /// </summary>
        void Render(RenderTexture texture)
        {
            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(view, request))
            {
                RenderPipeline.SubmitRenderRequest(view, request);
                return;
            }
            view.targetTexture = texture;
            view.Render();
            view.targetTexture = null;
        }

        void Keep(string key, Picture picture)
        {
            pictures[key] = picture;
            drawnOrder.Add(key);
            if (drawnOrder.Count <= CacheSize) return;
            string oldest = drawnOrder[0];
            drawnOrder.RemoveAt(0);
            if (pictures.TryGetValue(oldest, out Picture old) && old != picture) Release(old);
            pictures.Remove(oldest);
        }

        void SetPicture(Picture picture, string text)
        {
            image.texture = picture?.Texture;
            image.enabled = picture != null;
            if (label != null) label.text = text ?? "";

            RectTransform content = scroll.content;
            float width = picture != null ? picture.WidthPx * Scale : ((RectTransform)scroll.viewport).rect.width;
            content.sizeDelta = new Vector2(width, content.sizeDelta.y);
            scroll.StopMovement();
            scroll.horizontalNormalizedPosition = 0f;
            driftDirection = 1f;
            driftPausedUntil = Time.unscaledTime + DriftPauseSeconds;
        }

        static void Release(Picture picture)
        {
            if (picture.Texture == null) return;
            picture.Texture.Release();
            Destroy(picture.Texture);
        }

        void Update()
        {
            if (waiting != null && Time.frameCount > viewMadeOnFrame)
            {
                StoredLevel level = waiting;
                Shown = null;
                Show(level);
            }
            if (image.texture == null) return;
            float overflow = scroll.content.rect.width - scroll.viewport.rect.width;
            if (overflow <= 1f) return;

            // The mouse over the strip means someone is looking closely, or about to scroll it themselves.
            Mouse mouse = Mouse.current;
            bool pointing = mouse != null && RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,
                mouse.position.ReadValue(), null);
            if (pointing)
            {
                driftPausedUntil = Time.unscaledTime + DriftPauseSeconds;
                return;
            }
            if (Time.unscaledTime < driftPausedUntil) return;

            float position = scroll.horizontalNormalizedPosition + driftDirection * DriftUnitsPerSecond / overflow
                * Time.unscaledDeltaTime;
            if (position >= 1f || position <= 0f)
            {
                position = Mathf.Clamp01(position);
                driftDirection = -driftDirection;
                driftPausedUntil = Time.unscaledTime + DriftPauseSeconds;
            }
            scroll.horizontalNormalizedPosition = position;
        }
    }
}
