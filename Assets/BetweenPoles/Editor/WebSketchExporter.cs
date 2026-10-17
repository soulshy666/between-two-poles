using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace BetweenPoles.Authoring {
public static class WebSketchExporter {
    [MenuItem("两极之间/网页关卡/导出当前世界为 JSON（网页可打开）")]
    public static void ExportMenu(){
        try{
            var scene=SceneManager.GetActiveScene();var json=Export(scene);
            var path=EditorUtility.SaveFilePanel("导出网页关卡 JSON","",scene.name+"-关卡草图","json");
            if(string.IsNullOrEmpty(path))return;
            File.WriteAllText(path,json,new System.Text.UTF8Encoding(false));
            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("导出完成","在网页关卡编辑器中点击导入 JSON，选择刚保存的文件即可。\n导出的是关卡规则和布局，不包含 Unity 美术、背景和镜头。","知道了");
        }catch(Exception e){Debug.LogWarning("关卡导出："+e.Message);EditorUtility.DisplayDialog("无法导出",e.Message,"知道了");}
    }
    sealed class Room {
        public FiveIslandWindow.Room source;public string id;public int number;public Vector2Int corner;public List<GridTile> tiles;
    }
    static Vector2Int Cell(Vector3 p,float size){
        var c=new Vector2Int(Mathf.RoundToInt(p.x/size),Mathf.RoundToInt(p.z/size));
        if(Mathf.Abs(p.x-c.x*size)>.02f||Mathf.Abs(p.z-c.y*size)>.02f)throw new Exception("对象未对齐格子："+p+"。请先对齐再导出。");
        return c;
    }
    static Transform Owner(Component c){var a=c.GetComponentInParent<IslandSurfaceAnchor>();return a?a.center:null;}
    public static string Export(Scene scene){
        if(EditorApplication.isPlaying)throw new Exception("请先停止运行，再导出编辑状态的关卡。");
        var boards=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridPlayground>()).ToArray();
        if(boards.Length!=1)throw new Exception("当前场景需要有且仅有一个启用的关卡棋盘。");
        var board=boards[0];var window=board.GetComponent<FiveIslandWindow>();
        if(!board.player)throw new Exception("当前场景缺少出生点。");
        var sources=window&&window.rooms.Length>0?window.rooms:board.tiles.Where(t=>t).Select(Owner).Where(t=>t).Distinct().Select((t,i)=>new FiveIslandWindow.Room{id=t.name,roomNumber=i+1,center=t}).ToArray();
        if(sources.Length==0)throw new Exception("当前场景缺少房间归属信息。");
        var rooms=new List<Room>();var ids=new HashSet<string>();var numbers=new HashSet<int>();
        for(int i=0;i<sources.Length;i++){
            var source=sources[i];if(!source.center)throw new Exception("房间中心丢失："+i);
            var tiles=board.tiles.Where(t=>t&&Owner(t)==source.center).Distinct().ToList();
            if(tiles.Count==0)throw new Exception("房间没有地块："+source.center.name);
            foreach(var tile in tiles)if(Mathf.Abs(tile.surfaceHeight)>.001f)throw new Exception("网页不支持多层地面："+tile.name);
            string id=string.IsNullOrEmpty(source.id)?source.center.name:source.id;int number=source.roomNumber>0?source.roomNumber:i+1;
            if(!ids.Add(id)||!numbers.Add(number))throw new Exception("房间 ID 或编号重复，请先修正。");
            var cells=tiles.Select(t=>Cell(t.transform.position,board.cellSize)).ToArray();
            rooms.Add(new Room{source=source,id=id,number=number,tiles=tiles,corner=new Vector2Int(cells.Min(c=>c.x),cells.Max(c=>c.y))});
        }
        if(board.tiles.Any(t=>!t||!rooms.Any(r=>r.tiles.Contains(t))))throw new Exception("存在没有所属房间的地块。");
        var spawn=Cell(board.player.position,board.cellSize);
        var origin=rooms.FirstOrDefault(r=>r.tiles.Any(t=>Cell(t.transform.position,board.cellSize)==spawn));
        if(origin==null)throw new Exception("出生点不在地块上。");
        if(Mathf.Abs(board.player.position.y)>.02f)throw new Exception("网页出生点必须在地面上。");
        var root=new JObject{["schemaVersion"]=1,["cellPixels"]=40,["roomW"]=5,["roomH"]=5,["originId"]=origin.id};
        var roomJson=new JArray();var placements=new JObject();var items=new JArray();var catalog=new Dictionary<string,string>();
        foreach(var r in rooms.OrderBy(r=>r.number)){
            var cells=new JObject();int w=1,h=1;
            foreach(var tile in r.tiles){var c=Cell(tile.transform.position,board.cellSize)-r.corner;cells[c.x+","+(-c.y)]=true;w=Math.Max(w,c.x+1);h=Math.Max(h,-c.y+1);}
            roomJson.Add(new JObject{["id"]=r.id,["name"]=r.source.center.parent?r.source.center.parent.name:r.source.center.name,["roomNumber"]=r.number,["x"]=(r.corner.x-origin.corner.x)*40,["y"]=-(r.corner.y-origin.corner.y)*40,["w"]=Math.Max(2,w),["h"]=Math.Max(2,h),["cells"]=cells});
            placements[r.id]=new JArray();
        }
        Action<Room,Vector2Int,string,string,int,int> add=(room,cell,kind,pole,yaw,vertical)=>{
            string key=kind+"-"+pole+"-"+yaw+"-"+vertical;string id;
            if(!catalog.TryGetValue(key,out id)){
                id="unity-"+key;catalog.Add(key,id);
                string name=kind=="rock"?"石头":kind=="player"?"出生点":kind=="goal"?"终点":kind=="blackhole"?"黑洞":kind=="collectible"?"收藏品":pole+" "+(kind=="bar"?"长条":kind=="u"?"U型":kind=="wide"?"加宽条":kind=="ring"?"圆环":"两格桥");
                string color=kind=="blackhole"?"#9a60cf":kind=="rock"?"#85939d":kind=="collectible"?"#f4cb4c":pole=="S"?"#499ed4":"#e95261";
                var item=new JObject{["id"]=id,["name"]=name,["kind"]=kind,["color"]=color,["w"]=kind=="bridge"&&yaw%180==0?2:1,["h"]=kind=="bridge"&&yaw%180!=0?2:1};
                bool magnetic=kind=="bar"||kind=="u"||kind=="wide"||kind=="bridge"||kind=="ring";
                if(magnetic||kind=="rock")item["magnet"]=new JObject{["type"]=kind,["pole"]=pole,["direction"]=yaw.ToString()};
                string shape=kind=="ring"?"<g transform='rotate("+yaw+" 20 20)'><path d='M7 20a13 13 0 0 1 26 0' stroke='"+(pole=="S"?"#499ed4":"#e95261")+"' stroke-width='7' fill='none'/><path d='M33 20a13 13 0 0 1-26 0' stroke='"+(pole=="S"?"#e95261":"#499ed4")+"' stroke-width='7' fill='none'/></g>":kind=="blackhole"?"<circle cx='20' cy='20' r='16' fill='#241332' stroke='#b78be8' stroke-width='4'/>":kind=="collectible"?"<path d='M20 3L25 14L38 15L28 24L31 37L20 30L9 37L12 24L2 15L15 14Z' fill='#f4cb4c'/>":"<rect x='8' y='6' width='24' height='28' fill='"+color+"'/>";
                item["img"]="data:image/svg+xml;charset=utf-8,"+Uri.EscapeDataString("<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40' viewBox='0 0 40 40'>"+shape+"</svg>");
                items.Add(item);
            }
            var local=cell-room.corner;
            ((JArray)placements[room.id]).Add(new JObject{["id"]=Guid.NewGuid().ToString("N"),["itemId"]=id,["cx"]=local.x,["cy"]=-local.y,["rotation"]=0,["vertical"]=vertical});
        };
        foreach(var r in rooms)foreach(var tile in r.tiles){var c=Cell(tile.transform.position,board.cellSize);if(tile.blocked)add(r,c,"rock","N",0,0);if(tile.goal)add(r,c,"goal","N",0,0);}
        add(origin,spawn,"player","N",0,0);
        foreach(var m in board.magnets.Distinct()){
            if(!m)throw new Exception("磁铁列表存在空引用。");if(!m.gameObject.activeInHierarchy||!m.enabled)continue;
            var r=rooms.FirstOrDefault(x=>x.source.center==Owner(m));if(r==null)throw new Exception("磁铁没有所属房间："+m.name);
            if(Mathf.Abs(m.transform.position.y)>.02f)throw new Exception("网页不支持悬空或叠层磁铁："+m.name);
            string kind;int yaw=0,vertical=0;var cell=Cell(m.transform.position,board.cellSize);
            if(m.product==MagnetProduct.None){
                if(m.combined)throw new Exception("不支持的组合磁铁："+m.name);
                kind=m.shape==MagnetShape.Bar?"bar":"u";bool found=false;
                for(int y=0;y<360&&!found;y+=90)for(int v=0;v<360;v+=90){
                    var q=Quaternion.Euler(0,(y+(kind=="u"?180:0))%360,0)*Quaternion.Euler(0,0,v);
                    if(Quaternion.Angle(q,m.Pose)<.5f){yaw=y;vertical=v;found=true;break;}
                }
                if(!found)throw new Exception("网页无法表达此磁铁姿态："+m.name);
            }else if(m.product==MagnetProduct.Ring){
                kind="ring";bool found=false;
                // Ring import uses yaw * local Z rotation, without the U prefab's 180-degree correction.
                for(int y=0;y<360&&!found;y+=90)for(int v=0;v<360;v+=90){
                    var pose=Quaternion.Euler(0,y,0)*Quaternion.Euler(0,0,v);
                    if(Quaternion.Angle(pose,m.Pose)<.5f){yaw=y;vertical=v;found=true;break;}
                }
                if(!found)throw new Exception("网页无法表达此圆环姿态："+m.name+"，请对齐到90度旋转后导出。");
            }else if(m.product==MagnetProduct.WideBar||m.product==MagnetProduct.Bridge){
                kind=m.product==MagnetProduct.Bridge?"bridge":"wide";
                Vector3 axis=kind=="bridge"?new Vector3(m.bridgeDirection.x,0,m.bridgeDirection.y):m.Pose*Vector3.right;
                yaw=((Mathf.RoundToInt(Mathf.Atan2(-axis.z,axis.x)*Mathf.Rad2Deg/90)*90)%360+360)%360;
                if(kind=="bridge"){if(m.bridgeDirection.x<0)cell.x--;if(m.bridgeDirection.y>0)cell.y++;}
            }else throw new Exception("网页导入协议暂不支持此组合类型："+m.product+"（"+m.name+"）。");
            add(r,cell,kind,m.north?"N":"S",yaw,vertical);
        }
        foreach(var portal in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BlackHolePortal>()).Where(p=>p.board==board)){
            if(!portal.tile)throw new Exception("黑洞没有绑定地块。");var r=rooms.First(x=>x.tiles.Contains(portal.tile));add(r,Cell(portal.tile.transform.position,board.cellSize),"blackhole","N",0,0);
        }
        foreach(var mesh in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()).Where(m=>m.sharedMesh&&(m.name=="金色星星"||m.sharedMesh.name=="Faceted collectible star"))){
            var c=Cell(mesh.transform.parent.position,board.cellSize);var r=rooms.FirstOrDefault(x=>x.tiles.Any(t=>Cell(t.transform.position,board.cellSize)==c));
            if(r==null)throw new Exception("收藏品没有所属地块："+mesh.name);add(r,c,"collectible","N",0,0);
        }
        root["rooms"]=roomJson;root["items"]=items;root["placements"]=placements;
        string json=root.ToString();WebSketchImporter.Parse(json);return json;
    }
}
}
