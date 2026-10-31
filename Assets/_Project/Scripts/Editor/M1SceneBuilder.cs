using System.IO;
using Lingjing.AI;
using Lingjing.Fairy;
using Lingjing.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lingjing.Editor
{
    /// <summary>
    /// 无头生成 M1 竖切场景：相机 + BattleRunner + 输入，精灵引用直接烘进场景。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.M1SceneBuilder.Run
    /// </summary>
    public static class M1SceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/M1.unity";

        const string EousIdle = "Assets/_Project/Art/Characters/eous/idle/eous_idle_s_00.png";
        const string TailLightIdle = "Assets/_Project/Art/Enemies/taillight/idle/taillight_idle_s_00.png";
        const string Knife = "Assets/_Project/Art/Items/knife/knife_00.png";

        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/_Project/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            // 960x540 基准、PPU 32：半高 = 540 / 2 / 32
            cam.orthographicSize = 540f / 2f / 32f;
            cam.backgroundColor = new Color(0.055f, 0.063f, 0.075f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = -100f;
            camGo.transform.position = new Vector3(20f, 12f, -10f);

            var battleGo = new GameObject("Battle");
            var runner = battleGo.AddComponent<BattleRunner>();
            runner.playerSprite = LoadSprite(EousIdle);
            runner.enemySprite = LoadSprite(TailLightIdle);
            runner.bladeSprite = LoadSprite(Knife);
            runner.viewCamera = cam;

            var follow = camGo.AddComponent<CameraFollow>();
            follow.runner = runner;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[m1] scene saved: {ScenePath} (sprites: player={runner.playerSprite != null}, enemy={runner.enemySprite != null}, blade={runner.bladeSprite != null})");
        }

        /// <summary>无头探针：打开场景、建世界、推进 600 tick，检查世界确实在动。</summary>
        public static void Probe()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var runner = Object.FindObjectOfType<BattleRunner>();
            if (runner == null)
            {
                Debug.Log("[m1] PROBE FAIL: 场景里找不到 BattleRunner");
                EditorApplication.Exit(1);
                return;
            }

            runner.Build();
            if (!runner.Ready)
            {
                Debug.Log("[m1] PROBE FAIL: 世界没建起来");
                EditorApplication.Exit(1);
                return;
            }

            Vector3 start = runner.PlayerTransform.position;
            runner.StepFixed(600, new Command(new Lingjing.Core.Vec2(1, 0), 1, false));
            Vector3 end = runner.PlayerTransform.position;

            int enemies = runner.World.World.Enemies.Count;
            int bladeSprites = GameObject.FindObjectsOfType<SpriteRenderer>().Length;

            Debug.Log($"[m1] probe start={start.x:0.00},{start.y:0.00} end={end.x:0.00},{end.y:0.00} enemies={enemies} sprites={bladeSprites} ticks={runner.World.Tick}");

            bool moved = Mathf.Abs(end.x - start.x) > 1f;
            Debug.Log(moved && enemies > 0 && bladeSprites >= 10
                ? "[m1] PROBE PASS"
                : "[m1] PROBE FAIL: 位置没变化或对象不全");

            if (!moved)
            {
                EditorApplication.Exit(1);
            }
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[m1] 精灵加载失败：{path}");
            }

            return sprite;
        }
    }
}
