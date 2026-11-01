using System.Collections.Generic;
using Lingjing.AI;
using Lingjing.Audio;
using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Fairy;
using Lingjing.Level;
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
        public Sprite healthSprite;
        public Sprite energySprite;

        [Header("关卡")]
        [Tooltip("levels.json（Assets/_Project/Data），由场景生成器烘进来")]
        public TextAsset levelJson;
        public int levelId = 1;
        public ulong seed = 7;

        [Header("音频")]
        [Tooltip("voice.json（Assets/_Project/Data）：台词 id → 字幕文本")]
        public TextAsset voiceJson;

        [Header("引用")]
        [Tooltip("留空则运行时自动搭触屏 HUD（摇杆 + 两个按钮）")]
        public MonoBehaviour input;
        public Camera viewCamera;

        ICommandSource _source;

        BattleWorld _world;
        LevelRun _run;
        AudioDirector _audio;
        int _lastSectionIndex = -1;
        bool _bossAnnounced;
        Transform _playerTx;
        SpriteRenderer _playerSr;
        Transform _bladeRoot;
        readonly List<Transform> _blades = new List<Transform>();
        readonly Dictionary<int, EnemyView> _enemyViews = new Dictionary<int, EnemyView>();
        readonly List<int> _staleIds = new List<int>();
        readonly Dictionary<int, SpriteRenderer> _pickupViews = new Dictionary<int, SpriteRenderer>();
        readonly List<int> _stalePickupIds = new List<int>();
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
        ChoiceScreen _choice;
        ResultScreen _result;
        bool _paused;
        bool _resultShown;
        bool _choiceShown;

        public BattleWorld World => _world;
        public bool Ready => _world != null;
        public LevelRun Run => _run;
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
            _audio = AudioDirector.Attach(gameObject, VoiceLineTable.Parse(voiceJson != null ? voiceJson.text : null));
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

            LevelDef level = LoadCurrentLevel();
            if (level == null)
            {
                return;
            }

            Grid grid = LevelField.Build(level);
            _world = new BattleWorld(grid, LevelField.PlayerStart(grid), seed);
            _world.SetFairyPlanner(new LexPlanner());
            _world.BladeHit += OnBladeHit;
            _world.EnemyKilled += OnEnemyKilled;
            _world.PickupTaken += OnPickupTaken;
            _world.FairyStarted += OnFairyStarted;
            _world.FairyEnded += OnFairyEnded;
            _world.PlayerDied += OnPlayerDied;

            // 一局的推进规则独立于世界：世界只管 1/60 步进，节与波次归 LevelRun。
            _run = new LevelRun(level, _world, seed);

            CreatePlayer();
            CreateBlades();

            // 立刻投第一波，避免开场空场等一个 tick。
            _run.Tick();
            Sync();

            _audio?.Say("vo_fairy_level_start_1");
        }

        void OnBladeHit(Enemy enemy, double damage)
        {
            _hits++;
            _audio?.PlaySfxThrottled("sfx_hit", 0.045f, 0.7f);
        }

        void OnPickupTaken(Pickup pickup)
        {
            _audio?.PlaySfx(pickup.Kind == PickupKind.Health ? "sfx_pickup_health" : "sfx_pickup_energy", 0.8f);
        }

        void OnFairyStarted()
        {
            _audio?.PlaySfx("sfx_fairy_start");
            _audio?.Say("vo_fairy_daika_start");
        }

        void OnFairyEnded()
        {
            _audio?.PlaySfx("sfx_fairy_end");
            _audio?.Say("vo_fairy_daika_end");
        }

        void OnPlayerDied()
        {
            _audio?.PlaySfx("sfx_death");
            _audio?.Say("vo_fairy_death");
        }

        LevelDef LoadCurrentLevel()
        {
            if (levelJson == null)
            {
                Debug.LogError("[battle] 没有 levelJson：场景生成器没把 levels.json 烘进场景");
                return null;
            }

            LevelCatalog catalog = JsonUtility.FromJson<LevelCatalog>(levelJson.text);
            LevelDef level = catalog != null ? catalog.Find(levelId) : null;
            if (level == null)
            {
                Debug.LogError($"[battle] levels.json 里找不到 id={levelId} 的关卡");
            }

            return level;
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
            _audio?.PlaySfx("sfx_kill", 0.8f);

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

        void Update()
        {
            if (_world == null || _resultShown)
            {
                return;
            }

            // 升级待选优先于一切：世界停住，等玩家选完再继续（§7.1「升级时暂停游戏」）。
            if (_run != null && _run.Growth.HasPendingChoice)
            {
                ShowChoice();
                return;
            }

            // 暂停/结算期间直接不推进世界；Time.timeScale 只负责停表现层动画。
            if (_paused)
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
                _run?.Tick();
                _accumulator -= SimConstants.Dt;
            }

            if (_accumulator > SimConstants.Dt)
            {
                _accumulator = 0;
            }

            Sync();
            UpdateHud();
            CheckOutcome();
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
                // 待选期间停住：探针不会自己选牌，硬推下去只会让节卡在原地。
                if (_run != null && _run.Growth.HasPendingChoice)
                {
                    break;
                }

                _world.Step(command);
                _run?.Tick();
            }

            Sync();
            UpdateHud();
            CheckOutcome();
        }

        /// <summary>结局的唯一出口：Cleared 才推进解锁，Failed 不改任何进度。</summary>
        void CheckOutcome()
        {
            if (_resultShown || _run == null)
            {
                return;
            }

            if (_run.Outcome == RunOutcome.Cleared)
            {
                Finish(true);
            }
            else if (_run.Outcome == RunOutcome.Failed)
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

            SyncPickups();
        }

        /// <summary>掉落物：按 Id 绑定，被拾取后下一帧销毁。位置在生成时就定死，不会移动。</summary>
        void SyncPickups()
        {
            List<Pickup> pickups = _world.World.Pickups;
            for (int i = 0; i < pickups.Count; i++)
            {
                Pickup pickup = pickups[i];
                if (_pickupViews.ContainsKey(pickup.Id))
                {
                    continue;
                }

                var go = new GameObject("Pickup" + pickup.Id);
                go.transform.position = new Vector3((float)pickup.Pos.X, (float)pickup.Pos.Y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = pickup.Kind == PickupKind.Health ? healthSprite : energySprite;
                sr.sortingOrder = 6;
                _pickupViews.Add(pickup.Id, sr);
            }

            _stalePickupIds.Clear();
            foreach (KeyValuePair<int, SpriteRenderer> pair in _pickupViews)
            {
                bool alive = false;
                for (int i = 0; i < pickups.Count; i++)
                {
                    if (pickups[i].Id == pair.Key)
                    {
                        alive = true;
                        break;
                    }
                }

                if (!alive)
                {
                    _stalePickupIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < _stalePickupIds.Count; i++)
            {
                SpriteRenderer sr = _pickupViews[_stalePickupIds[i]];
                if (sr != null)
                {
                    Destroy(sr.gameObject);
                }

                _pickupViews.Remove(_stalePickupIds[i]);
            }
        }

        void BuildUi()
        {
            UiKit.EnsureEventSystem();
            _hud = HudView.Build(Pause);
            _pause = PauseScreen.Build(Resume, Restart, ToMainMenu);
            _choice = ChoiceScreen.Build(OnGrowthChosen);
            _result = ResultScreen.Build(Restart, ToMainMenu);
            UpdateHud();
        }

        /// <summary>升级待选期间世界不推进，所以这里只需要把面板弹出来一次。</summary>
        void ShowChoice()
        {
            if (_choiceShown || _choice == null || _run == null)
            {
                return;
            }

            _choiceShown = true;
            _audio?.PlaySfx("sfx_level_up");
            _choice.Show(_run.Growth);
        }

        void OnGrowthChosen(GrowthKind kind)
        {
            _choiceShown = false;
            _audio?.PlaySfx("sfx_choose", 0.9f);
            if (_run == null || _world == null)
            {
                return;
            }

            _run.Growth.Choose(_world.World.Player, kind);
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
            _hud.SetSubtitle(_audio != null ? _audio.Subtitle : null);

            if (_run != null && _run.SectionCount > 0)
            {
                _hud.SetSection(_run.SectionIndex + 1, _run.SectionCount, _run.Intermission);

                if (_run.SectionIndex != _lastSectionIndex)
                {
                    // -1 是开局那一节，不算"刚清掉上一节"。
                    if (_lastSectionIndex >= 0)
                    {
                        _audio?.PlaySfx("sfx_section_clear", 0.8f);
                    }

                    _lastSectionIndex = _run.SectionIndex;
                }
            }

            AnnounceBossIfAny();
        }

        /// <summary>恶名以骸第一次出现在场上时喊一次。</summary>
        void AnnounceBossIfAny()
        {
            if (_bossAnnounced)
            {
                return;
            }

            List<Enemy> enemies = _world.World.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Kind != EnemyKind.ChainBoss)
                {
                    continue;
                }

                _bossAnnounced = true;
                _audio?.PlaySfx("sfx_boss_intro");
                _audio?.Say("vo_fairy_boss_intro");
                return;
            }
        }

        /// <summary>暂停：冻结时间缩放并停掉世界推进，界面只负责显示。</summary>
        public void Pause()
        {
            if (_world == null || _paused || _resultShown || _world.PlayerDead || _pause == null)
            {
                return;
            }

            // 三选一优先于手动暂停：不然玩家会把待选面板压在暂停底下再也看不到。
            if (_run != null && _run.Growth.HasPendingChoice)
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

        /// <summary>结算：通关推进解锁，死亡不改进度；两者都提交分数并冻结世界。</summary>
        void Finish(bool clear)
        {
            if (_resultShown || _result == null)
            {
                return;
            }

            _resultShown = true;
            _paused = false;

            if (clear)
            {
                // 解锁的唯一来源是通关本身（game-design §2 / §8）。
                GameSave.UnlockedLevel = levelId + 1;
                _audio?.PlaySfx("sfx_win");
            }

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
