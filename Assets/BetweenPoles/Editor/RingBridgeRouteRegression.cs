using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles.EditorTools {
public static class RingBridgeRouteRegression {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(GridPlayground board,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Flags).Invoke(board,args);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    [MenuItem("两极之间/测试/圆环连续穿过两格桥")]
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();int count=0;
        try{
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
            foreach(bool reverse in new[]{false,true})
            foreach(bool rolling in new[]{false,true}){
                var root=new GameObject("Ring bridge route");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
                try{
                    var b=root.AddComponent<GridPlayground>();b.enabled=false;
                    var axis=new Vector3(dir.x,0,dir.y);var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                    b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);b.player.position=-axis*b.cellSize;
                    b.tiles=new GridTile[4];int[] cells={-1,0,3,4};
                    for(int i=0;i<cells.Length;i++){
                        var obj=new GameObject("Shore");obj.transform.SetParent(root.transform);obj.transform.position=axis*cells[i]*b.cellSize;
                        b.tiles[i]=obj.AddComponent<GridTile>();b.tiles[i].blocked=cells[i]==4;
                    }
                    var bridge=MagnetVisuals.Create(root.transform,axis*b.cellSize,MagnetShape.Horseshoe,true,yaw,b.cellSize,root.transform);
                    bridge.bridgeDirection=dir;MagnetVisuals.Product(bridge,MagnetProduct.Bridge,b.cellSize,yaw,true);
                    var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,false,Quaternion.identity,b.cellSize,root.transform);
                    var pose=Quaternion.FromToRotation(Vector3.up,rolling?Vector3.Cross(axis,Vector3.up):axis);
                    MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,pose,false);ring.walkable=false;
                    b.magnets=reverse?new[]{ring,bridge}:new[]{bridge,ring};root.SetActive(true);
                    typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(b,true);
                    Check(b.TryStep(dir),"First push rejected: "+b.LastRule);Call(b,"FinishMovement");
                    if(!rolling){
                        Check(Vector3.Distance(ring.transform.position,axis*b.cellSize)<.001f,"Ring missed bridge entrance");
                        Check(!(bool)Call(b,"RingShore",ring),"Ring on bridge became floating terrain");
                        Check((bool)Call(b,"Supported",ring)&&b.MagnetAt(dir)==ring,"Bridge hid or failed to support ring");
                        Check(b.TryStep(dir),"Second push rejected: "+b.LastRule);Call(b,"FinishMovement");
                        Check(Vector3.Distance(ring.transform.position,axis*b.cellSize*2)<.001f,"Ring did not cross to second bridge cell");
                        Check(b.PlayerCell==dir,"Player did not follow onto first bridge cell");
                        Check(b.UndoStep(),"Undo failed");
                        Check((bool)Call(b,"Supported",ring)&&!(bool)Call(b,"RingShore",ring),"Undo lost bridge support");
                        Check(b.TryStep(dir),"Replay rejected");Call(b,"FinishMovement");
                        Check(b.TryStep(dir),"Exit push rejected: "+b.LastRule);Call(b,"FinishMovement");
                    }
                    Check(Vector3.Distance(ring.transform.position,axis*b.cellSize*3)<.001f,"Ring did not reach opposite shore");
                    Check(Vector3.Distance(bridge.transform.position,axis*b.cellSize)<.001f,"Bridge moved");
                    Check(Mathf.Abs(b.player.position.y)<.001f,"Player left bridge height");
                    var side=new Vector2Int(-dir.y,dir.x);
                    Check(!b.CanPortalRingEnter(ring,dir,0,dir+side),"Sideways bridge exit allowed");
                    count++;
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            File.WriteAllText("output/ring-bridge-route-result.txt","PASS: "+count+" cases; four directions, array orders, tipping and rolling, both bridge cells, opposite shore, undo/replay and sideways rejection.");
        }catch(Exception ex){File.WriteAllText("output/ring-bridge-route-result.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
