using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace BetweenPoles.EditorTools {
static partial class BlackHoleTestRegression {
    static int ExitObstacleCases(){
        int checks=0;
        foreach(var dir in new[]{Vector2Int.left,Vector2Int.up,Vector2Int.right,Vector2Int.down})for(int kind=0;kind<4;kind++){
            var root=new GameObject("Exit obstacle regression");root.SetActive(false);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;var actor=new GameObject("Player");actor.transform.SetParent(root.transform);b.player=actor.transform;
                var center=new GameObject("Center");center.transform.SetParent(root.transform);
                var lab=root.AddComponent<MagnetTestLayouts>();lab.board=b;lab.center=center.transform;lab.enabled=false;
                var tiles=new List<GridTile>();for(int x=-5;x<=5;x++)for(int z=-5;z<=5;z++){
                    var go=new GameObject("Tile");go.transform.SetParent(root.transform);go.transform.position=new Vector3(x*1.5f,0,z*1.5f);tiles.Add(go.AddComponent<GridTile>());
                }
                b.tiles=tiles.ToArray();b.magnets=new MagnetPiece[0];var source=lab.PlacePortal(b.TileAt(-dir*2));var target=lab.PlacePortal(b.TileAt(Vector2Int.zero));
                var yaw=Quaternion.FromToRotation(Vector3.right,new Vector3(dir.x,0,dir.y));
                var original=new Vector3(-dir.x*4.5f,0,-dir.y*4.5f);
                var cargo=MagnetVisuals.Create(root.transform,original,MagnetShape.Bar,true,yaw,1.5f,center.transform);
                MagnetPiece receiver=null;var exit=b.TileAt(dir);
                if(kind==0){receiver=MagnetVisuals.Create(root.transform,exit.transform.position,MagnetShape.Bar,false,yaw,1.5f,center.transform);b.magnets=new[]{cargo,receiver};}
                else{
                    exit.blocked=true;exit.rockStyle=kind==1?2:kind==2?0:1;
                    var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);stone.name="调试高台";stone.transform.SetParent(exit.transform,false);b.magnets=new[]{cargo};
                }
                b.player.position=original-new Vector3(dir.x,0,dir.y)*1.5f;root.SetActive(true);b.CaptureInitialState();
                Check(b.TryStep(dir),"Obstacle test deposit failed");typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(b,null);b.RecordPortalJourney();
                BlackHoleTravel.ExitPlan plan;Check(BlackHoleTravel.PlanExit(target,dir,cargo,out plan),"Obstacle exit rejected "+kind);checks++;
                Check(kind==0?plan.receiver==receiver:plan.bounce,"Wrong obstacle branch");checks++;
                if(kind>0)Check(plan.magnet.magnitude<.3f,"Flat cargo penetrates stone before bouncing");
                b.player.position=Vector3.zero;var travel=root.AddComponent<BlackHoleTravel>();float peak=0;bool first=true;var originalScale=cargo.geometry.localScale;
                Drain((IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",Private).Invoke(travel,new object[]{target,dir,cargo,plan}),()=>{
                    peak=Mathf.Max(peak,b.player.position.y);
                    if(kind>0)Check(Vector3.Distance(cargo.geometry.localScale,originalScale)<.001f,"Bounce changed cargo scale");
                    if(b.player.localScale.sqrMagnitude>.01f){
                        if(kind==0)Check(receiver.product==MagnetProduct.WideBar,"Player emerged before assembly");
                        else Check(Vector3.Distance(cargo.transform.position,original)<.001f,"Player emerged before cargo returned");
                        first=false;
                    }
                });
                Check(!first&&peak>b.player.position.y+.5f&&b.PlayerCell==dir,"Throw arc or landing failed");checks++;
                if(kind==0)Check(receiver.product==MagnetProduct.WideBar&&!cargo.gameObject.activeSelf&&Mathf.Abs(b.player.position.y-.24f)<.01f,"Wide bar landing wrong");
                else Check(cargo.gameObject.activeSelf&&!source.Cargo&&Vector3.Distance(cargo.transform.position,original)<.001f&&b.player.position.y>.5f,"Stone bounce/landing wrong");checks++;
                var platform=b.TileAt(dir*2);
                if(kind<3){platform.surfaceHeight=b.LayerHeight*2;Check(!b.TryStep(dir),"Climbed two layers from low support");}
                platform.surfaceHeight=b.LayerHeight;
                Check(b.TryStep(dir),"Cannot step to one-layer platform: "+b.LastRule);Drain((IEnumerator)typeof(GridPlayground).GetMethod("WalkOffObject",Private).Invoke(b,new object[]{dir*2,platform.transform.position+Vector3.up*b.LayerHeight}));b.StopAllCoroutines();
                Check(Mathf.Abs(b.player.position.y-b.LayerHeight)<.01f,"Platform height wrong");checks++;
                b.UndoStep();Check(b.UndoStep()&&source.Cargo==cargo&&!cargo.gameObject.activeSelf,"Journey undo lost cargo");checks++;
                if(receiver)Check(receiver.product==MagnetProduct.None,"Undo retained merged receiver");
                b.player.position=new Vector3(dir.x*1.5f,0,dir.y*1.5f);Check(!b.TryStep(dir),"Ground player climbed platform");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
}
}
