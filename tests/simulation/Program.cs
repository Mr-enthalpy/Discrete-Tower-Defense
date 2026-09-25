using System.Diagnostics;
using DiscreteTD.Content;
using DiscreteTD.Simulation;

var definition=DemoLoader.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"demo-a.json")));
int count=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;Console.WriteLine($"PASS {name}");}
void Advance(World w,int n){for(int i=0;i<n;i++)w.Step();}

var world=new World(definition);
Check(!world.Build(1,1,4).Success&&world.Gold==definition.InitialGold,"road placement has no side effects");
Check(world.Build(2,16,6).Success,"build valid tower");
int after=world.Gold;
world.Build(2,17,6);
Check(world.Gold==after&&world.Towers.Count==2,"duplicate request is idempotent");
Check(!world.Build(3,16,6).Success&&world.Gold==after,"occupied footprint is atomic");
var sold=world.Towers[1].Id;
world.Sell(4,sold);world.Sell(4,sold);
Check(world.Gold==definition.InitialGold&&world.Towers.Count==1,"sell refunds exactly once");
Check(!new World(definition,2).Sell(1,sold).Success,"old world handle rejected");
var multi=new World(definition with{Tower=definition.Tower with{Width=2,Height=2}});
Check(multi.Build(1,1,1).Success&&ReferenceEquals(multi.At(1,1),multi.At(2,2)),"multi-cell footprint belongs to one entity");
int multiGold=multi.Gold;
Check(!multi.Build(2,2,2).Success&&multi.At(3,3)==null&&multi.Gold==multiGold,"partial multi-cell overlap is fully atomic");
multi.Sell(3,multi.At(1,1)!.Id);
Check(multi.At(1,1)==null&&multi.At(2,2)==null&&multi.Gold==definition.InitialGold,"multi-cell sale clears whole footprint and refunds once");
Advance(world,100);
Check(world.Tick==0&&world.TotalSpawned==0,"preparation does not advance time");

var slow=definition with{DaySeconds=4,NightSeconds=1,Waves=[new(2,.1f,2)],Enemy=definition.Enemy with{Variants=definition.Enemy.Variants.Select(v=>v with{Speed=.1f}).ToArray()}};
var dawn=new World(slow);dawn.Begin();Advance(dawn,31);
Check(dawn.Phase==Phase.Clearing&&dawn.Enemies.Count==2,"dawn retains enemies instead of clearing");
Check(dawn.Enemies[0].Variant.Id=="small"&&dawn.Enemies[1].Variant.Id=="medium"&&dawn.Enemies[1].Health>dawn.Enemies[0].Health,"same type distinct size stats");
int spawned=dawn.TotalSpawned;var before=dawn.Enemies[0].Position;
Advance(dawn,30);
Check(dawn.TotalSpawned==spawned&&dawn.Enemies[0].Position!=before,"daytime stops spawning but continues movement");
Check(dawn.Navigation.BuildCount==1,"navigation shared across sizes and legions");

var recoveryDef=slow with {Waves=[new(1,1,2),new(1,1,2)],Tower=slow.Tower with{Range=.1f}};
var recovery=new World(recoveryDef);recovery.Build(1,3,3);recovery.Build(2,5,3);recovery.Begin();recovery.Step();
var wreck=recovery.Towers[1];wreck.Health=0;wreck.Energy=.5f;
var damaged=recovery.Towers[2];damaged.Health=50;
var retained=recovery.Enemies[0];retained.Position=wreck.Position;
Advance(recovery,29);
Check(recovery.Phase==Phase.Day&&wreck.Health==recoveryDef.Tower.Health,"dawn revives destroyed permanent tower");
Check(damaged.Health==50&&wreck.Energy==.5f,"dawn neither heals living tower nor refills stored energy");
Check(recovery.Enemies.Contains(retained)&&retained.Health==retained.Variant.Health&&!wreck.Blocking,"overlapping enemy retained without damage or entrapment");
wreck.Health=0;recovery.Step();
Check(wreck.Health==0,"daytime revival occurs once, not continuously");
var fatal=new World(slow);fatal.Begin();Advance(fatal,29);fatal.Core.Health=0;fatal.Step();
Check(fatal.Phase==Phase.Lost,"core death wins over same-step dawn");

var projectileDef=slow with {Tower=slow.Tower with{ProjectileSpeed=.1f}};
var flight=new World(projectileDef);flight.Build(1,1,3);flight.Begin();Advance(flight,29);
var bullet=flight.Projectiles.First();flight.Step();
Check(flight.Projectiles.Contains(bullet),"in-flight projectile survives dawn");

var battle=new World(definition);long cmd=0;
foreach(var p in new[]{(15,6),(16,10),(18,6),(19,10)})battle.Build(++cmd,p.Item1,p.Item2);
battle.Begin();Advance(battle,900);
Check(battle.Kills>0,"projectiles actually hit moving enemies");
Check(battle.Towers.Skip(1).All(t=>t.Energy>=0&&t.Energy<=definition.Tower.Capacitor),"energy remains bounded");

World Play(bool defend)
{
    var w=new World(definition);long r=0;
    if(defend)foreach(var p in new[]{(15,6),(16,10),(18,6),(19,10)})w.Build(++r,p.Item1,p.Item2);
    w.Begin();
    for(int i=0;i<15000&&!w.Terminal;i++)
    {
        if(defend&&i%120==0)
            foreach(var p in new[]{(17,6),(18,9),(20,7),(21,9),(19,7),(17,10),(20,10),(21,7)})
                if(w.Quote(p.Item1,p.Item2).Success){w.Build(++r,p.Item1,p.Item2);break;}
        w.Step();
    }
    return w;
}
var win=Play(true);var repeat=Play(true);var lose=Play(false);
Console.WriteLine($"OUTCOMES defended={win.Phase} hp={win.Core.Health} kills={win.Kills}; empty={lose.Phase}");
Check(win.Phase==Phase.Won,"one playable winning defense");
Check(lose.Phase==Phase.Lost,"undefended core can lose");
Check(win.Tick==repeat.Tick&&win.Core.Health==repeat.Core.Health&&win.Kills==repeat.Kills&&win.Gold==repeat.Gold,"same inputs reproduce outcome");
Check(new World(definition).Kills==0&&new World(definition).Enemies.Count==0,"restart resets state");

try{DemoLoader.Validate(definition with{Enemy=definition.Enemy with{Variants=[definition.Enemy.Variants[0]]}});throw new Exception("invalid variants accepted");}
catch(InvalidDataException){Check(true,"missing wave size reference rejected");}
try{DemoLoader.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"demo-a.json")).Replace("\"tower\"","\"missingTower\""));throw new Exception("missing tower accepted");}
catch(InvalidDataException){Check(true,"missing definition object has an explicit validation error");}
Check(MathF.Abs(new World(definition).TerrainHit(new(.5f,3.5f),new(100.5f,3.5f),.7f)-.055f)<.00001f,"high-speed terrain sweep detects first crossed mountain");

var watch=Stopwatch.StartNew();for(int i=0;i<3;i++)Play(true);watch.Stop();
Console.WriteLine($"CHECKS_OK {count} | three deterministic scenario runs {watch.ElapsedMilliseconds}ms (CPU smoke, not full rendering benchmark)");

if(args.Contains("--stress"))
{
    var stressDef=definition with{InitialGold=100000,CoreHealth=100000000,NightSeconds=120,
        Tower=definition.Tower with{Damage=.00001f,Health=100000000},
        Enemy=definition.Enemy with{Variants=definition.Enemy.Variants.Select(v=>v with{Health=1000000,Damage=.01f}).ToArray()},
        Waves=[new(1000,World.FixedDelta,4)]};
    var stress=new World(stressDef);long r=0;
    for(int y=0;y<stress.Height&&stress.Towers.Count<201;y++)for(int x=0;x<stress.Width&&stress.Towers.Count<201;x++)
        if(stress.Quote(x,y).Success)stress.Build(++r,x,y);
    stress.Begin();Advance(stress,1100);
    var times=new double[300];long alloc=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<times.Length;i++){long begin=Stopwatch.GetTimestamp();stress.Step();times[i]=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;}
    long allocated=GC.GetAllocatedBytesForCurrentThread()-alloc;Array.Sort(times);
    Console.WriteLine($"STRESS facilities={stress.Towers.Count} enemies={stress.Enemies.Count} steps={times.Length} p95={times[284]:F3}ms p99={times[296]:F3}ms allocation={allocated}B navigationFields={stress.Navigation.BuildCount}");
}
