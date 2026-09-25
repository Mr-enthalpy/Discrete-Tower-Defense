using System.Collections.ObjectModel;
using System.Numerics;

namespace DiscreteTD.Simulation;

public sealed class World
{
    public const float FixedDelta = 1f / 30f;
    readonly List<Tower> towers=[];
    readonly List<Enemy> enemies=[];
    readonly List<Projectile> projectiles=[];
    readonly List<ShotTrace> traces=[];
    readonly List<Enemy> candidates=[];
    readonly SpatialIndex spatial;
    readonly Dictionary<long,CommandResult> commands=[];
    readonly Tower?[] occupancy;
    readonly int epoch;
    int sequence, spawned, phaseTicks, incomeTicks, spawnTicks;
    public DemoDefinition Definition { get; }
    public ReadOnlyCollection<Tower> Towers { get; }
    public ReadOnlyCollection<Enemy> Enemies { get; }
    public ReadOnlyCollection<Projectile> Projectiles { get; }
    public ReadOnlyCollection<ShotTrace> Traces { get; }
    public NavigationField Navigation { get; }
    public Tower Core => towers[0];
    public Phase Phase { get; private set; } = Phase.Preparation;
    public int Gold { get; private set; }
    public int Night { get; private set; }
    public int Tick { get; private set; }
    public int Kills { get; private set; }
    public int TotalSpawned { get; private set; }
    public int DawnSurvivors { get; private set; }
    public int Width => Definition.Map[0].Length;
    public int Height => Definition.Map.Length;
    public bool Terminal => Phase is Phase.Won or Phase.Lost;
    public float Remaining => MathF.Max(0, (Phase==Phase.Night ? Definition.NightSeconds : Definition.DaySeconds)-phaseTicks*FixedDelta);

    public World(DemoDefinition definition,int epoch=1)
    {
        Definition=definition;this.epoch=epoch;Gold=definition.InitialGold;
        Towers=towers.AsReadOnly();Enemies=enemies.AsReadOnly();Projectiles=projectiles.AsReadOnly();Traces=traces.AsReadOnly();
        occupancy=new Tower?[Width*Height];spatial=new(Width,Height);Navigation=new(definition);
        var core=new Tower { Id=NewId(),X=definition.CoreX,Y=definition.CoreY,Health=definition.CoreHealth,Core=true };
        towers.Add(core);occupancy[core.Y*Width+core.X]=core;
    }
    EntityId NewId()=>new(epoch,++sequence);
    public bool Inside(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height;
    public char Terrain(int x,int y)=>Inside(x,y)?Definition.Map[y][x]:'#';
    public int Elevation(int x,int y)=>Terrain(x,y)=='#'?2:0;
    public Tower? At(int x,int y)=>Inside(x,y)?occupancy[y*Width+x]:null;
    public CommandResult Quote(int x,int y)
    {
        if(Terminal)return new(false,"战斗已结束");
        for(int cy=y;cy<y+Definition.Tower.Height;cy++)for(int cx=x;cx<x+Definition.Tower.Width;cx++)
        {
            if(!Inside(cx,cy))return new(false,"请选择地图内的格子");
            if(Terrain(cx,cy)=='='||cx==0&&Definition.EntranceRows.Contains(cy))return new(false,"道路保留通行，选择路旁或高地");
            if(At(cx,cy)!=null)return new(false,"格子已占用（残骸也保留占地）");
            if(Elevation(cx,cy)!=Elevation(x,y))return new(false,"多格设施需要等高地形");
        }
        if(enemies.Any(e=>Touches(e.Position,e.Variant.Radius,x,y,Definition.Tower.Width,Definition.Tower.Height)))return new(false,"敌人正在经过此处");
        if(Gold<Definition.Tower.Cost)return new(false,"金币不足");
        return new(true,$"建造机炮 · {Definition.Tower.Cost} 金币");
    }
    public CommandResult Build(long request,int x,int y)
    {
        if(commands.TryGetValue(request,out var previous))return previous;
        var result=Quote(x,y);
        if(result.Success)
        {
            Gold-=Definition.Tower.Cost;
            var t=new Tower{Id=NewId(),X=x,Y=y,Width=Definition.Tower.Width,Height=Definition.Tower.Height,Health=Definition.Tower.Health,Cooldown=Definition.Tower.Cooldown};
            towers.Add(t);
            for(int cy=y;cy<y+t.Height;cy++)for(int cx=x;cx<x+t.Width;cx++)occupancy[cy*Width+cx]=t;
        }
        commands.Add(request,result);return result;
    }
    public CommandResult Sell(long request,EntityId id)
    {
        if(commands.TryGetValue(request,out var previous))return previous;
        var t=towers.FirstOrDefault(t=>t.Id==id);
        var result=Terminal?new CommandResult(false,"战斗已结束"):
            t==null||t.Core?new(false,"无法出售此对象"):new(true,"已出售机炮");
        if(result.Success)
        {
            Gold+=Phase==Phase.Preparation?Definition.Tower.Cost:Definition.Tower.Refund;
            for(int cy=t!.Y;cy<t.Y+t.Height;cy++)for(int cx=t.X;cx<t.X+t.Width;cx++)occupancy[cy*Width+cx]=null;
            towers.Remove(t);
        }
        commands.Add(request,result);return result;
    }
    public void Begin()
    {
        if(Phase!=Phase.Preparation)return;
        Phase=Phase.Night;Night=1;phaseTicks=0;spawnTicks=0;spawned=0;
    }
    public void Step()
    {
        traces.Clear();
        if(Terminal||Phase==Phase.Preparation)return;
        Tick++;phaseTicks++;
        if(++incomeTicks>=Ticks(Definition.IncomeInterval)){incomeTicks=0;Gold+=Definition.Income;}
        SpawnWave();
        MoveEnemies();
        foreach(var t in towers)
            if(t.PendingBlock&&!enemies.Any(e=>e.Health>0&&Touches(e.Position,e.Variant.Radius,t.X,t.Y,t.Width,t.Height)))t.PendingBlock=false;
        spatial.Rebuild(enemies);
        FireTowers();
        MoveProjectiles();
        enemies.RemoveAll(e=>e.Health<=0);
        if(Core.Health<=0){Phase=Phase.Lost;return;}
        if(Phase==Phase.Night&&phaseTicks>=Ticks(Definition.NightSeconds))Dawn();
        else if(Phase==Phase.Day&&phaseTicks>=Ticks(Definition.DaySeconds))
        {Phase=Phase.Night;Night++;phaseTicks=0;spawned=0;spawnTicks=0;}
        if(Phase==Phase.Clearing&&enemies.Count==0)Phase=Phase.Won;
    }
    static int Ticks(float seconds)=>(int)MathF.Ceiling(seconds/FixedDelta);
    void Dawn()
    {
        DawnSurvivors=enemies.Count;phaseTicks=0;
        Phase=Night>=Definition.Waves.Length?Phase.Clearing:Phase.Day;
        foreach(var t in towers)
            if(!t.Core&&t.Health<=0)
            {
                t.Health=Definition.Tower.Health;
                // Preserve all overlapping enemies. Blocking resumes once the cell is clear.
                t.PendingBlock=enemies.Any(e=>Touches(e.Position,e.Variant.Radius,t.X,t.Y,t.Width,t.Height));
            }
    }
    void SpawnWave()
    {
        if(Phase!=Phase.Night)return;
        var wave=Definition.Waves[Night-1];
        if(spawned>=wave.Count||--spawnTicks>0)return;
        var size=spawned%wave.MediumEvery==wave.MediumEvery-1?"medium":"small";
        var variant=Definition.Enemy.Variants.Single(v=>v.Id==size);
        int row=Definition.EntranceRows[spawned%Definition.EntranceRows.Length];
        var p=new Vector2(.5f,row+.5f);
        enemies.Add(new Enemy{Id=NewId(),TypeId=Definition.Enemy.Id,Variant=variant,
            Position=p,Previous=p,Health=variant.Health,Legion=(Night-1)*100+spawned/6});
        spawned++;TotalSpawned++;spawnTicks=Ticks(wave.Interval);
    }
    void MoveEnemies()
    {
        foreach(var e in enemies)
        {
            if(e.Health<=0)continue;
            e.Previous=e.Position;e.Cooldown=MathF.Max(0,e.Cooldown-FixedDelta);
            Tower? victim=null;
            // Constant-size cell neighborhood; buildings never turn into per-enemy path requests.
            int ex=(int)e.Position.X,ey=(int)e.Position.Y;
            for(int y=Math.Max(0,ey-1);y<=Math.Min(Height-1,ey+1);y++)
                for(int x=Math.Max(0,ex-1);x<=Math.Min(Width-1,ex+1);x++)
                {
                    var t=At(x,y);
                    if(t is {Blocking:true}&&Touches(e.Position,e.Variant.Radius+.055f,t.X,t.Y,t.Width,t.Height)
                        &&(victim==null||t.Id.Sequence<victim.Id.Sequence))victim=t;
                }
            if(victim!=null)
            {
                if(e.Cooldown<=0){victim.Health=MathF.Max(0,victim.Health-e.Variant.Damage);e.Cooldown=.85f;}
                continue;
            }
            var target=Navigation.Next(e.Position);var delta=target-e.Position;
            if(delta.LengthSquared()<.00001f)continue;
            var next=e.Position+Vector2.Normalize(delta)*MathF.Min(delta.Length(),e.Variant.Speed*FixedDelta);
            if(Terrain((int)next.X,(int)next.Y)!='#')e.Position=next;
        }
    }
    void FireTowers()
    {
        var def=Definition.Tower;
        foreach(var t in towers)
        {
            if(t.Core||t.Health<=0)continue;
            // Explicit internal TestSupply fixture. No real grid or infinite stored charge.
            t.Energy=MathF.Min(def.Capacitor,t.Energy+def.ChargePower*FixedDelta);
            t.Cooldown=MathF.Max(0,t.Cooldown-FixedDelta);
            spatial.Query(t.Position,def.Range+.4f,candidates);
            Enemy? target=null;float best=float.MaxValue;
            foreach(var e in candidates)
            {
                float distance=Vector2.DistanceSquared(t.Position,e.Position);
                if(e.Health<=0||distance>def.Range*def.Range||!Visible(t.Position,Elevation(t.X,t.Y)+.7f,e.Position))continue;
                if(distance<best||(distance==best&&(target==null||e.Id.Sequence<target.Id.Sequence))) {target=e;best=distance;}
            }
            if(target==null)continue;
            var aim=target.Position-t.Position;t.Angle=MathF.Atan2(aim.Y,aim.X);
            if(t.Cooldown>0||t.Energy<def.ShotEnergy)continue;
            t.Energy-=def.ShotEnergy;t.Cooldown=def.Cooldown;
            var direction=aim.LengthSquared()>.0001f?Vector2.Normalize(aim):Vector2.UnitX;
            projectiles.Add(new Projectile{Position=t.Position,Previous=t.Position,Velocity=direction*def.ProjectileSpeed,
                Damage=def.Damage,Height=Elevation(t.X,t.Y)+.7f,Life=def.Range/def.ProjectileSpeed+.1f});
        }
    }
    bool Visible(Vector2 from,float launchHeight,Vector2 to)
    {
        int steps=Math.Max(1,(int)(Vector2.Distance(from,to)*8));
        for(int i=1;i<steps;i++)
        {
            float a=(float)i/steps;var p=Vector2.Lerp(from,to,a);
            if(Elevation((int)p.X,(int)p.Y)>float.Lerp(launchHeight,.35f,a))return false;
        }
        return true;
    }
    void MoveProjectiles()
    {
        for(int i=projectiles.Count-1;i>=0;i--)
        {
            var p=projectiles[i];p.Previous=p.Position;
            var end=p.Position+p.Velocity*FixedDelta;
            spatial.Query((p.Position+end)*.5f,Vector2.Distance(p.Position,end)*.5f+.5f,candidates);
            Enemy? hit=null;float hitAt=1.01f;
            foreach(var e in candidates)
            {
                if(e.Health<=0)continue;
                float a=SegmentCircle(p.Position,end,e.Position,e.Variant.Radius+.035f);
                if(a>=0&&(a<hitAt||(a==hitAt&&(hit==null||e.Id.Sequence<hit.Id.Sequence)))){hitAt=a;hit=e;}
            }
            // A projectile leaving a high tile may travel downhill; a higher intervening tile blocks.
            float terrainAt=TerrainHit(p.Position,end,p.Height);
            bool blocked=terrainAt>=0&&terrainAt<=hitAt;
            if(blocked){hit=null;end=Vector2.Lerp(p.Position,end,terrainAt);}
            if(hit!=null)
            {
                end=Vector2.Lerp(p.Position,end,hitAt);hit.Health=MathF.Max(0,hit.Health-p.Damage);
                if(hit.Health<=0)Kills++;
            }
            traces.Add(new(p.Position,end,hit!=null));p.Position=end;p.Life-=FixedDelta;
            if(hit!=null||blocked||p.Life<=0)projectiles.RemoveAt(i);
        }
    }
    internal float TerrainHit(Vector2 from,Vector2 to,float height)
    {
        int x=(int)MathF.Floor(from.X),y=(int)MathF.Floor(from.Y);
        var delta=to-from;int sx=Math.Sign(delta.X),sy=Math.Sign(delta.Y);
        float dx=sx==0?float.PositiveInfinity:MathF.Abs(1/delta.X);
        float dy=sy==0?float.PositiveInfinity:MathF.Abs(1/delta.Y);
        float tx=sx==0?float.PositiveInfinity:(sx>0?x+1-from.X:from.X-x)*dx;
        float ty=sy==0?float.PositiveInfinity:(sy>0?y+1-from.Y:from.Y-y)*dy;
        if(!Inside(x,y)||Elevation(x,y)>height)return 0;
        while(MathF.Min(tx,ty)<=1)
        {
            float at;
            if(tx<=ty){at=tx;x+=sx;tx+=dx;}
            else{at=ty;y+=sy;ty+=dy;}
            if(!Inside(x,y)||Elevation(x,y)>height)return at;
        }
        return -1;
    }
    static bool Touches(Vector2 p,float radius,int x,int y,int width=1,int height=1)
    {
        float dx=p.X-Math.Clamp(p.X,x,x+width),dy=p.Y-Math.Clamp(p.Y,y,y+height);
        return dx*dx+dy*dy<radius*radius;
    }
    static float SegmentCircle(Vector2 a,Vector2 b,Vector2 c,float radius)
    {
        var d=b-a;var f=a-c;float aa=Vector2.Dot(d,d),cc=Vector2.Dot(f,f)-radius*radius;
        if(cc<=0)return 0;if(aa<=0)return -1;
        float bb=2*Vector2.Dot(f,d),disc=bb*bb-4*aa*cc;
        if(disc<0)return -1;float t=(-bb-MathF.Sqrt(disc))/(2*aa);
        return t>=0&&t<=1?t:-1;
    }
}
