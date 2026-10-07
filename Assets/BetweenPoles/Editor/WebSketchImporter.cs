using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BetweenPoles.Authoring {
public static class WebSketchImporter {
    const float Cell=1.5f;
    public class RoomData { public string id,name;public List<Vector2Int> cells=new List<Vector2Int>();public Vector2Int offset;public int[] neighbors=new int[0]; }
    public class PieceData { public int room;public string kind,pole;public Vector2Int cell;public int angle,vertical,w=1,h=1;public bool upright; }
    public class Layout { public List<RoomData> rooms=new List<RoomData>();public List<PieceData> pieces=new List<PieceData>();public List<string> warnings=new List<string>();public Vector2Int spawn;public int start; }
    static int Int(JToken t,string name){if(t==null||!double.TryParse(t.ToString(),out var n)||double.IsNaN(n)||double.IsInfinity(n)||n!=Math.Round(n)||Math.Abs(n)>100000)throw new Exception(name+" 必须是有效整数");return (int)n;}
    static string Kind(JObject it){var explicitKind=(string)it["kind"];if(!string.IsNullOrEmpty(explicitKind))return explicitKind;var m=(string)it["magnet"]?["type"];if(m!=null)return m;var n=(string)it["name"]??"";if(n=="石头"||n=="土地")return "rock";if(n=="出生点"||n=="主角")return "player";if(n=="终点")return "goal";if(n.Contains("收藏")||n.Contains("纪念")||n.Contains("宝物")||n.Contains("遗物"))return "collectible";if(n.Contains("两格桥"))return "bridge";if(n.Contains("加宽"))return "wide";if(n.Contains("U型")||n.Contains("U 型"))return "u";if(n.Contains("长条"))return "bar";return "unknown";}
    public static Layout Parse(string json){
        var root=JObject.Parse(json);var input=root["rooms"] as JArray;var items=root["items"] as JArray;var placements=root["placements"] as JObject;
        if(input==null||items==null||placements==null||input.Count==0||input.Count>200)throw new Exception("JSON 需要 rooms、items、placements，房间数量须为 1–200。");
        if(root["schemaVersion"]!=null&&Int(root["schemaVersion"],"格式版本")!=1)throw new Exception("不支持此 JSON 格式版本。");
        if(root["cellPixels"]!=null&&Int(root["cellPixels"],"网页网格大小")!=40)throw new Exception("当前导入器需要40像素一格的网页布局。");
        var data=new Layout();var ids=new HashSet<string>();var occupied=new Dictionary<Vector2Int,int>();
        foreach(JObject r in input){
            string id=(string)r["id"];if(string.IsNullOrEmpty(id)||!ids.Add(id))throw new Exception("房间 ID 缺失或重复");
            int x=Int(r["x"],"房间X"),y=Int(r["y"],"房间Y"),w=Int(r["w"],"房间宽"),h=Int(r["h"],"房间高");
            if(x%40!=0||y%40!=0||w<1||h<1||w>100||h>100)throw new Exception("房间必须对齐网页40像素网格，尺寸为1–100格。");
            var room=new RoomData{id=id,name=(string)r["name"]??id,offset=new Vector2Int(x/40,-y/40)};
            var cells=r["cells"] as JObject;
            if(cells!=null){foreach(var p in cells.Properties()){if(p.Value.Type!=JTokenType.Boolean||!(bool)p.Value)continue;var xy=p.Name.Split(',');if(xy.Length!=2||!int.TryParse(xy[0],out int cx)||!int.TryParse(xy[1],out int cy)||cx<0||cy<0||cx>=w||cy>=h)throw new Exception("地块坐标超出房间范围："+room.name);room.cells.Add(room.offset+new Vector2Int(cx,-cy));}}
            else for(int cy=0;cy<h;cy++)for(int cx=0;cx<w;cx++)room.cells.Add(room.offset+new Vector2Int(cx,-cy));
            if(room.cells.Count==0)throw new Exception("空房间无法生成小岛："+room.name);
            foreach(var c in room.cells){if(occupied.ContainsKey(c))throw new Exception("房间地块重叠："+room.name+" "+c);occupied.Add(c,data.rooms.Count);}
            if(occupied.Count>20000)throw new Exception("本次导入最多20000个地块，请拆分关卡。");
            data.rooms.Add(room);
        }
        var itemMap=new Dictionary<string,JObject>();foreach(JObject it in items){string id=(string)it["id"];if(string.IsNullOrEmpty(id)||itemMap.ContainsKey(id))throw new Exception("物品ID缺失或重复");itemMap.Add(id,it);}
        foreach(var p in placements.Properties())if(!ids.Contains(p.Name))throw new Exception("物品引用了不存在的房间："+p.Name);
        var used=new HashSet<Vector2Int>();int players=0,goals=0;
        for(int ri=0;ri<data.rooms.Count;ri++){
            var room=data.rooms[ri];var list=placements[room.id] as JArray;if(list==null)continue;
            foreach(JObject p in list){if(!itemMap.TryGetValue((string)p["itemId"]??"",out var it))throw new Exception("摆放引用了不存在的物品");string kind=Kind(it);
                if(!new[]{"bar","u","wide","bridge","rock","player","goal","collectible","collection","artifact","display"}.Contains(kind))throw new Exception("暂不支持物品“"+(string)it["name"]+"”的游戏规则。请先移除其摆放；当前支持石头、长条、U型、加宽条、两格桥、出生点、终点和收藏品。");
                if(kind=="collection"||kind=="artifact"||kind=="display")kind="collectible";
                string direction=(string)it["magnet"]?["direction"]??"0";
                int angle=(int.TryParse(direction,out int a)?a:0)+(p["rotation"]==null?0:Int(p["rotation"],"物品旋转"));if(angle%90!=0)throw new Exception("物品方向必须是90度的倍数");angle=(angle%360+360)%360;
                var piece=new PieceData{room=ri,kind=kind,upright=direction=="upright",pole=(string)it["magnet"]?["pole"]??(((string)it["name"]??"").Contains("S")?"S":"N"),cell=room.offset+new Vector2Int(Int(p["cx"],"物品X"),-Int(p["cy"],"物品Y")),angle=angle};
                piece.vertical=(direction=="upright"?90:0)+(p["vertical"]==null?0:Int(p["vertical"],"物品竖直旋转"));
                if(piece.vertical%90!=0)throw new Exception("竖直旋转必须是90度的倍数");
                piece.vertical=(piece.vertical%360+360)%360;piece.upright=piece.vertical%180!=0;
                if(kind=="bridge"){piece.w=angle%180==0?2:1;piece.h=angle%180==0?1:2;}
                for(int dy=0;dy<piece.h;dy++)for(int dx=0;dx<piece.w;dx++){var c=piece.cell+new Vector2Int(dx,-dy);if(!used.Add(c))throw new Exception("物品重叠："+c);}
                if((kind=="rock"||kind=="player"||kind=="goal"||kind=="collectible")&&!occupied.ContainsKey(piece.cell))throw new Exception("石头、出生点、终点和收藏品必须放在地块上。");
                if(kind=="player"){players++;data.spawn=piece.cell;data.start=occupied[piece.cell];}if(kind=="goal")goals++;
                data.pieces.Add(piece);
            }
        }
        if(players>1||goals>1)throw new Exception("只允许一个出生点和一个终点");
        if(players==0){int ri=data.rooms.FindIndex(r=>r.id==(string)root["originId"]);data.start=Mathf.Max(0,ri);var available=data.rooms[data.start].cells.Where(c=>!used.Contains(c)).ToList();if(available.Count==0)throw new Exception("原点房间没有空地，请放置出生点");data.spawn=available[0];data.warnings.Add("未设置出生点：使用原点房间第一块空地。");}
        if(goals==0)data.warnings.Add("未设置终点：可游玩测试，但没有通关目标。");

        BuildNeighbors(data,occupied);
        return data;
    }
    // A shared missing cell connects offset shorelines too; proximity alone is not a link.
    static void BuildNeighbors(Layout data,Dictionary<Vector2Int,int> occupied){
        var links=Enumerable.Range(0,data.rooms.Count).Select(_=>new HashSet<int>()).ToArray();
        var directions=new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down};
        Action<int,int> connect=(a,b)=>{if(a!=b){links[a].Add(b);links[b].Add(a);}};
        var gaps=new Dictionary<Vector2Int,HashSet<int>>();
        foreach(var tile in occupied)foreach(var d in directions){
            var next=tile.Key+d;
            if(occupied.TryGetValue(next,out var owner)){connect(tile.Value,owner);continue;}
            if(!gaps.TryGetValue(next,out var shores))gaps[next]=shores=new HashSet<int>();
            foreach(int other in shores)connect(tile.Value,other);
            shores.Add(tile.Value);
        }
        var spans=new Dictionary<Vector2Int,int>();
        foreach(var p in data.pieces){
            if(p.upright||!(p.kind=="bar"||p.kind=="wide"||p.kind=="bridge"))continue;
            int axis=p.angle%180==0?1:2;
            for(int y=0;y<p.h;y++)for(int x=0;x<p.w;x++)spans[p.cell+new Vector2Int(x,-y)]=axis;
        }
        foreach(var tile in occupied)foreach(var d in directions){
            var pos=tile.Key+d;int axis=d.x!=0?1:2,steps=0;
            while(!occupied.ContainsKey(pos)&&spans.TryGetValue(pos,out int spanAxis)&&spanAxis==axis){pos+=d;steps++;}
            if(steps>0&&occupied.TryGetValue(pos,out var owner))connect(tile.Value,owner);
        }
        for(int i=0;i<data.rooms.Count;i++)data.rooms[i].neighbors=links[i].OrderBy(n=>n).ToArray();
    }
    [MenuItem("两极之间/网页关卡/导入 JSON 到当前世界")]
    public static void ImportMenu(){
        if(EditorApplication.isPlaying){EditorUtility.DisplayDialog("先停止运行","请先退出播放模式再导入。","知道了");return;}
        string path=EditorUtility.OpenFilePanel("选择网页导出的关卡 JSON","","json");if(string.IsNullOrEmpty(path))return;
        try{var data=Parse(File.ReadAllText(path));ValidateTarget(SceneManager.GetActiveScene());
            if(!EditorUtility.DisplayDialog("更新当前世界的小岛",SceneManager.GetActiveScene().name+"\n将用 JSON 替换当前小岛布局。旧布局会停用保留，支持撤销；星球、背景和镜头设置保留。\n"+string.Join("\n",data.warnings),"导入到此世界","取消"))return;
            string result=Build(data,path);EditorUtility.DisplayDialog("导入完成","已更新当前世界："+result+"\n请保存场景。显示当前岛与直接相连的边缘岛，不显示边缘岛的下一圈。","知道了");
        }catch(Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("未能导入",e.Message,"知道了");}
    }
    static Vector3 Pos(Vector2Int p){return new Vector3(p.x*Cell,0,p.y*Cell);}
    static T Find<T>(Scene scene) where T:Component {return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).FirstOrDefault(c=>c.gameObject.activeInHierarchy);}
    static GameObject Child(string name,Transform parent){var o=new GameObject(name);o.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(o,"创建关卡对象");return o;}
    static Material Material(string folder,string name,Color color,bool grid=false){var m=new Material(Shader.Find("BetweenPoles/ImportedIslandSurface"));m.color=color;m.SetFloat("_Grid",grid?1:0);m.SetFloat("_IceArt",0);m.SetFloat("_Snow",grid?1:0);AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;}
    static void Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(o,"创建物品外观");o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=position;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=material;Undo.DestroyObjectImmediate(o.GetComponent<Collider>());}
    public static Mesh CollectibleMesh(){
        var vertices=new List<Vector3>();var triangles=new List<int>();
        Action<Vector3,Vector3,Vector3> face=(a,b,c)=>{int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);};
        var edge=new Vector3[10];for(int i=0;i<10;i++){float a=(90+i*36)*Mathf.Deg2Rad;float r=i%2==0?.48f:.23f;edge[i]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0);}
        for(int i=0;i<10;i++){var a=edge[i];var b=edge[(i+1)%10];var back=Vector3.forward*.12f;
            face(new Vector3(0,0,-.14f),b,a);face(new Vector3(0,0,.18f),a+back,b+back);
            face(a,b,b+back);face(a,b+back,a+back);
        }
        var mesh=new Mesh{name="Faceted collectible star"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static Mesh IslandMesh(IEnumerable<Vector2Int> points){return IceIslandArt.Build(points,Cell);}
    [MenuItem("两极之间/网页关卡/更新当前关卡的小岛美术")]
    public static void UpdateIslandArt(){
        if(EditorApplication.isPlaying){EditorUtility.DisplayDialog("请先停止运行","退出 Play 后再更新小岛美术。","知道了");return;}
        var scene=SceneManager.GetActiveScene();var window=Find<FiveIslandWindow>(scene);
        if(!window||string.IsNullOrEmpty(scene.path)){EditorUtility.DisplayDialog("未找到导入关卡","请先打开网页 JSON 生成的关卡场景。","知道了");return;}
        string folder=null;
        foreach(var room in window.rooms){
            var surface=room.surfaces.FirstOrDefault(r=>r&&r.name=="冰面及外侧壁");
            var filter=surface?surface.GetComponent<MeshFilter>():null;
            string meshPath=filter?AssetDatabase.GetAssetPath(filter.sharedMesh):null;
            if(!string.IsNullOrEmpty(meshPath)){folder=Path.GetDirectoryName(meshPath).Replace('\\','/');break;}
        }
        if(string.IsNullOrEmpty(folder)){
            const string assetsRoot="Assets/BetweenPoles/ImportedLevels";
            if(!AssetDatabase.IsValidFolder(assetsRoot))AssetDatabase.CreateFolder("Assets/BetweenPoles","ImportedLevels");
            folder=AssetDatabase.GenerateUniqueAssetPath(assetsRoot+"/"+Path.GetFileNameWithoutExtension(scene.path));
            AssetDatabase.CreateFolder(assetsRoot,Path.GetFileName(folder));
        }
        Material material=null;
        foreach(var room in window.rooms){
            var surface=room.surfaces.FirstOrDefault(r=>r&&r.name=="冰面及外侧壁");if(!surface)continue;
            var filter=surface.GetComponent<MeshFilter>();if(!filter)continue;
            if(!material){material=new Material(surface.sharedMaterial);material.color=new Color(.76f,.88f,.94f);material.SetFloat("_IceArt",0);material.SetFloat("_Snow",1);AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(folder+"/PixelIceArt.mat"));}
            var cells=room.center.parent.GetComponentsInChildren<GridTile>(true).Select(t=>new Vector2Int(Mathf.RoundToInt(t.transform.position.x/Cell),Mathf.RoundToInt(t.transform.position.z/Cell)));
            var mesh=IceIslandArt.Build(cells,Cell);AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/PixelIceIsland.asset"));
            Undo.RecordObject(filter,"更新小岛美术");Undo.RecordObject(surface,"更新小岛美术");filter.sharedMesh=mesh;surface.sharedMaterial=material;
        }
        ImportedLevelArt.Apply(scene,folder);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();
        Debug.Log("小岛美术已更新：像素冰面、冰层高光和原版石头。请保存场景。");
    }
    public static void ValidateTarget(Scene scene){
        if(EditorApplication.isPlaying)throw new Exception("请退出播放模式");
        if(!scene.IsValid()||!scene.isLoaded||string.IsNullOrEmpty(scene.path))throw new Exception("请先打开并保存要修改的世界场景。");
        if(scene.name.Contains("测试"))throw new Exception("当前是磁铁测试场景，请打开要导入的星球世界，测试画布不会被替换。");
        var board=Find<GridPlayground>(scene);var camera=Find<IslandCamera>(scene);var curved=Find<CurvedTrialCamera>(scene);
        if(!board||!board.player||!camera||!curved||!curved.planet)throw new Exception("当前场景不是可导入的小岛世界：缺少棋盘、人物或星球镜头。");
    }
    static Material CopySurface(Scene scene,string folder){
        var material=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>())
            .Where(r=>r.gameObject.activeInHierarchy&&r.enabled).SelectMany(r=>r.sharedMaterials)
            .Where(m=>m&&m.HasProperty("_Grid")&&m.GetFloat("_Grid")>.5f)
            .OrderByDescending(m=>m.shader.name=="BetweenPoles/ChapterIslandSurface"?3:m.shader.name=="BetweenPoles/PaintedIceTrial"?2:m.shader.name=="BetweenPoles/ImportedIslandSurface"?1:0)
            .FirstOrDefault();
        if(!material)throw new Exception("当前世界没有可复用的小岛地面材质，已停止导入。");
        var copy=new Material(material);AssetDatabase.CreateAsset(copy,folder+"/WorldSurface.mat");return copy;
    }
    static void MatchSurface(Material material,Material surface){
        var color=material.color;material.shader=surface.shader;material.CopyPropertiesFromMaterial(surface);material.color=color;
        if(material.HasProperty("_Grid"))material.SetFloat("_Grid",0);
        if(material.HasProperty("_Painted"))material.SetFloat("_Painted",0);
        if(material.HasProperty("_Snow"))material.SetFloat("_Snow",0);
    }
    public static string Build(Layout data,string source){
        var scene=SceneManager.GetActiveScene();ValidateTarget(scene);
        if(data==null||data.rooms.Count==0||!File.Exists(source))throw new Exception("布局或 JSON 来源无效。");
        string scenePath=scene.path;
        string baseFolder="Assets/BetweenPoles/ImportedLevels";if(!AssetDatabase.IsValidFolder(baseFolder))AssetDatabase.CreateFolder("Assets/BetweenPoles","ImportedLevels");
        string folder=AssetDatabase.GenerateUniqueAssetPath(baseFolder+"/"+scene.name+"-"+Path.GetFileNameWithoutExtension(source));AssetDatabase.CreateFolder(baseFolder,Path.GetFileName(folder));
        var ice=CopySurface(scene,folder);
        var board=Find<GridPlayground>(scene);var camera=Find<IslandCamera>(scene);var curved=Find<CurvedTrialCamera>(scene);
        var existingWindow=Find<FiveIslandWindow>(scene);
        var oldObjects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<IslandSurfaceAnchor>())
            .Where(a=>a.gameObject.activeInHierarchy&&!a.transform.IsChildOf(board.player)&&!board.transform.IsChildOf(a.transform)&&!camera.transform.IsChildOf(a.transform)&&!curved.planet.IsChildOf(a.transform))
            .Select(a=>a.gameObject).Concat(board.tiles.Where(t=>t).Select(t=>t.gameObject)).Concat(board.magnets.Where(m=>m).Select(m=>m.gameObject)).Distinct().ToArray();
        // Legacy rocks and the goal live together under SET DRESSING rather than under GridTile.
        // Archive complete gameplay roots only when they contain none of the preserved systems.
        var roots=oldObjects.Select(o=>o.transform.root.gameObject).Concat(board.goalLight?new[]{board.goalLight.transform.root.gameObject}:new GameObject[0])
            .Where(o=>!board.transform.IsChildOf(o.transform)&&!camera.transform.IsChildOf(o.transform)&&!curved.planet.IsChildOf(o.transform)&&!board.player.IsChildOf(o.transform));
        oldObjects=oldObjects.Concat(roots).Distinct().ToArray();
        var helpers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<IslandGroundCenters>()).ToArray();
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("导入 JSON 到当前世界");
        try{
        Undo.RecordObjects(new UnityEngine.Object[]{board,camera,curved,camera.transform,board.player},"更新世界布局");
        if(existingWindow)Undo.RecordObject(existingWindow,"更新相邻岛屿");
        foreach(var helper in helpers)Undo.RecordObject(helper,"更新镜头岛屿");
        // Preserve the live player and environment. Old authored objects remain recoverable via Undo.
        Undo.SetTransformParent(board.player,null,"保留人物");
        foreach(var helper in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PrejoinedTestBridges>()).ToArray()){Undo.RecordObject(helper,"停用旧桥显示");helper.enabled=false;}
        foreach(var old in oldObjects){Undo.RecordObject(old,"保留旧布局（停用）");old.SetActive(false);}
        var playerCopy=board.player.gameObject;
        board.tiles=new GridTile[0];board.magnets=new MagnetPiece[0];board.goalLight=null;board.cellSize=Cell;
        var root=Child("网页关卡 · 生成内容",null);SceneManager.MoveGameObjectToScene(root,scene);var rooms=new List<FiveIslandWindow.Room>();var tiles=new Dictionary<Vector2Int,GridTile>();var magnets=new List<MagnetPiece>();
        var rock=Material(folder,"Stone",ice.color*.65f);var red=Material(folder,"N",new Color(.92f,.23f,.32f));var blue=Material(folder,"S",new Color(.18f,.48f,.73f));var gold=Material(folder,"Goal",new Color(.95f,.84f,.43f));
        foreach(var material in new[]{rock,red,blue,gold})MatchSurface(material,ice);
        // Rebase the map to the initial island; logical relative positions remain exact.
        var startCells=data.rooms[data.start].cells;var shift=new Vector2Int(Mathf.RoundToInt((startCells.Min(c=>c.x)+startCells.Max(c=>c.x))*.5f),Mathf.RoundToInt((startCells.Min(c=>c.y)+startCells.Max(c=>c.y))*.5f));
        foreach(var r in data.rooms){for(int i=0;i<r.cells.Count;i++)r.cells[i]-=shift;}foreach(var p in data.pieces)p.cell-=shift;data.spawn-=shift;
        foreach(var r in data.rooms){
            var island=Child(r.name,root.transform);var center=Child(r.id,island.transform).transform;center.position=Pos(new Vector2Int(0,0))+new Vector3((r.cells.Min(c=>c.x)+r.cells.Max(c=>c.x))*Cell*.5f,0,(r.cells.Min(c=>c.y)+r.cells.Max(c=>c.y))*Cell*.5f);
            var anchor=Undo.AddComponent<IslandSurfaceAnchor>(island);anchor.center=center;anchor.flatten=0;
            var surface=Child("冰面及外侧壁",island.transform);var mesh=IslandMesh(r.cells);AssetDatabase.CreateAsset(mesh,folder+"/Island-"+rooms.Count+".asset");Undo.AddComponent<MeshFilter>(surface).sharedMesh=mesh;Undo.AddComponent<MeshRenderer>(surface).sharedMaterial=ice;
            foreach(var c in r.cells){var tile=Undo.AddComponent<GridTile>(Child("Cell "+c,island.transform));tile.transform.position=Pos(c);tiles.Add(c,tile);}
            anchor.enabled=false;anchor.enabled=true;
            rooms.Add(new FiveIslandWindow.Room{id=r.id,center=center,surfaces=new[]{surface.GetComponent<Renderer>()},neighbors=r.neighbors});
        }
        foreach(var p in data.pieces){
            if(p.kind=="player")continue;var island=rooms[p.room].center.parent;
            if(p.kind=="rock"||p.kind=="goal"){
                var o=Child(p.kind,island);o.transform.position=Pos(p.cell);Box(p.kind,o.transform,new Vector3(0,p.kind=="rock"?.4f:.12f,0),p.kind=="rock"?new Vector3(1,.8f,1):new Vector3(.7f,.24f,.7f),p.kind=="rock"?rock:gold);
                if(p.kind=="rock")tiles[p.cell].blocked=true;else{tiles[p.cell].goal=true;board.goalLight=o.GetComponentInChildren<Renderer>();board.goalCompleteMaterial=blue;}
                var binding=Undo.AddComponent<IslandSurfaceAnchor>(o);binding.center=rooms[p.room].center;
                rooms[p.room].surfaces=rooms[p.room].surfaces.Concat(o.GetComponentsInChildren<Renderer>()).ToArray();continue;
            }
            if(p.kind=="collectible"){
                var o=Child("收藏品 · "+p.cell,island);o.transform.position=Pos(p.cell);
                var starMaterial=Material(folder,"Collectible-"+p.cell.x+"-"+p.cell.y,new Color(.96f,.78f,.30f));
                MatchSurface(starMaterial,ice);
                var mesh=CollectibleMesh();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/CollectibleStar.asset"));
                var model=Child("金色星星",o.transform);model.transform.localPosition=new Vector3(0,.55f,0);
                model.transform.localRotation=Quaternion.Euler(55,p.angle,0);
                Undo.AddComponent<MeshFilter>(model).sharedMesh=mesh;Undo.AddComponent<MeshRenderer>(model).sharedMaterial=starMaterial;                var binding=Undo.AddComponent<IslandSurfaceAnchor>(o);binding.center=rooms[p.room].center;
                rooms[p.room].surfaces=rooms[p.room].surfaces.Concat(o.GetComponentsInChildren<Renderer>()).ToArray();continue;
            }
            if(p.kind=="bar"||p.kind=="u"){
                string prefab="Assets/BetweenPoles/Prefabs/"+(p.kind=="bar"?"Bar ":"U ")+(p.pole=="S"?"S":"N")+".prefab";
                var o=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab),scene);Undo.RegisterCreatedObjectUndo(o,"创建磁铁");o.transform.SetParent(root.transform);o.transform.position=Pos(p.cell);o.transform.rotation=Quaternion.Euler(0,p.angle,0);
                foreach(var renderer in o.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=p.pole=="S"?blue:red;
                var piece=o.GetComponent<MagnetPiece>();if(p.vertical!=0){piece.geometry.localRotation=Quaternion.Euler(0,0,p.vertical);MagnetVisuals.Ground(piece);}
                var binding=Undo.AddComponent<IslandSurfaceAnchor>(o);binding.center=rooms[p.room].center;magnets.Add(piece);
            }else{
                // Use the same product geometry and occupancy as the test scene and live assembly.
                var yaw=Quaternion.Euler(0,p.angle,0);var axis=yaw*Vector3.right;
                var dir=new Vector2Int(Mathf.RoundToInt(axis.x),Mathf.RoundToInt(axis.z));
                var home=p.cell;if(p.kind=="bridge"){if(dir.x<0)home.x++;if(dir.y>0)home.y--;}
                var o=Child(p.kind+" · 已拼接",root.transform);o.transform.position=Pos(home);
                var m=Undo.AddComponent<MagnetPiece>(o);m.shape=MagnetShape.Bar;m.north=p.pole!="S";m.bridgeDirection=dir;
                m.geometry=Child("初始姿态",o.transform).transform;
                var binding=Undo.AddComponent<IslandSurfaceAnchor>(o);binding.center=rooms[p.room].center;
                MagnetVisuals.Product(m,p.kind=="bridge"?MagnetProduct.Bridge:MagnetProduct.WideBar,Cell,yaw,m.north);
                Undo.RegisterCreatedObjectUndo(m.geometry.gameObject,"创建共用磁铁模型");
                // Runtime factories share transient materials; persist copies for a saved scene.
                var savedMaterials=new Dictionary<Material,Material>();
                foreach(var renderer in m.geometry.GetComponentsInChildren<Renderer>()){
                    var sourceMaterial=renderer.sharedMaterial;if(!sourceMaterial)continue;
                    if(!savedMaterials.TryGetValue(sourceMaterial,out var saved)){
                        saved=new Material(sourceMaterial);
                        AssetDatabase.CreateAsset(saved,AssetDatabase.GenerateUniqueAssetPath(folder+"/MagnetProduct.mat"));savedMaterials[sourceMaterial]=saved;
                    }
                    renderer.sharedMaterial=saved;
                }
                magnets.Add(m);

            }
        }
        board.tiles=tiles.Values.ToArray();board.magnets=magnets.ToArray();board.player.position=Pos(data.spawn);camera.player=board.player;camera.board=board;camera.islandCenters=rooms.Select(r=>r.center).ToArray();
        var playerBinding=playerCopy.GetComponent<IslandSurfaceAnchor>();if(playerBinding)Undo.RecordObject(playerBinding,"更新人物所属岛屿");else playerBinding=Undo.AddComponent<IslandSurfaceAnchor>(playerCopy);playerBinding.center=rooms[data.start].center;playerBinding.ground=board;
        foreach(var renderer in playerCopy.GetComponentsInChildren<Renderer>()){var mat=new Material(renderer.sharedMaterial);MatchSurface(mat,ice);Undo.RecordObject(renderer,"更新人物投影材质");AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(folder+"/Player.mat"));renderer.sharedMaterial=mat;}
        var window=existingWindow?existingWindow:Undo.AddComponent<FiveIslandWindow>(board.gameObject);window.enabled=false;window.board=board;window.view=camera;window.rooms=rooms.ToArray();window.initialRoom=data.start;window.enabled=true;window.RefreshLayout();window.Show(data.start);
        var bridgeView=Undo.AddComponent<PrejoinedTestBridges>(root);bridgeView.board=board;var links=new List<PrejoinedTestBridges.Link>();
        foreach(var m in magnets.Where(m=>m.walkable)){
            var p=new Vector2Int(Mathf.RoundToInt(m.transform.position.x/Cell),Mathf.RoundToInt(m.transform.position.z/Cell));var right=m.transform.right;var dir=new Vector2Int(Mathf.RoundToInt(right.x),Mathf.RoundToInt(right.z));
            GridTile a=null,b=null;
            for(int step=1;step<=3;step++){var c=p-dir*step;if(tiles.TryGetValue(c,out a))break;if(!magnets.Any(other=>other.walkable&&(other.transform.position-Pos(c)).sqrMagnitude<.01f))break;}
            for(int step=1;step<=3;step++){var c=p+dir*step;if(tiles.TryGetValue(c,out b))break;if(!magnets.Any(other=>other.walkable&&(other.transform.position-Pos(c)).sqrMagnitude<.01f))break;}
            if(a&&b)links.Add(new PrejoinedTestBridges.Link{shoreA=a,shoreB=b,magnet=m,renderers=m.GetComponentsInChildren<Renderer>()});
        }
        bridgeView.links=links.ToArray();window.bridges=bridgeView;window.Show(data.start);bridgeView.Refresh();
        curved.editFocus=rooms[data.start].center.position;camera.transform.position=curved.editFocus+camera.viewingOffset;camera.transform.LookAt(curved.editFocus);curved.Apply(curved.editFocus);
        foreach(var helper in helpers){helper.view=curved;helper.editIsland=rooms[data.start].center;}
        File.Copy(source,folder+"/SourceLayout.json");File.WriteAllText(folder+"/ImportReport.txt","房间："+rooms.Count+"，地块："+tiles.Count+"\n"+string.Join("\n",data.warnings));
        // Keep the world's own materials, lighting, pixel finish and planet; never apply the ice template here.
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(undo);SceneView.RepaintAll();return scenePath;
        }catch{Undo.RevertAllDownToGroup(undo);throw;}

    }
}
}

