using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
public static class UnstableMagnetJumpRegression {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();int count=0;
        try{
            foreach(var dir in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back}){
                var root=new GameObject("Unstable jump fixture");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var board=root.AddComponent<GridPlayground>();board.enabled=false;
                board.player=new GameObject("Player").transform;board.player.SetParent(root.transform);
                var floor=new GameObject("Stone");floor.transform.SetParent(root.transform);var tile=floor.AddComponent<GridTile>();tile.blocked=true;board.tiles=new[]{tile};
                var start=Vector3.up*.75f;board.player.position=start;
                var magnet=MagnetVisuals.Create(root.transform,dir*board.cellSize,MagnetShape.Bar,true,Quaternion.FromToRotation(Vector3.right,dir),board.cellSize,root.transform);
                board.magnets=new[]{magnet};var pos=magnet.geometry.localPosition;var rot=magnet.geometry.localRotation;
                typeof(GridPlayground).GetField("recordingPush",Private).SetValue(board,true);
                typeof(GridPlayground).GetField("movementBusy",Private).SetValue(board,true);
                int landings=0;board.Landed+=t=>{Check(t==tile,"Unstable magnet was committed as a landing");landings++;};
                var action=(IEnumerator)typeof(GridPlayground).GetMethod("JumpOntoUnstableMagnet",Private).Invoke(board,new object[]{magnet,dir*board.cellSize});
                var stack=new Stack<IEnumerator>();stack.Push(action);int guard=2000;bool wobbled=false,hung=false;
                while(stack.Count>0){
                    Check(--guard>0,"Animation never completes");var current=stack.Peek();
                    if(!current.MoveNext()){stack.Pop();continue;}
                    if(current.Current is IEnumerator child){stack.Push(child);continue;}
                    Check(board.Busy,"Input unlocked during recovery");
                    wobbled|=Quaternion.Angle(rot,magnet.geometry.localRotation)>2;
                    hung|=Vector3.Dot(board.player.position,dir)<board.cellSize*.65f&&board.player.position.y<start.y-.6f;
                }
                Check(wobbled&&hung,"Missing wobble or ledge hang");
                Check(board.player.position==start&&!board.Busy&&landings==1,"Wrong final position or input lock");
                Check(magnet.geometry.localPosition==pos&&Quaternion.Angle(magnet.geometry.localRotation,rot)<.01f,"Magnet did not restore");
                count++;UnityEngine.Object.DestroyImmediate(root);
            }
            Directory.CreateDirectory("output");File.WriteAllText("output/unstable-jump.txt","PASS: "+count+" directions; wobble, ledge hang, original stone landing, magnet restoration, input lock and release.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/unstable-jump.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
