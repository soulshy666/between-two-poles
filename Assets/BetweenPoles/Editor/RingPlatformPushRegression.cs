using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetweenPoles.EditorTools {
public static class RingPlatformPushRegression {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(GridPlayground board,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Private).Invoke(board,args);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static Bounds BoundsOf(MagnetPiece piece){
        var bounds=new Bounds();bool first=true;
        foreach(var filter in piece.geometry.GetComponentsInChildren<MeshFilter>())
            foreach(var vertex in filter.sharedMesh.vertices){
                var point=filter.transform.TransformPoint(vertex);
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
        return bounds;
    }
    [MenuItem("两极之间/测试/圆环推上悬浮平台后继续推动")]
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();int count=0;
        try{
            foreach(var direction in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
            foreach(bool reverse in new[]{false,true})
            foreach(bool upright in new[]{false,true})
            foreach(bool lowered in new[]{false,true}){
                string label=direction+" reverse="+reverse+" upright="+upright+" lowered="+lowered;
                var root=new GameObject(label);root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
                try{
                    var board=root.AddComponent<GridPlayground>();board.enabled=false;
                    var forward=new Vector3(direction.x,0,direction.y);
                    board.player=new GameObject("Actor").transform;board.player.SetParent(root.transform);board.player.position=-forward*board.cellSize;
                    board.tiles=new GridTile[2];
                    for(int i=0;i<2;i++){
                        var tile=new GameObject("Shore");tile.transform.SetParent(root.transform);tile.transform.position=-forward*i*board.cellSize;
                        board.tiles[i]=tile.AddComponent<GridTile>();
                    }
                    var platform=MagnetVisuals.Create(root.transform,forward*board.cellSize,MagnetShape.Horseshoe,true,Quaternion.identity,board.cellSize,root.transform);
                    MagnetVisuals.Product(platform,MagnetProduct.Ring,board.cellSize,Quaternion.identity,true);
                    var wheel=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,false,Quaternion.identity,board.cellSize,root.transform);
                    var pose=upright?Quaternion.FromToRotation(Vector3.up,forward):Quaternion.identity;
                    MagnetVisuals.Product(wheel,MagnetProduct.Ring,board.cellSize,pose,false);wheel.walkable=!upright;
                    if(lowered)platform.geometry.position+=Vector3.up*(platform.transform.position.y-BoundsOf(platform).max.y);
                    board.magnets=reverse?new[]{wheel,platform}:new[]{platform,wheel};root.SetActive(true);
                    typeof(GridPlayground).GetField("recordingPush",Private).SetValue(board,true);
                    Check(board.TryStep(direction),label+": first push rejected: "+board.LastRule);
                    Call(board,"FinishMovement");
                    Check(Vector3.Distance(wheel.transform.position,forward*board.cellSize)<.001f,label+": missed platform");
                    Check((bool)Call(board,"RingShore",platform)&&!(bool)Call(board,"RingShore",wheel),label+": rider classified as terrain");
                    Check(BoundsOf(wheel).min.y>=BoundsOf(platform).max.y-.001f,label+": ring meshes overlap");
                    Check(board.MagnetAt(direction)==wheel&&(bool)Call(board,"Supported",wheel),label+": rider not selectable/supported");
                    Check(board.player.position.sqrMagnitude<.001f,label+": player climbed onto occupied cell");
                    Check(board.TryStep(direction),label+": second push rejected: "+board.LastRule);
                    Call(board,"FinishMovement");
                    Check(Vector3.Distance(wheel.transform.position,forward*board.cellSize*2)<.001f,label+": second push did not move rider");
                    Check(Vector3.Distance(platform.transform.position,forward*board.cellSize)<.001f,label+": platform moved");
                    Check(Vector3.Distance(board.player.position,forward*board.cellSize)<.001f,label+": player did not enter vacated platform");
                    Check(board.UndoStep(),label+": undo failed");
                    Check(board.MagnetAt(direction)==wheel&&(bool)Call(board,"Supported",wheel),label+": undo lost rider support");
                    Check(board.TryStep(direction),label+": replay after undo rejected");Call(board,"FinishMovement");
                    Check(Vector3.Distance(wheel.transform.position,forward*board.cellSize*2)<.001f,label+": replay failed");
                    count++;
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            Directory.CreateDirectory("output");File.WriteAllText("output/ring-platform-push-result.txt","PASS: "+count+" cases; four directions, both array orders and postures, authored/settled platforms, second push and undo/replay.");
            Debug.Log("PASS: ring platform push "+count+" cases");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/ring-platform-push-result.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
