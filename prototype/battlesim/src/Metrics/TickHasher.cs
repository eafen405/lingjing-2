using BattleSim.Core;
using BattleSim.Combat;
using BattleSim.Fairy;
using BattleSim.Sim;

namespace BattleSim.Metrics;

/// <summary>
/// 每 tick 哈希：FNV-1a 64。上一 tick 的链尾参与本 tick，形成哈希链。
/// 位置量化为 1e-3 后取位模式，避免浮点末位差异影响比对。
/// </summary>
public sealed class TickHasher
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private byte[] _buffer = new byte[8192];
    private int _length;

    public ulong Chain { get; private set; } = FnvOffsetBasis;

    public void Begin()
    {
        _length = 0;
        WriteUInt64(Chain);
    }

    public ulong HashTick(int tick, ulong seed, in Command playerCommand, in Command fairyCommand, bool fairyActive, WorldState w)
    {
        Begin();

        WriteInt32(tick);
        WriteUInt64(seed);
        WriteCommand(in playerCommand);
        WriteCommand(in fairyCommand);
        WriteBool(fairyActive);

        Player p = w.Player;
        WriteVec(p.Pos);
        WriteVec(p.Vel);
        WriteDouble(p.Hp);
        WriteDouble(p.Energy);
        WriteInt32(p.SpinSign);
        WriteDouble(p.Ring.Phase);

        // 敌人：按 Id 排序后写入（列表本身保序，但显式排序让哈希与刷新顺序解耦）。
        EnemyHash[] enemies = new EnemyHash[w.Enemies.Count];
        for (int i = 0; i < w.Enemies.Count; i++)
        {
            Enemies.Enemy e = w.Enemies[i];
            enemies[i] = new EnemyHash(e.Id, e.Kind, e.Pos, e.Hp, e.State, e.Alive);
        }

        Array.Sort(enemies, static (a, b) => a.Id.CompareTo(b.Id));
        WriteInt32(enemies.Length);
        for (int i = 0; i < enemies.Length; i++)
        {
            WriteInt32(enemies[i].Id);
            WriteInt32((int)enemies[i].Kind);
            WriteVec(enemies[i].Pos);
            WriteDouble(enemies[i].Hp);
            WriteInt32(enemies[i].State);
            WriteBool(enemies[i].Alive);
        }

        WriteInt32(w.Projectiles.Count);
        for (int i = 0; i < w.Projectiles.Count; i++)
        {
            WriteVec(w.Projectiles[i].Pos);
            WriteVec(w.Projectiles[i].Vel);
        }

        WriteInt32(w.Zones.Count);
        for (int i = 0; i < w.Zones.Count; i++)
        {
            HazardZone z = w.Zones[i];
            WriteDouble(z.X);
            WriteDouble(z.Y);
            WriteInt32(z.WarnTicksLeft);
        }

        // 随机流状态也进哈希：逻辑相同但随机数走岔会被立刻发现。
        WriteUInt64(w.Rng.State);

        Chain = Finish();
        return Chain;
    }

    private readonly struct EnemyHash
    {
        public readonly int Id;
        public readonly Enemies.EnemyKind Kind;
        public readonly Vec2 Pos;
        public readonly double Hp;
        public readonly int State;
        public readonly bool Alive;

        public EnemyHash(int id, Enemies.EnemyKind kind, Vec2 pos, double hp, int state, bool alive)
        {
            Id = id;
            Kind = kind;
            Pos = pos;
            Hp = hp;
            State = state;
            Alive = alive;
        }
    }

    private void WriteCommand(in Command command)
    {
        WriteVec(command.Move);
        WriteInt32(command.SpinSign);
        WriteBool(command.RequestReverse);
    }

    private void WriteVec(Vec2 v)
    {
        WriteInt64(SimMath.QuantizeLong(v.X));
        WriteInt64(SimMath.QuantizeLong(v.Y));
    }

    private void WriteDouble(double v)
    {
        WriteInt64(SimMath.QuantizeLong(v));
    }

    private void WriteBool(bool v)
    {
        Ensure(1);
        _buffer[_length++] = v ? (byte)1 : (byte)0;
    }

    private void WriteInt32(int v)
    {
        Ensure(4);
        uint u = unchecked((uint)v);
        _buffer[_length++] = (byte)u;
        _buffer[_length++] = (byte)(u >> 8);
        _buffer[_length++] = (byte)(u >> 16);
        _buffer[_length++] = (byte)(u >> 24);
    }

    private void WriteInt64(long v) => WriteUInt64(unchecked((ulong)v));

    private void WriteUInt64(ulong v)
    {
        Ensure(8);
        for (int i = 0; i < 8; i++)
        {
            _buffer[_length++] = (byte)(v >> (i * 8));
        }
    }

    private void Ensure(int extra)
    {
        if (_length + extra <= _buffer.Length)
        {
            return;
        }

        int size = _buffer.Length;
        while (size < _length + extra)
        {
            size *= 2;
        }

        Array.Resize(ref _buffer, size);
    }

    private ulong Finish()
    {
        ulong h = FnvOffsetBasis;
        for (int i = 0; i < _length; i++)
        {
            h ^= _buffer[i];
            h *= FnvPrime;
        }

        return h;
    }
}
