using System;
using LowercaseUppercase.Levels;
using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A custom level built to be looked at, not played: the level editor's preview and the My levels picture. Every
    /// behaviour in it is switched off before it starts, so enemies don't walk, springs don't bounce and nothing hides
    /// itself, and its text is laid out at once, so it can be measured or drawn in the same frame.
    /// </summary>
    public static class FrozenLevel
    {
        public static CustomLevelLayout Build(Transform root, CustomLevelData data, TMP_FontAsset mono,
            TMP_FontAsset monoBold, TMP_FontAsset black, Sprite white, Action<CustomPiece, Transform> onBuilt = null)
        {
            // A callback also keeps the builder from logging skipped pieces: whoever looks at the level shows them.
            CustomLevelLayout layout = PrototypeBuilder.BuildCustomLevel(root, data, mono, monoBold, black, white,
                onBuilt ?? ((piece, part) => { }));
            Freeze(root);
            // Meshes are made lazily, and both the editor's clicks and a picture taken now need them.
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate();
            return layout;
        }

        /// <summary>TextMesh Pro's own components stay on, or the words wouldn't show.</summary>
        static void Freeze(Transform root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour.GetType().Namespace != "TMPro") behaviour.enabled = false;
            foreach (Rigidbody2D body in root.GetComponentsInChildren<Rigidbody2D>(true))
                body.simulated = false;
        }
    }
}
