using System.Numerics;

namespace DiscreteTD.Simulation;

// One terrain field shared by every legion and variant in this demo's clearance class.
public sealed class NavigationField
{
    readonly int[] distances;
    readonly int width, height;
    static readonly (int X, int Y)[] Directions = [(1,0),(0,-1),(0,1),(-1,0)];
    public int BuildCount => 1;
    public NavigationField(DemoDefinition d)
    {
        width=d.Map[0].Length; height=d.Map.Length;
        distances=Enumerable.Repeat(-1,width*height).ToArray();
        var queue=new Queue<(int X,int Y)>();
        queue.Enqueue((d.CoreX,d.CoreY)); distances[d.CoreY*width+d.CoreX]=0;
        while(queue.TryDequeue(out var c))
            foreach(var delta in Directions)
            {
                int x=c.X+delta.X,y=c.Y+delta.Y;
                if(x<0||y<0||x>=width||y>=height||d.Map[y][x]=='#'||Distance(x,y)>=0)continue;
                distances[y*width+x]=Distance(c.X,c.Y)+1;queue.Enqueue((x,y));
            }
    }
    public int Distance(int x,int y)=> x<0||y<0||x>=width||y>=height ? -1 : distances[y*width+x];
    public Vector2 Next(Vector2 p)
    {
        int x=(int)p.X,y=(int)p.Y;
        var center=new Vector2(x+.5f,y+.5f);
        // Reach the current cell's center before changing direction, avoiding corner cutting.
        if(Vector2.DistanceSquared(p,center)>.012f && (MathF.Abs(p.X-center.X)>.12f && MathF.Abs(p.Y-center.Y)>.12f))return center;
        int best=Distance(x,y); Vector2 target=center;
        foreach(var q in Directions)
        {
            int dist=Distance(x+q.X,y+q.Y);
            if(dist>=0 && (best<0||dist<best)){best=dist;target=new(x+q.X+.5f,y+q.Y+.5f);}
        }
        return target;
    }
}
