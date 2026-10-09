using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
    [InitializeOnLoad] static class BridgeCenterRegression {
        const string Report="output/bridge-center-regression.txt";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static BridgeCenterRegression(){
            EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("BridgeCenterRegression.OwnPlay",false))EditorApplication.delayCall+=Run;
            };
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static object Call(GridPlayground board,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Flags).Invoke(board,args);}
        static void Drain(IEnumerator routine,Action sample){
            var stack=new Stack<IEnumerator>();stack.Push(routine);int frames=0;
            while(stack.Count>0){var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}if(top.Current is IEnumerator child){stack.Push(child);continue;}sample?.Invoke();Check(++frames<1000,"Animation timeout");}
        }
        [MenuItem("两极之间/验证两格桥中心通行")]
        static void Run(){
            Directory.CreateDirectory("output");
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Run;return;}
            if(!EditorApplication.isPlaying){SessionState.SetBool("BridgeCenterRegression.OwnPlay",true);EditorApplication.isPlaying=true;return;}
            var root=new GameObject("Bridge center regression");root.SetActive(false);int checks=0;
            try{
                for(int direction=0;direction<4;direction++){
                    var yaw=Quaternion.Euler(0,direction*90,0);var axis=yaw*Vector3.right;var side=yaw*Vector3.forward;
                    var dir=new Vector2Int(Mathf.RoundToInt(axis.x),Mathf.RoundToInt(axis.z));
                    var host=new GameObject("Case");host.transform.SetParent(root.transform,false);
                    var b=host.AddComponent<GridPlayground>();b.enabled=false;
                    var player=new GameObject("Player");player.transform.SetParent(host.transform,false);b.player=player.transform;
                    new GameObject("Visual").transform.SetParent(player.transform,false);
                    var bridgeObject=new GameObject("Bridge");bridgeObject.transform.SetParent(host.transform,false);
                    var bridge=bridgeObject.AddComponent<MagnetPiece>();bridge.bridgeDirection=dir;
                    bridge.geometry=new GameObject("Initial").transform;bridge.geometry.SetParent(bridge.transform,false);
                    MagnetVisuals.Product(bridge,MagnetProduct.Bridge,1.5f,yaw,true);
                    b.magnets=new[]{bridge};
                    var tiles=new List<GridTile>();
                    Func<int,GridTile> tile=n=>{var obj=new GameObject("Ground");obj.transform.SetParent(host.transform,false);obj.transform.position=axis*(n*1.5f);return obj.AddComponent<GridTile>();};
                    tiles.Add(tile(-1));tiles.Add(tile(2));b.tiles=tiles.ToArray();
                    b.player.position=-axis*1.5f;b.player.rotation=Quaternion.LookRotation(axis);root.SetActive(true);
                    b.CaptureInitialState();typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(b,true);
                    // The actual movement routine must remain on the centerline at every sample.
                    for(int cell=0;cell<=2;cell++){
                        var target=dir*cell;
                        Drain((IEnumerator)Call(b,"MovePlayer",target),()=>Check(Mathf.Abs(Vector3.Dot(b.player.position,side))<.0001f,"Side drift during bridge walking"));
                        Check((new Vector2(b.player.position.x,b.player.position.z)-(Vector2)target*1.5f).sqrMagnitude<.0001f,"Wrong center landing");checks++;
                    }
                    b.player.position=-axis*1.5f;
                    var follow=(Vector3)Call(b,"PushPlayerEnd",Vector2Int.zero,null);
                    Check(Mathf.Abs(Vector3.Dot(follow,side))<.0001f&&Mathf.Abs(follow.y-.24f)<.0001f,"Push-follow offset");checks++;
                    object[] travel={-dir,0f,Vector2Int.zero,null,0f};
                    Check((bool)Call(b,"TravelSurface",travel),"Gap bridge entry rejected");checks++;
                    // Reject either bridge cell on land, even via the object-top shortcut.
                    for(int cell=0;cell<2;cell++){
                        var ground=tile(cell);tiles.Add(ground);b.tiles=tiles.ToArray();
                        b.player.position=axis*((cell-1)*1.5f);b.player.position+=Vector3.up*(cell==0?0:.24f);
                        Check(!(bool)Call(b,"TryStepCore",dir),"Land bridge was walkable at cell "+cell);checks++;
                        travel=new object[]{dir*(cell-1),b.player.position.y,dir*cell,null,0f};
                        Check(!(bool)Call(b,"TravelSurface",travel),"Materials can enter land bridge");checks++;
                        tiles.Remove(ground);UnityEngine.Object.DestroyImmediate(ground.gameObject);b.tiles=tiles.ToArray();
                    }
                    // Half bridges remain non-walkable, rings retain their rim offset.
                    bridge.product=MagnetProduct.BridgeHalf;bridge.walkable=false;b.player.position=-axis*1.5f;
                    Check(!(bool)Call(b,"TryStepCore",dir),"Half bridge became walkable");checks++;
                    bridge.product=MagnetProduct.Ring;bridge.walkable=true;
                    var rim=(Vector3)Call(b,"Support",Vector2Int.zero,b.player.position);
                    Check(Mathf.Abs(Vector3.Dot(rim,side))>.4f,"Ring rim traversal changed");checks++;
                    UnityEngine.Object.DestroyImmediate(host);root.SetActive(false);
                }
                File.WriteAllText(Report,"PASS: "+checks+" assertions; four directions; centered bridge walking/exit and push-follow; both land cells block players/materials; gap entry; half bridge blocked; ring rim preserved.");
            }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);}
            finally{UnityEngine.Object.DestroyImmediate(root);if(SessionState.GetBool("BridgeCenterRegression.OwnPlay",false)){SessionState.SetBool("BridgeCenterRegression.OwnPlay",false);EditorApplication.isPlaying=false;}}
        }
    }
}
