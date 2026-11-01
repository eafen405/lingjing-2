using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Sim;
using UnityEditor;
using UnityEngine;
using Grid = Lingjing.Nav.Grid;

namespace Lingjing.Editor
{
    /// <summary>
    /// 手感这一版的数值与素材回归：不用进播放模式，直接对常量表、以骸数值表、特效素材与闪白 shader 断言。
    /// 数值改了但没同步到某个以骸、或特效图被改名，这里会先响。
    /// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod Lingjing.Editor.FeedbackProbe.Run
    /// </summary>
    public static class FeedbackProbe
    {
        static int _failures;

        public static void Run()
        {
            CheckNumbers();
            CheckEnemyTable();
            CheckEffects();
            CheckShaders();
            CheckBarSprites();

            Debug.Log(_failures == 0 ? "[feedback] PROBE PASS" : $"[feedback] PROBE FAIL: {_failures} 项");
            if (_failures > 0)
            {
                EditorApplication.Exit(1);
            }
        }

        static void CheckNumbers()
        {
            Expect("半径", Tuning.RingRadius, 2.2);
            Expect("移速", Tuning.PlayerSpeed, 5.4);
            Expect("角速度", Tuning.RingOmega, 3.0);
            Expect("刀伤", Tuning.BladeDamage, 18.0);
            Expect("命中回能", Tuning.EnergyPerHit, 1.0);
            Expect("击杀回能", Tuning.EnergyPerKill, 8.0);
            Expect("代行秒数", Tuning.FairySeconds, 10.0);
            Expect("代行 tick", SimConstants.FairyDurationTicks, 600);
            Expect("代行每级秒数", Tuning.FairySecondsPerLevel, 2.0);
            Expect("代行级数", Tuning.FairyMaxLevels, 4);

            if (Tuning.FairySecondsPerLevel * Tuning.FairyMaxLevels > 10.0)
            {
                Fail("代行成长上限超过初始时长的两倍，成长项失去意义");
            }
        }

        static void CheckEnemyTable()
        {
            // 五种以骸都走同一个构造，数值必须落在表上——这是「一张表管所有以骸」的回归。
            var world = new BattleWorld(new Grid(64, 36), new Vec2(32.0, 18.0), 1UL);
            EnemyKind[] kinds =
            {
                EnemyKind.TailLight, EnemyKind.Striker, EnemyKind.NailGun,
                EnemyKind.Dropper, EnemyKind.ChainBoss
            };

            for (int i = 0; i < kinds.Length; i++)
            {
                EnemyKind kind = kinds[i];
                Enemy enemy = world.Spawn(kind, new Vec2(30.0, 18.0));
                Expect($"{kind} HP", enemy.Hp, EnemyTuning.MaxHp(kind));
                Expect($"{kind} 移速", enemy.MoveSpeed, EnemyTuning.MoveSpeed(kind));
                Expect($"{kind} 半径", enemy.Radius, EnemyTuning.Radius(kind));

                if (enemy.MoveSpeed >= Tuning.PlayerSpeed)
                {
                    Fail($"{kind} 的移速不低于玩家：走位就躲不开了");
                }
            }

            Expect("尾灯 HP", EnemyTuning.MaxHp(EnemyKind.TailLight), 40.0);
            Expect("撞针 HP", EnemyTuning.MaxHp(EnemyKind.Striker), 55.0);
            Expect("钉枪 HP", EnemyTuning.MaxHp(EnemyKind.NailGun), 45.0);
            Expect("坠物 HP", EnemyTuning.MaxHp(EnemyKind.Dropper), 60.0);
            Expect("恶名以骸 HP", EnemyTuning.MaxHp(EnemyKind.ChainBoss), 400.0);

            // 判定体积要盖住可见身体：素材实测普通以骸体宽 36–38px（1.1–1.2 单位），
            // 半径低于 0.45 就会出现「刀从身上划过去却没命中」。
            EnemyKind[] normal =
            {
                EnemyKind.TailLight, EnemyKind.Striker, EnemyKind.NailGun, EnemyKind.Dropper
            };
            for (int i = 0; i < normal.Length; i++)
            {
                if (EnemyTuning.Radius(normal[i]) < 0.45)
                {
                    Fail($"{normal[i]} 的判定半径 {EnemyTuning.Radius(normal[i])} 小于可见身体的八成");
                }
            }

            if (EnemyTuning.Radius(EnemyKind.ChainBoss) < 0.8)
            {
                Fail("恶名以骸的判定半径小于 0.8");
            }
        }

        static void CheckEffects()
        {
            var library = AssetDatabase.LoadAssetAtPath<Lingjing.View.ArtLibrary>(ArtLibraryBuilder.AssetPath);
            if (library == null)
            {
                Fail("找不到 ArtLibrary 资产");
                return;
            }

            string[] required = { "hit", "break", "pickup" };
            for (int i = 0; i < required.Length; i++)
            {
                var clip = library.EffectOf(required[i]);
                if (clip == null || clip.frames.Length == 0 || clip.fps <= 0f)
                {
                    Fail($"特效组 {required[i]} 缺失或没有帧");
                }
            }

            // 飞行物与落区以前「模拟有、画面没有」，这两张图是它们的可见性保障。
            if (AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Items/nail/nail_00.png") == null)
            {
                Fail("钉枪弹丸素材缺失（Items/nail/nail_00.png）");
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Fx/warn/warn_danger.png") == null)
            {
                Fail("落区警示素材缺失（Fx/warn/warn_danger.png）");
            }
        }

        static void CheckShaders()
        {
            Shader shader = Resources.Load<Shader>(Lingjing.View.HitFlash.ShaderPath);
            if (shader == null)
            {
                Fail("找不到 Resources/" + Lingjing.View.HitFlash.ShaderPath);
                return;
            }

            if (!shader.isSupported)
            {
                Fail("SpriteFlash shader 在当前平台不受支持");
            }
        }

        static void CheckBarSprites()
        {
            const string trackPath = "Assets/_Project/Art/Fx/bar/bar_track.png";
            const string fillPath = "Assets/_Project/Art/Fx/bar/bar_fill.png";

            var track = AssetDatabase.LoadAssetAtPath<Sprite>(trackPath);
            var fill = AssetDatabase.LoadAssetAtPath<Sprite>(fillPath);
            if (track == null || fill == null)
            {
                Fail("血条素材缺失（先跑 python tools/art-pipeline/build_bar.py）");
                return;
            }

            ExpectSize("血条轨道", track, 32, 5);
            ExpectSize("血条填充", fill, 1, 3);

            if (track.pixelsPerUnit != 32f || fill.pixelsPerUnit != 32f)
            {
                Fail("血条素材的 PPU 不是 32，会和世界层对不齐");
            }
        }

        static void ExpectSize(string name, Sprite sprite, int width, int height)
        {
            if (Mathf.RoundToInt(sprite.rect.width) != width || Mathf.RoundToInt(sprite.rect.height) != height)
            {
                Fail($"{name} 尺寸是 {sprite.rect.width}×{sprite.rect.height}，应为 {width}×{height}");
            }
        }

        static void Expect(string name, double actual, double expected)
        {
            if (System.Math.Abs(actual - expected) > 1e-6)
            {
                Fail($"{name} = {actual}，期望 {expected}");
            }
        }

        static void Fail(string message)
        {
            _failures++;
            Debug.LogError("[feedback] " + message);
        }
    }
}
