using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class MagnetDebugPanel:MonoBehaviour {
    public bool IsOpen {get;private set;}
    GridPlayground board;Vector2Int selected;int shape=8,pole,yaw,pose,rockStyle,lowRockVariant,rockTheme=1;string status="先选择画布中的格子，再点击左侧样式。";
    public void Toggle(){if(!board)board=GetComponent<GridPlayground>();if(board.Busy||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit)return;IsOpen=!IsOpen;if(IsOpen)SelectCell(board.PlayerCell);}
    public bool Place(){
        if(board.Busy||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit)return false;
        if(shape==8){Erase();return true;}
        if(shape==10){
            var ground=board.TileAt(selected);var lab=board.GetComponent<MagnetTestLayouts>();
            if(!lab||!ground||ground.blocked||board.MagnetAt(selected)||selected==board.PlayerCell){status="黑洞需要空的地面格，不能覆盖主角、石头或磁铁。";return false;}
            lab.PlacePortal(ground);board.CaptureInitialState();status="已放置黑洞。至少放置两个，开始游玩后走入黑洞即可选择出口。";return true;
        }
        if(PortalAt(selected)){status="请先清除这个格子的黑洞，再放置其他物品。";return false;}
        if(shape==9){var ground=board.TileAt(selected);if(!ground||selected==board.PlayerCell||board.MagnetAt(selected)){status="石头需要空的地面格，不能覆盖主角或磁铁。";return false;}SetFloor(Mathf.RoundToInt(ground.surfaceHeight/board.LayerHeight),true);return true;}
        var existing=board.MagnetAt(selected);
        if(selected==board.PlayerCell){status="主角所在格不能放置磁铁。";return false;}
        var tile=board.TileAt(selected);if(tile&&tile.blocked){status="石头挡住这个格子。";return false;}
        Transform island=null;if(tile){var a=tile.GetComponentInParent<IslandSurfaceAnchor>();if(a)island=a.center;}
        if(!island){var w=board.GetComponent<FiveIslandWindow>();if(w)island=w.rooms[w.CurrentRoom].center;}
        if(shape==1&&pose>1)pose=0;
        var rotation=Quaternion.Euler(0,yaw*90,0)*(shape>=2?Quaternion.identity:shape==1?(pose==1?Quaternion.Euler(0,0,180):Quaternion.identity):pose==1?Quaternion.Euler(0,0,90):pose==2?Quaternion.Euler(180,0,0):Quaternion.identity);
        var bridgeDir=new Vector2Int(Mathf.RoundToInt((Quaternion.Euler(0,yaw*90,0)*Vector3.right).x),Mathf.RoundToInt((Quaternion.Euler(0,yaw*90,0)*Vector3.right).z));
        if(shape>=6){
            var second=selected+bridgeDir;var occupant=board.MagnetAt(second);
            var ground=board.TileAt(second);
            if((ground&&(ground.blocked||Mathf.Abs(ground.surfaceHeight-(tile?tile.surfaceHeight:0))>.3f))||(occupant&&occupant!=existing)||second==board.PlayerCell){status="无法放置：两格桥第二格 ("+second.x+", "+second.y+") 被石头、高差、其他磁铁或主角占据。普通同高地面允许放桥，原物品已保留。";return false;}
        }
        if(existing&&existing.Occupies(board.PlayerCell,board.cellSize)){status="请先将主角移出这座桥，再修改它。原物品已保留。";return false;}
        var m=MagnetVisuals.Create(board.transform,new Vector3(selected.x*board.cellSize,tile?tile.surfaceHeight:0,selected.y*board.cellSize),shape==1?MagnetShape.Horseshoe:MagnetShape.Bar,pole==0,rotation,board.cellSize,island);
        if(shape>=2){m.bridgeDirection=bridgeDir;MagnetVisuals.Product(m,(MagnetProduct)(shape-1),board.cellSize,Quaternion.Euler(0,yaw*90,0),pole==0);}
        if(existing)existing.gameObject.SetActive(false);
        board.magnets=board.magnets.Where(x=>x&&x.gameObject.activeInHierarchy).Concat(new[]{m}).ToArray();board.CaptureInitialState();status="已放置。关闭面板后推动；R 可回到当前布置。";return true;
    }
    void ApplySelectedStyle(){
        if(!IsOpen||board.Busy)return;
        if(shape==8){Erase();return;}
        Place();
    }
    void Erase(){
        var portal=PortalAt(selected);if(portal&&portal.Cargo){status="黑洞中还有磁铁，请先完成传送或撤销入洞，再清除黑洞。";return;}if(portal){portal.gameObject.SetActive(false);Destroy(portal.gameObject);board.CaptureInitialState();status="已清除黑洞。";return;}
        var m=board.MagnetAt(selected);if(!m){var ground=board.TileAt(selected);if(ground&&ground.blocked){SetFloor(Mathf.RoundToInt(ground.surfaceHeight/board.LayerHeight),false);status="已清除石头。";}else status="该格没有可清除的物品。";return;}
        if(selected==board.PlayerCell){
            Vector2Int[] dirs={Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
            var safe=dirs.Select(d=>selected+d).FirstOrDefault(p=>{var t=board.TileAt(p);return t&&!t.blocked&&board.MagnetAt(p)==null;});
            var safeTile=board.TileAt(safe);
            if(!safeTile){status="主角脚下的磁铁不能清除：周围没有安全地面格。";return;}
            board.player.position=new Vector3(safe.x*board.cellSize,safeTile.surfaceHeight,safe.y*board.cellSize);
        }
        m.StopAllCoroutines();m.gameObject.SetActive(false);board.magnets=board.magnets.Where(x=>x&&x.gameObject.activeInHierarchy).ToArray();board.CaptureInitialState();status="已清除所选格磁铁。主角已移到安全格。";
    }
    void Teleport(){var t=board.TileAt(selected);if(!t||t.blocked||board.MagnetAt(selected)){status="请选择空的地面格。";return;}board.player.position=new Vector3(selected.x*board.cellSize,t.surfaceHeight,selected.y*board.cellSize);board.CaptureInitialState();var win=board.GetComponent<FiveIslandWindow>();if(win){var owner=t.GetComponentInParent<IslandSurfaceAnchor>();int i=Array.FindIndex(win.rooms,r=>owner&&r.center==owner.center);if(i>=0)win.Show(i);}status="主角站位已设置。";}
    void SetFloor(int layer,bool blocked){
        if(PortalAt(selected)){status="请先清除黑洞，再修改其地块高度或障碍。";return;}
        var t=board.TileAt(selected);if(!t||selected==board.PlayerCell||board.MagnetAt(selected)){status="请选择没有主角或磁铁的已有地面格。";return;}
        t.surfaceHeight=layer*board.LayerHeight;t.blocked=blocked;if(blocked){t.rockStyle=rockStyle;t.rockTheme=rockTheme;t.lowRockVariant=lowRockVariant;}
        var previous=t.transform.Find("调试高台");if(previous){previous.name="待清理高台";previous.gameObject.SetActive(false);Destroy(previous.gameObject);}
        float height=blocked?t.surfaceHeight+.8f:t.surfaceHeight;if(height>.01f){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="调试高台";g.transform.SetParent(t.transform,false);g.transform.localPosition=Vector3.up*height*.5f;g.transform.localScale=new Vector3(board.cellSize*.96f,height,board.cellSize*.96f);Destroy(g.GetComponent<Collider>());var mat=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));mat.color=blocked?new Color(.3f,.5f,.6f):new Color(.72f,.85f,.92f);g.GetComponent<Renderer>().sharedMaterial=mat;var binding=g.AddComponent<IslandSurfaceAnchor>();var owner=t.GetComponentInParent<IslandSurfaceAnchor>();if(owner){binding.center=owner.center;binding.Apply();}}
        board.CaptureInitialState();status=blocked?"已加入测试障碍。":"已设置 "+layer+" 层高台。";
    }
    BlackHolePortal PortalAt(Vector2Int cell){var tile=board.TileAt(cell);return tile?tile.GetComponentInChildren<BlackHolePortal>():null;}
}
}

