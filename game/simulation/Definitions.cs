using System.Numerics;

namespace DiscreteTD.Simulation;

public sealed record SizeVariant(string Id, float Health, float Speed, float Damage, float Radius);
public sealed record EnemyDefinition(string Id, SizeVariant[] Variants);
public sealed record TowerDefinition(int Cost, int Refund, float Health, float Range, float Damage,
    float Cooldown, float ProjectileSpeed, float Capacitor, float ShotEnergy, float ChargePower,
    int Width = 1, int Height = 1);
public sealed record WaveDefinition(int Count, float Interval, int MediumEvery);
public sealed record DemoDefinition(int SchemaVersion, string[] Map, int CoreX, int CoreY,
    int[] EntranceRows, int InitialGold, float CoreHealth, int Income, float IncomeInterval,
    float DaySeconds, float NightSeconds, TowerDefinition Tower, EnemyDefinition Enemy,
    WaveDefinition[] Waves);
public readonly record struct EntityId(int Epoch, int Sequence);
public enum Phase { Preparation, Day, Night, Clearing, Won, Lost }
public readonly record struct CommandResult(bool Success, string Reason);
public readonly record struct ShotTrace(Vector2 From, Vector2 To, bool Hit);

public sealed class Tower
{
    public EntityId Id { get; internal set; }
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public int Width { get; internal set; } = 1;
    public int Height { get; internal set; } = 1;
    public float Health { get; internal set; }
    public float Energy { get; internal set; }
    public float Cooldown { get; internal set; }
    public float Angle { get; internal set; }
    public bool Core { get; internal set; }
    // Revived towers let only pre-existing overlapping enemies leave before blocking again.
    internal bool PendingBlock;
    public bool Blocking => Health > 0 && !PendingBlock;
    public Vector2 Position => new(X + Width*.5f, Y + Height*.5f);
}

public sealed class Enemy
{
    public EntityId Id { get; internal set; }
    public string TypeId { get; internal set; } = "";
    public SizeVariant Variant { get; internal set; } = null!;
    public Vector2 Position { get; internal set; }
    public Vector2 Previous { get; internal set; }
    public float Health { get; internal set; }
    public int Legion { get; internal set; }
    internal float Cooldown;
}

public sealed class Projectile
{
    public Vector2 Position { get; internal set; }
    public Vector2 Previous { get; internal set; }
    internal Vector2 Velocity;
    internal float Damage, Height, Life;
}
