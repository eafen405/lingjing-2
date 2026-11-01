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
            Expect("移速", Tuning.PlayerSpeed, 4.8);
            Expect("角速度", Tuning.RingOmega, 2.5);
            Expect("刀伤", Tuning.BladeDamage, 12.0);
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
            Expect("恶名以骸 HP", EnemyTuning.MaxHp(EnemyKind.ChainBoss), 600.0);
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
