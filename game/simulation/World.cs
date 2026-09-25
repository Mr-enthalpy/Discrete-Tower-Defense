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
    readonly TerrainDefinition[] ground;
    readonly int epoch;
    int sequence, phaseTicks, incomeTicks;
    int[] detachmentSpawned=[];
    readonly List<Enemy> neighbors=[];
    readonly List<Vector2> escapeCandidates=[];
    readonly List<Tower> crushContacts=[];
    int dawnProtectedUntil;
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
    public float DawnProtectionRemaining=>MathF.Max(0,(dawnProtectedUntil-Tick)*FixedDelta);
    public int Width => Definition.Map[0].Length;
    public int Height => Definition.Map.Length;
    public bool Terminal => Phase is Phase.Won or Phase.Lost;
    public float Remaining => MathF.Max(0, (Phase==Phase.Night ? Definition.NightSeconds : Definition.DaySeconds)-phaseTicks*FixedDelta);

    public World(DemoDefinition definition,int epoch=1)
    {
        Definition=definition;this.epoch=epoch;Gold=definition.InitialGold;
        Towers=towers.AsReadOnly();Enemies=enemies.AsReadOnly();Projectiles=projectiles.AsReadOnly();Traces=traces.AsReadOnly();
        occupancy=new Tower?[Width*Height];spatial=new(Width,Height);Navigation=new(definition);
        ground=new TerrainDefinition[Width*Height];
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)ground[y*Width+x]=definition.TerrainAt(x,y);
        var core=new Tower { Id=NewId(),X=definition.CoreX,Y=definition.CoreY,Health=definition.CoreHealth,Core=true };
        towers.Add(core);occupancy[core.Y*Width+core.X]=core;
    }
    EntityId NewId()=>new(epoch,++sequence);
    public bool Inside(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height;
    public char Terrain(int x,int y)=>Inside(x,y)?Definition.Map[y][x]:'#';
    public TerrainDefinition Ground(int x,int y)=>ground[y*Width+x];
    public bool Walkable(int x,int y)=>Inside(x,y)&&Ground(x,y).EnemyPassable&&!Ground(x,y).BlocksMovement;
    public bool FireBlocked(int x,int y)=>!Inside(x,y)||Ground(x,y).BlocksFire;
    void RefreshNavigation()=>Navigation.Update((x,y)=>At(x,y) is { Blocking:true,Core:false });
    public Tower? At(int x,int y)=>Inside(x,y)?occupancy[y*Width+x]:null;
    public CommandResult Quote(int x,int y)
    {
        if(Terminal)return new(false,"战斗已结束");
        for(int cy=y;cy<y+Definition.Tower.Height;cy++)for(int cx=x;cx<x+Definition.Tower.Width;cx++)
        {
            if(!Inside(cx,cy))return new(false,"请选择地图内的格子");
            if(!Ground(cx,cy).PermanentBuildable)return new(false,"此地不能部署永久设施，请选择可争夺地面");
            if(At(cx,cy)!=null)return new(false,"格子已占用（残骸也保留占地）");
        }
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
            RefreshNavigation();
            ResolveBuildingOverlaps(false);
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
            towers.Remove(t);RefreshNavigation();
        }
        commands.Add(request,result);return result;
    }
    public void Begin()
    {
        if(Phase!=Phase.Preparation)return;
        Phase=Phase.Night;Night=1;phaseTicks=0;detachmentSpawned=new int[Definition.Waves[0].Detachments.Length];
    }
    public void Step()
    {
        traces.Clear();
        if(Terminal||Phase==Phase.Preparation)return;
        Tick++;phaseTicks++;
        if(++incomeTicks>=Ticks(Definition.IncomeInterval)){incomeTicks=0;Gold+=Definition.Income;}
        RefreshNavigation();
        SpawnWave();
        ResolveBuildingOverlaps(true);
        spatial.Rebuild(enemies);
        MoveEnemies();
        spatial.Rebuild(enemies);
        FireTowers();
        MoveProjectiles();
        enemies.RemoveAll(e=>e.Health<=0);
        if(Core.Health<=0){Phase=Phase.Lost;return;}
        if(Phase==Phase.Night&&phaseTicks>=Ticks(Definition.NightSeconds))Dawn();
        else if(Phase==Phase.Day&&phaseTicks>=Ticks(Definition.DaySeconds))
        {Phase=Phase.Night;Night++;phaseTicks=0;detachmentSpawned=new int[Definition.Waves[Night-1].Detachments.Length];}
        RefreshNavigation();
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
            }
        dawnProtectedUntil=Tick+Ticks(Definition.Space.DawnProtectionSeconds);
        foreach(var t in towers)if(t.Health>0)t.InvulnerableUntilTick=dawnProtectedUntil;
        ResolveBuildingOverlaps(false);
    }
    void SpawnWave()
    {
        if(Phase!=Phase.Night)return;
        var detachments=Definition.Waves[Night-1].Detachments;
        for(int d=0;d<detachments.Length;d++)
        {
            var batch=detachments[d];var front=Definition.Fronts.First(f=>f.Id==batch.FrontId);
            int deployed=detachmentSpawned[d];
            // Fill the entire front as a rank, then introduce the next rank during the window.
            int ranks=(batch.Count+front.Cells.Length-1)/front.Cells.Length;
            int rank=deployed/front.Cells.Length;
            float due=batch.StartSeconds+(ranks<=1?0:rank*batch.SpawnWindow/(ranks-1));
            if(deployed>=batch.Count||(phaseTicks-1)*FixedDelta+.0001f<due)continue;
            int end=Math.Min(batch.Count,(rank+1)*front.Cells.Length);
            while(deployed<end)
            {
                var cell=front.Cells[deployed%front.Cells.Length];
                var variant=Definition.Enemy.Variants.Single(v=>v.Id==(deployed%batch.MediumEvery==batch.MediumEvery-1?"medium":"small"));
                var position=new Vector2(cell.X+.5f,cell.Y+.5f);
                // Congested fronts queue their remaining members; never spawn overlapping entities.
                if(!CanOccupy(position,variant.Radius)||enemies.Any(e=>e.Health>0&&Vector2.DistanceSquared(e.Position,position)<MathF.Pow(e.Variant.Radius+variant.Radius+.06f,2)))break;
                enemies.Add(new Enemy{Id=NewId(),TypeId=Definition.Enemy.Id,Variant=variant,
                    Position=position,Previous=position,Health=variant.Health,
                    Legion=(Night-1)*1000+d,FrontId=front.Id,DetachmentId=batch.Id});
                deployed++;TotalSpawned++;
            }
            detachmentSpawned[d]=deployed;
        }
    }
    internal bool CanOccupy(Vector2 p,float radius)
    {
        for(int y=(int)MathF.Floor(p.Y-radius);y<=(int)MathF.Floor(p.Y+radius);y++)
            for(int x=(int)MathF.Floor(p.X-radius);x<=(int)MathF.Floor(p.X+radius);x++)
                if(Touches(p,radius,x,y))
                {
                    if(!Walkable(x,y))return false;
                    var t=At(x,y);
                    if(t is {Blocking:true})return false;
                }
        return true;
    }
    Tower? OverlappingTower(Enemy e)
    {
        for(int y=(int)MathF.Floor(e.Position.Y-e.Variant.Radius);y<=(int)MathF.Floor(e.Position.Y+e.Variant.Radius);y++)
            for(int x=(int)MathF.Floor(e.Position.X-e.Variant.Radius);x<=(int)MathF.Floor(e.Position.X+e.Variant.Radius);x++)
                if(At(x,y) is {Blocking:true} t&&Touches(e.Position,e.Variant.Radius,t.X,t.Y,t.Width,t.Height))return t;
        return null;
    }
    bool EscapeSegmentClear(Enemy e,Vector2 destination)
    {
        int steps=Math.Max(1,(int)MathF.Ceiling(Vector2.Distance(e.Position,destination)/.1f));
        for(int i=1;i<=steps;i++)
        {
            var p=Vector2.Lerp(e.Position,destination,(float)i/steps);float radius=e.Variant.Radius;
            for(int y=(int)MathF.Floor(p.Y-radius);y<=(int)MathF.Floor(p.Y+radius);y++)
                for(int x=(int)MathF.Floor(p.X-radius);x<=(int)MathF.Floor(p.X+radius);x++)
                    if(Touches(p,radius,x,y))
                    {
                        if(!Walkable(x,y))return false;
                        var t=At(x,y);
                        if(t is {Blocking:true}&&!Touches(e.Position,radius,t.X,t.Y,t.Width,t.Height))return false;
                    }
        }
        return true;
    }
    void ResolveBuildingOverlaps(bool applyDamage)
    {
        foreach(var e in enemies)
        {
            if(e.Health<=0)continue;
            var t=OverlappingTower(e);e.Crushed=t!=null;
            if(t==null)continue;
            ResolveBuildingOverlap(e,t,applyDamage);
        }
    }
    // Keep closures for the rare displacement search out of every soldier's normal movement step.
    void ResolveBuildingOverlap(Enemy e,Tower t,bool applyDamage)
    {
        float r=e.Variant.Radius+.02f;
        escapeCandidates.Clear();
        escapeCandidates.Add(new(t.X-r,Math.Clamp(e.Position.Y,t.Y,t.Y+t.Height)));
        escapeCandidates.Add(new(t.X+t.Width+r,Math.Clamp(e.Position.Y,t.Y,t.Y+t.Height)));
        escapeCandidates.Add(new(Math.Clamp(e.Position.X,t.X,t.X+t.Width),t.Y-r));
        escapeCandidates.Add(new(Math.Clamp(e.Position.X,t.X,t.X+t.Width),t.Y+t.Height+r));
        for(int y=t.Y-1;y<=t.Y+t.Height;y++)for(int x=t.X-1;x<=t.X+t.Width;x++)
            escapeCandidates.Add(new(x+.5f,y+.5f));
        escapeCandidates.Sort((a,b)=>
        {
            int distance=Vector2.DistanceSquared(e.Position,a).CompareTo(Vector2.DistanceSquared(e.Position,b));
            return distance!=0?distance:a.X!=b.X?a.X.CompareTo(b.X):a.Y.CompareTo(b.Y);
        });
        foreach(var p in escapeCandidates)
        {
            if(!CanOccupy(p,e.Variant.Radius)||!EscapeSegmentClear(e,p)||
                enemies.Any(other=>other!=e&&other.Health>0&&Vector2.DistanceSquared(other.Position,p)<MathF.Pow(other.Variant.Radius+e.Variant.Radius+.015f,2)))continue;
            e.Position=p;e.Previous=p;e.Crushed=false;break;
        }
        if(e.Crushed&&applyDamage)
        {
            crushContacts.Clear();
            for(int y=(int)MathF.Floor(e.Position.Y-e.Variant.Radius);y<=(int)MathF.Floor(e.Position.Y+e.Variant.Radius);y++)
                for(int x=(int)MathF.Floor(e.Position.X-e.Variant.Radius);x<=(int)MathF.Floor(e.Position.X+e.Variant.Radius);x++)
                    if(At(x,y) is {Blocking:true} contact&&Touches(e.Position,e.Variant.Radius,contact.X,contact.Y,contact.Width,contact.Height)&&!crushContacts.Contains(contact))crushContacts.Add(contact);
            float damage=Definition.Space.CrushDamagePerSecond*FixedDelta;
            // Snapshot contacts, then resolve both sides. Multi-cell facilities count once.
            e.Health=MathF.Max(0,e.Health-damage*crushContacts.Count);
            foreach(var contact in crushContacts)
                if(Tick>=contact.InvulnerableUntilTick)contact.Health=MathF.Max(0,contact.Health-damage);
            if(e.Health<=0)Kills++;
            if(crushContacts.Any(contact=>contact.Health<=0))RefreshNavigation();
            e.Crushed=OverlappingTower(e)!=null;
        }
    }
    bool ClearOfUnits(Enemy unit,Vector2 position)
    {
        foreach(var other in neighbors)
        {
            if(other==unit||other.Health<=0)continue;
            float distance=Vector2.DistanceSquared(position,other.Position);
            float oldDistance=Vector2.DistanceSquared(unit.Position,other.Position);
            float minimum=unit.Variant.Radius+other.Variant.Radius+.015f;
            if(distance<minimum*minimum&&distance<oldDistance-.000001f)return false;
        }
        return true;
    }
    void MoveEnemies()
    {
        foreach(var e in enemies)
        {
            if(e.Health<=0)continue;
            e.Previous=e.Position;e.Cooldown=MathF.Max(0,e.Cooldown-FixedDelta);e.AttackTarget=null;
            var nextCell=Navigation.NextCell(e.Position);
            bool siege=Navigation.NeedsBreach(e.Position);
            Tower? victim=e.Crushed?OverlappingTower(e):Touches(e.Position,e.Variant.Radius+.08f,Core.X,Core.Y)?Core:null;
            var blocking=At(nextCell.X,nextCell.Y);
            if(victim==null&&siege&&blocking is {Blocking:true})victim=blocking;
            // At a diagonal corner, attack an adjacent obstruction of the chosen breach direction.
            if(siege&&victim==null)
                for(int y=(int)e.Position.Y-1;y<=(int)e.Position.Y+1;y++)
                    for(int x=(int)e.Position.X-1;x<=(int)e.Position.X+1;x++)
                    {
                        var t=At(x,y);
                        if(t is {Blocking:true}&&Touches(e.Position,e.Variant.Radius+.08f,t.X,t.Y,t.Width,t.Height)&&
                           Vector2.Dot(t.Position-e.Position,Navigation.Next(e.Position)-e.Position)>0)victim=t;
                    }
            if(victim is {Health:>0}&&Touches(e.Position,e.Variant.Radius+.08f,victim.X,victim.Y,victim.Width,victim.Height))
            {
                e.AttackTarget=victim.Id;
                if(e.Cooldown<=0)
                {
                    if(Tick>=victim.InvulnerableUntilTick)victim.Health=MathF.Max(0,victim.Health-e.Variant.Damage);
                    e.Cooldown=.85f;
                    if(victim.Health<=0)RefreshNavigation();
                }
                continue;
            }
            Vector2 target=victim?.Position??new Vector2(nextCell.X+.5f,nextCell.Y+.5f);
            var delta=target-e.Position;
            if(delta.LengthSquared()<.00001f)continue;
            var desired=Vector2.Normalize(delta);
            spatial.Query(e.Position,1.8f,neighbors);
            Vector2 separation=Vector2.Zero;
            foreach(var other in neighbors)
            {
                if(other==e||other.Health<=0)continue;
                var away=e.Position-other.Position;float distance=away.Length();
                float comfort=e.Variant.Radius+other.Variant.Radius+.55f;
                if(distance>0.0001f&&distance<comfort)separation+=away/distance*(comfort-distance)/comfort;
            }
            var direction=desired+separation*1.8f;
            if(direction.LengthSquared()>.0001f)direction=Vector2.Normalize(direction);
            float travel=e.Variant.Speed*FixedDelta;
            int steps=Math.Max(1,(int)MathF.Ceiling(travel/.1f));
            for(int step=0;step<steps;step++)
            {
                var start=e.Position;var offset=direction*(travel/steps);
                bool TryMove(Vector2 candidate)
                {
                    if(!CanOccupy(candidate,e.Variant.Radius)||!ClearOfUnits(e,candidate))return false;
                    e.Position=candidate;return true;
                }
                if(!TryMove(start+offset)&&!TryMove(start+new Vector2(offset.X,0)))
                    TryMove(start+new Vector2(0,offset.Y));
            }
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
                if(e.Health<=0||distance>def.Range*def.Range||!Visible(t.Position,e.Position))continue;
                if(distance<best||(distance==best&&(target==null||e.Id.Sequence<target.Id.Sequence))) {target=e;best=distance;}
            }
            if(target==null)continue;
            var aim=target.Position-t.Position;t.Angle=MathF.Atan2(aim.Y,aim.X);
            if(t.Cooldown>0||t.Energy<def.ShotEnergy)continue;
            t.Energy-=def.ShotEnergy;t.Cooldown=def.Cooldown;
            var direction=aim.LengthSquared()>.0001f?Vector2.Normalize(aim):Vector2.UnitX;
            projectiles.Add(new Projectile{Position=t.Position,Previous=t.Position,Velocity=direction*def.ProjectileSpeed,
                Damage=def.Damage,Life=def.Range/def.ProjectileSpeed+.1f});
        }
    }
    bool Visible(Vector2 from,Vector2 to)=>TerrainHit(from,to)<0;
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
            // Terrain fire blocking is independent of movement and permanent deployment permissions.
            float terrainAt=TerrainHit(p.Position,end);
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
    internal float TerrainHit(Vector2 from,Vector2 to)
    {
        int x=(int)MathF.Floor(from.X),y=(int)MathF.Floor(from.Y);
        var delta=to-from;int sx=Math.Sign(delta.X),sy=Math.Sign(delta.Y);
        float dx=sx==0?float.PositiveInfinity:MathF.Abs(1/delta.X);
        float dy=sy==0?float.PositiveInfinity:MathF.Abs(1/delta.Y);
        float tx=sx==0?float.PositiveInfinity:(sx>0?x+1-from.X:from.X-x)*dx;
        float ty=sy==0?float.PositiveInfinity:(sy>0?y+1-from.Y:from.Y-y)*dy;
        if(FireBlocked(x,y))return 0;
        while(MathF.Min(tx,ty)<=1)
        {
            float at;
            if(tx<=ty){at=tx;x+=sx;tx+=dx;}
            else{at=ty;y+=sy;ty+=dy;}
            if(FireBlocked(x,y))return at;
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
