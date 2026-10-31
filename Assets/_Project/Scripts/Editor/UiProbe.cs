using System;
using System.IO;
using System.Linq;
using Lingjing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lingjing.Editor
{
    /// <summary>
    /// UI 层无头探针：生成/打开主菜单、点一次关卡选择、校验收到的状态与分数读写。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.UiProbe.Run
    /// </summary>
    public static class UiProbe
    {
        // GameSave 的键没有公开，探针为了「测完恢复原值」按同一份存储约定直接快照。
        const string HighScoreKey = "lingjing.highScore";
        const string TotalScoreKey = "lingjing.totalScore";

        public static void Run()
        {
            bool pass = false;
            try
            {
                pass = Check();
            }
            catch (Exception e)
            {
                Debug.LogError("[ui] PROBE 异常: " + e);
                pass = false;
            }

            Debug.Log(pass ? "[ui] PROBE PASS" : "[ui] PROBE FAIL");
            if (!pass)
            {
                EditorApplication.Exit(1);
            }
        }

        static bool Check()
        {
            bool pass = true;

            void Expect(bool ok, string what)
            {
                pass &= ok;
                Debug.Log($"[ui] {(ok ? "PASS" : "FAIL")} {what}");
            }

            if (!File.Exists(UiSceneBuilder.ScenePath))
            {
                Debug.Log("[ui] MainMenu.unity 不存在，先调用 UiSceneBuilder.Run() 生成");
                UiSceneBuilder.Run();
            }

            EditorSceneManager.OpenScene(UiSceneBuilder.ScenePath, OpenSceneMode.Single);
            var boot = UnityEngine.Object.FindObjectOfType<UiBootstrap>();
            Expect(boot != null, "MainMenu 场景里有 UiBootstrap");
            if (boot == null)
            {
                return false;
            }

            // 编辑模式不跑 Awake，这里调用与运行时同一个构建入口。
            boot.Build();

            Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            Expect(canvas != null, "构建后找到 Canvas");
            if (canvas == null)
            {
                return false;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            bool scalerOk = scaler != null
                && Mathf.Approximately(scaler.referenceResolution.x, UiKit.RefWidth)
                && Mathf.Approximately(scaler.referenceResolution.y, UiKit.RefHeight);
            Expect(scalerOk, $"CanvasScaler 参考分辨率 = {UiKit.RefWidth}x{UiKit.RefHeight}");

            Button[] activeButtons = canvas.GetComponentsInChildren<Button>(false);
            Expect(activeButtons.Length >= 3, $"主菜单活动按钮数 {activeButtons.Length} >= 3");

            Text[] activeTexts = canvas.GetComponentsInChildren<Text>(false);
            bool titleOk = activeTexts.Any(t => !string.IsNullOrEmpty(t.text) && t.text == UiText.GameTitle);
            Expect(titleOk, $"标题文本 = {UiText.GameTitle}");

            bool fontOk = UiKit.Font != null;
            Expect(fontOk, "UiKit.Font 已加载");

            UnityEditor.EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            bool buildSettingsOk = buildScenes.Length >= 2
                && buildScenes[0].path == UiSceneBuilder.ScenePath
                && buildScenes.Any(s => s.path == UiSceneBuilder.LevelScenePath);
            Expect(buildSettingsOk, "EditorBuildSettings：MainMenu 第 0、M1 已加入");

            LevelSelectPanel panel = boot.Menu != null ? boot.Menu.LevelSelect : null;
            Expect(panel != null, "MainMenuScreen 持有 LevelSelectPanel");
            bool hiddenBefore = panel != null && !panel.Visible;
            Expect(hiddenBefore, "关卡面板初始隐藏");

            Button openButton = FindButton(canvas.transform, UiText.LevelSelect);
            Expect(openButton != null, $"找到「{UiText.LevelSelect}」按钮");
            if (openButton != null)
            {
                openButton.onClick.Invoke();
            }

            bool shown = panel != null && panel.Visible;
            Expect(shown, $"点击「{UiText.LevelSelect}」后关卡面板可见");

            Button level2Button = panel != null && panel.Root != null
                ? FindButton(panel.Root, UiText.Level2)
                : null;
            bool level2Locked = level2Button != null && !level2Button.interactable;
            Expect(level2Locked, $"「{UiText.Level2}」按钮 interactable == false");

            bool lockedText = panel != null && panel.Root != null && HasText(panel.Root, UiText.Locked);
            Expect(lockedText, $"不可进节点显示「{UiText.Locked}」");

            // 战斗内 HUD/暂停/结算的构造冒烟：不进入播放模式，只验证界面树能搭出来。
            HudView hud = HudView.Build(null);
            hud.SetStats(100f, 100f, 0f, 100f, true, 3.2f);
            bool hudBars = hud.GetComponentsInChildren<UiBar>(true).Length >= 2;
            bool hudHp = HasText(hud.transform, $"{UiText.Hp} 100/100");
            bool hudEnergy = HasText(hud.transform, $"{UiText.Energy} 0/100");
            bool hudFairy = hud.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains(UiText.FairyTakeover));
            bool hudPause = FindButton(hud.transform, UiText.Pause) != null;
            Expect(hudBars && hudHp && hudEnergy && hudFairy && hudPause,
                "HUD：双条 + 生命/充能/代行文本 + 暂停键");

            PauseScreen pause = PauseScreen.Build(null, null, null);
            bool pauseHidden = !pause.Visible;
            pause.Show();
            bool pauseShown = pause.Visible
                && FindButton(pause.transform, UiText.Resume) != null
                && FindButton(pause.transform, UiText.Restart) != null
                && FindButton(pause.transform, UiText.MainMenu) != null
                && HasText(pause.transform, UiText.Pause);
            pause.Hide();
            Expect(pauseHidden && pauseShown, "暂停：默认隐藏，显示后有标题与三个按钮");

            ResultScreen result = ResultScreen.Build(null, null);
            result.Show(false, 1234, 9999);
            bool failShown = result.Visible
                && HasText(result.transform, UiText.ResultFail)
                && HasText(result.transform, $"{UiText.Score} 1234")
                && HasText(result.transform, $"{UiText.HighScore} 9999");
            result.Show(true, 0, 0);
            bool clearShown = HasText(result.transform, UiText.ResultClear);
            result.Hide();
            Expect(failShown && clearShown, "结算：失败/通关标题、得分与最高分");

            UnityEngine.Object.DestroyImmediate(hud.gameObject);
            UnityEngine.Object.DestroyImmediate(pause.gameObject);
            UnityEngine.Object.DestroyImmediate(result.gameObject);

            bool hadHigh = PlayerPrefs.HasKey(HighScoreKey);
            int beforeHigh = PlayerPrefs.GetInt(HighScoreKey, 0);
            bool hadTotal = PlayerPrefs.HasKey(TotalScoreKey);
            int beforeTotal = PlayerPrefs.GetInt(TotalScoreKey, 0);

            bool scoreOk = false;
            try
            {
                GameSave.SubmitScore(1234);
                scoreOk = GameSave.HighScore >= 1234;
                Debug.Log($"[ui] SubmitScore(1234) 后 HighScore={GameSave.HighScore}（原值 {beforeHigh}）");
            }
            finally
            {
                if (hadHigh)
                {
                    PlayerPrefs.SetInt(HighScoreKey, beforeHigh);
                }
                else
                {
                    PlayerPrefs.DeleteKey(HighScoreKey);
                }

                if (hadTotal)
                {
                    PlayerPrefs.SetInt(TotalScoreKey, beforeTotal);
                }
                else
                {
                    PlayerPrefs.DeleteKey(TotalScoreKey);
                }

                PlayerPrefs.Save();
            }

            Expect(scoreOk, "SubmitScore(1234) 后 HighScore >= 1234");

            bool restored = hadHigh ? GameSave.HighScore == beforeHigh : GameSave.HighScore == 0;
            Expect(restored, "PlayerPrefs 已恢复原值");

            string scalerText = scaler != null
                ? $"{scaler.referenceResolution.x}x{scaler.referenceResolution.y}"
                : "null";
            string fontText = UiKit.Font != null ? UiKit.Font.name : "null";
            Debug.Log($"[ui] summary canvas={canvas.name} scaler={scalerText} buttons={activeButtons.Length} "
                + $"title={titleOk} font={fontText} levelPanelHidden={hiddenBefore} levelPanelShown={shown} "
                + $"level2Interactable={(level2Button != null && level2Button.interactable)} lockedText={lockedText} "
                + $"score={scoreOk} restored={restored}");

            return pass;
        }

        static Button FindButton(Transform root, string label)
        {
            if (root == null)
            {
                return null;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                Transform labelTx = button.transform.Find("Label");
                Text text = labelTx != null
                    ? labelTx.GetComponent<Text>()
                    : button.GetComponentInChildren<Text>(true);

                if (text != null && text.text == label)
                {
                    return button;
                }
            }

            return null;
        }

        static bool HasText(Transform root, string text)
        {
            if (root == null)
            {
                return false;
            }

            return root.GetComponentsInChildren<Text>(true).Any(t => t.text == text);
        }
    }
}
