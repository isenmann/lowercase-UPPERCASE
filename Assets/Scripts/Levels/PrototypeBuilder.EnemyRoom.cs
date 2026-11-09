#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// A room for trying each new enemy on its own before it goes into the levels. It is not in the build
    /// settings, so it never ships and the build indices the tests use don't move. Open it and press Play.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string EnemyRoomScene = SceneFolder + "/EnemyRoom.unity";

#if UNITY_EDITOR
        [MenuItem("lowercase UPPERCASE/Rebuild Enemy Room")]
        public static void RebuildEnemyRoom()
        {
            PrepareAssets(out FormSettings lower, out FormSettings upper);
            BuildEnemyRoom(lower, upper);
        }

        static void BuildEnemyRoom(FormSettings lower, FormSettings upper)
        {
            Transform level = BeginScene(EnemyRoomScene, "Enemy Room");
            float x = 40f;

            Floor(level, "the enemy room", ref x);
            PlaceCheckpoint(level, x - 60f, Ground);
            x = EnemyPen(level, x, "a plain walker", "& stomp it or shoot it", BuildEnemy);
            PowerBlock(level, x - 120f, LowPlatform);
            x = EnemyPen(level, x, "a spike walks here", "^ shoot it, don't land on it", BuildSpike);
            x = EnemyPen(level, x, "armour walks here", "§ stomp it, asterisks bounce off", BuildArmour);
            x = Exit(level, x, "the end of the room", "the", "nd");

            FinishScene(EnemyRoomScene, level, "Enemy room", lower, upper, new Vector2(110f, Ground),
                "try every ", "e", "nemy", scrollEndPx: x + 200f, configure: (flow, hud) => flow.practice = true);
            EditorSceneManager.OpenScene(EnemyRoomScene);
        }

        /// <summary>A long word for one enemy to walk on, with a note above it and a checkpoint after it.</summary>
        static float EnemyPen(Transform level, float x, string floor, string note,
            System.Func<Transform, WordBlock, float, Enemy> build)
        {
            float left = x;
            WordBlock ground = Word(level, floor + " and back again", x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            Note(level, left + 40f, 300f, note);
            build(level, ground, left + 300f);
            PlaceCheckpoint(level, x - 60f, Ground);
            return x;
        }
#endif
    }
}
