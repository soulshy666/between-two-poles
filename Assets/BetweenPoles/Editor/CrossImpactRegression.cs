using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles.EditorTools {
public static class CrossImpactRegression {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(GridPlayground b,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Flags).Invoke(b,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("两极之间/测试/圆环撞落十字上层")]
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();int cases=0;
        try{
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
            foreach(bool north in new[]{false,true})
            foreach(bool receiverOnTop in new[]{false,true})
            foreach(bool adjacent in new[]{false,true})
            for(int mode=0;mode<9;mode++){
                string label=dir+" north="+north+" receiverOnTop="+receiverOnTop+" adjacent="+adjacent+" mode="+mode;
                var root=new GameObject(label);root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
                try{
                    var b=root.AddComponent<GridPlayground>();b.enabled=false;
                    var axis=new Vector3(dir.x,0,dir.y);var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                    b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);b.player.position=-axis*b.cellSize;
                    var tiles=new System.Collections.Generic.List<GridTile>();
                    for(int i=-1;i<=4;i++){
                        if(i==3&&(mode==1||mode==3))continue;
                        var obj=new GameObject("Tile");obj.transform.SetParent(root.transform);obj.transform.position=axis*i*b.cellSize;
                        var tile=obj.AddComponent<GridTile>();tile.blocked=i==3&&mode==4;tiles.Add(tile);
                    }
                    b.tiles=tiles.ToArray();
                    var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                    var wheelPose=Quaternion.FromToRotation(Vector3.up,Vector3.Cross(axis,Vector3.up));
                    MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,wheelPose,true);ring.walkable=false;
                    if(adjacent){ring.transform.position=axis*b.cellSize;b.player.position=Vector3.zero;}
                    var cross=MagnetVisuals.Create(root.transform,axis*b.cellSize*2,MagnetShape.Bar,receiverOnTop?north:!north,Quaternion.identity,b.cellSize,root.transform);
                    var crossYaw=yaw*Quaternion.Euler(0,receiverOnTop?0:90,0);
                    if(mode==5)crossYaw*=Quaternion.Euler(0,90,0);
                    MagnetVisuals.Product(cross,MagnetProduct.Cross,b.cellSize,crossYaw,cross.north,receiverOnTop);
                    var pieces=new System.Collections.Generic.List<MagnetPiece>{ring,cross};MagnetPiece target=null;
                    // Exercise an assembled cross's retained material as well as an authored cross.
                    if(receiverOnTop){
                        var retained=MagnetVisuals.Create(cross.transform,cross.transform.position,MagnetShape.Bar,!cross.north,yaw,b.cellSize,root.transform);
                        retained.combined=true;retained.enabled=false;retained.gameObject.SetActive(false);pieces.Add(retained);
                    }
                    if(mode==2||mode==3||mode==6||mode==7||mode==8){
                        target=MagnetVisuals.Create(root.transform,axis*b.cellSize*3,MagnetShape.Bar,mode==6?north:!north,yaw,b.cellSize,root.transform);pieces.Add(target);
                        if(mode==7)MagnetVisuals.Product(target,MagnetProduct.WideBar,b.cellSize,yaw,target.north);
                        if(mode==8){b.tiles.First(t=>Mathf.RoundToInt(Vector3.Dot(t.transform.position,axis)/b.cellSize)==3).surfaceHeight=1;target.transform.position+=Vector3.up;}
                    }
                    b.magnets=pieces.ToArray();root.SetActive(true);
                    b.CaptureInitialState();Call(b,"InitializeDiskReferences");
                    int authored=b.magnets.Length;
                    int activeBefore=pieces.Count(p=>p.gameObject.activeInHierarchy);
                    typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(b,true);
                    Check(b.TryStep(dir),label+": push rejected");
                    // Undo after actual frame sampling has split the cross, not only after completion.
                    if(mode==0){
                        int guard=1000;
                        while(cross.product==MagnetProduct.Cross&&b.Busy&&guard-->0)Call(b,"AdvanceMovement");
                        Check(cross.product==MagnetProduct.None,label+": sampled collision never split");
                        Check(b.UndoStep()&&cross.product==MagnetProduct.Cross,label+": mid-impact undo failed");
                        Check(b.TryStep(dir),label+": mid-impact replay rejected");
                    }
                    Call(b,"FinishMovement");
                    bool blocked=mode==4||mode==5||mode==7||mode==8;
                    Check(cross.product==(blocked?MagnetProduct.Cross:MagnetProduct.None),label+": wrong split result");
                    Check(b.PlayerCell==Vector2Int.zero&&Vector3.Distance(ring.transform.position,axis*b.cellSize)<.001f,label+": ring/player moved past cross");
                    if(!blocked){
                        Check(cross.north==!north&&cross.CanBePushed,label+": lower pole lost");
                        Check(Mathf.Abs(Vector3.Dot(cross.Pose*Vector3.right,axis))<.01f,label+": lower orientation changed");
                        if(mode==2||mode==3)Check(target.product==MagnetProduct.WideBar,label+": opposite pole did not assemble");
                        else{
                            var loose=b.MagnetAt(dir*3);Check(loose&&loose.north==north&&loose.product==MagnetProduct.None,label+": upper bar missing");
                            Check(Mathf.Abs(Vector3.Dot(loose.Pose*Vector3.right,axis))>.99f,label+": upper bar rolled instead of sliding");
                            if(mode==6)Check(Vector3.Distance(target.transform.position,axis*b.cellSize*4)<.001f,label+": same pole not repelled");
                        }
                    }
                    Check(b.UndoStep(),label+": undo failed");
                    Check(cross.product==MagnetProduct.Cross&&b.MagnetAt(dir*3)==target,label+": undo failed to restore materials");
                    Check(b.TryStep(dir),label+": replay rejected");Call(b,"FinishMovement");
                    Check(cross.product==(blocked?MagnetProduct.Cross:MagnetProduct.None),label+": replay differs");
                    var saved=b.CaptureDiskState();
                    Check(saved.authoredCount==authored,label+": runtime material changed authored save count");
                    b.RestoreDiskState(saved);
                    Check(cross.product==(blocked?MagnetProduct.Cross:MagnetProduct.None),label+": disk restore lost split");
                    Check(b.magnets.Count(m=>m&&m.gameObject.activeInHierarchy)==(blocked?activeBefore:(mode==2||mode==3)?activeBefore:activeBefore+1),label+": duplicate or missing material after load");
                    cases++;
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            Directory.CreateDirectory("output");File.WriteAllText("output/cross-impact-result.txt","PASS: "+cases+" cases. Four directions, both upper poles and layer orders, adjacent and rolling approach; ground, space, opposite-pole ground/space assembly, stone, wrong axis, same-pole repulsion, fixed obstacle, height mismatch; sampled mid-impact undo, replay and disk roundtrip.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/cross-impact-result.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
