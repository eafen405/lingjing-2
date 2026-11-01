using System.Collections.Generic;
using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Fairy;
using Lingjing.Sim;
using Lingjing.UI;
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
        public ArtLibrary art;
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
        readonly Dictionary<int, EnemyView> _enemyViews = new Dictionary<int, EnemyView>();
        readonly List<int> _staleIds = new List<int>();
        ActorAnimator _playerAnim;

        sealed class EnemyView
        {
            public Transform tx;
            public ActorAnimator anim;
            public Vector3 last;
        }

        double _accumulator;
        double _hits;
        double _kills;

        HudView _hud;
        PauseScreen _pause;
        ResultScreen _result;
        bool _paused;
        bool _resultShown;

        public BattleWorld World => _world;
        public bool Ready => _world != null;
        public Transform PlayerTransform => _playerTx;
        public bool Paused => _paused;
        public bool ResultShown => _resultShown;

        /// <summary>占位结算口径：击杀 ×100 + 命中 ×10；正式数值接入后替换。</summary>
        public int Score => Mathf.RoundToInt((float)(_kills * 100.0 + _hits * 10.0));

        void Awake()
        {
            if (input == null)
            {
                input = TouchHud.Build();
            }

            _source = input as ICommandSource;
            Build();
            BuildUi();
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
            _world.SetFairyPlanner(new LexPlanner());
            _world.BladeHit += (_, __) => _hits++;
            _world.EnemyKilled += OnEnemyKilled;

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

            _playerAnim = go.AddComponent<ActorAnimator>();
            _playerAnim.target = _playerSr;
            _playerAnim.Init(art, "eous", "idle");
        }

        /// <summary>以骸死亡：留一具放死亡帧的尸体，播完自毁。</summary>
        void OnEnemyKilled(Enemy enemy)
        {
            _kills++;

            var go = new GameObject("Corpse" + enemy.Id);
            go.transform.position = new Vector3((float)enemy.Pos.X, (float)enemy.Pos.Y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 7;

            var anim = go.AddComponent<ActorAnimator>();
            anim.target = sr;
            string entity = EntityOf(enemy.Kind);
            anim.Init(art, entity, "death");
            anim.Face(new Vec2(enemy.Pos.X < World.World.Player.Pos.X ? -1 : 1, 0));

            Destroy(go, Mathf.Max(0.2f, anim.Duration("death")));
        }

        static string EntityOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.TailLight: return "taillight";
                case EnemyKind.Striker: return "striker";
                case EnemyKind.NailGun: return "nailgun";
                case EnemyKind.Dropper: return "dropper";
                case EnemyKind.ChainBoss: return "chain";
                default: return "taillight";
            }
        }

        void CreateBlades()
        {
            var root = new GameObject("Blades");
            _bladeRoot = root.transform;
            EnsureBladeViews(_world.World.Player.Ring.BladeCount);
        }

        /// <summary>刀数随成长增加，精灵按需补；只增不减。</summary>
        void EnsureBladeViews(int count)
        {
            while (_blades.Count < count)
            {
                var go = new GameObject("Blade" + _blades.Count);
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
            // 暂停/结算期间直接不推进世界；Time.timeScale 只负责停表现层动画。
            if (_world == null || _paused || _resultShown)
            {
                return;
            }

            _accumulator += Time.deltaTime;

            if (_source != null && _source.TakeSkillPress())
            {
                _world.TryActivateFairy();
            }

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
            UpdateHud();

            if (_world.PlayerDead)
            {
                Finish(false);
            }
        }

        /// <summary>推进 n 个固定步长并同步。无头探针与回归都走这里。</summary>
        public void StepFixed(int count, Command command)
        {
            if (_world == null || _paused || _resultShown)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                _world.Step(command);
            }

            Sync();
            UpdateHud();

            if (_world.PlayerDead)
            {
                Finish(false);
            }
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

            bool moving = p.Vel.LengthSquared > 0.04;
            if (_playerAnim != null)
            {
                _playerAnim.SetState(moving ? "walk" : "idle");
                if (moving)
                {
                    _playerAnim.Face(p.Vel);
                }
            }

            EnsureBladeViews(p.Ring.BladeCount);
            for (int i = 0; i < p.Ring.BladeCount; i++)
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
                if (!_enemyViews.TryGetValue(enemy.Id, out EnemyView view))
                {
                    var go = new GameObject("Enemy" + enemy.Id);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = enemySprite;
                    sr.sortingOrder = 8;

                    var anim = go.AddComponent<ActorAnimator>();
                    anim.target = sr;
                    anim.Init(art, EntityOf(enemy.Kind), "idle");

                    view = new EnemyView { tx = go.transform, anim = anim, last = go.transform.position };
                    _enemyViews.Add(enemy.Id, view);
                }

                var now = new Vector3((float)enemy.Pos.X, (float)enemy.Pos.Y, 0f);
                Vector3 delta = now - view.last;
                view.last = now;
                view.tx.position = now;

                if (view.anim != null)
                {
                    view.anim.SetState(EnemyAnimMap.StateFor(enemy));
                    view.anim.Face(new Vec2(delta.x, delta.y));
                }
            }

            _staleIds.Clear();
            foreach (KeyValuePair<int, EnemyView> pair in _enemyViews)
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
                EnemyView view = _enemyViews[_staleIds[i]];
                if (view.tx != null)
                {
                    Destroy(view.tx.gameObject);
                }

                _enemyViews.Remove(_staleIds[i]);
            }
        }

        void BuildUi()
        {
            UiKit.EnsureEventSystem();
            _hud = HudView.Build(Pause);
            _pause = PauseScreen.Build(Resume, Restart, ToMainMenu);
            _result = ResultScreen.Build(Restart, ToMainMenu);
            UpdateHud();
        }

        void UpdateHud()
        {
            if (_hud == null || _world == null)
            {
                return;
            }

            Player p = _world.World.Player;
            float fairySeconds = (float)(_world.FairyTicksLeft * SimConstants.Dt);
            _hud.SetStats((float)p.Hp, (float)p.MaxHp, (float)p.Energy,
                (float)DamageRules.EnergyMax, _world.FairyActive, fairySeconds);
        }

        /// <summary>暂停：冻结时间缩放并停掉世界推进，界面只负责显示。</summary>
        public void Pause()
        {
            if (_world == null || _paused || _resultShown || _world.PlayerDead || _pause == null)
            {
                return;
            }

            _paused = true;
            Time.timeScale = 0f;
            _pause.Show();
        }

        public void Resume()
        {
            if (!_paused)
            {
                return;
            }

            _paused = false;
            Time.timeScale = 1f;
            _pause?.Hide();
        }

        void Restart()
        {
            Time.timeScale = 1f;
            UiNav.Restart();
        }

        void ToMainMenu()
        {
            Time.timeScale = 1f;
            UiNav.MainMenu();
        }

        /// <summary>死亡结算：提交占位分数后冻结世界并弹结算面板。</summary>
        void Finish(bool clear)
        {
            if (_resultShown || _result == null)
            {
                return;
            }

            _resultShown = true;
            _paused = false;

            int score = Score;
            GameSave.SubmitScore(score);

            Time.timeScale = 0f;
            // HUD 常驻：结算叠在 HUD 之上（Doozy sortingOrder 110 > 20），不隐藏 HUD。
            _pause?.Hide();
            _result.Show(clear, score, GameSave.HighScore);
        }

        void OnDestroy()
        {
            // 暂停/结算把 timeScale 压成 0，切场景或退出时恢复，避免污染后续场景。
            Time.timeScale = 1f;
        }
    }
}
