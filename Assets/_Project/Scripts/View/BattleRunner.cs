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
    /// 表现层入口：持有 BattleWorld，按固定 1/60 推进，把世界状态同步到精灵。
    /// 世界是唯一权威，这里只读不改；所有位置都直接取自模拟，不做插值（像素对齐优先）。
    /// 打击感（火花 / 闪白 / 顿帧 / 震屏 / 抖动）全部只发生在这里，模拟层一无所知。
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

        [Header("表现")]
        [Tooltip("以骸血条轨道 32×5（Art/Fx/bar/bar_track.png），由场景生成器烘进来")]
        public Sprite healthBarTrack;

        [Tooltip("以骸血条填充 1×3（Art/Fx/bar/bar_fill.png）")]
        public Sprite healthBarFill;

        [Tooltip("钉枪的铆钉（Art/Items/nail/nail_00.png），飞行方向为 +x")]
        public Sprite projectileSprite;

        [Tooltip("坠物落区（Art/Fx/warn/warn_danger.png），按矩形拉伸")]
        public Sprite zoneSprite;

        [Header("关卡")]
        [Tooltip("levels.json（Assets/_Project/Data），由场景生成器烘进来")]
        public TextAsset levelJson;
        public int levelId = 1;
        public ulong seed = 7;

        [Header("音频")]
        [Tooltip("voice.json（Assets/_Project/Data）：台词 id → 字幕文本")]
        public TextAsset voiceJson;

        [Header("地形")]
        public Sprite wallNorth;
        public Sprite wallSouth;
        public Sprite wallEast;
        public Sprite wallWest;
        public Sprite wallNorthEast;
        public Sprite wallNorthWest;
        public Sprite wallSouthEast;
        public Sprite wallSouthWest;
        public Sprite propBlock;

        [Header("引用")]
        [Tooltip("留空则运行时自动搭触屏 HUD（摇杆 + 两个按钮）")]
        public MonoBehaviour input;
        public Camera viewCamera;

        /// <summary>命中闪白的持续时长（秒）。够短才不会糊成一片白。</summary>
        public const float HitFlashSeconds = 0.06f;

        /// <summary>受击抖动的持续时长（秒）。</summary>
        public const float HitShakeSeconds = 0.1f;

        /// <summary>击杀顿帧时长（秒）：3–4 帧，只在击杀触发。</summary>
        public const float KillFreezeSeconds = 0.06f;

        /// <summary>命中顿帧时长（秒）：约 2 帧。很轻，但要能感觉到刀刃"咬"进身体。</summary>
        public const float HitFreezeSeconds = 0.035f;

        /// <summary>两次命中顿帧之间的最小间隔（秒）：多刀同时命中时不能每击都停。</summary>
        public const float HitFreezeCooldownSeconds = 0.12f;

        ICommandSource _source;

        BattleWorld _world;
        LevelRun _run;
        AudioDirector _audio;
        EffectPool _fx;
        Material _flashMaterial;
        CameraFollow _camera;
        float _freezeUntil;
        float _nextHitFreeze;
        float _playerFlashUntil;
        bool _playerFlashApplied;
        int _lastSectionIndex = -1;
        bool _bossAnnounced;
        Transform _playerTx;
        SpriteRenderer _playerSr;
        Transform _bladeRoot;
        Transform _barRoot;
        readonly List<Transform> _blades = new List<Transform>();
        readonly Dictionary<int, EnemyView> _enemyViews = new Dictionary<int, EnemyView>();
        readonly List<int> _staleIds = new List<int>();
        readonly HashSet<int> _aliveIds = new HashSet<int>();
        readonly Dictionary<int, SpriteRenderer> _pickupViews = new Dictionary<int, SpriteRenderer>();
        readonly List<int> _stalePickupIds = new List<int>();
        readonly Dictionary<Projectile, Transform> _shotViews = new Dictionary<Projectile, Transform>();
        readonly Dictionary<HazardZone, Transform> _zoneViews = new Dictionary<HazardZone, Transform>();
        readonly List<Projectile> _staleShots = new List<Projectile>();
        readonly List<HazardZone> _staleZones = new List<HazardZone>();
        ActorAnimator _playerAnim;

        sealed class EnemyView
        {
            public Transform tx;
            public ActorAnimator anim;
            public SpriteRenderer sr;
            public EnemyHealthBar bar;
            public Vector3 last;
            public float shakeUntil;
            public float flashUntil;
            public bool flashApplied;
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

        /// <summary>玩家当前速度（单位/秒），给相机做前瞻用。</summary>
        public Vector2 PlayerVelocity => _world != null
            ? new Vector2((float)_world.World.Player.Vel.X, (float)_world.World.Player.Vel.Y)
            : Vector2.zero;
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
            _flashMaterial = HitFlash.Material;
            _camera = viewCamera != null ? viewCamera.GetComponent<CameraFollow>() : null;
            _audio = AudioDirector.Attach(gameObject, VoiceLineTable.Parse(voiceJson != null ? voiceJson.text : null));
            Build();
            BuildUi();
            DebugOverlay.Build(this);
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
            _world.PlayerHurt += OnPlayerHurt;
            _world.FairyStarted += OnFairyStarted;
            _world.FairyEnded += OnFairyEnded;
            _world.PlayerDied += OnPlayerDied;

            // 一局的推进规则独立于世界：世界只管 1/60 步进，节与波次归 LevelRun。
            _run = new LevelRun(level, _world, seed);

            _fx = new EffectPool(transform, art);
            var barRootGo = new GameObject("HpBars");
            _barRoot = barRootGo.transform;
            _barRoot.SetParent(transform, false);
            CreatePlayer();
            CreateBlades();
            BuildTerrain(level, grid);

            // 立刻投第一波，避免开场空场等一个 tick。
            _run.Tick();
            Sync();

            _audio?.Say("vo_fairy_level_start_1");
        }

        /// <summary>
        /// 把场地的边界与障碍画出来。没有这一步，场地就是一片纯色——玩家会撞上看不见的墙。
        /// 边界用 8 块朝向墙瓦（32×32，中心锚点），障碍用 prop_block（64×64，底边中心锚点）。
        /// </summary>
        void BuildTerrain(LevelDef level, Grid grid)
        {
            var root = new GameObject("Terrain");
            Transform terrainRoot = root.transform;

            int width = grid.Width;
            int height = grid.Height;
            const float terrainOrder = -10f;

            for (int x = 0; x < width; x++)
            {
                Sprite bottom = x == 0 ? wallSouthWest : x == width - 1 ? wallSouthEast : wallSouth;
                Sprite top = x == 0 ? wallNorthWest : x == width - 1 ? wallNorthEast : wallNorth;
                PlaceTile(terrainRoot, bottom, x + 0.5f, 0.5f, terrainOrder);
                PlaceTile(terrainRoot, top, x + 0.5f, height - 0.5f, terrainOrder);
            }

            for (int y = 1; y < height - 1; y++)
            {
                PlaceTile(terrainRoot, wallWest, 0.5f, y + 0.5f, terrainOrder);
                PlaceTile(terrainRoot, wallEast, width - 0.5f, y + 0.5f, terrainOrder);
            }

            RectDef[] obstacles = level?.obstacles;
            if (obstacles == null)
            {
                return;
            }

            // 障碍是 2×2 格一块的道具图，大矩形按块拼，不拉伸。
            for (int i = 0; i < obstacles.Length; i++)
            {
                RectDef rect = obstacles[i];
                if (rect == null)
                {
                    continue;
                }

                for (int y = rect.y; y + 2 <= rect.y + rect.h; y += 2)
                {
                    for (int x = rect.x; x + 2 <= rect.x + rect.w; x += 2)
                    {
                        PlaceTile(terrainRoot, propBlock, x + 1f, y, terrainOrder + 1f);
                    }
                }
            }
        }

        static void PlaceTile(Transform parent, Sprite sprite, float x, float y, float sortingOrder)
        {
            if (sprite == null)
            {
                return;
            }

            var go = new GameObject("Tile");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = (int)sortingOrder;
        }

        void OnBladeHit(Enemy enemy, double damage)
        {
            _hits++;
            _audio?.PlaySfxThrottled("sfx_hit", 0.045f, 0.7f);

            // 命中给 2 帧顿帧 + 1px 震。限频到 0.12 秒一次：多刀同时切上去时不能每击都停。
            if (Time.unscaledTime >= _nextHitFreeze)
            {
                _nextHitFreeze = Time.unscaledTime + HitFreezeCooldownSeconds;
                Freeze(HitFreezeSeconds);
                _camera?.Shake(1, 0.08f);
            }

            DamageNumber.Spawn(new Vector3((float)enemy.Pos.X, (float)enemy.Pos.Y, 0f), damage);

            // 火花放在刀刃压上去的那一侧，不是敌人中心：刀从哪个方向切进来，火花就从哪儿炸。
            if (_fx != null && _world != null)
            {
                Vec2 player = _world.World.Player.Pos;
                Vec2 toEnemy = enemy.Pos - player;
                Vec2 sparkAt = toEnemy == Vec2.Zero
                    ? enemy.Pos
                    : enemy.Pos - toEnemy.Normalized() * (enemy.Radius * 0.6);
                _fx.Play("hit", new Vector3((float)sparkAt.X, (float)sparkAt.Y, 0f));
            }

            if (_enemyViews.TryGetValue(enemy.Id, out EnemyView view))
            {
                view.flashUntil = Time.time + HitFlashSeconds;
                view.shakeUntil = Time.time + HitShakeSeconds;
                view.bar?.MarkDamaged();
            }
        }

        void OnPickupTaken(Pickup pickup)
        {
            _audio?.PlaySfx(pickup.Kind == PickupKind.Health ? "sfx_pickup_health" : "sfx_pickup_energy", 0.8f);
            var at = new Vector3((float)pickup.Pos.X, (float)pickup.Pos.Y, 0f);
            _fx?.Play("pickup", at);
            TweenFx.PopSprite(pickup.Kind == PickupKind.Health ? healthSprite : energySprite, at, 9);
        }

        void OnPlayerHurt(double amount)
        {
            _audio?.PlaySfxThrottled("sfx_hurt", 0.3f, 0.85f);
            _playerFlashUntil = Time.time + HitFlashSeconds * 1.6f;
            _hud?.PulseHurt();
            _camera?.Shake(2, 0.16f);
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
            _playerAnim?.SetState("death");
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
            if (_flashMaterial != null)
            {
                _playerSr.sharedMaterial = _flashMaterial;
            }

            _playerAnim = go.AddComponent<ActorAnimator>();
            _playerAnim.target = _playerSr;
            _playerAnim.Init(art, "eous", "idle");
        }

        /// <summary>以骸死亡：留一具放死亡帧的尸体，播完自毁。</summary>
        void OnEnemyKilled(Enemy enemy)
        {
            _kills++;
            _audio?.PlaySfx("sfx_kill", 0.8f);

            var position = new Vector3((float)enemy.Pos.X, (float)enemy.Pos.Y, 0f);
            _fx?.Play("break", position);

            var go = new GameObject("Corpse" + enemy.Id);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 7;

            var anim = go.AddComponent<ActorAnimator>();
            anim.target = sr;
            string entity = EntityOf(enemy.Kind);
            anim.Init(art, entity, "death");
            anim.Face(new Vec2(enemy.Pos.X < World.World.Player.Pos.X ? -1 : 1, 0));

            Destroy(go, Mathf.Max(0.2f, anim.Duration("death")));

            // 顿帧 + 微震：只给击杀，命中不给——命中频率太高，每击都停会毁掉节奏。
            Freeze(KillFreezeSeconds);
            _camera?.Shake(1, 0.12f);
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

        /// <summary>画面冻结一小段（只在击杀）。和暂停共用 timeScale，所以这里只负责"解冻到 1"。</summary>
        void Freeze(float seconds)
        {
            float until = Time.unscaledTime + Mathf.Max(0f, seconds);
            if (until > _freezeUntil)
            {
                _freezeUntil = until;
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

            // 顿帧：世界与动画一起定住 3–4 帧，只留相机在震。
            if (Time.unscaledTime < _freezeUntil)
            {
                if (Time.timeScale != 0f)
                {
                    Time.timeScale = 0f;
                }

                return;
            }

            if (Time.timeScale != 1f)
            {
                Time.timeScale = 1f;
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
            _fx?.Step(Time.deltaTime);
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

            SyncPlayerFlash();

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

            SyncEnemies();
            SyncPickups();
            SyncProjectiles();
            SyncZones();
        }

        void SyncPlayerFlash()
        {
            if (_playerSr == null || _flashMaterial == null)
            {
                return;
            }

            float amount = 0f;
            if (Time.time < _playerFlashUntil)
            {
                amount = Mathf.Clamp01((_playerFlashUntil - Time.time) / (HitFlashSeconds * 1.6f));
            }

            // 只在真的闪的时候写：每帧都 SetPropertyBlock 是一笔白给的 native 调用。
            if (amount > 0f || _playerFlashApplied)
            {
                HitFlash.Apply(_playerSr, amount, new Color(1f, 0.35f, 0.35f, 1f));
                _playerFlashApplied = amount > 0f;
            }
        }

        void SyncEnemies()
        {
            List<Enemy> enemies = _world.World.Enemies;
            _aliveIds.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                _aliveIds.Add(enemies[i].Id);
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (!_enemyViews.TryGetValue(enemy.Id, out EnemyView view))
                {
                    view = CreateEnemyView(enemy);
                }

                var now = new Vector3((float)enemy.Pos.X, (float)enemy.Pos.Y, 0f);
                Vector3 delta = now - view.last;
                view.last = now;

                // 受击抖动只动渲染位置，不动模拟坐标：确定性一点不受影响。
                Vector3 shake = Vector3.zero;
                if (Time.time < view.shakeUntil)
                {
                    const float pixel = 1f / 32f;
                    shake = new Vector3(
                        Random.Range(-1, 2) * pixel,
                        Random.Range(-1, 2) * pixel,
                        0f);
                }

                view.tx.position = now + shake;

                if (view.anim != null)
                {
                    view.anim.SetState(EnemyAnimMap.StateFor(enemy));
                    view.anim.Face(new Vec2(delta.x, delta.y));
                }

                SyncEnemyFlash(view);

                if (view.bar != null)
                {
                    view.bar.Sync(enemy.Hp, enemy.MaxHp, now, Time.deltaTime);
                }
            }

            _staleIds.Clear();
            foreach (KeyValuePair<int, EnemyView> pair in _enemyViews)
            {
                if (!_aliveIds.Contains(pair.Key))
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

                view.bar?.Destroy();
                _enemyViews.Remove(_staleIds[i]);
            }
        }

        EnemyView CreateEnemyView(Enemy enemy)
        {
            var go = new GameObject("Enemy" + enemy.Id);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = enemySprite;
            sr.sortingOrder = 8;
            if (_flashMaterial != null)
            {
                sr.sharedMaterial = _flashMaterial;
            }

            var anim = go.AddComponent<ActorAnimator>();
            anim.target = sr;
            anim.Init(art, EntityOf(enemy.Kind), "idle");

            EnemyHealthBar bar = null;
            if (healthBarTrack != null && healthBarFill != null)
            {
                float top = SpriteTopHeight(sr.sprite);
                bar = new EnemyHealthBar(_barRoot, healthBarTrack, healthBarFill, top + 5f / 32f);
            }

            var view = new EnemyView
            {
                tx = go.transform,
                anim = anim,
                sr = sr,
                bar = bar,
                last = go.transform.position
            };

            _enemyViews.Add(enemy.Id, view);
            return view;
        }

        /// <summary>精灵顶点到枢轴的高度（单位）。血条贴在它上面一点点。</summary>
        static float SpriteTopHeight(Sprite sprite)
        {
            if (sprite == null)
            {
                return 1.5f;
            }

            return (sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit;
        }

        static void SyncEnemyFlash(EnemyView view)
        {
            float amount = 0f;
            if (Time.time < view.flashUntil)
            {
                amount = Mathf.Clamp01((view.flashUntil - Time.time) / HitFlashSeconds);
            }

            if (amount > 0f || view.flashApplied)
            {
                HitFlash.Apply(view.sr, amount, Color.white);
                view.flashApplied = amount > 0f;
            }
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

        /// <summary>
        /// 钉枪的铆钉。以前模拟里有、画面上没有——玩家看不见就没法躲，等于凭空掉血。
        /// 按对象引用绑定（模拟层没给飞行物发 Id），飞完/撞墙后下一帧销毁。
        /// </summary>
        void SyncProjectiles()
        {
            List<Projectile> shots = _world.World.Projectiles;
            for (int i = 0; i < shots.Count; i++)
            {
                Projectile shot = shots[i];
                if (!_shotViews.TryGetValue(shot, out Transform tx))
                {
                    var go = new GameObject("Shot");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = projectileSprite;
                    sr.sortingOrder = 11;
                    tx = go.transform;
                    _shotViews.Add(shot, tx);
                }

                tx.position = new Vector3((float)shot.Pos.X, (float)shot.Pos.Y, 0f);
                // 素材指向 +x，按速度方向转；像素风里斜向会被重采样，但子弹小、动得快，可以接受。
                tx.rotation = Quaternion.Euler(0f, 0f,
                    (float)(System.Math.Atan2(shot.Vel.Y, shot.Vel.X) * Mathf.Rad2Deg));
            }

            _staleShots.Clear();
            foreach (KeyValuePair<Projectile, Transform> pair in _shotViews)
            {
                if (!shots.Contains(pair.Key))
                {
                    _staleShots.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleShots.Count; i++)
            {
                Transform tx = _shotViews[_staleShots[i]];
                if (tx != null)
                {
                    Destroy(tx.gameObject);
                }

                _shotViews.Remove(_staleShots[i]);
            }
        }

        /// <summary>
        /// 坠物的矩形落区。同样是「模拟有、画面没有」的那一类：不画出来，12 点伤害就是无声的。
        /// 落区是 2×2 单位，警示区素材是 1 单位，按矩形等比拉开。
        /// </summary>
        void SyncZones()
        {
            List<HazardZone> zones = _world.World.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                HazardZone zone = zones[i];
                if (!_zoneViews.TryGetValue(zone, out Transform tx))
                {
                    var go = new GameObject("Zone");
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = zoneSprite;
                    sr.sortingOrder = 5;
                    sr.color = new Color(1f, 1f, 1f, 0.85f);
                    tx = go.transform;
                    _zoneViews.Add(zone, tx);
                    tx.localScale = new Vector3((float)zone.Width, (float)zone.Height, 1f);
                }

                tx.position = new Vector3(
                    (float)(zone.X + zone.Width * 0.5),
                    (float)(zone.Y + zone.Height * 0.5),
                    0f);
            }

            _staleZones.Clear();
            foreach (KeyValuePair<HazardZone, Transform> pair in _zoneViews)
            {
                if (!zones.Contains(pair.Key))
                {
                    _staleZones.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleZones.Count; i++)
            {
                Transform tx = _zoneViews[_staleZones[i]];
                if (tx != null)
                {
                    Destroy(tx.gameObject);
                }

                _zoneViews.Remove(_staleZones[i]);
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
                (float)Tuning.EnergyMax, _world.FairyActive, fairySeconds);
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

            UpdateBossBar();
            AnnounceBossIfAny();
        }

        /// <summary>恶名以骸的血条：场上有一只就显示，没有就收起。</summary>
        void UpdateBossBar()
        {
            if (_hud == null)
            {
                return;
            }

            List<Enemy> enemies = _world.World.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy.Kind != EnemyKind.ChainBoss)
                {
                    continue;
                }

                _hud.SetBoss(UiText.EnemyName(enemy.Kind), (float)enemy.Hp, (float)enemy.MaxHp);
                return;
            }

            _hud.SetBoss(null, 0f, 0f);
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
