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
    /// 无头生成关卡场景：相机 + BattleRunner + 输入，精灵与 levels.json 引用直接烘进场景。
    /// 两关各一个场景，差别只有 levelId —— 关卡内容全在 levels.json 里。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.M1SceneBuilder.Run
    /// </summary>
    public static class M1SceneBuilder
    {
        public const string ScenePath = SceneCatalog.Level1;
        public const string SecondScenePath = SceneCatalog.Level2;
        public const string LevelDataPath = "Assets/_Project/Data/levels.json";
        public const string VoiceDataPath = "Assets/_Project/Data/voice.json";

        const string EousIdle = "Assets/_Project/Art/Characters/eous/idle/eous_idle_s_00.png";
        const string TailLightIdle = "Assets/_Project/Art/Enemies/taillight/idle/taillight_idle_s_00.png";
        const string Knife = "Assets/_Project/Art/Items/knife/knife_00.png";
        const string HealthPickup = "Assets/_Project/Art/Items/pickup_health/pickup_health_00.png";
        const string EnergyPickup = "Assets/_Project/Art/Items/pickup_energy/pickup_energy_00.png";

        /// <summary>两关一起生成；场景清单统一走 SceneCatalog，避免谁后跑谁覆盖。</summary>
        public static void Run()
        {
            var levelData = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelDataPath);
            if (levelData == null)
            {
                Debug.LogError($"[m1] 找不到关卡数据：{LevelDataPath}");
            }

            var voiceData = AssetDatabase.LoadAssetAtPath<TextAsset>(VoiceDataPath);
            if (voiceData == null)
            {
                Debug.LogError($"[m1] 找不到台词数据：{VoiceDataPath}");
            }

            Build(ScenePath, 1, levelData, voiceData);
            Build(SecondScenePath, 2, levelData, voiceData);

            SceneCatalog.Sync();
            AssetDatabase.SaveAssets();
            Debug.Log($"[m1] scenes saved: {ScenePath}, {SecondScenePath}; levelData={levelData != null} voiceData={voiceData != null}");
        }

        static void Build(string scenePath, int levelId, TextAsset levelData, TextAsset voiceData)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath) ?? "Assets/_Project/Scenes");

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
            camGo.transform.position = new Vector3(48f, 27f, -10f);

            var battleGo = new GameObject("Battle");
            var runner = battleGo.AddComponent<BattleRunner>();
            runner.art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(ArtLibraryBuilder.AssetPath);
            runner.playerSprite = LoadSprite(EousIdle);
            runner.enemySprite = LoadSprite(TailLightIdle);
            runner.bladeSprite = LoadSprite(Knife);
            runner.healthSprite = LoadSprite(HealthPickup);
            runner.energySprite = LoadSprite(EnergyPickup);
            runner.viewCamera = cam;
            runner.levelJson = levelData;
            runner.levelId = levelId;
            runner.voiceJson = voiceData;

            var follow = camGo.AddComponent<CameraFollow>();
            follow.runner = runner;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[m1] scene saved: {scenePath} levelId={levelId} data={levelData != null} " +
                      $"sprites: player={runner.playerSprite != null} enemy={runner.enemySprite != null} blade={runner.bladeSprite != null}");
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
                Debug.Log("[m1] PROBE FAIL: 世界没建起来（看上面的 levelJson 报错）");
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
