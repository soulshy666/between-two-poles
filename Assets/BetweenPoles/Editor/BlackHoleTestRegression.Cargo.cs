using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace BetweenPoles.EditorTools {
    static partial class BlackHoleTestRegression {
        static void Drain(IEnumerator routine,Action sample=null){
            var stack=new Stack<IEnumerator>();stack.Push(routine);int count=0;
            while(stack.Count>0){
                if(++count>50000)throw new Exception("Coroutine did not finish");
                var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}
                var child=current.Current as IEnumerator;if(child!=null&&!(child is CustomYieldInstruction))stack.Push(child);else if(sample!=null)sample();
            }
        }
        static int CargoCases(){
            int checks=0;
            foreach(var dir in new[]{Vector2Int.left,Vector2Int.up,Vector2Int.right,Vector2Int.down})for(int kind=0;kind<5;kind++)for(int pole=0;pole<2;pole++){
                var root=new GameObject("Portal cargo regression");root.SetActive(false);
                try{
                    var b=root.AddComponent<GridPlayground>();b.enabled=false;
                    var player=new GameObject("Player");player.transform.SetParent(root.transform);b.player=player.transform;
                    var center=new GameObject("Center");center.transform.SetParent(root.transform);
                    var lab=root.AddComponent<MagnetTestLayouts>();lab.enabled=false;lab.board=b;lab.center=center.transform;
                    var tiles=new List<GridTile>();
                    for(int x=-5;x<=5;x++)for(int z=-5;z<=5;z++){
                        var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=new Vector3(x*1.5f,0,z*1.5f);
                        var t=go.AddComponent<GridTile>();var anchor=go.AddComponent<IslandSurfaceAnchor>();anchor.center=center.transform;tiles.Add(t);
                    }
                    b.tiles=tiles.ToArray();b.magnets=new MagnetPiece[0];
                    var source=lab.PlacePortal(b.TileAt(-dir*2));var target=lab.PlacePortal(b.TileAt(Vector2Int.zero));
                    var yaw=Quaternion.FromToRotation(Vector3.right,new Vector3(dir.x,0,dir.y));
                    var rotation=kind==1?Quaternion.Euler(0,0,90):Quaternion.identity;
                    var m=MagnetVisuals.Create(root.transform,new Vector3(-dir.x*4.5f,0,-dir.y*4.5f),kind==2?MagnetShape.Horseshoe:MagnetShape.Bar,pole==0,yaw*rotation,1.5f,center.transform);
                    if(kind>=3){MagnetVisuals.Product(m,MagnetProduct.Ring,1.5f,yaw,pole==0);if(kind==4){m.geometry.rotation=yaw*Quaternion.Euler(90,0,0);MagnetVisuals.Ground(m);m.walkable=false;}}
                    b.magnets=new[]{m};player.transform.position=new Vector3(-dir.x*6,0,-dir.y*6);root.SetActive(true);b.CaptureInitialState();var original=m.Pose;var originalScale=m.geometry.localScale;
                    Check(b.TryStep(dir),"Deposit rejected "+b.LastRule);typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(b,null);
                    Check(source.Cargo==m&&!m.gameObject.activeSelf&&Quaternion.Angle(m.Pose,original)<.01f,"Deposit changed pose or lost cargo");checks++;
                    Check(b.PlayerCell==-dir*3,"Push moved player inside portal prematurely");checks++;
                    Check(b.UndoStep()&&!source.Cargo&&m.gameObject.activeSelf,"Undo deposit failed");checks++;
                    Check(b.TryStep(dir),"Repeat deposit rejected");typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(b,null);
                    b.RecordPortalJourney();BlackHoleTravel.ExitPlan plan;
                    Check(BlackHoleTravel.PlanExit(target,dir,m,out plan),"Exit rejected");checks++;
                    bool kick=kind==1||kind==4;Check(plan.kick==kick,"Wrong posture branch");checks++;
                    var blocker=b.TileAt(kick?dir*2:dir);blocker.blocked=true;
                    Check(!BlackHoleTravel.PlanExit(target,dir,m,out plan),"Obstacle was ignored");checks++;blocker.blocked=false;
                    Check(BlackHoleTravel.PlanExit(target,dir,m,out plan),"Unblocked exit rejected");
                    player.transform.SetPositionAndRotation(Vector3.zero,Quaternion.LookRotation(new Vector3(dir.x,0,dir.y)));
                    var host=new GameObject("Ejection runner");host.transform.SetParent(root.transform);var travel=host.AddComponent<BlackHoleTravel>();
                    bool sawCargoFirst=false;int frames=0;
                    Drain((IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",Private).Invoke(travel,new object[]{target,dir,m,plan}),()=>{
                        frames++;Check(Vector3.Distance(m.geometry.localScale,originalScale)<.001f,"Cargo changed size during ejection");if(m.gameObject.activeSelf&&player.transform.localScale.sqrMagnitude<.001f){sawCargoFirst=true;Check(Quaternion.Angle(m.Pose,original)<.01f,"Cargo rotated before player emerged");}
                    });
                    Check(sawCargoFirst&&frames>0,"Cargo/player order untested");checks++;
                    Check(b.PlayerCell==dir,"Player left through wrong side");checks++;
                    Check(Vector3.Distance(m.transform.position,kick?plan.kicked:plan.magnet)<.001f&&!source.Cargo,"Cargo final position wrong");checks++;
                    if(kick)Check(kind==4?MagnetPiece.FlatU(m.Pose):!MagnetPiece.VerticalBar(m.Pose),"Upright cargo did not fall");
                    else Check(player.transform.position.y>m.transform.position.y&&Quaternion.Angle(m.Pose,original)<.01f,"Player not standing on flat cargo");checks++;
                    Check(b.UndoStep()&&source.Cargo==m&&!m.gameObject.activeSelf,"Undo journey failed");checks++;
                    b.ResetPuzzle();Check(!source.Cargo&&m.gameObject.activeSelf&&Quaternion.Angle(m.Pose,original)<.01f,"Reset duplicated/lost cargo");checks++;
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            return checks;
        }
    }
}
