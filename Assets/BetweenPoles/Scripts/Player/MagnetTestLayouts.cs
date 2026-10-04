using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
// Only installed in the dedicated mechanism test scene. All presets are Play-mode objects.
public sealed class MagnetTestLayouts:MonoBehaviour {
    public GridPlayground board;public Transform center;public string instruction;
    GameObject runtimeRoot;GridTile[] originalTiles;Renderer[] originalSurfaces;
    public void Load(int preset){
        if(!Application.isPlaying||board.Busy)return;
        if(originalTiles==null){originalTiles=board.tiles;var anchor=center.parent;originalSurfaces=anchor.GetComponentsInChildren<Renderer>(true);}
        if(runtimeRoot){runtimeRoot.SetActive(false);Destroy(runtimeRoot);}
        foreach(var r in originalSurfaces)if(r)r.enabled=false;
        foreach(var m in board.magnets)if(m)m.gameObject.SetActive(false);
        runtimeRoot=new GameObject("临时机制测试布局");runtimeRoot.transform.SetParent(transform,false);
        var tiles=new List<GridTile>();var pieces=new List<MagnetPiece>();
        var floorMat=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));floorMat.color=new Color(.76f,.88f,.94f);floorMat.SetFloat("_Grid",1);floorMat.SetFloat("_Painted",1);
        for(int z=-3;z<=3;z++)for(int x=-4;x<=4;x++){
            if((preset==5&&z==0&&(x==0||x==1))||(preset==1&&x==0&&z==0))continue;
            var g=new GameObject("测试格 "+x+","+z);g.transform.SetParent(runtimeRoot.transform,false);g.transform.position=new Vector3(x*board.cellSize,0,z*board.cellSize);
            var t=g.AddComponent<GridTile>();float height=preset==4&&x==1&&z==0?board.cellSize:preset==7&&x==0&&z==1?2*board.cellSize:0;t.surfaceHeight=height;
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.transform.SetParent(g.transform,false);box.transform.localPosition=Vector3.up*(height*.5f-.08f);box.transform.localScale=new Vector3(board.cellSize-.02f,height+.16f,board.cellSize-.02f);Destroy(box.GetComponent<Collider>());box.GetComponent<Renderer>().sharedMaterial=floorMat;
            var binding=g.AddComponent<IslandSurfaceAnchor>();binding.center=center;binding.Apply();tiles.Add(t);
        }
        board.tiles=tiles.ToArray();
        System.Func<int,int,MagnetShape,bool,Quaternion,MagnetPiece> add=(x,z,shape,north,pose)=>{var m=MagnetVisuals.Create(runtimeRoot.transform,new Vector3(x*board.cellSize,0,z*board.cellSize),shape,north,pose,board.cellSize,center);pieces.Add(m);return m;};
        Quaternion flat=Quaternion.identity,vertical=Quaternion.Euler(0,0,90),alongZ=Quaternion.Euler(0,90,0);
        var playerCell=new Vector2Int(-2,0);
        if(preset==0){add(-1,0,MagnetShape.Bar,true,alongZ);add(0,0,MagnetShape.Bar,false,flat);instruction="向右推：两条长轴垂直，接收格有冰面，按蓝色接收条的朝向对齐成一格宽桥。";}
        if(preset==1){add(-1,0,MagnetShape.Bar,true,alongZ);add(0,0,MagnetShape.Bar,false,flat);instruction="向右推：同样的垂直长条，接收格没有冰面；蓝条下沉、红条叠上，形成十字。再次推会原地旋转。";}
        if(preset==8||preset==9){add(-1,0,MagnetShape.Bar,true,preset==8?alongZ:flat);add(0,0,MagnetShape.Bar,false,vertical);instruction=preset==8?"向右推：蓝色接收条竖立，沿推动方向倒下；与红条长轴垂直，在冰面上也能叠成十字。":"向右推：蓝色接收条沿推动方向倒下；红条翻滚后也沿同一轴向落平，合成一格宽桥。";}
        if(preset==2){add(-1,0,MagnetShape.Horseshoe,true,flat);add(0,0,MagnetShape.Horseshoe,false,flat);instruction="向右推：异极 U 型闭合圆环，继续走沿实体环边通过。";}
        if(preset==3){add(-1,0,MagnetShape.Bar,true,vertical);add(0,0,MagnetShape.Bar,true,flat);var t=tiles.First(v=>v.transform.position==new Vector3(board.cellSize,0,0));t.blocked=true;var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);stone.name="调试高台";stone.transform.SetParent(t.transform,false);stone.transform.localPosition=Vector3.up*.4f;stone.GetComponent<Renderer>().sharedMaterial=floorMat;instruction="向右推：远端后面有石头，磁铁不动，主角反冲。面板可去掉障碍再比较。";}
        if(preset==4){add(-1,0,MagnetShape.Bar,true,flat);add(0,0,MagnetShape.Horseshoe,false,flat);instruction="向右推：长条翻成立柱，与平躺 U 合成磁流；再向右走入并登上一层高台。";}
        if(preset==5){add(-1,0,MagnetShape.Bar,true,vertical);add(0,0,MagnetShape.Horseshoe,false,Quaternion.Euler(0,90,0));add(-1,-1,MagnetShape.Bar,true,vertical);instruction="第一根向右推成半桥；第二根从左下格向上推，再向右推入同一桥首，完成两格桥。";}
        if(preset==6||preset==7){var m=add(0,0,MagnetShape.Bar,true,flat);MagnetVisuals.Product(m,MagnetProduct.Cross,board.cellSize,flat,true);
            if(preset==6){add(1,0,MagnetShape.Bar,true,vertical);playerCell=new Vector2Int(-1,0);instruction="向右推十字：十字不移位，右边竖直长条向外拨一格。";}
            else{add(-1,0,MagnetShape.Horseshoe,true,Quaternion.Euler(0,90,0));add(1,0,MagnetShape.Horseshoe,false,Quaternion.Euler(0,-90,0));playerCell=new Vector2Int(0,-1);instruction="左右异极 U 开口相向：十字持续转动。向上走入磁流，再向上走到两层高台。";}
        }
        board.magnets=pieces.ToArray();board.player.position=new Vector3(playerCell.x*board.cellSize,0,playerCell.y*board.cellSize);board.CaptureInitialState();
        var window=board.GetComponent<FiveIslandWindow>();if(window){window.rooms[0].surfaces=runtimeRoot.GetComponentsInChildren<Renderer>(true);window.RefreshLayout();}
    }
    void Start(){Load(0);var panel=GetComponent<MagnetDebugPanel>();if(!panel)panel=gameObject.AddComponent<MagnetDebugPanel>();if(!panel.IsOpen)panel.Toggle();}
}
}
