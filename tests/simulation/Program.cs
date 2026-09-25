using System.Diagnostics;
using System.Numerics;
using DiscreteTD.Content;
using DiscreteTD.Simulation;

var definition=DemoLoader.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"demo-a.json")));
int count=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;Console.WriteLine($"PASS {name}");}
void Advance(World w,int n){for(int i=0;i<n;i++)w.Step();}
WaveDefinition Wave(string id,int count=8,float window=2)=>new([new(id,"A",count,0,window,6)]);
DemoDefinition Arena()=>definition with {
    Map=["############","#..........#","A..........#","A..........#","A..........#","A..........#","A..........#","#..........#","############"],
    CoreX=10,CoreY=4,InitialGold=10000,CoreHealth=100000,NightSeconds=120,
    Regions=[new("arena","测试战区",0,0,12,9)],Fronts=[new("A","arena",[new(0,2),new(0,3),new(0,4),new(0,5),new(0,6)])],
    Waves=[Wave("test")],Tower=definition.Tower with{Damage=.001f,Health=27},
    Enemy=definition.Enemy with{Variants=definition.Enemy.Variants.Select(v=>v with{Health=10000,Damage=9,Speed=1}).ToArray()}
};
void Invalid(DemoDefinition d,string message)
{try{DemoLoader.Validate(d);}catch(InvalidDataException){Check(true,message);return;}throw new Exception(message);}

Check(definition.Map.Length==20&&definition.Map.All(r=>r.Length==32)&&definition.CoreX==26&&definition.CoreY==10,"32x20 battlefield and inset core");
Check(definition.Fronts.Length==2&&definition.Fronts.All(f=>f.Cells.Length==5),"two five-cell fronts");
Check(definition.Fronts.SelectMany(f=>f.Cells).All(c=>new NavigationField(definition).Distance(c.X,c.Y)>0),"every front cell reaches core");
var world=new World(definition);
Check(!world.Build(1,2,4).Success&&!world.Build(2,0,3).Success&&!world.Build(3,8,2).Success&&world.Gold==definition.InitialGold,"maneuver ground, fronts and barriers reject permanent deployment atomically");
Check(world.Build(4,25,8).Success,"contested ground permits construction");
int money=world.Gold;world.Build(4,24,8);
Check(world.Gold==money&&world.Towers.Count==2,"duplicate build does not spend twice");
Check(!world.Build(5,25,8).Success&&world.Gold==money,"occupied footprint rejected");
var id=world.Towers[1].Id;world.Sell(6,id);world.Sell(6,id);
Check(world.Gold==definition.InitialGold&&world.Towers.Count==1,"sell refunds once");
Check(!new World(definition,2).Sell(1,id).Success,"old world handle rejected");
var multi=new World(definition with{Tower=definition.Tower with{Width=2,Height=2}});
Check(multi.Build(1,25,8).Success&&ReferenceEquals(multi.At(25,8),multi.At(26,9)),"multi-cell shares one entity");
int gold=multi.Gold;
Check(!multi.Build(2,26,9).Success&&multi.Gold==gold&&multi.At(27,9)==null,"partial footprint conflict is atomic");
multi.Sell(3,multi.At(25,8)!.Id);
Check(multi.At(25,8)==null&&multi.At(26,9)==null,"sale clears complete footprint");
Advance(world,90);Check(world.Tick==0&&world.TotalSpawned==0,"preparation does not advance");

var front=new World(definition);front.Begin();Advance(front,91);
Check(front.Enemies.Count==8&&front.Enemies.Select(e=>e.Legion).Distinct().Count()==1,"first eight-person detachment shares identity");
Check(front.Enemies.Select(e=>e.Position).Distinct().Count()==8&&front.Enemies.Max(e=>e.Position.Y)-front.Enemies.Min(e=>e.Position.Y)>2.5f,"detachment occupies a broad front after entering");
Check(front.Enemies.All(e=>e.FrontId=="A"&&e.Position.X>1),"northwest formation advances east");
Advance(front,180);
var south=front.Enemies.Where(e=>e.FrontId=="B").ToArray();
Check(south.Length>0&&south.All(e=>e.Position.Y<19.5f),"south formation advances north");
Advance(front,270);
Check(front.TotalSpawned==24&&front.Enemies.Select(e=>e.Legion).Distinct().Count()==4,"four detachments include simultaneous attacks on both fronts");
Check(front.Enemies.All(e=>front.CanOccupy(e.Position,e.Variant.Radius)),"moving units never overlap terrain or core");
Check(front.Enemies.Any(e=>e.Variant.Id=="medium")&&front.Enemies.Where(e=>e.Variant.Id=="medium").All(e=>e.Health>32),"sparse size variants carry actual stats");
Check(front.Navigation.BuildCount==1,"all units share unchanged topology fields");

var arena=Arena();DemoLoader.Validate(arena);
var bypass=new World(arena);
var initialNext=bypass.Navigation.Next(new(4.5f,4.5f));
for(int y=3;y<=5;y++)bypass.Build(y,5,y);
Check(bypass.Navigation.Next(new(4.5f,4.5f))!=initialNext,"live buildings change navigation");
bypass.Begin();bool collision=false;
for(int i=0;i<750;i++){bypass.Step();collision|=bypass.Enemies.Any(e=>!bypass.CanOccupy(e.Position,e.Variant.Radius));}
Check(!collision,"units cannot pass through live buildings");
Check(bypass.Enemies.Any(e=>e.Position.X>6)&&bypass.Towers.Skip(1).All(t=>t.Health==arena.Tower.Health),"open detour is used without attacking bypassable towers");

var siege=new World(arena);for(int y=1;y<=7;y++)Check(siege.Build(y,5,y).Success,$"complete barricade placement legal row {y}");
Check(siege.Navigation.Distance(1,4)<0,"complete barricade closes open reachability");
siege.Begin();bool attacked=false;collision=false;int firstBreach=-1;
for(int i=0;i<1200;i++)
{
    siege.Step();attacked|=siege.Enemies.Any(e=>e.AttackTarget!=null&&e.AttackTarget!=siege.Core.Id);
    collision|=siege.Enemies.Any(e=>!siege.CanOccupy(e.Position,e.Variant.Radius));
    if(firstBreach<0&&siege.Towers.Skip(1).Any(t=>t.Health<=0))
    {firstBreach=i;Check(siege.Navigation.Distance(1,4)>=0,"destroyed tower restores navigation in the same step");}
}
Check(attacked&&firstBreach>0,"enclosed troops attack and breach instead of stalling");
Check(!collision,"siege does not phase through intact barricade");
Check(siege.Enemies.Any(e=>e.Position.X>7)&&siege.Core.Health<arena.CoreHealth,"troops exploit breach and attack core as a building");
Check(siege.Towers.Skip(1).Count(t=>t.Health<=0)<7,"breach redirects army instead of demolishing every tower");

var narrow=new World(arena);
for(int y=1;y<=7;y++)if(y!=3)narrow.Build(y,5,y);
narrow.Begin();Advance(narrow,1200);
Check(narrow.Enemies.All(e=>e.Position.X>5)&&narrow.Towers.Skip(1).All(t=>t.Health==arena.Tower.Health),"one-cell off-axis gap is traversable by both sizes");
var diagonal=new World(arena);
for(int y=1;y<=7;y++){if(y!=3)diagonal.Build(y,5,y);if(y!=4)diagonal.Build(10+y,6,y);}
Check(diagonal.Navigation.Distance(1,4)<0,"diagonal-only gap is not a valid open passage");
diagonal.Begin();Advance(diagonal,1200);
Check(diagonal.Enemies.Any(e=>e.Position.X>7),"diagonal corner obstruction can be breached without deadlock");
var large=new World(arena with{Tower=arena.Tower with{Width=2,Height=2}});
for(int y=1;y<=5;y+=2)large.Build(y,5,y);
large.Begin();Advance(large,90);
var crossing=large.Enemies[0];
var crossingBefore=crossing.Position;
Check(large.Build(99,(int)crossing.Position.X,(int)crossing.Position.Y).Success&&(crossing.Position!=crossingBefore||crossing.Crushed),"construction displaces overlapping soldier or marks confinement");
var removed=large.At(5,3)!;int navBefore=large.Navigation.BuildCount;
large.Sell(100,removed.Id);
Check(large.At(5,3)==null&&large.At(6,4)==null&&large.Navigation.BuildCount>navBefore,"combat sale opens all multi-cell footprint immediately");
var narrowMap=arena with{Map=["############","############","A..........#","A..........#","A..........#","A..........#","A..........#","############","############"],CoreY=4};
var multiSiege=new World(narrowMap with{Tower=arena.Tower with{Width=2,Height=2}});
multiSiege.Build(1,5,2);multiSiege.Build(2,5,4);
// The remaining fifth row is closed with terrain to form a two-building seal.
var multiDef=narrowMap with{Map=narrowMap.Map.Select((row,y)=>y==6?row[..5]+"##"+row[7..]:row).ToArray(),Tower=arena.Tower with{Width=2,Height=2}};
multiSiege=new World(multiDef);multiSiege.Build(1,5,2);multiSiege.Build(2,5,4);multiSiege.Begin();Advance(multiSiege,1200);
Check(multiSiege.Towers.Skip(1).Any(t=>t.Health<=0)&&multiSiege.Enemies.Any(e=>e.Position.X>7),"destroyed 2x2 building opens entire footprint for army");

var slow=arena with{DaySeconds=4,NightSeconds=1,Waves=[Wave("d1",2,0),Wave("d2",2,0)],Enemy=arena.Enemy with{Variants=arena.Enemy.Variants.Select(v=>v with{Speed=.1f}).ToArray()}};
var recovery=new World(slow);recovery.Build(1,3,3);recovery.Build(2,5,3);recovery.Begin();recovery.Step();
var wreck=recovery.Towers[1];wreck.Health=0;wreck.Energy=.5f;var damaged=recovery.Towers[2];damaged.Health=20;
var retained=recovery.Enemies[0];retained.Position=wreck.Position;
Advance(recovery,29);
Check(recovery.Phase==Phase.Day&&recovery.Enemies.Count==2,"dawn retains enemy entities");
Check(wreck.Health==slow.Tower.Health&&damaged.Health==20&&wreck.Energy==.5f,"dawn revives wreck only without bonus health or energy");
Check(recovery.Enemies.Contains(retained)&&wreck.Blocking&&retained.Position!=wreck.Position&&recovery.CanOccupy(retained.Position,retained.Variant.Radius),"revival pushes overlapping soldier to nearby clear ground");
int spawned=recovery.TotalSpawned;var before=retained.Position;Advance(recovery,30);
Check(recovery.TotalSpawned==spawned&&retained.Position!=before,"day stops spawning but continues existing combat");
Check(!recovery.CanOccupy(wreck.Position,retained.Variant.Radius),"revived tower blocks displaced soldier from re-entering");
wreck.Health=0;recovery.Step();Check(wreck.Health==0,"revival occurs only once per dawn");
var fatal=new World(slow);fatal.Begin();Advance(fatal,29);fatal.Core.Health=0;fatal.Step();Check(fatal.Phase==Phase.Lost,"core death precedes dawn");
var flight=new World(slow with{Tower=slow.Tower with{ProjectileSpeed=.1f}});flight.Build(1,1,3);flight.Begin();Advance(flight,29);
var bullet=flight.Projectiles.First();flight.Step();Check(flight.Projectiles.Contains(bullet),"projectile survives dawn");

var trapMap=arena.Map.Select((row,y)=>new string(row.Select((c,x)=>Math.Abs(x-4)<=1&&Math.Abs(y-4)<=1&&(x!=4||y!=4)?'#':c).ToArray())).ToArray();
var crushDef=arena with{Map=trapMap,Tower=arena.Tower with{Health=200},Waves=[Wave("trap",1,0)]};
var crush=new World(crushDef);crush.Begin();crush.Step();var trapped=crush.Enemies[0];trapped.Position=new(4.5f,4.5f);trapped.Cooldown=100;
float trappedHealth=trapped.Health;
Check(crush.Build(1,4,4).Success&&trapped.Crushed,"no empty neighbor confines soldier instead of deleting it");
Advance(crush,15);
float expectedCrush=crushDef.Space.CrushDamagePerSecond*World.FixedDelta*15;
Check(MathF.Abs(trapped.Health-(trappedHealth-expectedCrush))<.02f&&MathF.Abs(crush.At(4,4)!.Health-(200-expectedCrush))<.02f,"confinement causes continuous bilateral collision damage");
var crushTower=crush.At(4,4)!;crushTower.Health=.1f;crush.Step();
Check(crushTower.Health==0&&!crushTower.Blocking&&!trapped.Crushed,"collision can destroy tower and release confinement");
trapped.Health=.1f;crushTower.Health=200;crush.Step();
Check(!crush.Enemies.Contains(trapped)&&crush.Kills==1,"collision death settles once and removes enemy");

var protectedDef=crushDef with{NightSeconds=1,DaySeconds=10,Waves=[Wave("p1",1,0),Wave("p2",1,0)]};
var protectedWorld=new World(protectedDef);protectedWorld.Build(1,4,4);protectedWorld.Build(2,7,4);protectedWorld.Begin();protectedWorld.Step();
var pinned=protectedWorld.Enemies[0];pinned.Position=new(4.5f,4.5f);pinned.Cooldown=100;
var reviving=protectedWorld.At(4,4)!;reviving.Health=0;Advance(protectedWorld,29);
Check(protectedWorld.Towers.All(t=>t.InvulnerableUntilTick==protectedWorld.Tick+60),"dawn protects core, living towers and revived towers together");
float pinnedBefore=pinned.Health;Advance(protectedWorld,15);
Check(reviving.Health==200&&pinned.Health<pinnedBefore,"dawn immunity prevents tower collision damage but not enemy damage");
Advance(protectedWorld,60);
Check(reviving.Health<200&&protectedWorld.DawnProtectionRemaining==0,"bilateral collision resumes after dawn immunity expires");
var coreGuard=new World(slow);coreGuard.Begin();Advance(coreGuard,30);
var attacker=coreGuard.Enemies[0];attacker.Position=new(coreGuard.Core.X-.2f,coreGuard.Core.Y+.5f);attacker.Cooldown=0;
float guardedHealth=coreGuard.Core.Health;coreGuard.Step();Check(coreGuard.Core.Health==guardedHealth,"dawn protection also blocks ordinary melee against core");
Advance(coreGuard,65);attacker.Cooldown=0;coreGuard.Step();Check(coreGuard.Core.Health<guardedHealth,"core can be damaged again after protection");
var multiTrapMap=arena.Map.Select((row,y)=>new string(row.Select((c,x)=>x>=3&&x<=6&&y>=2&&y<=5&&(x==3||x==6||y==2||y==5)?'#':c).ToArray())).ToArray();
var multiCrush=new World(crushDef with{Map=multiTrapMap,Tower=crushDef.Tower with{Width=2,Height=2}});multiCrush.Begin();multiCrush.Step();
var seam=multiCrush.Enemies[0];seam.Position=new(5,4);seam.Cooldown=100;float seamHealth=seam.Health;multiCrush.Build(1,4,3);multiCrush.Step();
Check(MathF.Abs(multiCrush.At(4,3)!.Health-(200-crushDef.Space.CrushDamagePerSecond*World.FixedDelta))<.001f&&MathF.Abs(seam.Health-(seamHealth-crushDef.Space.CrushDamagePerSecond*World.FixedDelta))<.01f,"2x2 contact is deduplicated for both sides");

World Play(bool defend)
{
    var w=new World(definition);long r=0;
    var positions=new[]{(24,8),(25,12),(24,10),(27,12),(27,8),(28,10),(25,7),(26,13),(29,11),(29,8),(24,14),(23,6)};
    if(defend)foreach(var p in positions.Take(4))w.Build(++r,p.Item1,p.Item2);
    w.Begin();
    for(int i=0;i<18000&&!w.Terminal;i++)
    {if(defend&&i%120==0)foreach(var p in positions)if(w.Quote(p.Item1,p.Item2).Success){w.Build(++r,p.Item1,p.Item2);break;}w.Step();}
    return w;
}
var win=Play(true);var repeat=Play(true);var lose=Play(false);
Console.WriteLine($"OUTCOMES defended={win.Phase} hp={win.Core.Health} kills={win.Kills} remaining={win.Enemies.Count}; empty={lose.Phase} remaining={lose.Enemies.Count}");
Check(win.Phase==Phase.Won,"playable defense wins all nights");
Check(lose.Phase==Phase.Lost,"undefended core is destroyed");
Check(win.Tick==repeat.Tick&&win.Core.Health==repeat.Core.Health&&win.Gold==repeat.Gold&&win.Kills==repeat.Kills,"same commands reproduce outcome");
Check(win.Towers.Skip(1).All(t=>t.Energy>=0&&t.Energy<=definition.Tower.Capacitor),"capacitor remains bounded");
Check(new World(definition).Enemies.Count==0,"restart clears previous army");
Invalid(definition with{Enemy=definition.Enemy with{Variants=[definition.Enemy.Variants[0]]}},"missing size reference rejected");
Invalid(definition with{Waves=[new([new("bad","missing",8,0,2,6)])]},"missing front reference rejected");
Invalid(definition with{Fronts=[new("bad","northwest",[new(0,3),new(0,5)])]},"discontinuous front rejected");
Invalid(definition with{Terrains=definition.Terrains.Where(t=>t.Symbol!=":").ToArray()},"unknown terrain rejected");
Check(MathF.Abs(new World(arena).TerrainHit(new(1.5f,1.5f),new(101.5f,1.5f))-.095f)<.00001f,"high-speed projectile sweep hits first barrier");
Console.WriteLine($"CHECKS_OK {count}");

if(args.Contains("--stress"))
{
    var stressDef=definition with{InitialGold=100000,CoreHealth=100000000,NightSeconds=1000,
        Tower=definition.Tower with{Damage=.00001f,Health=100000000},
        Enemy=definition.Enemy with{Variants=definition.Enemy.Variants.Select(v=>v with{Health=1000000,Damage=.01f}).ToArray()},
        Waves=[new([new("large-a","A",500,0,100,4),new("large-b","B",500,0,100,4)])]};
    var stress=new World(stressDef);long r=0;
    for(int y=0;y<stress.Height&&stress.Towers.Count<101;y++)for(int x=0;x<stress.Width&&stress.Towers.Count<101;x++)if(stress.Quote(x,y).Success)stress.Build(++r,x,y);
    stress.Begin();Advance(stress,4000);
    var times=new double[300];long alloc=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<times.Length;i++){long begin=Stopwatch.GetTimestamp();stress.Step();times[i]=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;}
    long allocated=GC.GetAllocatedBytesForCurrentThread()-alloc;Array.Sort(times);
    Console.WriteLine($"STRESS facilities={stress.Towers.Count} actualEnemies={stress.Enemies.Count} steps=300 p95={times[284]:F3}ms p99={times[296]:F3}ms allocation={allocated}B topologyBuilds={stress.Navigation.BuildCount}");
}
