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
        Require(d.SchemaVersion == 1, "schemaVersion");
        Require(d.Map is { Length: >= 5 and <= 100 } && d.Map.All(r=>r!=null) && d.Map[0].Length is >=8 and <=100, "map");
        Require(d.EntranceRows!=null && d.Tower!=null && d.Enemy!=null && d.Waves!=null,"required entranceRows / tower / enemy / waves");
        Require(!string.IsNullOrEmpty(d.Enemy.Id) && d.Enemy.Variants!=null && d.Enemy.Variants.All(v=>v!=null&&!string.IsNullOrEmpty(v.Id)),"enemy variants");
        Require(d.Waves.All(w=>w!=null),"wave entries");
        int w = d.Map[0].Length;
        Require(d.Map.All(r => r.Length == w && r.All(c => c is '.' or '#' or '=')), "map cells");
        Require(d.CoreX > 0 && d.CoreX < w && d.CoreY >= 0 && d.CoreY < d.Map.Length, "core position");
        Require(d.Map[d.CoreY][d.CoreX] == '.', "core terrain");
        Require(d.EntranceRows.Length > 0 && d.EntranceRows.All(y => y >= 0 && y < d.Map.Length && d.Map[y][0] != '#'), "entrances");
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
        Require(d.Waves.Length > 0 && d.Waves.All(v => v.Count > 0 && float.IsFinite(v.Interval) && v.Interval > 0 && v.MediumEvery > 0), "waves");
        var nav = new NavigationField(d);
        Require(d.EntranceRows.All(y => nav.Distance(0, y) >= 0), "entrance has no route to core");
    }
}
