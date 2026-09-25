using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using DiscreteTD.Simulation;

namespace DiscreteTD.Content;

public static class DemoLoader
{
    public static DemoDefinition Parse(string json)
    {
        DemoDefinition d;
        try { d = JsonSerializer.Deserialize<DemoDefinition>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty demo definition."); }
        catch(JsonException error) { throw new InvalidDataException($"Invalid JSON at {error.Path}",error); }
        Validate(d);
        return d;
    }

    public static void Validate(DemoDefinition d)
    {
        void Require([DoesNotReturnIf(false)] bool valid, string field) { if (!valid) throw new InvalidDataException(field); }
        Require(d.SchemaVersion == 2, "schemaVersion");
        Require(d.Map is { Length: >= 5 and <= 100 } && d.Map.All(r=>r!=null) && d.Map[0].Length is >=8 and <=100, "map");
        Require(d.Fronts!=null && d.Terrains!=null && d.Regions!=null && d.Tower!=null && d.Enemy!=null && d.Waves!=null,"required fronts / terrains / regions / tower / enemy / waves");
        Require(d.Space!=null&&float.IsFinite(d.Space.CrushDamagePerSecond)&&d.Space.CrushDamagePerSecond>0&&float.IsFinite(d.Space.DawnProtectionSeconds)&&d.Space.DawnProtectionSeconds>0,"space collision / dawn protection");
        Require(!string.IsNullOrEmpty(d.Enemy.Id) && d.Enemy.Variants!=null && d.Enemy.Variants.All(v=>v!=null&&!string.IsNullOrEmpty(v.Id)),"enemy variants");
        Require(d.Waves.All(w=>w!=null),"wave entries");
        int w = d.Map[0].Length;
        Require(d.Terrains.Length>0&&d.Terrains.All(t=>t!=null&&!string.IsNullOrWhiteSpace(t.Id)&&t.Symbol is {Length:1}),"terrain definitions");
        Require(d.Terrains.Select(t=>t.Id).Distinct().Count()==d.Terrains.Length&&d.Terrains.Select(t=>t.Symbol).Distinct().Count()==d.Terrains.Length,"duplicate terrain");
        Require(d.Map.All(r => r.Length == w && r.All(c => d.Terrains.Any(t=>t.Symbol[0]==c))), "map cells");
        Require(d.CoreX > 0 && d.CoreX < w && d.CoreY >= 0 && d.CoreY < d.Map.Length, "core position");
        Require(d.TerrainAt(d.CoreX,d.CoreY) is {EnemyPassable:true,PermanentBuildable:true,BlocksMovement:false}, "core terrain");
        Require(d.Regions.Length>0&&d.Regions.All(r=>r!=null&&!string.IsNullOrWhiteSpace(r.Id)&&!string.IsNullOrWhiteSpace(r.Name)&&r.X>=0&&r.Y>=0&&r.Width>0&&r.Height>0&&r.X+r.Width<=w&&r.Y+r.Height<=d.Map.Length),"regions");
        Require(d.Regions.Select(r=>r.Id).Distinct().Count()==d.Regions.Length,"duplicate region");
        Require(d.Fronts.Length>0&&d.Fronts.All(f=>f!=null&&!string.IsNullOrWhiteSpace(f.Id)&&f.Cells is {Length:>0}&&d.Regions.Any(r=>r.Id==f.RegionId)),"fronts");
        Require(d.Fronts.Select(f=>f.Id).Distinct().Count()==d.Fronts.Length,"duplicate front");
        foreach(var front in d.Fronts)
        {
            Require(front.Cells.Distinct().Count()==front.Cells.Length,"duplicate front cell");
            var region=d.Regions.Single(r=>r.Id==front.RegionId);
            foreach(var c in front.Cells)
            {
                Require(c.X>=0&&c.X<w&&c.Y>=0&&c.Y<d.Map.Length&&(c.X==0||c.Y==0||c.X==w-1||c.Y==d.Map.Length-1),"front boundary");
                Require(c.X>=region.X&&c.X<region.X+region.Width&&c.Y>=region.Y&&c.Y<region.Y+region.Height,"front region");
                Require(d.TerrainAt(c.X,c.Y) is {EnemyPassable:true,PermanentBuildable:false,BlocksMovement:false},"front terrain");
            }
            var cells=front.Cells;
            Require(cells.All(c=>c.X==cells[0].X)||cells.All(c=>c.Y==cells[0].Y),"front must be straight");
            Require(cells.Max(c=>c.X)-cells.Min(c=>c.X)+cells.Max(c=>c.Y)-cells.Min(c=>c.Y)+1==cells.Length,"front must be contiguous");
        }
        Require(d.InitialGold >= 0 && d.Income >= 0 && d.CoreHealth > 0, "economy");
        Require(float.IsFinite(d.DaySeconds) && d.DaySeconds > 0 && float.IsFinite(d.NightSeconds) && d.NightSeconds > 0 && d.IncomeInterval > 0, "time");
        var t = d.Tower;
        Require(t.Width is >=1 and <=2 && t.Height is >=1 and <=2,"demo footprint");
        Require(t.Cost > 0 && t.Refund >= 0 && t.Refund <= t.Cost, "tower cost");
        Require(new[] { t.Health,t.Range,t.Damage,t.Cooldown,t.ProjectileSpeed,t.Capacitor,t.ShotEnergy,t.ChargePower,d.CoreHealth,d.IncomeInterval }.All(v => float.IsFinite(v) && v > 0), "positive finite values");
        Require(t.Capacitor >= t.ShotEnergy, "capacitor");
        Require(d.Enemy.Id.Length > 0 && d.Enemy.Variants.Length > 0 && d.Enemy.Variants.Select(v => v.Id).Distinct().Count() == d.Enemy.Variants.Length, "enemy variants");
        foreach (var v in d.Enemy.Variants)
            Require(new[] {v.Health,v.Speed,v.Damage,v.Radius}.All(x => float.IsFinite(x) && x > 0) && v.Radius <= .4f, "variant values / demo clearance");
        Require(d.Enemy.Variants.Any(v => v.Id == "small") && d.Enemy.Variants.Any(v => v.Id == "medium"), "wave variant references");
        Require(d.Waves.Length>0&&d.Waves.All(v=>v.Detachments is {Length:>0}),"waves");
        var batches=d.Waves.SelectMany(v=>v.Detachments).ToArray();
        Require(batches.All(b=>b!=null&&!string.IsNullOrWhiteSpace(b.Id)&&b.Count>0&&b.MediumEvery>0&&float.IsFinite(b.StartSeconds)&&b.StartSeconds>=0&&float.IsFinite(b.SpawnWindow)&&b.SpawnWindow>=0&&b.StartSeconds+b.SpawnWindow<d.NightSeconds&&d.Fronts.Any(f=>f.Id==b.FrontId)),"detachment schedule / front reference");
        Require(batches.Select(b=>b.Id).Distinct().Count()==batches.Length,"duplicate detachment");
        var nav = new NavigationField(d);
        Require(d.Fronts.SelectMany(f=>f.Cells).All(c=>nav.Distance(c.X,c.Y)>=0), "front has no route to core");
    }
}
