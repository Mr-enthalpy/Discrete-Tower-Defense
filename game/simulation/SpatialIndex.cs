using System.Numerics;

namespace DiscreteTD.Simulation;

internal sealed class SpatialIndex
{
    readonly List<Enemy>[] buckets;
    readonly int width,height;
    public SpatialIndex(int width,int height)
    {
        this.width=width;this.height=height;
        buckets=Enumerable.Range(0,width*height).Select(_=>new List<Enemy>()).ToArray();
    }
    public void Rebuild(List<Enemy> enemies)
    {
        foreach(var b in buckets)b.Clear();
        foreach(var e in enemies)
            buckets[Math.Clamp((int)e.Position.Y,0,height-1)*width+Math.Clamp((int)e.Position.X,0,width-1)].Add(e);
    }
    public void Query(Vector2 center,float radius,List<Enemy> result)
    {
        result.Clear();
        for(int y=Math.Max(0,(int)(center.Y-radius));y<=Math.Min(height-1,(int)(center.Y+radius));y++)
            for(int x=Math.Max(0,(int)(center.X-radius));x<=Math.Min(width-1,(int)(center.X+radius));x++)
                result.AddRange(buckets[y*width+x]);
    }
}
