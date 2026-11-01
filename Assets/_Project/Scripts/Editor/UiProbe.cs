using System;
using System.IO;
using System.Linq;
using Doozy.Engine.UI;
using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Level;
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

            // 每个界面根节点都带 UIView 自带的嵌套 Canvas，缩放只由根画布负责，这里统一从 rootCanvas 取。
            CanvasScaler scaler = canvas.rootCanvas.GetComponent<CanvasScaler>();
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

            // Doozy 画布：运行时在 Awake 阶段建出来，界面的 UIView 全挂在它下面。
            UICanvas doozyCanvas = DoozyViewHost.FindCanvas();
            bool doozyCanvasOk = doozyCanvas != null
                && doozyCanvas.Canvas != null
                && doozyCanvas.Canvas.isRootCanvas
                && doozyCanvas.CanvasName == DoozyViewHost.CanvasName;
            Expect(doozyCanvasOk, $"主菜单场景有 Doozy UICanvas（{DoozyViewHost.CanvasName}）");

            UIView[] sceneViews = UnityEngine.Object.FindObjectsOfType<UIView>(true);
            UIView menuView = sceneViews.FirstOrDefault(v => v.ViewName == DoozyViewHost.MainMenuViewName);
            bool menuViewOk = menuView != null
                && boot.Menu != null
                && boot.Menu.View != null
                && menuView == boot.Menu.View.DoozyView;
            Expect(menuViewOk,
                $"场景里存在 UIView 且 ViewName == {DoozyViewHost.MainMenuViewName}（共 {sceneViews.Length} 个）");

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

            // Doozy：关卡面板自己的 UIView，Show(true)/Hide(true) 直接翻转可见状态。
            DoozyViewHost levelView = panel != null ? panel.View : null;
            if (levelView != null)
            {
                levelView.Show(true);
            }

            bool levelShown = levelView != null && levelView.IsVisible && panel.Visible;
            Expect(levelShown, "LevelSelect：Show(true) 后 UIView 可见");

            if (levelView != null)
            {
                levelView.Hide(true);
            }

            bool levelHidden = levelView != null && !levelView.IsVisible && !panel.Visible;
            Expect(levelHidden, "LevelSelect：Hide(true) 后 UIView 不可见");

            // Doozy：主菜单自己的 UIView，Hide/Show 各翻转一次 IsVisible，用完恢复显示。
            DoozyViewHost menuHost = boot.Menu != null ? boot.Menu.View : null;
            if (menuHost != null)
            {
                menuHost.Hide(true);
            }

            bool menuHidden = menuHost != null && !menuHost.IsVisible;
            Expect(menuHidden, "MainMenu：Hide(true) 后 IsVisible == false");

            if (menuHost != null)
            {
                menuHost.Show(true);
            }

            bool menuShownAfterHide = menuHost != null && menuHost.IsVisible;
            Expect(menuShownAfterHide, "MainMenu：Show(true) 后 IsVisible == true");

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

            // 升级三选一：三张牌、文案全部来自 UiText、点一张会回传它自己那类。
            GrowthKind picked = GrowthKind.Speed;
            int pickCount = 0;
            ChoiceScreen choice = ChoiceScreen.Build(kind =>
            {
                picked = kind;
                pickCount++;
            });

            var growth = new GrowthState(7UL);
            var dummy = new Player();
            growth.Award(dummy, EnemyKind.ChainBoss);   // 300 经验，直接够第一级阈值
            bool rolled = growth.HasPendingChoice && growth.OptionCount == 3;
            choice.Show(growth);
            bool choiceShown = choice.Visible
                && HasText(choice.transform, UiText.PickUpgrade)
                && HasText(choice.transform, UiText.GrowthName(growth.OptionAt(0)))
                && HasText(choice.transform, UiText.GrowthEffect(growth.OptionAt(0)))
                && choice.ShownAt(0) == growth.OptionAt(0)
                && FindButton(choice.transform, UiText.Choose) != null;
            Expect(rolled && choiceShown, "三选一：一次性给三张牌，卡面文案与所选一致");

            Button firstCard = FindButton(choice.transform, UiText.Choose);
            firstCard?.onClick.Invoke();
            bool pickedOk = pickCount == 1 && picked == growth.OptionAt(0) && !choice.Visible;
            Expect(pickedOk, "三选一：点第一张会把该类别回传并关闭面板");

            bool choiceViewOk = choice.View != null && choice.View.DoozyView != null
                && choice.View.DoozyView.ViewName == DoozyViewHost.ChoiceViewName
                && choice.View.DoozyView.Canvas.sortingOrder == DoozyViewHost.ChoiceSortingOrder
                && DoozyViewHost.PauseSortingOrder < DoozyViewHost.ChoiceSortingOrder
                && DoozyViewHost.ChoiceSortingOrder < DoozyViewHost.ResultSortingOrder;
            Expect(choiceViewOk, $"三选一：UIView 名为 Choice，层级 Pause({DoozyViewHost.PauseSortingOrder}) < Choice({DoozyViewHost.ChoiceSortingOrder}) < Result({DoozyViewHost.ResultSortingOrder})");

            // Doozy：HUD/暂停/结算三个视图都存在且 UIView.ViewName 固定。
            bool viewNamesOk = hud.View != null && hud.View.DoozyView != null
                && hud.View.DoozyView.ViewName == DoozyViewHost.HudViewName
                && pause.View != null && pause.View.DoozyView != null
                && pause.View.DoozyView.ViewName == DoozyViewHost.PauseViewName
                && result.View != null && result.View.DoozyView != null
                && result.View.DoozyView.ViewName == DoozyViewHost.ResultViewName;
            Expect(viewNamesOk, "HUD/暂停/结算三个 UIView 的 ViewName = Hud/Pause/Result");

            // Doozy：三者都在同一个画布下，靠嵌套 Canvas 的 overrideSorting + sortingOrder 叠加。
            bool layersOk = hud.View != null && pause.View != null && result.View != null
                && hud.View.DoozyView.Canvas.overrideSorting
                && pause.View.DoozyView.Canvas.overrideSorting
                && result.View.DoozyView.Canvas.overrideSorting
                && hud.View.DoozyView.Canvas.sortingOrder == DoozyViewHost.HudSortingOrder
                && pause.View.DoozyView.Canvas.sortingOrder == DoozyViewHost.PauseSortingOrder
                && result.View.DoozyView.Canvas.sortingOrder == DoozyViewHost.ResultSortingOrder
                && hud.View.DoozyView.Canvas.sortingOrder < pause.View.DoozyView.Canvas.sortingOrder
                && pause.View.DoozyView.Canvas.sortingOrder < result.View.DoozyView.Canvas.sortingOrder;
            Expect(layersOk,
                $"层级：HUD({DoozyViewHost.HudSortingOrder}) < Pause({DoozyViewHost.PauseSortingOrder}) < Result({DoozyViewHost.ResultSortingOrder})");

            UnityEngine.Object.DestroyImmediate(hud.gameObject);
            UnityEngine.Object.DestroyImmediate(pause.gameObject);
            UnityEngine.Object.DestroyImmediate(choice.gameObject);
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
                + $"doozyCanvas={(doozyCanvas != null ? doozyCanvas.CanvasName : "null")} views={sceneViews.Length} "
                + $"viewNames={viewNamesOk} layers={layersOk} score={scoreOk} restored={restored}");

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
