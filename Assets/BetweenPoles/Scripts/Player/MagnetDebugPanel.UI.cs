using UnityEngine;
namespace BetweenPoles {
public sealed partial class MagnetDebugPanel {
    readonly Color paper=new Color(.957f,.945f,.925f),ink=new Color(.07f,.07f,.07f),accent=new Color(.90f,0,.07f);
    GUIStyle textStyle;int section=1;Vector2Int pan;bool dragging;Vector2 lastMouse;
    readonly string[] kinds={"长条","U 型","宽条","圆环","十字","磁流升降台","两格桥 · 半成品","两格桥","空 / 清除物品","石头"};
    void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    void Label(Rect r,string s,Color color,int size=14,TextAnchor align=TextAnchor.MiddleLeft){textStyle.normal.textColor=color;textStyle.fontSize=size;textStyle.alignment=align;GUI.Label(r,s,textStyle);}
    bool Button(Rect r,string title,bool active=false){Fill(new Rect(r.x+3,r.y+3,r.width,r.height),Color.black);Fill(r,Color.black);Fill(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),active?accent:paper);Label(r,title,active?Color.white:ink,14,TextAnchor.MiddleCenter);return GUI.Button(r,GUIContent.none,GUIStyle.none);}
    void SelectCell(Vector2Int p){
        selected=p;var m=board.MagnetAt(p);if(!m){var tile=board.TileAt(p);shape=tile&&tile.blocked?9:8;return;}
        shape=m.product==MagnetProduct.None?(m.shape==MagnetShape.Bar?0:1):(int)m.product+1;
        pole=(m.product==MagnetProduct.Lift||m.product==MagnetProduct.Bridge||m.product==MagnetProduct.BridgeHalf?m.baseNorth:m.north)?0:1;
        yaw=Mathf.RoundToInt(m.transform.eulerAngles.y/90)%4;
        pose=m.product==MagnetProduct.None&&MagnetPiece.VerticalBar(m.Pose)?1:0;
        if(shape==1){
            var opening=m.Pose*Vector3.forward;
            yaw=(Mathf.RoundToInt(Mathf.Atan2(opening.x,opening.z)*Mathf.Rad2Deg/90)+4)%4;
            pose=(m.Pose*Vector3.up).y<0?1:0;
        }
    }
    void Pick(int kind){shape=kind;ApplySelectedStyle();}
    void OnGUI(){
        if(!board)board=GetComponent<GridPlayground>();if(textStyle==null)textStyle=new GUIStyle(GUI.skin.label){wordWrap=true,fontStyle=FontStyle.Bold};
        if(!IsOpen){if(Button(new Rect(18,18,190,38),"打开磁铁测试编辑器",true))Toggle();return;}
        float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);var oldMatrix=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float width=Screen.width/scale,height=Screen.height/scale;
        Fill(new Rect(0,0,width,height),new Color(.085f,.085f,.085f));Fill(new Rect(0,0,width,62),Color.black);Fill(new Rect(0,59,width,3),accent);
        Label(new Rect(20,8,260,42),"两极之间 / 磁铁测试",Color.white,21);
        string[] tabs={"测试布局","物品","地块"};for(int i=0;i<3;i++)if(Button(new Rect(300+i*104,12,94,35),tabs[i],section==i))section=i;
        if(Button(new Rect(width-280,12,110,35),"重置测试")){board.ResetPuzzle();SelectCell(selected);status="已恢复测试起点。";}
        if(Button(new Rect(width-155,12,135,35),"开始游玩 →",true))IsOpen=false;
        Fill(new Rect(0,62,280,height-62),paper);Fill(new Rect(280,62,4,height-62),Color.black);
        Fill(new Rect(0,62,280,42),ink);Label(new Rect(18,66,240,34),section==0?"/ 测试布局":section==1?"/ 物品样式":"/ 格子操作",Color.white,17);
        var lab=board.GetComponent<MagnetTestLayouts>();
        if(section==0&&lab){string[] names={"冰面宽桥","悬空十字","圆环","反冲","单层磁流","两格桥","十字拨动","两层磁流","竖立接收 · 十字","竖立接收 · 宽桥"};for(int i=0;i<names.Length;i++)if(Button(new Rect(16,120+i*43,246,35),names[i])){lab.Load(i);pan=Vector2Int.zero;SelectCell(board.PlayerCell);status=lab.instruction;}Label(new Rect(16,560,244,160),lab.instruction,ink);}
        if(section==1){for(int row=0;row<kinds.Length;row++){int i=row==8?9:row==9?8:row;var r=new Rect(16,114+row*38,246,33);bool clicked=Button(r,"",shape==i);Icon(new Rect(r.x+10,r.y+3,35,27),i,pole==0,0);Label(new Rect(r.x+54,r.y,185,r.height),kinds[i],shape==i?Color.white:ink);if(clicked)Pick(i);}
            Label(new Rect(16,510,240,24),"极性",ink);if(Button(new Rect(16,540,117,32),"N / 红",pole==0)){pole=0;if(shape<8)ApplySelectedStyle();}if(Button(new Rect(145,540,117,32),"S / 蓝",pole==1)){pole=1;if(shape<8)ApplySelectedStyle();}
            Label(new Rect(16,578,240,22),"朝向",ink);for(int i=0;i<4;i++)if(Button(new Rect(16+i*63,604,57,32),i*90+"°",yaw==i)){yaw=i;if(shape<8)ApplySelectedStyle();}
            Label(new Rect(16,645,240,22),shape<2?"姿态":"成品使用固定配方姿态",ink,13);GUI.enabled=shape<2;string[] poses=shape==1?new[]{"正面平躺","反面平躺"}:new[]{"平躺","竖立","倒立"};float poseWidth=252f/poses.Length;for(int i=0;i<poses.Length;i++)if(Button(new Rect(16+i*poseWidth,672,poseWidth-6,32),poses[i],pose==i)){pose=i;ApplySelectedStyle();}GUI.enabled=true;
        }
        if(section==2){string[] names={"主角移到该格","地面 / 0 层","高台 / 1 层","高台 / 2 层","石头障碍"};for(int i=0;i<5;i++)if(Button(new Rect(16,120+i*52,246,40),names[i])){if(i==0)Teleport();else SetFloor(i==4?0:i-1,i==4);}Label(new Rect(16,410,245,90),"选择右侧格子，再修改地形。磁铁清除请在物品中选择“空”。",ink);}
        Label(new Rect(305,74,width-600,36),"选中格  ("+selected.x+", "+selected.y+")   /   先选格子，再选左侧样式",Color.white,17);
        if(Button(new Rect(width-180,76,160,32),"定位当前岛屿"))pan=Vector2Int.zero;
        var canvas=new Rect(304,124,width-324,height-210);DrawCanvas(canvas);
        Fill(new Rect(284,height-64,width-284,64),Color.black);Label(new Rect(305,height-60,width-330,52),status,Color.white,14);
        Label(new Rect(16,height-67,245,55),"仅本次运行有效\n退出 Play 后恢复原场景",new Color(.4f,.4f,.4f),12);
        GUI.matrix=oldMatrix;
    }
    void DrawCanvas(Rect area){
        var center=board.PlayerCell;var window=board.GetComponent<FiveIslandWindow>();if(window&&window.rooms.Length>0){var p=window.rooms[window.CurrentRoom].center.position;center=new Vector2Int(Mathf.RoundToInt(p.x/board.cellSize),Mathf.RoundToInt(p.z/board.cellSize));}center+=pan;
        const float size=48;int cols=Mathf.Max(9,Mathf.FloorToInt(area.width/size)),rows=Mathf.Max(7,Mathf.FloorToInt((area.height-30)/size));float ox=area.x+(area.width-cols*size)/2,oy=area.y;
        for(int row=0;row<rows;row++)for(int col=0;col<cols;col++){
            var p=center+new Vector2Int(col-cols/2,rows/2-row);var t=board.TileAt(p);var m=board.MagnetAt(p);var r=new Rect(ox+col*size,oy+row*size,size,size);
            Fill(r,new Color(.27f,.27f,.27f));Fill(new Rect(r.x+1,r.y+1,size-1,size-1),t?paper:new Color(.105f,.105f,.105f));
            if(t&&t.blocked){Fill(new Rect(r.x+11,r.y+11,26,26),new Color(.4f,.47f,.52f));Fill(new Rect(r.x+14,r.y+8,23,8),new Color(.62f,.68f,.72f));}
            if(m)TopView(r,m,p);
            if(t&&t.surfaceHeight>.1f)Label(new Rect(r.x+1,r.y,20,16),Mathf.RoundToInt(t.surfaceHeight/board.cellSize)+"H",ink,10);
            if(p==board.PlayerCell){Fill(new Rect(r.center.x-8,r.center.y-8,16,16),new Color(1,.72f,.08f));Fill(new Rect(r.center.x-6,r.center.y-6,12,12),new Color(1,.9f,.5f));}
            if(p==selected){Fill(new Rect(r.x,r.y,size,3),accent);Fill(new Rect(r.x,r.y,3,size),accent);Fill(new Rect(r.xMax-3,r.y,3,size),accent);Fill(new Rect(r.x,r.yMax-3,size,3),accent);}
            if(GUI.Button(r,GUIContent.none,GUIStyle.none)){SelectCell(p);status="已选中 ("+p.x+", "+p.y+")，点击左侧样式即可修改。";}
        }
        for(int col=0;col<cols;col++)Label(new Rect(ox+col*size,oy+rows*size,size,24),(center.x+col-cols/2).ToString(),Color.gray,12,TextAnchor.MiddleCenter);
        var e=Event.current;if(area.Contains(e.mousePosition)&&e.type==EventType.MouseDown&&e.button==2){dragging=true;lastMouse=e.mousePosition;e.Use();}if(dragging&&e.type==EventType.MouseDrag){var d=e.mousePosition-lastMouse;if(d.magnitude>size){pan+=new Vector2Int(-Mathf.RoundToInt(d.x/size),Mathf.RoundToInt(d.y/size));lastMouse=e.mousePosition;}e.Use();}if(e.type==EventType.MouseUp)dragging=false;
    }
    void BridgeCell(Rect r,MagnetPiece m,Vector2Int cell){
        var home=new Vector2Int(Mathf.RoundToInt(m.transform.position.x/board.cellSize),Mathf.RoundToInt(m.transform.position.z/board.cellSize));
        bool first=cell==home;float angle=Mathf.Atan2(-m.bridgeDirection.y,m.bridgeDirection.x)*Mathf.Rad2Deg;
        var saved=GUI.matrix;var pivot=new Vector3(r.center.x,r.center.y,0);
        GUI.matrix=saved*Matrix4x4.Translate(pivot)*Matrix4x4.Rotate(Quaternion.Euler(0,0,angle))*Matrix4x4.Translate(-pivot);
        Color baseColor=m.baseNorth?new Color(.9f,.07f,.13f):new Color(.13f,.48f,.75f),rail=m.baseNorth?new Color(.13f,.48f,.75f):new Color(.9f,.07f,.13f);
        float start=first?r.x+r.width*.28f:r.x;
        Fill(new Rect(start,r.y+r.height*.2f,r.xMax-start,r.height*.13f),rail);
        if(m.product==MagnetProduct.Bridge)Fill(new Rect(start,r.y+r.height*.67f,r.xMax-start,r.height*.13f),rail);
        if(first)for(int i=0;i<15;i++){float a=(90+i*180f/14)*Mathf.Deg2Rad;Fill(new Rect(r.x+r.width*.3f+Mathf.Cos(a)*r.width*.23f-3,r.center.y+Mathf.Sin(a)*r.height*.24f-3,6,6),baseColor);}
        GUI.matrix=saved;
    }
    void Icon(Rect r,int type,bool north,int angle){
        var matrix=GUI.matrix;
        // Compose the local icon rotation before the screen-scale matrix.
        // RotateAroundPivot mixes screen and logical coordinates when GUI is scaled.
        var pivot=new Vector3(r.center.x,r.center.y,0);
        GUI.matrix=matrix*Matrix4x4.Translate(pivot)*Matrix4x4.Rotate(Quaternion.Euler(0,0,angle))*Matrix4x4.Translate(-pivot);Color red=new Color(.90f,.07f,.13f),blue=new Color(.13f,.48f,.75f),c=north?red:blue;float x=r.x,y=r.y,w=r.width,h=r.height;
        if(type==9){Fill(new Rect(x+w*.18f,y+h*.25f,w*.65f,h*.65f),new Color(.4f,.47f,.52f));Fill(new Rect(x+w*.25f,y+h*.12f,w*.53f,h*.24f),new Color(.62f,.68f,.72f));GUI.matrix=matrix;return;}
        if(type==8){Label(r,"×",Color.gray,24,TextAnchor.MiddleCenter);GUI.matrix=matrix;return;}
        if(type==0)Fill(new Rect(x+w*.1f,y+h*.4f,w*.8f,h*.2f),c);
        if(type==2){Fill(new Rect(x+w*.1f,y+h*.3f,w*.8f,h*.2f),c);Fill(new Rect(x+w*.1f,y+h*.5f,w*.8f,h*.2f),north?blue:red);}
        if(type==4){Fill(new Rect(x+w*.1f,y+h*.4f,w*.8f,h*.2f),c);Fill(new Rect(x+w*.4f,y+h*.1f,w*.2f,h*.8f),north?blue:red);}
        if(type==1||type==3||type==5||type==6||type==7){int n=type==3?24:13;for(int i=0;i<n;i++){float a=(type==3?i*360f/n:180+i*180f/12)*Mathf.Deg2Rad;Fill(new Rect(x+w*.5f+Mathf.Cos(a)*w*.31f-w*.065f,y+h*.5f+Mathf.Sin(a)*h*.31f-h*.065f,w*.14f,h*.14f),type==3?(i<12?red:blue):c);}if(type==5)Fill(new Rect(x+w*.44f,y+h*.35f,w*.15f,h*.62f),north?blue:red);if(type>=6){Fill(new Rect(x+w*.13f,y+h*.48f,w*.17f,h*.45f),north?blue:red);if(type==7)Fill(new Rect(x+w*.70f,y+h*.48f,w*.17f,h*.45f),north?blue:red);}}
        GUI.matrix=matrix;
    }
}
}
