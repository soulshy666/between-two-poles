using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetweenPoles.EditorTools {
    [InitializeOnLoad]
    static class MagnetSceneRegression {
        const string Report="output/magnet-scene-consistency-test.txt";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static MagnetSceneRegression(){
            EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("MagnetSceneRegression.OwnPlay",false))EditorApplication.delayCall+=Run;
            };
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        [MenuItem("两极之间/验证圆环逐格半圈滚动")]
        static void ValidateRingHalfTurns(){
            var scene=EditorSceneManager.NewPreviewScene();
            try{
                foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
                foreach(int cells in new[]{1,2,5}){
                    var root=new GameObject("Ring half-turn regression");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
                    var board=root.AddComponent<GridPlayground>();board.player=new GameObject("Player").transform;board.player.SetParent(root.transform);
                    typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(board,true);
                    var forward=new Vector3(dir.x,0,dir.y);var axis=new Vector3(dir.y,0,-dir.x);
                    board.player.position=-forward*board.cellSize;
                    board.tiles=new GridTile[cells+1];
                    for(int i=0;i<=cells;i++){
                        var tile=new GameObject("Floor");tile.transform.SetParent(root.transform);tile.transform.position=forward*(i*board.cellSize);
                        board.tiles[i]=tile.AddComponent<GridTile>();
                    }
                    var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,board.cellSize,root.transform);
                    var pose=Quaternion.FromToRotation(Vector3.up,axis);
                    MagnetVisuals.Product(ring,MagnetProduct.Ring,board.cellSize,pose,true);MagnetVisuals.Ground(ring);board.magnets=new[]{ring};
                    var motion=(IEnumerator)typeof(GridPlayground).GetMethod("MoveRing",Flags).Invoke(board,new object[]{ring,dir,Vector2Int.zero,cells,false});
                    int frames=0;float previousDistance=0,previousSpeed=0;bool accelerated=false;
                    Drain(motion,frame=>{
                        frames=frame;
                        float distance=Vector3.Dot(ring.transform.position,forward)/board.cellSize;
                        float speed=(distance-previousDistance)*60;
                        if(frame>2&&speed>previousSpeed+.01f)accelerated=true;
                        Check(speed<=1.5f/board.walkSeconds*1.08f+.01f,"Ring exceeded cruise speed");
                        previousDistance=distance;previousSpeed=speed;
                        Check(Quaternion.Angle(ring.Pose,Quaternion.AngleAxis(distance*180,axis)*pose)<.05f,"Ring colour orientation detached from travelled cells");
                    });
                    Check(Quaternion.Angle(ring.Pose,Quaternion.AngleAxis(cells*180,axis)*pose)<.05f,"Ring half-turn parity incorrect");
                    Check(Vector3.Distance(ring.transform.position,forward*(cells*board.cellSize))<.001f&&!board.Busy,"Ring missed final cell or remained locked");
                    Check(accelerated,"Ring started at constant speed instead of accelerating");
                    float cruise=cells*board.walkSeconds/(1.5f*1.08f);
                    float expected=cruise+Mathf.Min(.32f,cruise)*.5f;
                    Check(frames/60f>=expected-.02f,"Ring skipped its acceleration time");
                    UnityEngine.Object.DestroyImmediate(root);
                }
                Debug.Log("PASS: continuous half-turns, odd/even final colours, landing and speed in four directions.");
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        [MenuItem("两极之间/验证太空圆环平台")]
        static void ValidateSpaceRing(){
            var scene=EditorSceneManager.NewPreviewScene();
            try{
                foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
                foreach(int posture in new[]{0,1,2}){
                    var root=new GameObject("Space ring regression");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
                    var board=root.AddComponent<GridPlayground>();board.player=new GameObject("Player").transform;board.player.SetParent(root.transform);
                    typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(board,true);
                    var shore=new GameObject("Shore");shore.transform.SetParent(root.transform);var tile=shore.AddComponent<GridTile>();board.tiles=new[]{tile};
                    var axis=new Vector3(dir.x,0,dir.y);
                    board.player.position=-axis*board.cellSize;
                    var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,board.cellSize,root.transform);
                    var pose=posture==0?Quaternion.identity:Quaternion.FromToRotation(Vector3.up,posture==1?axis:Vector3.Cross(axis,Vector3.up));
                    MagnetVisuals.Product(ring,MagnetProduct.Ring,board.cellSize,pose,true);MagnetVisuals.Ground(ring);board.magnets=new[]{ring};
                    Func<string,object[],object> call=(name,args)=>typeof(GridPlayground).GetMethod(name,Flags).Invoke(board,args);
                    Drain((IEnumerator)call("MoveRing",new object[]{ring,dir,Vector2Int.zero,1,posture!=2}),null);
                    Check(MagnetPiece.FlatU(ring.Pose)&&ring.walkable,"Space ring remained upright: "+dir+" / "+posture);
                    Check(Mathf.Abs((float)call("DeckHeight",new object[]{ring}))<.0001f,"Space ring deck not at shore height");
                    Check(Mathf.Abs((float)call("MagnetGeometryTop",new object[]{ring}))<.0001f,"Visible ring top not at shore height");
                    Check(!(bool)call("ShouldHover",new object[]{ring}),"Floor ring still bobs vertically");
                    foreach(var side in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
                        var neighbor=new GameObject("Adjacent shore");neighbor.transform.SetParent(root.transform);
                        neighbor.transform.position=new Vector3(dir.x+side.x,0,dir.y+side.y)*board.cellSize;
                        board.tiles=new[]{neighbor.AddComponent<GridTile>()};board.InvalidateTileLookup();
                        board.player.position=neighbor.transform.position;
                        Drain((IEnumerator)call("MovePlayer",new object[]{dir}),null);
                        Check(Vector3.Distance(board.player.position,axis*board.cellSize)<.0001f,"Ring entry missed floor centre");
                        Drain((IEnumerator)call("MovePlayer",new object[]{dir+side}),null);
                        Check(Vector3.Distance(board.player.position,neighbor.transform.position)<.0001f,"Ring exit changed height");
                        UnityEngine.Object.DestroyImmediate(neighbor);
                    }
                    UnityEngine.Object.DestroyImmediate(root);
                }
                Debug.Log("PASS: space rings flatten from three postures in four directions, remain level and support four-way walking.");
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        [MenuItem("两极之间/验证圆环开口限制")]
        static void ValidateAlignedRingRecipes(){
            var scene=EditorSceneManager.NewPreviewScene();
            try{
                var root=new GameObject("Ring alignment regression");root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root,scene);
                var near=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,1.5f,root.transform);
                var far=MagnetVisuals.Create(root.transform,Vector3.forward*1.5f,MagnetShape.Horseshoe,false,Quaternion.identity,1.5f,root.transform);
                int cases=0;
                foreach(var push in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
                foreach(bool nearUpsideDown in new[]{false,true})foreach(bool farUpsideDown in new[]{false,true})
                for(int a=0;a<4;a++)for(int b=0;b<4;b++){
                    var facing=Quaternion.LookRotation(new Vector3(push.x,0,push.y));
                    near.geometry.rotation=facing*Quaternion.Euler(0,a*90,0)*Quaternion.Euler(0,0,nearUpsideDown?180:0);
                    far.geometry.rotation=facing*Quaternion.Euler(0,b*90,0)*Quaternion.Euler(0,0,farUpsideDown?180:0);
                    float degrees;var arrival=MagnetPiece.Rolled(near.shape,near.Pose,push,out degrees);
                    bool front=a==0&&b==2;
                    bool opposedSide=(a==1&&b==3)||(a==3&&b==1);
                    bool stacked=Vector3.Dot(arrival*Vector3.forward,far.Pose*Vector3.forward)>.95f;
                    var expected=stacked?MagnetProduct.ThickU:front||opposedSide?MagnetProduct.Ring:MagnetProduct.None;
                    Check(GridPlayground.Recipe(near,arrival,far,true,push)==expected,"Front/side ring alignment: "+push+" / "+a+" / "+b);
                    Check(GridPlayground.Recipe(near,arrival,far,false,push)==expected,"Floating receiver changed alignment rule");
                    Check(GridPlayground.Recipe(near,arrival,far,true,push)==expected,"All scenes must use facing openings");
                    far.north=true;
                    Check(GridPlayground.Recipe(near,arrival,far,true,push)==MagnetProduct.None,"Same poles formed a ring");
                    far.north=false;cases++;
                }
                Debug.Log("PASS: "+cases+" ring orientation cases; unified recipes, both flat faces, supported/floating receiver and same poles.");
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static string Fingerprint(Transform geometry){
            return string.Join(";",geometry.GetComponentsInChildren<MeshFilter>(true).SelectMany(f=>f.sharedMesh.vertices.Select(v=>geometry.InverseTransformPoint(f.transform.TransformPoint(v))))
                .Select(v=>Mathf.RoundToInt(v.x*1000)+","+Mathf.RoundToInt(v.y*1000)+","+Mathf.RoundToInt(v.z*1000)).OrderBy(v=>v));
        }
        [MenuItem("两极之间/验证章节磁铁一致性")]
        static void Run(){
            Directory.CreateDirectory("output");
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Run;return;}
            if(!EditorApplication.isPlaying){
                try{
                    for(int i=0;i<SceneManager.sceneCount;i++)Check(!SceneManager.GetSceneAt(i).isDirty,"Save scene changes before running regression");
                    ValidateScenes();File.WriteAllText(Report,"");SessionState.SetBool("MagnetSceneRegression.OwnPlay",true);EditorApplication.isPlaying=true;
                }
                catch(Exception ex){File.WriteAllText(Report,"FAIL edit validation: "+ex);}
                return;
            }
            try{File.WriteAllText(Report,"");RuntimeCases();File.AppendAllText(Report,"PASS: runtime regression completed\n");Debug.Log("Magnet scene regression passed: 112 cases. "+Report);}
            catch(Exception ex){File.AppendAllText(Report,"FAIL runtime: "+ex+"\n");}
            finally{
                if(SessionState.GetBool("MagnetSceneRegression.OwnPlay",false)){
                    SessionState.SetBool("MagnetSceneRegression.OwnPlay",false);EditorApplication.isPlaying=false;
                }
            }
        }
        static void ValidateScenes(){
            var original=SceneManager.GetActiveScene();var report=new List<string>();
            var root=new GameObject("Geometry validation");root.SetActive(false);
            try{
                var expected=new Dictionary<MagnetShape,string>();
                foreach(MagnetShape shape in Enum.GetValues(typeof(MagnetShape))){
                    var g=MagnetVisuals.Build(root.transform,shape,true,MagnetProduct.None,1.5f,true,Vector2Int.right);
                    expected[shape]=Fingerprint(g);
                }
                foreach(var path in Directory.GetFiles("Assets/BetweenPoles/Scenes","Chapter*.unity")){
                    var scene=SceneManager.GetSceneByPath(path.Replace('\\','/'));bool opened=!scene.IsValid()||!scene.isLoaded;
                    if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try{
                        int count=0;
                        foreach(var board in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridPlayground>(true))){
                            foreach(var m in board.magnets){
                                if(!m||m.combined)continue;
                                Check(Fingerprint(m.geometry)==expected[m.shape],path+" geometry mismatch "+m.name);
                                Check(m.GetComponentsInChildren<Renderer>().Length==m.geometry.GetComponentsInChildren<Renderer>().Length,path+" duplicate visible geometry "+m.name);
                                Check(m.geometry.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial&&AssetDatabase.Contains(r.sharedMaterial)),"Unsaved materials "+m.name);
                                count++;
                            }
                            Check(board.player.Find("Visual").GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh&&f.sharedMesh.name=="AstronautBody"),path+" missing animated character mesh");
                        }
                        report.Add(path+": PASS "+count+" actual scene magnets match test factory; astronaut mesh present");
                    }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
                }
                foreach(var name in new[]{"Bar N","Bar S","U N","U S"}){
                    var m=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Prefabs/"+name+".prefab").GetComponent<MagnetPiece>();
                    Check(Fingerprint(m.geometry)==expected[m.shape],"Prefab mismatch "+name);report.Add(name+": PASS canonical geometry");
                }
                File.WriteAllLines("output/magnet-scene-consistency-edit-test.txt",report);
            }finally{UnityEngine.Object.DestroyImmediate(root);if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);}
        }
        static void Drain(IEnumerator routine,Action<int> sample){
            var stack=new Stack<IEnumerator>();stack.Push(routine);int frames=0;
            while(stack.Count>0){var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}
                if(current.Current is IEnumerator nested){stack.Push(nested);continue;}
                sample?.Invoke(++frames);Check(frames<1000,"Animation timeout");
            }
        }
        static void RuntimeCases(){
            var root=new GameObject("Scene consistency regression");root.SetActive(false);
            var results=new List<string>();int cases=0;
            try{
                // Both colors, all four push directions, authored prefabs versus test-panel materials.
                for(int source=0;source<2;source++)for(int direction=0;direction<4;direction++)for(int color=0;color<2;color++)for(int variant=0;variant<7;variant++){
                    var host=new GameObject("Case");host.transform.SetParent(root.transform,false);
                    var b=host.AddComponent<GridPlayground>();b.enabled=false;
                    var p=new GameObject("Player");p.transform.SetParent(host.transform,false);b.player=p.transform;
                    var visual=new GameObject("Visual");visual.transform.SetParent(p.transform,false);
                    var yaw=Quaternion.Euler(0,direction*90,0);var forward=yaw*Vector3.right;
                    var push=new Vector2Int(Mathf.RoundToInt(forward.x),Mathf.RoundToInt(forward.z));
                    var tiles=new List<GridTile>();
                    for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++){
                        var t=new GameObject("Tile");t.transform.SetParent(host.transform,false);t.transform.position=new Vector3(x*1.5f,0,z*1.5f);tiles.Add(t.AddComponent<GridTile>());
                    }
                    b.tiles=tiles.ToArray();
                    Func<int,MagnetPiece> make=index=>{
                        var shape=variant>=2&&variant<=4?MagnetShape.Horseshoe:variant>=5&&index==1?MagnetShape.Horseshoe:MagnetShape.Bar;
                        bool north=(index==0)==(color==0);MagnetPiece m;
                        if(source==0){
                            var g=new GameObject("Test magnet");g.transform.SetParent(host.transform,false);m=g.AddComponent<MagnetPiece>();m.shape=shape;m.north=north;
                            m.geometry=MagnetVisuals.Build(g.transform,shape,north,MagnetProduct.None,1.5f,north,Vector2Int.right);
                        }else{
                            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Prefabs/"+(shape==MagnetShape.Bar?"Bar ":"U ")+(north?"N":"S")+".prefab");
                            m=UnityEngine.Object.Instantiate(prefab,host.transform).GetComponent<MagnetPiece>();
                        }
                        m.transform.position=forward*((index+1)*1.5f);m.geometry.rotation=yaw;MagnetVisuals.Ground(m);return m;
                    };
                    var incoming=make(0);var receiver=make(1);b.magnets=new[]{incoming,receiver};
                    // Cross uses a flat receiver transverse to the push; the other mixed posture is axial wide-bar.
                    if(variant==1){incoming.geometry.rotation=yaw*Quaternion.Euler(0,0,90);receiver.geometry.rotation=yaw*Quaternion.Euler(0,90,0);}
                    if(variant==6)incoming.geometry.rotation=yaw*Quaternion.Euler(0,0,90);
                    if(variant>=2&&variant<=4){
                        var facing=Quaternion.LookRotation(forward);
                        incoming.geometry.rotation=facing*Quaternion.Euler(0,0,variant==3?180:0);
                        receiver.geometry.rotation=facing*Quaternion.Euler(0,180,0)*Quaternion.Euler(0,0,variant==4?180:0);
                    }
                    MagnetVisuals.Ground(incoming);MagnetVisuals.Ground(receiver);
                    var product=variant==0?MagnetProduct.WideBar:variant==1?MagnetProduct.Cross:variant<=4?MagnetProduct.Ring:variant==5?MagnetProduct.BridgeHalf:MagnetProduct.Lift;
                    Check(GridPlayground.Recipe(incoming,incoming.Pose,receiver,true,push)==product,"Recipe mismatch "+variant);
                    b.CaptureInitialState();var saved=typeof(GridPlayground).GetMethod("SaveWorld",Flags).Invoke(b,null);
                    typeof(GridPlayground).GetField("<Busy>k__BackingField",Flags).SetValue(b,true);
                    typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(b,true);
                    var axis=yaw*Vector3.forward;var bridgeDir=new Vector2Int(Mathf.RoundToInt(axis.x),Mathf.RoundToInt(axis.z));
                    var routine=(IEnumerator)typeof(GridPlayground).GetMethod("Assemble",Flags).Invoke(b,new object[]{incoming,receiver,push,incoming.Pose,product,push,bridgeDir});
                    bool synced=false;
                    Drain(routine,frame=>{float distance=Vector3.Dot(p.transform.position,forward);if(distance>.001f&&distance<1.499f&&visual.transform.localEulerAngles.x>=5&&receiver.product==MagnetProduct.None)synced=true;});
                    Check(synced,"Player not synchronized "+variant);
                    Check(receiver.product==product&&!incoming.gameObject.activeSelf&&!b.Busy,"Assembly failed "+variant);
                    Check((p.transform.position-forward*1.5f).sqrMagnitude<.0001f,"Player landing mismatch");
                    Check(Quaternion.Angle(visual.transform.localRotation,Quaternion.identity)<.01f,"Pose not restored");
                    typeof(GridPlayground).GetMethod("RestoreWorld",Flags).Invoke(b,new[]{saved});
                    Check(incoming.gameObject.activeSelf&&receiver.product==MagnetProduct.None&&p.transform.position.sqrMagnitude<.0001f,"Undo did not restore materials");
                    UnityEngine.Object.DestroyImmediate(host);cases++;
                }
                results.Add("PASS: "+cases+" sampled assemblies (factory/prefab x four directions x two polarities x seven docking cases), recipes, simultaneous player movement, final states, snapshot restore");
            }finally{UnityEngine.Object.DestroyImmediate(root);File.AppendAllLines(Report,results);}
        }
    }
}
