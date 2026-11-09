using LowercaseUppercase.Levels;
using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Builds a custom level into the empty level of its scene as the scene loads, like <see cref="RandomLevel"/>.
    /// It wakes before the flow, so the checkpoints, the clock and the letter's start are set when the flow reads them.
    /// Custom levels keep the clock but have endless lives and no high scores.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class CustomLevel : MonoBehaviour
    {
        public Transform level;
        public PrototypeFlow flow;
        public ScreenFeedback feedback;
        public TMP_Text title;
        [Tooltip("Played when the scene is opened without a level handed to it through CustomRun.")]
        public TextAsset sampleLevel;

        [Header("Assets the level is built with")]
        public TMP_FontAsset mono;
        public TMP_FontAsset monoBold;
        public TMP_FontAsset black;
        public Sprite white;

        public CustomLevelData Data { get; private set; }

        void Awake()
        {
            Data = CustomRun.Level ?? CustomLevelData.FromJson(sampleLevel.text);
            CustomLevelLayout layout = PrototypeBuilder.BuildCustomLevel(level, Data, mono, monoBold, black, white);

            flow.checkpoints = layout.Checkpoints;
            flow.sentenceBefore = Data.sentenceBefore;
            flow.missingLetter = Data.missingLetter;
            flow.sentenceAfter = Data.sentenceAfter;
            flow.levelSeconds = Data.seconds;
            flow.endlessLives = true;
            flow.letter.transform.position = layout.StartFeet;
            feedback.maxX = layout.CameraMaxX;
            title.text = Data.name;

            if (CustomRun.Testing)
            {
                if (CustomRun.TestStartPx is Vector2 start)
                    flow.letter.transform.position = new Vector3(LevelEditor.WorldX(start.x), LevelEditor.WorldY(start.y), 0f);
                flow.leaveScene = CustomRun.EditorScene;
                var transition = flow.GetComponent<LevelTransition>();
                if (transition != null) transition.nextScene = CustomRun.EditorScene;
                title.text = $"testing  {Data.name}  ·  Esc back to the editor";
            }
        }

        void OnEnable() => flow.letter.Slotted += OnSlotted;
        void OnDisable() => flow.letter.Slotted -= OnSlotted;

        void OnSlotted()
        {
            if (CustomRun.Testing) CustomRun.TestFinished = true;
        }

        /// <summary>Leaves a test play for the editor, as Esc does.</summary>
        public static void BackToEditor() => UnityEngine.SceneManagement.SceneManager.LoadScene(CustomRun.EditorScene);
    }
}
