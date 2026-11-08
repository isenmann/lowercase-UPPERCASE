using LowercaseUppercase.Levels;
using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Builds a random level into the empty level of its scene as the scene loads. It wakes before the flow, so the
    /// checkpoints exist when the flow looks for the one to resume at, and before the camera, which needs the level's
    /// length.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class RandomLevel : MonoBehaviour
    {
        public Transform level;
        public PrototypeFlow flow;
        public ScreenFeedback feedback;
        public TMP_Text title;

        [Header("Assets the level is built with")]
        public TMP_FontAsset mono;
        public TMP_FontAsset monoBold;
        public TMP_FontAsset black;
        public Sprite white;

        void Awake()
        {
            int seed = RandomRun.Seed;
            RandomLevelLayout layout = PrototypeBuilder.BuildRandomLevel(level, seed, mono, monoBold, black, white);

            flow.checkpoints = layout.Checkpoints;
            flow.sentenceBefore = layout.SentenceBefore;
            flow.missingLetter = layout.MissingLetter;
            flow.sentenceAfter = layout.SentenceAfter;
            feedback.maxX = layout.CameraMaxX;
            title.text = $"Random · {RandomRun.LevelNumber}";

            Debug.Log($"Random level {RandomRun.LevelNumber}, seed {seed}, "
                + $"{(layout.UnderCeiling ? "under a ceiling" : "open air")}: {layout.Sections}");
        }

        void OnEnable() => flow.letter.Slotted += RandomRun.Advance;
        void OnDisable() => flow.letter.Slotted -= RandomRun.Advance;
    }
}
