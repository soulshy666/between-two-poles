using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
public static class PortalRepulsionRegression {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    [MenuItem("两极之间/测试/黑洞同极受阻反弹")]
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();int checks=0;
        try{
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
                var root=new GameObject("Portal repulsion fixture");root.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.magnets=new MagnetPiece[0];
                b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
                var tiles=new List<GridTile>();
                for(int n=-4;n<=2;n++){
                    var go=new GameObject("Floor");go.transform.SetParent(root.transform);
                    go.transform.position=new Vector3(dir.x*n*b.cellSize,0,dir.y*n*b.cellSize);tiles.Add(go.AddComponent<GridTile>());
                }
                b.tiles=tiles.ToArray();
                var source=tiles[2].gameObject.AddComponent<BlackHolePortal>();source.board=b;source.tile=tiles[2];
                var target=tiles[4].gameObject.AddComponent<BlackHolePortal>();target.board=b;target.tile=tiles[4];
                var yaw=Quaternion.FromToRotation(Vector3.right,new Vector3(dir.x,0,dir.y));
                var cargo=MagnetVisuals.Create(root.transform,source.tile.transform.position,MagnetShape.Bar,true,yaw*Quaternion.Euler(0,0,90),b.cellSize,root.transform);
                var receiver=MagnetVisuals.Create(root.transform,tiles[5].transform.position,MagnetShape.Bar,true,yaw,b.cellSize,root.transform);
                cargo.portalEntryPosition=tiles[1].transform.position;cargo.storedInPortal=source;cargo.gameObject.SetActive(false);b.magnets=new[]{cargo,receiver};
                root.SetActive(true);
                var savedPosition=receiver.transform.position;var savedPose=receiver.Pose;
                foreach(bool stone in new[]{true,false}){
                    tiles[6].blocked=stone;tiles[6].surfaceHeight=stone?0:b.LayerHeight;
                    BlackHoleTravel.ExitPlan plan;
                    Check(BlackHoleTravel.PlanExit(target,dir,cargo,out plan),"Blocked same-pole exit rejected");
                    Check(plan.bounce&&plan.kick&&!plan.displaced&&!plan.receiver,"Wrong rebound branch");
                    Check(Vector3.Distance(plan.player,tiles[1].transform.position)<.001f&&Vector3.Distance(plan.kicked,tiles[0].transform.position)<.001f,"Wrong return or backwards kick cell");
                    Check(receiver.transform.position==savedPosition&&receiver.Pose==savedPose,"Receiver mutated by plan");
                    Check(Vector3.Dot(plan.magnet-target.tile.transform.position,new Vector3(dir.x,0,dir.y))<b.cellSize,"Contact penetrates receiver");
                    tiles[0].blocked=true;Check(!BlackHoleTravel.PlanExit(target,dir,cargo,out plan),"Unsafe return kick accepted");tiles[0].blocked=false;
                    checks+=6;
                }
                tiles[6].surfaceHeight=0;cargo.geometry.rotation=yaw;
                BlackHoleTravel.ExitPlan free;
                Check(BlackHoleTravel.PlanExit(target,dir,cargo,out free)&&free.displaced==receiver&&!free.bounce,"Unblocked repulsion regressed");checks++;
                UnityEngine.Object.DestroyImmediate(root);
            }
            Directory.CreateDirectory("output");File.WriteAllText("output/portal-repulsion.txt","PASS: "+checks+" checks; four directions, stone/high platform, upright return kick, safe return validation, unchanged receiver, free repulsion.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/portal-repulsion.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
