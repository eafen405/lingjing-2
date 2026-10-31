using System.Collections.Generic;
using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Fairy;
using Lingjing.Sim;
using UnityEngine;
using Grid = Lingjing.Nav.Grid;

namespace Lingjing.View
{
    /// <summary>
    /// M1 竖切的表现层入口：持有 BattleWorld，按固定 1/60 推进，把世界状态同步到精灵。
    /// 世界是唯一权威，这里只读不改；所有位置都直接取自模拟，不做插值（像素对齐优先）。
    /// </summary>
    public sealed class BattleRunner : MonoBehaviour
    {
        [Header("精灵")]
        public Sprite playerSprite;
        public Sprite enemySprite;
        public Sprite bladeSprite;

        [Header("关卡")]
        public int mapWidth = 40;
        public int mapHeight = 24;
        public ulong seed = 7;
        public int initialEnemies = 6;

        [Header("引用")]
        [Tooltip("留空则运行时自动搭触屏 HUD（摇杆 + 两个按钮）")]
        public MonoBehaviour input;
        public Camera viewCamera;

        ICommandSource _source;

        BattleWorld _world;
        Transform _playerTx;
        SpriteRenderer _playerSr;
        Transform _bladeRoot;
        readonly List<Transform> _blades = new List<Transform>();
        readonly Dictionary<int, Transform> _enemyViews = new Dictionary<int, Transform>();
        readonly List<int> _staleIds = new List<int>();

        double _accumulator;
        double _hits;
        double _kills;

        public BattleWorld World => _world;
        public bool Ready => _world != null;
        public Transform PlayerTransform => _playerTx;

        void Awake()
        {
            if (input == null)
            {
                input = TouchHud.Build();
            }

            _source = input as ICommandSource;
            Build();
        }

        /// <summary>建世界。测试与探针可直接调用，不必进播放模式。</summary>
        public void Build()
        {
            if (_world != null)
            {
                return;
            }

            var grid = new Grid(mapWidth, mapHeight);
            for (int x = 0; x < mapWidth; x++)
            {
                grid.SetWall(x, 0);
                grid.SetWall(x, mapHeight - 1);
            }

            for (int y = 0; y < mapHeight; y++)
            {
                grid.SetWall(0, y);
                grid.SetWall(mapWidth - 1, y);
            }

            // 两段掩体，先给走位一点形状。
            grid.FillRect(10, 6, 1, 6);
            grid.FillRect(26, 12, 1, 6);

            _world = new BattleWorld(grid, new Vec2(mapWidth * 0.5, mapHeight * 0.5), seed);
            _world.BladeHit += (_, __) => _hits++;
            _world.EnemyKilled += _ => _kills++;

            CreatePlayer();
            CreateBlades();
            SpawnInitialEnemies();
            Sync();
        }

        void CreatePlayer()
        {
            var go = new GameObject("Eous");
            _playerTx = go.transform;
            _playerSr = go.AddComponent<SpriteRenderer>();
            _playerSr.sprite = playerSprite;
            _playerSr.sortingOrder = 10;
        }

        void CreateBlades()
        {
            var root = new GameObject("Blades");
            _bladeRoot = root.transform;
            for (int i = 0; i < BladeRing.BladeCount; i++)
            {
                var go = new GameObject("Blade" + i);
                go.transform.SetParent(_bladeRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = bladeSprite;
                sr.sortingOrder = 12;
                _blades.Add(go.transform);
            }
        }

        void SpawnInitialEnemies()
        {
            for (int i = 0; i < initialEnemies; i++)
            {
                double angle = i * 6.28318530718 / Mathf.Max(1, initialEnemies);
                var pos = new Vec2(
                    mapWidth * 0.5 + System.Math.Cos(angle) * 7.0,
                    mapHeight * 0.5 + System.Math.Sin(angle) * 5.0);
                _world.Spawn(EnemyKind.TailLight, pos);
            }
        }

        void Update()
        {
            if (_world == null)
            {
                return;
            }

            _accumulator += Time.deltaTime;
            int guard = 0;
            while (_accumulator >= SimConstants.Dt && guard++ < 8)
            {
                Command command = _source != null ? _source.CurrentCommand : Command.None;
                _world.Step(command);
                _accumulator -= SimConstants.Dt;
            }

            if (_accumulator > SimConstants.Dt)
            {
                _accumulator = 0;
            }

            Sync();
        }

        /// <summary>推进 n 个固定步长并同步。无头探针与回归都走这里。</summary>
        public void StepFixed(int count, Command command)
        {
            if (_world == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                _world.Step(command);
            }

            Sync();
        }

        /// <summary>把世界状态同步到精灵。只在表现层做读写，不回写世界。</summary>
        public void Sync()
        {
            if (_world == null)
            {
                return;
            }

            Player p = _world.World.Player;
            _playerTx.position = new Vector3((float)p.Pos.X, (float)p.Pos.Y, 0f);

            if (Mathf.Abs((float)p.Vel.X) > 0.01f)
            {
                _playerSr.flipX = p.Vel.X < 0;
            }

            for (int i = 0; i < _blades.Count; i++)
            {
                Vec2 tip = p.Ring.TipAt(p.Pos, i);
                Transform blade = _blades[i];
                blade.position = new Vector3((float)tip.X, (float)tip.Y, 0f);
                double dx = tip.X - p.Pos.X;
                double dy = tip.Y - p.Pos.Y;
                blade.rotation = Quaternion.Euler(0f, 0f, (float)(System.Math.Atan2(dy, dx) * Mathf.Rad2Deg));
            }

            // 以骸：按 Id 绑定，死亡被移除的下帧销毁。
            List<Enemy> enemies = _world.World.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (!_enemyViews.TryGetValue(enemy.Id, out Transform tx))
                {
                    var go = new GameObject("Enemy" + enemy.Id);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = enemySprite;
                    sr.sortingOrder = 8;
                    tx = go.transform;
                    _enemyViews.Add(enemy.Id, tx);
                }

                tx.position = new Vector3((float)enemy.Pos.X, (float)enemy.Pos.Y, 0f);
            }

            _staleIds.Clear();
            foreach (KeyValuePair<int, Transform> pair in _enemyViews)
            {
                bool alive = false;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].Id == pair.Key)
                    {
                        alive = true;
                        break;
                    }
                }

                if (!alive)
                {
                    _staleIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleIds.Count; i++)
            {
                Transform tx = _enemyViews[_staleIds[i]];
                if (tx != null)
                {
                    Destroy(tx.gameObject);
                }

                _enemyViews.Remove(_staleIds[i]);
            }
        }

        void OnGUI()
        {
            if (_world == null)
            {
                return;
            }

            Player p = _world.World.Player;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.Label(new Rect(16, 12, 420, 28), $"HP {p.Hp:0}/{p.MaxHp:0}", style);
            GUI.Label(new Rect(16, 40, 420, 28), $"充能 {p.Energy:0}/{DamageRules.EnergyMax:0}", style);
            GUI.Label(new Rect(16, 68, 420, 28), $"命中 {_hits:0}  击杀 {_kills:0}  以骸 {_world.World.Enemies.Count}", style);
        }
    }
}
