using System.Numerics;

namespace DiscreteTD.Simulation;

// Two shared fields per topology, never a full-map search per soldier.
// Prefer an open route; the breach field treats buildings as costly, destructible cells.
public sealed class NavigationField
{
    readonly int width, height, goal;
    readonly bool[] terrain, blocked;
    readonly int[] open, breach;
    static readonly Cell[] Directions = [new(1,0),new(0,-1),new(0,1),new(-1,0),
        new(1,-1),new(1,1),new(-1,-1),new(-1,1)];
    const int Unreachable = int.MaxValue;
    public int BuildCount { get; private set; }

    public NavigationField(DemoDefinition d)
    {
        width=d.Map[0].Length; height=d.Map.Length; goal=d.CoreY*width+d.CoreX;
        terrain=new bool[width*height]; blocked=new bool[terrain.Length];
        open=new int[terrain.Length]; breach=new int[terrain.Length];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {var t=d.TerrainAt(x,y);terrain[y*width+x]=t.EnemyPassable&&!t.BlocksMovement;}
        Rebuild();
    }
    bool Inside(int x,int y)=>x>=0&&y>=0&&x<width&&y<height;
    bool Pass(int x,int y,bool siege)=>Inside(x,y)&&terrain[y*width+x]&&(siege||!blocked[y*width+x]||y*width+x==goal);
    bool Edge(int x,int y,Cell q,bool siege)=>Pass(x+q.X,y+q.Y,siege)&&
        (q.X==0||q.Y==0||(Pass(x+q.X,y,siege)&&Pass(x,y+q.Y,siege)));
    int Cost(int destination,Cell delta,bool siege)=>(delta.X!=0&&delta.Y!=0?14:10)+
        (siege&&blocked[destination]&&destination!=goal?80:0);

    public void Update(Func<int,int,bool> isBlocked)
    {
        bool changed=false;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {int i=y*width+x;bool value=isBlocked(x,y);if(blocked[i]!=value){blocked[i]=value;changed=true;}}
        if(changed)Rebuild();
    }
    void Rebuild(){Solve(open,false);Solve(breach,true);BuildCount++;}
    void Solve(int[] values,bool siege)
    {
        Array.Fill(values,Unreachable);values[goal]=0;
        var queue=new PriorityQueue<int,(int Distance,int Cell)>();queue.Enqueue(goal,(0,goal));
        while(queue.TryDequeue(out int i,out var priority))
        {
            if(priority.Distance!=values[i])continue;
            int x=i%width,y=i/width;
            foreach(var q in Directions)
            {
                if(!Edge(x,y,q,siege))continue;
                int n=(y+q.Y)*width+x+q.X;
                int distance=values[i]+Cost(i,q,siege);
                if(distance>=values[n])continue;
                values[n]=distance;queue.Enqueue(n,(distance,n));
            }
        }
    }
    public int Distance(int x,int y)=>!Inside(x,y)||open[y*width+x]==Unreachable?-1:open[y*width+x];
    public bool NeedsBreach(Vector2 p)=>Distance((int)p.X,(int)p.Y)<0;
    public Cell NextCell(Vector2 p)
    {
        int x=(int)p.X,y=(int)p.Y;
        if(!Inside(x,y))return new(x,y);
        bool siege=NeedsBreach(p);var values=siege?breach:open;
        int best=Unreachable;float bestAlignment=float.NegativeInfinity;Cell result=new(x,y);
        var toGoal=new Vector2(goal%width+.5f,goal/width+.5f)-p;
        foreach(var q in Directions)
        {
            if(!Edge(x,y,q,siege))continue;
            int n=(y+q.Y)*width+x+q.X;
            if(values[n]==Unreachable)continue;
            int cost=values[n]+Cost(n,q,siege);
            float alignment=Vector2.Dot(Vector2.Normalize(new(q.X,q.Y)),toGoal);
            if(cost<best||(cost==best&&alignment>bestAlignment))
            {best=cost;bestAlignment=alignment;result=new(x+q.X,y+q.Y);}
        }
        return result;
    }
    public Vector2 Next(Vector2 p)
    {var c=NextCell(p);return new(c.X+.5f,c.Y+.5f);}
}
