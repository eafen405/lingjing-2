using System.IO;
using Lingjing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lingjing.Editor
{
    /// <summary>
    /// 无头生成 MainMenu 场景：相机 + UiBootstrap，界面本身留给运行时用 UiKit 搭。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.UiSceneBuilder.Run
    /// </summary>
    public static class UiSceneBuilder
    {
        public const string ScenePath = SceneCatalog.MainMenu;
        public const string LevelScenePath = SceneCatalog.Level1;

        [MenuItem("Tools/Lingjing/Build UI Scene")]
        public static void Run()
        {
            string dir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = UiKit.RefHeight / 2f / 32f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.043f, 0.055f, 1f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var bootGo = new GameObject("UiBootstrap");
            bootGo.AddComponent<UiBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            // 场景清单统一走 SceneCatalog：MainMenu 必须在第 0 位，关卡场景跟在后面。
            SceneCatalog.Sync();

            Debug.Log($"[ui] scene saved: {ScenePath}; build scenes: [0]={ScenePath}, [1]={LevelScenePath} (M1 exists={File.Exists(LevelScenePath)})");
        }
    }
}
