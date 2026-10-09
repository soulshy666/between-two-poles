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
                    if(variant==3){incoming.geometry.rotation=yaw*Quaternion.Euler(0,-90,0);receiver.geometry.rotation=yaw*Quaternion.Euler(0,90,0);}
                    if(variant==4){incoming.geometry.rotation=yaw*Quaternion.Euler(0,180,0);receiver.geometry.rotation=yaw*Quaternion.Euler(0,90,0);}
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
