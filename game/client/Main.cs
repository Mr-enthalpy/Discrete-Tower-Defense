using Godot;
using DiscreteTD.Content;
using DiscreteTD.Simulation;
using V2 = Godot.Vector2;
using N2 = System.Numerics.Vector2;

public partial class Main : Node2D
{
    const float Cell=36;
    static readonly V2 Origin=new(38,174);
    static readonly Color Ink=new("213c36"), Muted=new("71877a"), Paper=new("eceee4"), Teal=new("357766"), Red=new("c36747"), Gold=new("b28d47");
    DemoDefinition definition=null!;
    World world=null!;
    SystemFont font=null!;
    Label status=null!, details=null!, notice=null!;
    Button start=null!, pause=null!, speed=null!, build=null!, sell=null!;
    double accumulator;
    float visualTime,noticeTime;
    bool paused,building=true;
    int multiplier=1,epoch;
    long request;
    EntityId? selected;
    (int X,int Y) hover=(-1,-1);
    readonly List<(ShotTrace Trace,float Life)> flashes=[];
    bool smoke;
    int smokeFrames;
    AudioStreamPlayer shotAudio=null!;
    bool muted;
    float soundCooldown;

    public override void _Ready()
    {
        font=new SystemFont{FontNames=["Microsoft YaHei UI","Microsoft YaHei","Noto Sans CJK SC","sans-serif"]};
        definition=DemoLoader.Parse(Godot.FileAccess.GetFileAsString("res://generated/demo-a.json"));
        var theme=new Theme();theme.DefaultFont=font;theme.DefaultFontSize=16;
        var ui=new Control{MouseFilter=Control.MouseFilterEnum.Ignore,Theme=theme};AddChild(ui);
        shotAudio=new AudioStreamPlayer{Stream=ShotSound(),VolumeDb=-22};AddChild(shotAudio);
        Button? audioButton=null;
        audioButton=ButtonAt(ui,"声音 开",new(1064,34),()=>{muted=!muted;audioButton!.Text=muted?"声音 关":"声音 开";},new(85,36));
        status=Text(ui,"",new(950,166),new(292,84),21);
        Text(ui,"布防工具",new(950,282),new(290,30),16);
        build=ButtonAt(ui,"机炮   /   45 金币  [1]",new(950,324),()=>{building=!building;selected=null;});
        sell=ButtonAt(ui,"出售选中设施  [Delete]",new(950,382),Sell);
        start=ButtonAt(ui,"开始第一夜   →",new(950,655),()=>{world.Begin();paused=false;Tell("两处入口开始投放。守住核心。");});
        pause=ButtonAt(ui,"暂停  [Space]",new(950,709),()=>paused=!paused);
        speed=ButtonAt(ui,"速度 ×1  [Tab]",new(950,763),()=>multiplier=multiplier==1?2:1);
        ButtonAt(ui,"重开",new(1161,34),Reset,new(80,36));
        details=Text(ui,"",new(950,454),new(285,180),16);
        notice=Text(ui,"",new(40,786),new(860,30),15);
        Reset();
        smoke=OS.GetCmdlineUserArgs().Contains("--smoke");
        if(smoke)
        {
            foreach(var c in new[]{(15,6),(16,10),(18,6),(19,10)})world.Build(++request,c.Item1,c.Item2);
            world.Begin();
            for(int i=0;i<450;i++)world.Step();
        }
    }
    Label Text(Control parent,string text,V2 pos,V2 size,int point)
    {
        var label=new Label{Text=text,Position=pos,Size=size,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore};
        label.AddThemeColorOverride("font_color",Ink);label.AddThemeFontSizeOverride("font_size",point);parent.AddChild(label);return label;
    }
    Button ButtonAt(Control parent,string text,V2 pos,Action action,V2? size=null)
    {
        var b=new Button{Text=text,Position=pos,Size=size??new V2(292,42),FocusMode=Control.FocusModeEnum.None};
        var style=new StyleBoxFlat{BgColor=new Color("dae3d4"),CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6};
        b.AddThemeStyleboxOverride("normal",style);
        var over=(StyleBoxFlat)style.Duplicate();over.BgColor=new("c8d8c1");b.AddThemeStyleboxOverride("hover",over);
        var pressed=(StyleBoxFlat)style.Duplicate();pressed.BgColor=new("acc8b3");b.AddThemeStyleboxOverride("pressed",pressed);
        b.AddThemeColorOverride("font_color",Ink);b.AddThemeColorOverride("font_hover_color",Ink);b.AddThemeColorOverride("font_pressed_color",Ink);
        b.Pressed+=()=>action();parent.AddChild(b);return b;
    }
    void Reset()
    {
        world=new World(definition,++epoch);request=0;selected=null;building=true;paused=false;multiplier=1;accumulator=0;flashes.Clear();
        Tell("在浅色地面或高地布置机炮。道路不可建造；准备好后开始第一夜。",12);
    }
    void Tell(string text,float time=5){notice.Text=text;noticeTime=time;}
    static AudioStreamWav ShotSound()
    {
        const int rate=22050, samples=1102;
        var bytes=new byte[samples*2];
        for(int i=0;i<samples;i++)
        {
            float time=i/(float)rate,fade=MathF.Exp(-time*95);
            short sample=(short)(MathF.Sin(time*1100*MathF.PI)*fade*9000);
            bytes[i*2]=(byte)(sample&255);bytes[i*2+1]=(byte)(sample>>8);
        }
        return new AudioStreamWav{Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=rate,Data=bytes};
    }
    void Sell()
    {
        if(selected is not { } id){Tell("先选择一座机炮。");return;}
        var result=world.Sell(++request,id);Tell(result.Reason);if(result.Success)selected=null;
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(e is InputEventKey key&&key.Pressed&&!key.Echo)
        {
            switch(key.Keycode)
            {
                case Key.Space:paused=!paused;break;
                case Key.Tab:multiplier=multiplier==1?2:1;break;
                case Key.Key1:building=!building;selected=null;break;
                case Key.Delete:Sell();break;
                case Key.Escape:building=false;selected=null;break;
                case Key.R:Reset();break;
            }
        }
        if(e is InputEventMouseButton mouse&&mouse.Pressed)
        {
            if(mouse.ButtonIndex==MouseButton.Right){building=false;selected=null;return;}
            if(mouse.ButtonIndex!=MouseButton.Left)return;
            var p=(GetGlobalMousePosition()-Origin)/Cell;int x=(int)MathF.Floor(p.X),y=(int)MathF.Floor(p.Y);
            if(!world.Inside(x,y))return;
            var t=world.At(x,y);
            if(t!=null){selected=t.Id;building=false;return;}
            if(building){var result=world.Build(++request,x,y);Tell(result.Reason);}
            else selected=null;
        }
    }
    public override void _Process(double delta)
    {
        visualTime+=(float)delta;
        soundCooldown-=(float)delta;
        var p=(GetGlobalMousePosition()-Origin)/Cell;hover=((int)MathF.Floor(p.X),(int)MathF.Floor(p.Y));
        if(!paused&&!world.Terminal&&world.Phase!=Phase.Preparation)
        {
            accumulator+=delta*multiplier;
            int steps=0;
            while(accumulator>=World.FixedDelta&&steps++<12)
            {
                var previousPhase=world.Phase;world.Step();accumulator-=World.FixedDelta;
                foreach(var trace in world.Traces)flashes.Add((trace,.09f));
                if(world.Traces.Count>0&&!muted&&soundCooldown<=0){shotAudio.Play();soundCooldown=.09f;}
                if(previousPhase==Phase.Night&&world.Phase is Phase.Day or Phase.Clearing)
                    Tell($"天亮了 · 停止投放，复苏已毁机炮。仍有 {world.DawnSurvivors} 个残敌继续战斗。",9);
            }
        }
        for(int i=flashes.Count-1;i>=0;i--)
        {var item=flashes[i];item.Life-=(float)delta;if(item.Life<=0)flashes.RemoveAt(i);else flashes[i]=item;}
        noticeTime-=(float)delta;if(noticeTime<=0)notice.Text="左键建造 / 选择    右键取消    Space 暂停    Tab 倍速    R 重开";
        UpdateUi();QueueRedraw();
        if(smoke&&++smokeFrames==180)
        {
            GD.Print($"DEMO_SMOKE_OK ticks={world.Tick} enemies={world.Enemies.Count} towers={world.Towers.Count}");
            string? output=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--capture="));
            if(output!=null)GetViewport().GetTexture().GetImage().SavePng(output[10..]);
            GetTree().Quit();
        }
    }
    void UpdateUi()
    {
        string phase=world.Phase switch{Phase.Preparation=>"准备布防",Phase.Day=>"白天 · 清理与重建",Phase.Night=>$"第 {world.Night} 夜 · 来袭",Phase.Clearing=>"最后清理",Phase.Won=>"阵地守住了",_=>"核心失守"};
        status.Text=$"{phase}\n"+(world.Phase is Phase.Day or Phase.Night?$"{world.Remaining:0} 秒  /  残敌 {world.Enemies.Count}":$"残敌 {world.Enemies.Count}  /  击退 {world.Kills}");
        start.Visible=world.Phase==Phase.Preparation;pause.Text=paused?"继续  [Space]":"暂停  [Space]";speed.Text=$"速度 ×{multiplier}  [Tab]";
        build.Text=$"{(building?"● ":"")}机炮   /   {definition.Tower.Cost} 金币  [1]";
        var t=world.Towers.FirstOrDefault(t=>t.Id==selected);sell.Disabled=t==null||t.Core||world.Terminal;
        details.Text=t==null ? "机炮\n近距离连续火力。\n\n白天不清除残敌。\n最后一夜后清理残敌获胜。\n\n内部 A 原型 · 测试供能" :
            $"{(t.Core?"核心":"机炮")} {(t.Health<=0?"· 残骸":"")}\n生命 {t.Health:0} / {(t.Core?definition.CoreHealth:definition.Tower.Health):0}\n"+
            (t.Core?$"每 {definition.IncomeInterval:0} 秒收入 {definition.Income} 金币":$"电容 {t.Energy:0.0} / {definition.Tower.Capacitor:0}\n射程 {definition.Tower.Range:0.0} 格\n出售返还 {(world.Phase==Phase.Preparation?definition.Tower.Cost:definition.Tower.Refund)} 金币")+
            (t.Health<=0?"\n下次天亮复苏":"");
    }
    static V2 Screen(N2 p)=>Origin+new V2(p.X,p.Y)*Cell;
    void Words(string s,V2 p,int size,Color c)=>DrawString(font,p,s,HorizontalAlignment.Left,-1,size,c);
    public override void _Draw()
    {
        if(world==null)return;
        DrawRect(new Rect2(0,0,1280,106),Ink);
        Words("离散塔防",new(38,51),31,Paper);Words("DISCRETE / 阵地试验 01",new(40,82),13,new("a7bdab"));
        Words($"{world.Gold:000}  金币",new(494,49),25,new("e6c685"));
        Words($"核心  {MathF.Ceiling(world.Core.Health):0} / {definition.CoreHealth:0}",new(735,49),21,Paper);
        DrawRect(new Rect2(735,67,310,5),new Color("446057"));DrawRect(new Rect2(735,67,310*world.Core.Health/definition.CoreHealth,5),new Color("a7c99c"));
        Words("01 / 河谷阵地",new(40,143),18,Ink);Words("3 夜防守  ·  两处入口  ·  行军体 小 / 中",new(475,143),14,Muted);
        DrawRect(new Rect2(930,126,1,674),new Color("cbd3c4"));
        for(int y=0;y<world.Height;y++)for(int x=0;x<world.Width;x++)
        {
            char tile=world.Terrain(x,y);var pos=Origin+new V2(x,y)*Cell;
            Color c=tile=='#'?new("a7b99d"):tile=='='?new("d5d4bf"):((x+y)%2==0?new Color("e0e5d5"):new Color("dce2d2"));
            DrawRect(new Rect2(pos+V2.One,new V2(Cell-2,Cell-2)),c);
            if(tile=='#')
            {
                DrawPolyline([pos+new V2(5,25),pos+new V2(13,11),pos+new V2(19,20),pos+new V2(26,7),pos+new V2(32,25)],new("7e9677"),1.3f,true);
                DrawLine(pos+new V2(5,30),pos+new V2(31,30),new("7e9677"),1,true);
            }
        }
        foreach(int y in definition.EntranceRows)
        {var pos=Origin+new V2(0,y+.5f)*Cell;DrawLine(pos+new V2(-21,0),pos+new V2(-5,0),Red,3,true);DrawPolyline([pos+new V2(-11,-5),pos+new V2(-5,0),pos+new V2(-11,5)],Red,2,true);}
        var chosen=world.Towers.FirstOrDefault(t=>t.Id==selected);
        if(chosen!=null&&!chosen.Core)DrawArc(Screen(chosen.Position),definition.Tower.Range*Cell,0,Mathf.Tau,96,new Color(Teal,.38f),1.5f,true);
        if(building&&world.Inside(hover.X,hover.Y)&&!world.Terminal)
        {
            bool valid=world.Quote(hover.X,hover.Y).Success;var color=valid?Teal:Red;
            var pos=Origin+new V2(hover.X,hover.Y)*Cell;
            DrawRect(new Rect2(pos,V2.One*Cell),new Color(color,.18f));DrawRect(new Rect2(pos+V2.One,V2.One*(Cell-2)),color,false,2);
            DrawArc(pos+V2.One*Cell/2,definition.Tower.Range*Cell,0,Mathf.Tau,96,new Color(color,.3f),1,true);
        }
        foreach(var t in world.Towers)DrawTower(t);
        float alpha=paused?1:Math.Clamp((float)(accumulator/World.FixedDelta),0,1);
        foreach(var e in world.Enemies)
        {
            var p=Screen(N2.Lerp(e.Previous,e.Position,alpha));float r=e.Variant.Radius*Cell;
            DrawCircle(p+new V2(1,2),r+2,new Color(0,0,0,.08f));
            DrawColoredPolygon([p+new V2(-r,-r*.7f),p+new V2(r*.5f,-r),p+new V2(r,r*.2f),p+new V2(0,r),p+new V2(-r,r*.4f)],new("cb7653"));
            DrawLine(p+new V2(-r*.6f,0),p+new V2(r*.5f,0),new("6c3c2e"),1.8f,true);
            if(e.Variant.Id=="medium")DrawArc(p,r+3,0,Mathf.Tau,16,new("946144"),1.2f,true);
            if(e.Health<e.Variant.Health){DrawLine(p+new V2(-r,-r-5),p+new V2(r,-r-5),new("c3bba4"),2);DrawLine(p+new V2(-r,-r-5),p+new V2(-r+2*r*e.Health/e.Variant.Health,-r-5),Red,2);}
        }
        foreach(var p in world.Projectiles)DrawCircle(Screen(N2.Lerp(p.Previous,p.Position,alpha)),2,new("ecb84e"));
        foreach(var f in flashes){DrawLine(Screen(f.Trace.From),Screen(f.Trace.To),new Color(Gold,Math.Clamp(f.Life/.09f,0,1)),2,true);if(f.Trace.Hit)DrawCircle(Screen(f.Trace.To),3,new Color("f4d48c"));}
        if(world.Phase==Phase.Preparation&&world.Towers.Count==1)
            Words("先在道路旁部署几座机炮 →",Origin+new V2(360,290),20,Teal);
        if(paused&&!world.Terminal)Words("已暂停 · 可以继续布防",Origin+new V2(285,32),22,Ink);
        if(world.Terminal)
        {
            DrawRect(new Rect2(Origin,new V2(world.Width,world.Height)*Cell),new Color(.12f,.22f,.18f,.78f));
            Words(world.Phase==Phase.Won?"防线仍在运转。":"核心失守。",Origin+new V2(238,245),38,Paper);
            Words($"击退 {world.Kills} 个敌人  ·  已到第 {world.Night} 夜",Origin+new V2(238,291),19,new("c7d7bd"));
            Words("按 R 或点击右上角「重开」再试一次",Origin+new V2(238,337),17,Paper);
        }
    }
    void DrawTower(Tower t)
    {
        var p=Screen(t.Position);bool alive=t.Health>0;
        Color c=alive?Teal:new("8f9280");
        if(t.Core)
        {
            DrawRect(new Rect2(p-new V2(15,15),new V2(30,30)),Ink);
            DrawArc(p,11,0,Mathf.Tau,24,new("d8dba6"),2,true);
            DrawLine(p-new V2(6,0),p+new V2(6,0),Paper,2,true);DrawLine(p-new V2(0,6),p+new V2(0,6),Paper,2,true);return;
        }
        DrawRect(new Rect2(p-new V2(12,12),new V2(24,24)),new Color(c,.14f));
        DrawRect(new Rect2(p-new V2(12,12),new V2(24,24)),c,false,1.4f);
        DrawCircle(p,7,new("e7e9d7"));DrawArc(p,7,0,Mathf.Tau,20,c,2,true);
        if(alive)
        {
            var dir=new V2(MathF.Cos(t.Angle),MathF.Sin(t.Angle));DrawLine(p-dir*3,p+dir*16,c,4,true);
            DrawCircle(p,3,Ink);
            DrawLine(p+new V2(-10,15),p+new V2(-10+20*t.Energy/definition.Tower.Capacitor,15),Gold,2,true);
        }
        else {DrawLine(p-new V2(8,8),p+new V2(8,8),c,2);DrawLine(p-new V2(8,-8),p+new V2(8,-8),c,2);}
        if(alive&&t.Health<definition.Tower.Health)DrawLine(p+new V2(-12,-17),p+new V2(-12+24*t.Health/definition.Tower.Health,-17),Red,2,true);
        if(t.Id==selected)DrawRect(new Rect2(p-new V2(17,17),new V2(34,34)),Gold,false,2);
    }
}
