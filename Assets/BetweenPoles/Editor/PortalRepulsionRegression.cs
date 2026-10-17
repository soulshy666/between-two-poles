using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
public static class PortalRepulsionRegression {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    [MenuItem("两极之间/测试/悬浮圆环地面规则")]
    public static void RunRingTerrain(){
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Ring terrain fixture");root.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try{
            var b=root.AddComponent<GridPlayground>();b.enabled=false;
            b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);b.player.position=Vector3.left*b.cellSize;
            var tileObject=new GameObject("Portal tile");tileObject.transform.SetParent(root.transform);
            var tile=tileObject.AddComponent<GridTile>();b.tiles=new[]{tile};
            var portal=tileObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tile;
            var ring=MagnetVisuals.Create(root.transform,Vector3.right*b.cellSize,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
            MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,Quaternion.identity,true);ring.walkable=true;
            var u=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,false,Quaternion.identity,b.cellSize,root.transform);
            b.magnets=new[]{ring,u};root.SetActive(true);u.storedInPortal=portal;u.gameObject.SetActive(false);
            BlackHoleTravel.ExitPlan plan;
            Check(BlackHoleTravel.PlanExit(portal,Vector2Int.right,u,out plan)&&!plan.unstableLanding&&!plan.receiver&&!plan.displaced,"Floating floor treated as cargo obstacle");
            Check(Vector3.Distance(plan.magnet,ring.transform.position)<.001f,"Cargo floor height changed");
            u.storedInPortal=null;u.gameObject.SetActive(true);u.transform.position=ring.transform.position;
            Check(BlackHoleTravel.PlanExit(portal,Vector2Int.right,null,out plan)&&!plan.unstableLanding,"Supported U treated as unsupported");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var floor=typeof(GridPlayground).GetMethod("Floor",flags);
            var supported=typeof(GridPlayground).GetMethod("Supported",flags);
            Check((bool)floor.Invoke(b,new object[]{Vector2Int.right})&&(bool)supported.Invoke(b,new object[]{u}),"Floor/support missing");
            var incomingU=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
            var portalRecipe=typeof(BlackHoleTravel).GetMethod("PortalCargoRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
                float degrees;
                var stored=MagnetPiece.Rolled(MagnetShape.Horseshoe,Quaternion.LookRotation(new Vector3(dir.x,0,dir.y)),dir,out degrees);
                incomingU.geometry.rotation=stored;u.geometry.rotation=stored;
                Check((MagnetProduct)portalRecipe.Invoke(null,new object[]{incomingU,u,true,dir})==MagnetProduct.ThickU,"Same-facing portal U pair incorrectly formed ring");
                incomingU.storedInPortal=portal;incomingU.gameObject.SetActive(false);b.magnets=new[]{ring,u,incomingU};
                Check(BlackHoleTravel.PlanExit(portal,Vector2Int.right,incomingU,out plan)&&plan.receiver==u&&plan.product==MagnetProduct.ThickU,"Portal rejected U stack on ring terrain");
                Check(Quaternion.Angle(incomingU.Pose,stored)<.001f,"Portal planning changed saved entry pose");
            }
            UnityEngine.Object.DestroyImmediate(incomingU.gameObject);u.geometry.rotation=Quaternion.identity;b.magnets=new[]{ring,u};
            var rider=MagnetVisuals.Create(root.transform,ring.transform.position,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
            MagnetVisuals.Product(rider,MagnetProduct.Ring,b.cellSize,Quaternion.identity,true);
            MagnetVisuals.Ground(ring);MagnetVisuals.Ground(rider);
            ring.geometry.position-=Vector3.up*.24f;
            var shore=typeof(GridPlayground).GetMethod("RingShore",flags);
            foreach(bool reverse in new[]{false,true}){
                b.magnets=reverse?new[]{rider,ring}:new[]{ring,rider};
                Check((bool)shore.Invoke(b,new object[]{ring})&&!(bool)shore.Invoke(b,new object[]{rider}),"Upper ring became floating terrain");
                Check(!(bool)supported.Invoke(b,new object[]{ring})&&(bool)supported.Invoke(b,new object[]{rider}),"Ring platform/rider support inverted");
                Check(b.MagnetAt(Vector2Int.right)==rider,"Selected platform instead of movable rider");
                Check(b.CanPortalRingEnter(rider,Vector2Int.right,0,Vector2Int.right*2),"Rider cannot leave platform to next space cell");
            }
            UnityEngine.Object.DestroyImmediate(rider.gameObject);b.magnets=new[]{ring,u};
            u.combined=true;u.product=MagnetProduct.CrossU;u.walkable=true;
            foreach(bool reverse in new[]{false,true}){
                b.magnets=reverse?new[]{u,ring}:new[]{ring,u};
                Check(b.MagnetAt(Vector2Int.right)==u,"Platform hides upper assembly depending on array order");
                Check((bool)floor.Invoke(b,new object[]{Vector2Int.right}),"Assembly removed supporting floor");
            }
            Debug.Log("PASS: ring terrain, cargo landing, supported actor, fixed assembly and order-independent occupancy.");
        }finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("两极之间/测试/圆环出洞推动与受阻")]
    public static void RunRingExitPush(){
        var scene=EditorSceneManager.NewPreviewScene();int checks=0;
        try{
            var planner=typeof(BlackHoleTravel).GetMethod("TryRingExitPush",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
                var root=new GameObject("Ring exit push fixture");root.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var board=root.AddComponent<GridPlayground>();board.enabled=false;
                board.player=new GameObject("Player").transform;board.player.SetParent(root.transform);board.player.position=Vector3.one*50;
                var floor=new GridTile[4];var forward=new Vector3(dir.x,0,dir.y);
                for(int i=0;i<4;i++){
                    var tileObject=new GameObject("Floor "+i);tileObject.transform.SetParent(root.transform);tileObject.transform.position=forward*i*board.cellSize;
                    floor[i]=tileObject.AddComponent<GridTile>();
                }
                board.tiles=floor;
                var portal=floor[0].gameObject.AddComponent<BlackHolePortal>();portal.board=board;portal.tile=floor[0];
                var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,board.cellSize,root.transform);
                MagnetVisuals.Product(ring,MagnetProduct.Ring,board.cellSize,Quaternion.Euler(90,0,0),true);
                ring.storedInPortal=portal;ring.gameObject.SetActive(false);
                var receiver=MagnetVisuals.Create(root.transform,forward*board.cellSize,MagnetShape.Bar,false,Quaternion.identity,board.cellSize,root.transform);
                board.magnets=new[]{ring,receiver};root.SetActive(true);board.InvalidateTileLookup();
                var start=receiver.transform.position;var pose=receiver.Pose;
                foreach(bool north in new[]{false,true})foreach(float speed in new[]{0f,4f}){
                    receiver.north=north;ring.portalRollSpeed=speed;
                    object[] args={portal,ring,receiver,dir,Vector3.zero};
                    Check((bool)planner.Invoke(null,args),"Free cell rejected: "+dir);
                    Check(Vector3.Distance((Vector3)args[4],forward*board.cellSize*2)<.001f,"Not exactly one cell");
                    floor[2].blocked=true;board.InvalidateTileLookup();
                    Check(!(bool)planner.Invoke(null,args),"Pushed receiver into stone");
                    floor[2].blocked=false;floor[2].surfaceHeight=board.LayerHeight;board.InvalidateTileLookup();
                    Check(!(bool)planner.Invoke(null,args),"Pushed receiver onto higher wall");
                    floor[2].surfaceHeight=0;board.InvalidateTileLookup();
                    Check(receiver.transform.position==start&&Quaternion.Angle(receiver.Pose,pose)<.001f,"Planner mutated receiver");
                    Check(ring.storedInPortal==portal&&ring.portalRollSpeed==speed,"Planner changed stored momentum");checks++;
                }
                receiver.combined=true;receiver.product=MagnetProduct.CrossU;
                Check(!(bool)planner.Invoke(null,new object[]{portal,ring,receiver,dir,Vector3.zero}),"Moved fixed assembly");
                // After the cargo's first shove, the actor must topple it and
                // push the flat receiver one more cell, in every exit direction.
                MagnetVisuals.Product(receiver,MagnetProduct.Ring,board.cellSize,Quaternion.identity,true);
                receiver.geometry.rotation=Quaternion.identity;receiver.walkable=true;
                receiver.transform.position=forward*board.cellSize*2;MagnetVisuals.Ground(receiver);
                ring.storedInPortal=null;ring.portalRollSpeed=0;ring.gameObject.SetActive(true);
                ring.transform.position=forward*board.cellSize;
                ring.geometry.rotation=Quaternion.AngleAxis(90,new Vector3(dir.y,0,-dir.x));
                ring.walkable=false;MagnetVisuals.Ground(ring);
                BlackHoleTravel.ExitPlan followup;
                Check(BlackHoleTravel.PlanExit(portal,dir,null,out followup)&&followup.kick
                    &&followup.exitKickTarget==ring&&followup.kickDisplaced==receiver,"Actor skipped ring chain kick");
                Check(Vector3.Distance(followup.kicked,forward*board.cellSize*2)<.001f
                    &&Vector3.Distance(followup.kickDisplacedPosition,forward*board.cellSize*3)<.001f,"Ring chain endpoints incorrect");
                Check(MagnetPiece.FlatU(Quaternion.AngleAxis(90,new Vector3(dir.y,0,-dir.x))*ring.Pose)
                    &&MagnetPiece.FlatU(receiver.Pose),"Chain does not finish with both rings flat");
                var singleU=MagnetVisuals.Create(root.transform,receiver.transform.position,MagnetShape.Horseshoe,true,Quaternion.identity,board.cellSize,root.transform);
                var impactDegrees=typeof(BlackHoleTravel).GetMethod("ImpactPushDegrees",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                var applyImpact=typeof(BlackHoleTravel).GetMethod("ApplyImpactPush",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                float uDegrees=(float)impactDegrees.Invoke(null,new object[]{singleU,dir});
                Check(Mathf.Abs(uDegrees-180)<.001f,"Impact failed to flip U by half turn");
                var uStart=singleU.transform.position;var uPose=singleU.Pose;
                applyImpact.Invoke(null,new object[]{singleU,uStart,uStart+forward*board.cellSize,uPose,new Vector3(dir.y,0,-dir.x),uDegrees,1f});
                Check(Vector3.Dot(singleU.Pose*Vector3.up,uPose*Vector3.up)<-.99f,"U impact translated without flipping face");
                singleU.transform.position=uStart;singleU.geometry.rotation=uPose;MagnetVisuals.Ground(singleU);
                Check(Mathf.Abs((float)impactDegrees.Invoke(null,new object[]{ring,dir})-90)<.001f,"Upright ring impact did not topple");
                receiver.gameObject.SetActive(false);board.magnets=new[]{ring,singleU};
                Check(BlackHoleTravel.PlanExit(portal,dir,null,out followup)&&followup.kickDisplaced==singleU
                    &&Vector3.Distance(followup.kickDisplacedPosition,forward*board.cellSize*3)<.001f,"Toppling ring did not shove single flat U one cell");
                floor[3].blocked=true;board.InvalidateTileLookup();
                Check(!BlackHoleTravel.PlanExit(portal,dir,null,out followup),"Toppling ring shoved U through stone");
                floor[3].blocked=false;board.InvalidateTileLookup();
                singleU.combined=true;singleU.product=MagnetProduct.CrossU;
                Check(!BlackHoleTravel.PlanExit(portal,dir,null,out followup),"Toppling ring shoved fixed U assembly");
                singleU.gameObject.SetActive(false);receiver.gameObject.SetActive(true);board.magnets=new[]{ring,receiver};
                floor[3].blocked=true;board.InvalidateTileLookup();
                Check(!BlackHoleTravel.PlanExit(portal,dir,null,out followup),"Chain pushed flat ring through stone");
                floor[3].blocked=false;floor[3].surfaceHeight=board.LayerHeight;board.InvalidateTileLookup();
                Check(!BlackHoleTravel.PlanExit(portal,dir,null,out followup),"Chain pushed flat ring up wall");
                checks++;
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("PASS: "+checks+" ring exit push cases, four directions, both poles, rolling/nonrolling, stone and height obstruction.");
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("两极之间/测试/黑洞共享存储")]
    public static void RunSharedStorage(){
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            var root=new GameObject("Shared portal storage fixture");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var board=root.AddComponent<GridPlayground>();board.enabled=false;
            var other=new GameObject("Other board");other.transform.SetParent(root.transform);
            var otherBoard=other.AddComponent<GridPlayground>();otherBoard.enabled=false;otherBoard.magnets=new MagnetPiece[0];
            var portals=new BlackHolePortal[3];
            for(int i=0;i<3;i++){
                var go=new GameObject("Mouth "+i);go.transform.SetParent(root.transform);go.transform.position=Vector3.right*i*3;
                var tile=go.AddComponent<GridTile>();var portal=go.AddComponent<BlackHolePortal>();portal.board=board;portal.tile=tile;portals[i]=portal;
            }
            var cargoObject=new GameObject("Stored magnet");cargoObject.transform.SetParent(root.transform);
            var cargo=cargoObject.AddComponent<MagnetPiece>();cargo.geometry=new GameObject("Geometry").transform;cargo.geometry.SetParent(cargo.transform);
            cargo.geometry.rotation=Quaternion.Euler(90,0,45);cargo.portalRollSpeed=4;cargo.portalEntryPosition=Vector3.left;
            cargo.gameObject.SetActive(false);board.magnets=new[]{cargo};
            var direction=typeof(BlackHoleTravel).GetMethod("CargoExitDirection",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            for(int i=0;i<3;i++){
                cargo.storedInPortal=portals[i];cargo.portalEntryPosition=portals[i].transform.position-Vector3.right*board.cellSize;
                foreach(var mouth in portals)Check(mouth.Cargo==cargo,"Different mouth cannot retrieve stored cargo");
                Check(cargo.storedInPortal==portals[i]&&cargo.portalRollSpeed==4&&Quaternion.Angle(cargo.Pose,Quaternion.Euler(90,0,45))<.001f,"Storage query changed entry state");
                Check((Vector2Int)direction.Invoke(null,new object[]{cargo,Vector2Int.down})==Vector2Int.right,"Cargo direction followed player instead of stored entry");
            }
            portals[2].board=otherBoard;otherBoard.magnets=new[]{cargo};
            Check(!portals[0].Cargo&&portals[2].Cargo==cargo,"Storage leaked across boards");
            cargo.storedInPortal=null;Check(!portals[2].Cargo,"Ejected cargo still occupies storage");
            Debug.Log("PASS: shared retrieval, preserved pose/momentum/direction, board isolation and cleared storage.");
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
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
            checks+=WideBarKickCases(scene);
            checks+=DestinationCases(scene);
            checks+=UprightEntryFlatExitCases(scene);
            checks+=OrdinaryMovementCases(scene);
            checks+=EmptySpaceLedgeCases(scene);
            checks+=SkipSettlementCases(scene);
            checks+=GroundBarInertiaCases(scene);
            checks+=IndependentCargoCases(scene);
            checks+=BlockedChainCases(scene);
            checks+=RingResponseCases(scene);
            checks+=PortalRingToppleCases(scene);
            checks+=PortalRollingRingCases(scene);
            checks+=RingEditorCases(scene);
            checks+=FlatRingShoreCases(scene);
            checks+=RingRecipeCases(scene);
            Directory.CreateDirectory("output");File.WriteAllText("output/portal-repulsion.txt","PASS: "+checks+" checks; four directions; stone/high platform rebound; wide-bar support and visibility; kick onto ground/space/second portal; occupied portal rejection with animated return; cargo retention and mouth recovery.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/portal-repulsion.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    [InitializeOnLoadMethod]
    static void QueuePortalRollingCheck(){
        EditorApplication.delayCall+=()=>{
            const string request="output/portal-ring-roll.run";
            if(!File.Exists(request))return;
            File.Delete(request);RunPortalRollingCheck();
        };
    }
    [MenuItem("两极之间/测试/圆环黑洞动量")]
    public static void RunPortalRollingCheck(){
        var scene=EditorSceneManager.NewPreviewScene();
        try{int checks=PortalRollingRingCases(scene)+PortalRingToppleCases(scene)+IndependentRingReboundCases(scene)+PortalRingChainCases(scene)+PortalRingEntrySpeedCases(scene);File.WriteAllText("output/portal-ring-roll-result.txt","PASS: "+checks+" checks; four directions; rolling, floating, topple, independent rebound, collision chains and entry/exit speed continuity.");}
        catch(Exception ex){File.WriteAllText("output/portal-ring-roll-result.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static int PortalRollingRingCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var clock=typeof(BlackHoleTravel).GetField("settlingAnimation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})for(int mode=0;mode<3;mode++){
            var root=new GameObject("Portal rolling ring");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            bool savedClock=(bool)clock.GetValue(null);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;
                b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);
                var forward=new Vector3(dir.x,0,dir.y);var axis=new Vector3(dir.y,0,-dir.x);
                var tiles=new List<GridTile>();
                for(int n=0;n<(mode==1?3:4);n++){
                    var floor=new GameObject("Floor");floor.transform.SetParent(root.transform);floor.transform.position=forward*n*b.cellSize;
                    tiles.Add(floor.AddComponent<GridTile>());
                }
                if(mode==0)tiles[3].blocked=true;
                b.tiles=tiles.ToArray();var portal=tiles[0].gameObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tiles[0];
                BlackHolePortal second=null;
                if(mode==2){second=tiles[2].gameObject.AddComponent<BlackHolePortal>();second.board=b;second.tile=tiles[2];}
                var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                var pose=Quaternion.FromToRotation(Vector3.up,axis);
                MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,pose,true);ring.geometry.rotation=pose;MagnetVisuals.Ground(ring);
                ring.portalRollSpeed=b.cellSize*3;ring.storedInPortal=portal;ring.portalEntryPosition=-forward*b.cellSize;
                ring.gameObject.SetActive(false);b.magnets=new[]{ring};root.SetActive(true);
                var saved=typeof(GridPlayground).GetMethod("SavePiece",flags).Invoke(b,new object[]{ring});
                Check(Mathf.Abs((float)saved.GetType().GetField("portalRollSpeed").GetValue(saved)-ring.portalRollSpeed)<.001f,"Undo snapshot lost portal speed");
                BlackHoleTravel.ExitPlan plan;
                Check(BlackHoleTravel.PlanExit(portal,dir,ring,out plan)&&!plan.kick&&!plan.bounce,"Rolling ring was classified as a topple");
                plan.separateCargo=true;clock.SetValue(null,true);
                var travel=root.AddComponent<BlackHoleTravel>();
                Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{portal,dir,ring,plan}));
                Check((ring.transform.position-forward*(mode==1?3:2)*b.cellSize).sqrMagnitude<.0001f,"Rolling ring stopped in the wrong cell");
                Check(b.player.position==Vector3.zero&&!b.Busy,"Rolling cargo moved the player");
                if(mode==1)Check(ring.walkable&&MagnetPiece.FlatU(ring.Pose)&&ring.portalRollSpeed==0,"Space ring failed to settle");
                else Check(Quaternion.Angle(ring.Pose,pose)<.01f,"Rolling ring lost its rotation phase");
                Check(mode==2?second.Cargo==ring&&!ring.gameObject.activeSelf&&ring.portalRollSpeed>0:!ring.storedInPortal&&ring.gameObject.activeSelf,"Rolling ring storage/momentum state wrong");
                checks+=6;
            }finally{clock.SetValue(null,savedClock);UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int PortalRingToppleCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var clock=typeof(BlackHoleTravel).GetField("settlingAnimation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})foreach(float face in new[]{0f,180f})foreach(bool floatExit in new[]{false,true}){
            var root=new GameObject("Portal ring outward topple");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            bool savedClock=(bool)clock.GetValue(null);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;
                b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);
                var forward=new Vector3(dir.x,0,dir.y);var axis=new Vector3(dir.y,0,-dir.x);
                var tiles=new List<GridTile>();
                for(int n=0;n<(floatExit?1:2);n++){
                    var floor=new GameObject("Floor");floor.transform.SetParent(root.transform);floor.transform.position=forward*n*b.cellSize;
                    tiles.Add(floor.AddComponent<GridTile>());
                }
                b.tiles=tiles.ToArray();var portal=tiles[0].gameObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tiles[0];
                var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                var entryPose=Quaternion.AngleAxis(90,axis)*Quaternion.Euler(face,0,0);
                MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,entryPose,true);
                ring.geometry.rotation=entryPose;MagnetVisuals.Ground(ring);
                ring.storedInPortal=portal;ring.gameObject.SetActive(false);b.magnets=new[]{ring};root.SetActive(true);
                BlackHoleTravel.ExitPlan plan;
                Check(BlackHoleTravel.PlanExit(portal,dir,ring,out plan)&&plan.kick==!floatExit&&plan.floatRing==floatExit&&!plan.failedExit&&!plan.unstableLanding,"Wrong shore/space ring exit plan");
                var travel=root.AddComponent<BlackHoleTravel>();clock.SetValue(null,true);
                Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{portal,dir,ring,plan}));
                if(floatExit){
                    GridTile support;Vector3 top;
                    Check(BlackHolePortal.TrySurface(b,dir,out support,out top)&&(b.player.position-top).sqrMagnitude<.0001f,"Passenger did not land on the floating ring surface");
                }else Check(Quaternion.Angle(ring.Pose,Quaternion.AngleAxis(90,axis)*entryPose)<.01f,"Ring tipped backwards instead of continuing outward");
                Check((ring.transform.position-forward*(floatExit?1:2)*b.cellSize).sqrMagnitude<.0001f&&ring.walkable&&MagnetPiece.FlatU(ring.Pose),"Ring did not float flat in the expected space cell");
                Check(!ring.storedInPortal&&ring.gameObject.activeSelf&&!b.Busy&&b.PlayerCell==dir,"Ring exit did not release the player in the adjacent cell");
                Check(Mathf.Abs((float)typeof(GridPlayground).GetMethod("MagnetGeometryTop",flags).Invoke(b,new object[]{ring}))<.001f,"Floating ring surface lost shore height");
                checks+=5;
            }finally{clock.SetValue(null,savedClock);UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int RingRecipeCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var root=new GameObject("Universal ring recipe");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try{
            var near=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,1.5f,root.transform);
            var far=MagnetVisuals.Create(root.transform,Vector3.forward*1.5f,MagnetShape.Horseshoe,false,Quaternion.identity,1.5f,root.transform);
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
            for(int a=0;a<4;a++)for(int b=0;b<4;b++)foreach(bool same in new[]{false,true})foreach(bool supported in new[]{false,true}){
                var facing=Quaternion.LookRotation(new Vector3(dir.x,0,dir.y));
                near.geometry.rotation=facing*Quaternion.Euler(0,a*90,0);far.geometry.rotation=facing*Quaternion.Euler(0,b*90,0);far.north=same;
                var expected=!same&&a==0&&b==2?MagnetProduct.Ring:MagnetProduct.None;
                Check(GridPlayground.Recipe(near,near.Pose,far,supported,dir)==expected,"Ring recipe accepted non-facing openings or same poles");checks++;
            }
        }finally{UnityEngine.Object.DestroyImmediate(root);}
        return checks;
    }
    static int WideBarKickCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var root=new GameObject("Wide bar portal kick");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;
                b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
                var tiles=new List<GridTile>();
                foreach(int n in new[]{0,1,3}){
                    var go=new GameObject("Shore");go.transform.SetParent(root.transform);
                    go.transform.position=new Vector3(dir.x*n*b.cellSize,0,dir.y*n*b.cellSize);
                    tiles.Add(go.AddComponent<GridTile>());
                }
                b.tiles=tiles.ToArray();var portal=tiles[0].gameObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tiles[0];
                var islandA=new GameObject("Island A").transform;islandA.SetParent(root.transform);
                var islandB=new GameObject("Island B").transform;islandB.SetParent(root.transform);islandB.position=tiles[2].transform.position;
                for(int i=0;i<tiles.Count;i++)tiles[i].gameObject.AddComponent<IslandSurfaceAnchor>().center=i<2?islandA:islandB;
                var axis=new Vector3(dir.x,0,dir.y);var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                var bridge=MagnetVisuals.Create(root.transform,axis*b.cellSize*2,MagnetShape.Bar,true,yaw,b.cellSize,root.transform);
                MagnetVisuals.Product(bridge,MagnetProduct.WideBar,b.cellSize,yaw,true);
                var cargo=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Bar,false,yaw*Quaternion.Euler(0,0,90),b.cellSize,root.transform);
                cargo.storedInPortal=portal;cargo.gameObject.SetActive(false);b.magnets=new[]{bridge,cargo};root.SetActive(true);
                BlackHoleTravel.ExitPlan plan;
                Check(BlackHoleTravel.PlanExit(portal,dir,cargo,out plan)&&plan.kick,"Connected wide bar rejected same-portal cargo");checks++;
                Check((plan.kicked-axis*b.cellSize*2).sqrMagnitude<.000001f,"Kick used raised mesh height instead of shore deck");checks++;
                bridge.walkable=false;Check(!BlackHoleTravel.PlanExit(portal,dir,cargo,out plan),"Unstable wide bar accepted");bridge.walkable=true;checks++;
                var blocker=MagnetVisuals.Create(root.transform,bridge.transform.position,MagnetShape.Bar,false,yaw,b.cellSize,root.transform);
                b.magnets=new[]{bridge,cargo,blocker};Check(!BlackHoleTravel.PlanExit(portal,dir,cargo,out plan),"Occupied deck accepted");checks++;
                UnityEngine.Object.DestroyImmediate(blocker.gameObject);b.magnets=new[]{bridge,cargo};
                Check(BlackHoleTravel.PlanExit(portal,dir,cargo,out plan),"Clear deck rejected");
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                var travel=root.AddComponent<BlackHoleTravel>();
                var routine=(System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{portal,dir,cargo,plan});
                var stack=new Stack<System.Collections.IEnumerator>();stack.Push(routine);int guard=100000;
                while(stack.Count>0){
                    Check(--guard>0,"Wide bar kick stalled");var current=stack.Peek();
                    if(!current.MoveNext()){stack.Pop();continue;}
                    if(current.Current is System.Collections.IEnumerator child&&!(child is CustomYieldInstruction))stack.Push(child);
                }
                Check(!MagnetPiece.VerticalBar(cargo.Pose)&&(cargo.transform.position-plan.kicked).sqrMagnitude<.000001f,"Cargo did not lie on bridge");checks++;
                Check((bool)typeof(GridPlayground).GetMethod("Supported",flags).Invoke(b,new object[]{cargo}),"Cargo remains unsupported on bridge");checks++;
                Check((b.player.position-plan.player).sqrMagnitude<.000001f,"Player did not recover in vacated cell");checks++;
                var bridges=root.AddComponent<PrejoinedTestBridges>();bridges.board=b;
                bridge.GetComponent<IslandSurfaceAnchor>().center=islandA;
                cargo.GetComponent<IslandSurfaceAnchor>().center=islandA;
                var window=root.AddComponent<FiveIslandWindow>();window.board=b;window.bridges=bridges;
                window.rooms=new[]{new FiveIslandWindow.Room{center=islandA,surfaces=new Renderer[0]},new FiveIslandWindow.Room{center=islandB,surfaces=new Renderer[0]}};
                window.Show(0);
                foreach(bool reverse in new[]{false,true}){
                    b.magnets=reverse?new[]{cargo,bridge}:new[]{bridge,cargo};
                    typeof(FiveIslandWindow).GetMethod("RefreshBridgeLinks",flags).Invoke(window,null);
                    typeof(FiveIslandWindow).GetMethod("RefreshPieces",flags).Invoke(window,null);
                    Check(bridges.links.Length==1&&bridges.links[0].magnet==bridge,"Cargo replaced support bridge projection");checks++;
                    bridges.Refresh();var block=new MaterialPropertyBlock();
                    foreach(var renderer in bridge.GetComponentsInChildren<Renderer>()){
                        renderer.GetPropertyBlock(block);Check(renderer.enabled&&block.GetFloat("_BridgeEnabled")==1,"Supporting bridge lost presentation");
                    }
                }
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int FlatRingShoreCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var root=new GameObject("Flat ring shore slide");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;var forward=new Vector3(dir.x,0,dir.y);
                b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);b.player.position=-forward*b.cellSize;
                var floor=new GameObject("Floor");floor.transform.SetParent(root.transform);b.tiles=new[]{floor.AddComponent<GridTile>()};
                var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,Quaternion.identity,true);MagnetVisuals.Ground(ring);
                b.magnets=new[]{ring};root.SetActive(true);typeof(GridPlayground).GetField("recordingPush",flags).SetValue(b,true);
                var initial=ring.Pose;bool tilted=false,sunk=false;int guard=10000;
                var stack=new Stack<System.Collections.IEnumerator>();stack.Push((System.Collections.IEnumerator)typeof(GridPlayground).GetMethod("MoveRing",flags).Invoke(b,new object[]{ring,dir,Vector2Int.zero,1,true}));
                while(stack.Count>0){
                    Check(--guard>0,"Flat shore slide stalled");var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}
                    if(current.Current is System.Collections.IEnumerator child){stack.Push(child);continue;}
                    Check(Quaternion.Angle(ring.Pose,initial)<25,"Flat ring stood upright while leaving shore");
                    float travel=Vector3.Dot(ring.transform.position,forward);
                    if(travel<b.cellSize-.001f&&Quaternion.Angle(ring.Pose,initial)>2){
                        tilted=true;
                        var localFront=Quaternion.Inverse(initial)*forward;
                        Check((ring.Pose*localFront).y<0,"Leading edge tilted up instead of down");
                    }
                    if((float)typeof(GridPlayground).GetMethod("MagnetGeometryTop",flags).Invoke(b,new object[]{ring})<-.01f)sunk=true;
                }
                Check(tilted&&sunk,"Flat ring missed gradual tipping or sinking");checks++;
                Check(Quaternion.Angle(ring.Pose,initial)<.1f&&ring.walkable,"Flat ring changed coloured face or lost walkability");checks++;
                Check((ring.transform.position-forward*b.cellSize).sqrMagnitude<.0001f&&Mathf.Abs((float)typeof(GridPlayground).GetMethod("MagnetGeometryTop",flags).Invoke(b,new object[]{ring}))<.001f,"Flat ring missed final cell or shore height");checks++;
                Check((bool)typeof(GridPlayground).GetMethod("ShouldHover",flags).Invoke(b,new object[]{ring}),"Walkable space ring excluded from hovering");checks++;
                b.player.position=ring.transform.position;
                var visual=new GameObject("Visual").transform;visual.SetParent(b.player,false);
                var logical=b.player.position;var rest=visual.position;
                Check(Mathf.Abs((float)typeof(GridPlayground).GetMethod("RingRiderWeight",flags).Invoke(b,new object[]{ring})-1)<.001f,"Ring rider does not fully follow at rest");checks++;
                foreach(float bob in new[]{.09f,-.09f}){
                    typeof(GridPlayground).GetMethod("RenderRingRider",flags).Invoke(b,new object[]{bob});
                    Check(Vector3.Distance(visual.position,rest+Vector3.up*bob)<.001f&&b.player.position==logical,"Rider bob changed grid state or did not follow both directions");checks++;
                    typeof(GridPlayground).GetMethod("RestoreMagnetHover",flags).Invoke(b,new object[]{null});
                    Check(visual.position==rest,"Rider render offset accumulated between cameras");checks++;
                }
                b.player.position=logical-forward*b.cellSize;
                Check((float)typeof(GridPlayground).GetMethod("RingRiderWeight",flags).Invoke(b,new object[]{ring})==0,"Rider kept floating after leaving ring");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int RingEditorCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        for(int pose=0;pose<3;pose++)for(int yaw=0;yaw<4;yaw++){
            var root=new GameObject("Ring editor roundtrip");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.magnets=new MagnetPiece[0];
                b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);b.player.position=Vector3.left*b.cellSize;
                var floor=new GameObject("Floor");floor.transform.SetParent(root.transform);b.tiles=new[]{floor.AddComponent<GridTile>()};
                var panel=root.AddComponent<MagnetDebugPanel>();panel.enabled=false;root.SetActive(true);
                var type=typeof(MagnetDebugPanel);
                type.GetField("board",flags).SetValue(panel,b);type.GetField("selected",flags).SetValue(panel,Vector2Int.zero);
                type.GetField("shape",flags).SetValue(panel,3);type.GetField("pose",flags).SetValue(panel,pose);type.GetField("yaw",flags).SetValue(panel,yaw);
                Check(panel.Place(),"Ring editor rejected placement");checks++;
                var ring=b.MagnetAt(Vector2Int.zero);var expected=Quaternion.Euler(0,yaw*90,0)*Quaternion.Euler(pose==1?90:pose==2?180:0,0,0);
                Check(ring&&Quaternion.Angle(ring.Pose,expected)<.1f&&ring.walkable==(pose!=1),"Ring editor lost pose or walkability");checks++;
                type.GetMethod("SelectCell",flags).Invoke(panel,new object[]{Vector2Int.zero});
                Check((int)type.GetField("pose",flags).GetValue(panel)==pose&&(int)type.GetField("yaw",flags).GetValue(panel)==yaw,"Ring selection lost controls");checks++;
                ring.geometry.rotation=Quaternion.Euler(35,23,12);b.ResetPuzzle();
                Check(Quaternion.Angle(ring.Pose,expected)<.1f&&ring.walkable==(pose!=1),"Ring reset lost edited orientation");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int RingResponseCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})for(int mode=0;mode<4;mode++)for(int obstacleKind=0;obstacleKind<(mode==2?3:1);obstacleKind++){
            var root=new GameObject("Ring response");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;var axis=new Vector3(dir.x,0,dir.y);
                b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);b.player.position=-axis*b.cellSize;
                var tiles=new List<GridTile>();for(int n=-1;n<=4;n++){var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=axis*n*b.cellSize;tiles.Add(go.AddComponent<GridTile>());}
                b.tiles=tiles.ToArray();var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                if(mode==2){b.tiles[b.tiles.Length-1].blocked=obstacleKind==0;if(obstacleKind==1)b.tiles[b.tiles.Length-1].surfaceHeight=b.LayerHeight;}
                if(mode==3)b.tiles=tiles.GetRange(0,4).ToArray();
                MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,Quaternion.identity,true);
                if(mode>0)ring.geometry.rotation=Quaternion.FromToRotation(Vector3.up,mode==1?axis:Vector3.Cross(Vector3.up,axis));
                MagnetVisuals.Ground(ring);ring.walkable=mode==0;b.magnets=new[]{ring};if(mode==2&&obstacleKind==2){
                    var blocker=MagnetVisuals.Create(root.transform,axis*4*b.cellSize,MagnetShape.Bar,false,Quaternion.identity,b.cellSize,root.transform);
                    b.magnets=new[]{ring,blocker};
                }
                root.SetActive(true);
                var initialPose=ring.Pose;var initialPosition=ring.transform.position;
                var history=(System.Collections.IList)typeof(GridPlayground).GetField("history",flags).GetValue(b);
                history.Add(typeof(GridPlayground).GetMethod("SaveWorld",flags).Invoke(b,null));
                typeof(GridPlayground).GetProperty("Busy").SetValue(b,true);
                typeof(GridPlayground).GetField("recordingPush",flags).SetValue(b,true);
                var routine=(System.Collections.IEnumerator)typeof(GridPlayground).GetMethod("MoveRing",flags).Invoke(b,new object[]{ring,dir,Vector2Int.zero,mode>=2?3:1,mode<2});
                Check(routine.MoveNext()&&Vector3.Dot(ring.transform.position,axis)>0,"Ring did not respond on first animation frame");checks++;
                Check((bool)typeof(GridPlayground).GetField("interruptiblePush",flags).GetValue(b),"Ring disabled push interruption");checks++;
                if(mode<2){
                    Check((bool)typeof(GridPlayground).GetField("flowingPush",flags).GetValue(b),"Ring tipping bypassed continuous push clock");checks++;
                    typeof(GridPlayground).GetField("pushUndo",flags).SetValue(b,typeof(GridPlayground).GetMethod("SaveWorld",flags).Invoke(b,null));
                    Check(!(bool)typeof(GridPlayground).GetMethod("CanInterruptPush",flags).Invoke(b,new object[]{dir,true}),"Same-direction ring tap restarted animation");checks++;
                    Check((bool)typeof(GridPlayground).GetMethod("CanInterruptPush",flags).Invoke(b,new object[]{-dir,true}),"Ring direction change lost responsiveness");checks++;
                }
                bool sunk=false,turned=false,rose=false;
                float peak=0;bool reversed=false;var stack=new Stack<System.Collections.IEnumerator>();stack.Push(routine);int guard=10000;
                while(stack.Count>0){
                    Check(--guard>0,"Ring motion stalled");var current=stack.Peek();
                    if(!current.MoveNext()){stack.Pop();continue;}
                    if(current.Current is System.Collections.IEnumerator child){stack.Push(child);continue;}
                    if(mode>=2&&Vector3.Distance(b.player.position,Vector3.zero)<.001f){
                        var actorPose=b.player.GetComponent<PlayerPushPose>();
                        Check((bool)typeof(PlayerPushPose).GetField("flowReleaseRequested",flags).GetValue(actorPose),"Player held push pose after releasing rolling ring");
                    }
                    if(mode==3){
                        float travel=Vector3.Dot(ring.transform.position,axis);
                        bool flat=MagnetPiece.FlatU(ring.Pose);
                        float top=(float)typeof(GridPlayground).GetMethod("MagnetGeometryTop",flags).Invoke(b,new object[]{ring});
                        if(travel<3*b.cellSize-.001f)Check(!flat&&Mathf.Abs((ring.Pose*Vector3.up).y)<.001f,"Ring flattened while still crossing shore");
                        else{
                            if(!flat&&top<-.01f)sunk=true;
                            if(flat&&top<-.01f){Check(sunk,"Ring turned flat before sinking clear of shore");turned=true;}
                            if(turned&&top>-.05f)rose=true;
                        }
                    }
                    if(mode==2){
                        float offset=Vector3.Dot(ring.geometry.position-ring.transform.position,axis);
                        if(offset>0.001f){
                            var rollAxis=new Vector3(dir.y,0,-dir.x);
                            var expected=Quaternion.AngleAxis(540+180*offset/b.cellSize,rollAxis)*initialPose;
                            Check(Quaternion.Angle(ring.Pose,expected)<.1f,"Stone rebound rotation did not follow forward/reverse travel");
                            Check((ring.transform.position-axis*b.cellSize*3).sqrMagnitude<.0001f,"Stone rebound changed grid occupancy");
                            if(offset<peak-.001f)reversed=true;peak=Mathf.Max(peak,offset);
                        }
                    }
                }
                if(mode==3){
                    Check(sunk&&turned&&rose,"Ring space exit missed sinking, submerged turn or rising phase");checks++;
                    Check(Mathf.Abs((float)typeof(GridPlayground).GetMethod("MagnetGeometryTop",flags).Invoke(b,new object[]{ring}))<.001f,"Floating ring deck did not finish at shore height");checks++;
                }
                if(mode==2){Check(peak>b.cellSize*.01f&&reversed,"Ring did not roll into contact and recoil from stone");checks++;}
                Check((ring.transform.position-axis*b.cellSize*(mode>=2?3:1)).sqrMagnitude<.0001f&&!b.Busy,"Ring endpoint or input release changed");checks++;
                Check(MagnetPiece.FlatU(ring.Pose)==(mode==1||mode==3)&&ring.walkable==(mode==1||mode==3),"Ring final orientation/walkability changed");checks++;
                Check(b.UndoStep()&&Quaternion.Angle(ring.Pose,initialPose)<.1f&&ring.transform.position==initialPosition,"Ring undo did not restore grid pose");checks++;
                var rocks=(System.Collections.IDictionary)typeof(GridPlayground).GetField("barRocks",flags).GetValue(b);
                Check(rocks.Contains(ring),"Ring undo failed to start settling");checks++;
                var rock=rocks[ring];var rockType=rock.GetType();
                Check((bool)rockType.GetField("touchdown").GetValue(rock)==(mode==0)&&(bool)rockType.GetField("undoBounce").GetValue(rock)==(mode==0),"Ring undo chose wrong settling motion");checks++;
                rockType.GetField("started").SetValue(rock,Time.time-.15f);
                typeof(GridPlayground).GetMethod("RenderBarRocks",flags).Invoke(b,null);
                Check(Quaternion.Angle(ring.Pose,initialPose)>.1f&&ring.transform.position==initialPosition,"Ring undo settling was invisible or moved its grid cell");checks++;
                if(mode>0){
                    Check(Mathf.Abs((ring.Pose*Vector3.up).y)>.001f,"Upright ring undo rolled sideways instead of tilting across its face");checks++;
                }
                typeof(GridPlayground).GetMethod("RestoreMagnetHover",flags).Invoke(b,new object[]{null});
                Check(Quaternion.Angle(ring.Pose,initialPose)<.1f,"Ring undo visual motion polluted logical pose");checks++;
                var actorPosition=b.player.position;var ringPosition=ring.transform.position;
                b.player.rotation=Quaternion.LookRotation(Vector3.Cross(Vector3.up,axis));
                var blocked=(System.Collections.IEnumerator)typeof(GridPlayground).GetMethod("BlockedRingShove",flags).Invoke(b,new object[]{ring,dir});
                Check(blocked.MoveNext()&&Vector3.Dot(b.player.forward,axis)>.999f,"Blocked ring shove retained previous facing on its first frame");checks++;
                var response=blocked.Current as System.Collections.IEnumerator;
                Check(response!=null&&response.MoveNext()&&Quaternion.Angle(ring.Pose,initialPose)>.01f,"Blocked ring waited for player shove to finish before reacting");checks++;
                Check((float)typeof(PlayerPushPose).GetField("flowEffort",flags).GetValue(b.player.GetComponent<PlayerPushPose>())>0,"Ring response started without player effort");checks++;
                Drain(response);
                Drain(blocked);
                Check(Vector3.Dot(b.player.forward,axis)>.999f&&b.player.position==actorPosition&&ring.transform.position==ringPosition,"Blocked ring shove changed cell or lost push facing");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int BlockedChainCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var root=new GameObject("Blocked chain pulse");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);
                var axis=new Vector3(dir.x,0,dir.y);var tiles=new List<GridTile>();var magnets=new List<MagnetPiece>();
                for(int n=0;n<5;n++){
                    var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=axis*n*b.cellSize;var tile=go.AddComponent<GridTile>();tile.blocked=n==4;tiles.Add(tile);
                    if(n>0&&n<4)magnets.Add(MagnetVisuals.Create(root.transform,axis*n*b.cellSize,MagnetShape.Bar,true,Quaternion.identity,b.cellSize,root.transform));
                }
                b.tiles=tiles.ToArray();b.magnets=magnets.ToArray();root.SetActive(true);
                typeof(GridPlayground).GetField("recordingPush",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(b,true);
                var saved=new Vector3[3];for(int i=0;i<3;i++)saved[i]=magnets[i].geometry.position;
                var routine=(System.Collections.IEnumerator)typeof(GridPlayground).GetMethod("BlockedChainPulse",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(b,new object[]{magnets,dir});
                int outward=0,back=0,guard=10000;var moved=new bool[3];var returned=new bool[3];
                while(routine.MoveNext()){
                    Check(--guard>0,"Blocked pulse stalled");
                    for(int i=0;i<3;i++){
                        float offset=Vector3.Dot(magnets[i].geometry.position-saved[i],axis);
                        if(!moved[i]&&offset>.01f){Check(i==outward++,"Force propagated out of order");moved[i]=true;}
                        if(moved[i]&&!returned[i]&&offset<.001f){Check(i==2-back++,"Return force propagated out of order");returned[i]=true;}
                        Check((magnets[i].transform.position-axis*(i+1)*b.cellSize).sqrMagnitude<.0001f,"Failed push changed occupancy");
                    }
                }
                Check(outward==3&&back==3&&!b.Busy,"Pulse did not finish the outward and return chain");checks++;
                for(int i=0;i<3;i++){Check((magnets[i].geometry.position-saved[i]).sqrMagnitude<.0001f,"Pulse left a visual offset");checks++;}
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    [MenuItem("两极之间/测试/圆环跨越两格桥")]
    public static void RunRingBridgeCheck(){
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            int checks=0;
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
                var root=new GameObject("Ring bridge traversal");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                try{
                    var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);
                    var axis=new Vector3(dir.x,0,dir.y);b.player.position=-axis*2*b.cellSize;
                    var tiles=new List<GridTile>();
                    foreach(int n in new[]{-2,-1,2,3}){var go=new GameObject("Shore");go.transform.SetParent(root.transform);go.transform.position=axis*n*b.cellSize;tiles.Add(go.AddComponent<GridTile>());}
                    b.tiles=tiles.ToArray();
                    var bridge=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                    bridge.bridgeDirection=dir;MagnetVisuals.Product(bridge,MagnetProduct.Bridge,b.cellSize,Quaternion.LookRotation(axis),true);
                    var ring=MagnetVisuals.Create(root.transform,-axis*b.cellSize,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                    var pose=Quaternion.FromToRotation(Vector3.up,new Vector3(dir.y,0,-dir.x));
                    MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,pose,true);ring.geometry.rotation=pose;MagnetVisuals.Ground(ring);
                    b.magnets=new[]{bridge,ring};root.SetActive(true);
                    for(int n=0;n<=3;n++){Check(b.CanPortalRingEnter(ring,dir*(n-1),0,dir*n),"Connected bridge stopped ring at "+n);checks++;}
                    var side=new Vector2Int(-dir.y,dir.x);
                    Check(!b.CanPortalRingEnter(ring,-side,0,Vector2Int.zero),"Ring entered bridge sideways");checks++;
                    Check(!b.CanPortalRingEnter(ring,Vector2Int.zero,0,side),"Ring left bridge sideways");checks++;
                    tiles[2].surfaceHeight=b.LayerHeight;
                    Check(b.CanPortalRingEnter(ring,-dir,0,Vector2Int.zero),"Far shore incorrectly prevented entering completed bridge");checks++;
                    Check(!b.CanPortalRingEnter(ring,dir,0,dir*2),"Ring climbed wrong-height far shore");checks++;
                    tiles[2].surfaceHeight=0;
                    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                    ring.geometry.rotation=Quaternion.identity;ring.walkable=true;MagnetVisuals.Ground(ring);
                    typeof(GridPlayground).GetField("recordingPush",flags).SetValue(b,true);
                    for(int step=0;step<3;step++){
                        Check(b.TryStep(dir),"Flat/tipping ring push rejected at bridge step "+step);
                        typeof(GridPlayground).GetMethod("FinishMovement",flags).Invoke(b,null);
                        Check((ring.transform.position-axis*step*b.cellSize).sqrMagnitude<.001f,"Ring failed to advance across bridge at step "+step);
                        Check(b.PlayerCell==dir*(step-1),"Player failed to follow ring across bridge");checks+=3;
                    }
                    ring.transform.position=-axis*b.cellSize;b.player.position=-axis*2*b.cellSize;
                    bridge.product=MagnetProduct.BridgeHalf;bridge.walkable=false;
                    Check(!b.CanPortalRingEnter(ring,-dir,0,Vector2Int.zero),"Unfinished bridge accepted");checks++;
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            File.WriteAllText("output/ring-bridge-result.txt","PASS: "+checks+" checks; four directions, both bridge cells, shore exit, side entry/exit, height mismatch and unfinished bridge.");
        }catch(Exception ex){File.WriteAllText("output/ring-bridge-result.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static int PortalRingEntrySpeedCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var root=new GameObject("Portal ring entry speed");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);
                var forward=new Vector3(dir.x,0,dir.y);var axis=new Vector3(dir.y,0,-dir.x);
                var tiles=new List<GridTile>();
                for(int i=-2;i<=0;i++){var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=forward*i*b.cellSize;tiles.Add(go.AddComponent<GridTile>());}
                b.tiles=tiles.ToArray();var portal=tiles[2].gameObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tiles[2];
                var ring=MagnetVisuals.Create(root.transform,-forward*b.cellSize,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                var pose=Quaternion.FromToRotation(Vector3.up,axis);
                MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,pose,true);ring.geometry.rotation=pose;MagnetVisuals.Ground(ring);
                b.magnets=new[]{ring};b.player.position=-forward*2*b.cellSize;root.SetActive(true);
                typeof(GridPlayground).GetField("finishingMovement",flags).SetValue(b,true);
                Drain((System.Collections.IEnumerator)typeof(GridPlayground).GetMethod("PushIntoPortalAnimation",flags).Invoke(b,new object[]{ring,portal,dir}));
                float speed=(float)typeof(GridPlayground).GetMethod("RingRollSpeed",flags).Invoke(b,new object[]{1});
                foreach(int cells in new[]{2,5,12}){
                    float longRollSpeed=(float)typeof(GridPlayground).GetMethod("RingRollSpeed",flags).Invoke(b,new object[]{cells});
                    Check(Mathf.Abs(speed-longRollSpeed)<.0001f,"Short collision/portal route reduced ring cruise speed");checks++;
                }
                Check(portal.Cargo==ring&&!ring.gameObject.activeSelf&&Mathf.Abs(ring.portalRollSpeed-speed)<.0001f,"Adjacent portal entry lost ground cruise speed");checks++;
                Check(Quaternion.Angle(ring.Pose,Quaternion.AngleAxis(180,axis)*pose)<.01f,"Portal entry changed rotation phase");checks++;
                float cruise=b.cellSize/speed,acceleration=Mathf.Min(.14f,cruise),duration=cruise+acceleration*.5f;
                var distance=typeof(GridPlayground).GetMethod("RingRollDistance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
                float end=(float)distance.Invoke(null,new object[]{duration,speed,acceleration});
                float previous=(float)distance.Invoke(null,new object[]{duration-.001f,speed,acceleration});
                Check(Mathf.Abs(end-b.cellSize)<.0001f&&Mathf.Abs((end-previous)/.001f-ring.portalRollSpeed)<.001f,"Stored exit speed differs from actual entry velocity");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int PortalRingChainCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var clock=typeof(BlackHoleTravel).GetField("settlingAnimation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})for(int mode=0;mode<4;mode++){
            var root=new GameObject("Portal ring collision chain");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            bool savedClock=(bool)clock.GetValue(null);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);
                var forward=new Vector3(dir.x,0,dir.y);var axis=new Vector3(dir.y,0,-dir.x);var tiles=new List<GridTile>();
                for(int i=0;i<=5;i++){
                    var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=forward*i*b.cellSize;
                    var tile=go.AddComponent<GridTile>();tile.blocked=i==5;tiles.Add(tile);
                }
                b.tiles=tiles.ToArray();var source=tiles[0].gameObject.AddComponent<BlackHolePortal>();source.board=b;source.tile=tiles[0];
                var target=tiles[4].gameObject.AddComponent<BlackHolePortal>();target.board=b;target.tile=tiles[4];
                var pose=Quaternion.FromToRotation(Vector3.up,axis);
                var ring=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,pose,true);ring.geometry.rotation=pose;MagnetVisuals.Ground(ring);
                ring.storedInPortal=source;ring.portalEntryPosition=-forward*b.cellSize;ring.portalRollSpeed=b.cellSize*3;ring.gameObject.SetActive(false);
                var other=MagnetVisuals.Create(root.transform,forward*(mode==3?1:2)*b.cellSize,mode==1?MagnetShape.Bar:MagnetShape.Horseshoe,false,Quaternion.identity,b.cellSize,root.transform);
                var otherPose=mode==1?Quaternion.FromToRotation(Vector3.up,forward)*Quaternion.Euler(0,0,90):pose;
                if(mode!=1)MagnetVisuals.Product(other,MagnetProduct.Ring,b.cellSize,pose,true);
                else otherPose=Quaternion.Euler(0,0,90);
                other.geometry.rotation=otherPose;MagnetVisuals.Ground(other);
                MagnetPiece held=null;
                if(mode==2){held=MagnetVisuals.Create(root.transform,target.transform.position,MagnetShape.Bar,true,Quaternion.identity,b.cellSize,root.transform);held.storedInPortal=target;held.gameObject.SetActive(false);}
                b.magnets=held?new[]{ring,other,held}:new[]{ring,other};root.SetActive(true);clock.SetValue(null,true);
                var travel=root.AddComponent<BlackHoleTravel>();
                Check((bool)typeof(BlackHoleTravel).GetMethod("SeparateCargo",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{ring,dir}),"Same-direction ring was coupled to actor");checks++;
                Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("EjectIndependentCargo",flags).Invoke(travel,new object[]{source,dir,ring,Vector3.one}));
                if(mode==0||mode==3)Check(target.Cargo==other&&!other.gameObject.activeSelf&&other.portalRollSpeed>0,"Transferred ring failed to enter empty hole");
                else if(mode==2)Check(target.Cargo==held&&other.gameObject.activeSelf&&!other.storedInPortal&&(other.transform.position-forward*3*b.cellSize).sqrMagnitude<.001f,"Occupied portal lost cargo or accepted second ring");
                else Check(!MagnetPiece.VerticalBar(other.Pose)&&(other.transform.position-forward*3*b.cellSize).sqrMagnitude<.001f,"Portal ring failed to topple upright bar");checks++;
                Check(b.player.position==Vector3.zero&&b.player.localScale==Vector3.zero&&!b.Busy,"Collision chain released actor early");checks++;
            }finally{clock.SetValue(null,savedClock);UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int IndependentRingReboundCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var clock=typeof(BlackHoleTravel).GetField("settlingAnimation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})for(int mode=0;mode<4;mode++)foreach(bool remote in new[]{false,true}){
            var root=new GameObject("Independent ring rebound");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            bool savedClock=(bool)clock.GetValue(null);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);
                var actorDir=new Vector2Int(-dir.y,dir.x);var axis=new Vector3(dir.x,0,dir.y);
                var tiles=new List<GridTile>();
                foreach(var cell in new[]{Vector2Int.zero,dir,-dir,actorDir,new Vector2Int(8,8)}){
                    if(mode==1&&cell==-dir)continue;
                    var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=new Vector3(cell.x,0,cell.y)*b.cellSize;
                    var tile=go.AddComponent<GridTile>();tile.blocked=(mode!=3&&cell==dir)||(mode==2&&cell==-dir);
                    if(mode==3&&cell==dir)tile.surfaceHeight=b.LayerHeight;
                    tiles.Add(tile);
                }
                b.tiles=tiles.ToArray();var target=tiles[0].gameObject.AddComponent<BlackHolePortal>();target.board=b;target.tile=tiles[0];
                var source=target;
                if(remote){source=tiles[tiles.Count-1].gameObject.AddComponent<BlackHolePortal>();source.board=b;source.tile=tiles[tiles.Count-1];}
                var ring=MagnetVisuals.Create(root.transform,source.transform.position,MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,root.transform);
                var pose=Quaternion.FromToRotation(Vector3.up,axis);
                MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,pose,true);ring.geometry.rotation=pose;MagnetVisuals.Ground(ring);
                ring.storedInPortal=source;ring.portalEntryPosition=source.transform.position-axis*b.cellSize;
                ring.gameObject.SetActive(false);b.magnets=new[]{ring};root.SetActive(true);clock.SetValue(null,true);
                var travel=root.AddComponent<BlackHoleTravel>();
                Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("EjectIndependentCargo",flags).Invoke(travel,new object[]{target,dir,ring,Vector3.one}));
                Check(mode==2?target.Cargo==ring&&!ring.gameObject.activeSelf:!ring.storedInPortal&&ring.gameObject.activeSelf&&(ring.transform.position+axis*b.cellSize).sqrMagnitude<.001f,"Ring failed to reverse at selected mouth");checks++;
                Check(mode==1?ring.walkable&&MagnetPiece.FlatU(ring.Pose):Quaternion.Angle(ring.Pose,pose)<.01f,"Rebound ring lost expected pose");checks++;
                if(mode==2){
                    var next=(Vector2Int)typeof(BlackHoleTravel).GetMethod("CargoExitDirection",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{ring,actorDir});
                    Check(next==dir,"Stored ring lost its last rebound direction");checks++;
                }
                Check(b.player.localScale==Vector3.zero&&b.player.position==Vector3.zero,"Actor emerged before ring settled");checks++;
                BlackHoleTravel.ExitPlan plan;Check(BlackHoleTravel.PlanExit(target,actorDir,null,out plan),"Actor independent lane rejected");
                b.player.localScale=Vector3.one;
                Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{target,actorDir,null,plan}));
                Check(b.PlayerCell==actorDir&&!b.Busy,"Rebound redirected actor");checks++;
            }finally{clock.SetValue(null,savedClock);UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int IndependentCargoCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})foreach(bool blocked in new[]{false,true}){
            var root=new GameObject("Independent cargo exit");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);
                var tiles=new List<GridTile>();
                foreach(var cell in new[]{Vector2Int.zero,Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down}){
                    var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=new Vector3(cell.x,0,cell.y)*b.cellSize;
                    var tile=go.AddComponent<GridTile>();tile.blocked=blocked&&cell==dir;tiles.Add(tile);
                }
                b.tiles=tiles.ToArray();var portal=tiles[0].gameObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tiles[0];
                var axis=new Vector3(dir.x,0,dir.y);var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                var cargo=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Bar,true,yaw*Quaternion.Euler(0,0,90),b.cellSize,root.transform);
                cargo.storedInPortal=portal;cargo.portalEntryPosition=-axis*b.cellSize;cargo.gameObject.SetActive(false);b.magnets=new[]{cargo};root.SetActive(true);
                var actorDir=new Vector2Int(-dir.y,dir.x);var travel=root.AddComponent<BlackHoleTravel>();
                var resolved=(Vector2Int)typeof(BlackHoleTravel).GetMethod("CargoExitDirection",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{cargo,actorDir});
                Check(resolved==dir,"Player direction overwrote cargo entry direction");checks++;
                Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("EjectIndependentCargo",flags).Invoke(travel,new object[]{portal,resolved,cargo,Vector3.one}));
                Check(blocked?portal.Cargo==cargo&&!cargo.gameObject.activeSelf:cargo.gameObject.activeSelf&&!cargo.storedInPortal&&(cargo.transform.position-axis*b.cellSize).sqrMagnitude<.0001f,"Independent cargo lost its exit or storage");checks++;
                Check(MagnetPiece.VerticalBar(cargo.Pose),"Actor in another lane kicked upright cargo");checks++;
                BlackHoleTravel.ExitPlan plan;Check(BlackHoleTravel.PlanJourneyExit(portal,actorDir,null,out plan)&&!plan.failedExit,"Independent player lane rejected");
                b.player.localScale=Vector3.one;
                Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{portal,actorDir,null,plan}));
                Check(b.PlayerCell==actorDir&&!b.Busy,"Actor followed cargo lane");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int GroundBarInertiaCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})foreach(bool upright in new[]{false,true}){
            var root=new GameObject("Ground bar inertia");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;
                var axis=new Vector3(dir.x,0,dir.y);b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);b.player.position=-axis*b.cellSize;
                var tiles=new List<GridTile>();
                for(int n=-1;n<=1;n++){var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=axis*(n*b.cellSize);tiles.Add(go.AddComponent<GridTile>());}
                b.tiles=tiles.ToArray();var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                var initial=yaw*(upright?Quaternion.Euler(0,0,90):Quaternion.identity);
                var m=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Bar,true,initial,b.cellSize,root.transform);b.magnets=new[]{m};root.SetActive(true);
                typeof(GridPlayground).GetField("recordingPush",flags).SetValue(b,true);
                float degrees;var final=MagnetPiece.Rolled(m.shape,initial,dir,out degrees);
                var routine=(System.Collections.IEnumerator)typeof(GridPlayground).GetMethod("FlowingRoll",flags).Invoke(b,new object[]{m,new MagnetRollStep(dir,final,degrees),dir,Vector2Int.zero});
                var stack=new Stack<System.Collections.IEnumerator>();stack.Push(routine);float previous=0,slideStart=-1;int guard=10000;
                while(stack.Count>0){
                    Check(--guard>0,"Ground roll stalled");var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}
                    if(current.Current is System.Collections.IEnumerator child){stack.Push(child);continue;}
                    float travel=Vector3.Dot(m.transform.position,axis)/b.cellSize;
                    Check(travel>=previous-.0001f&&travel<=1.0001f,"Ground bar moved backward or beyond its destination");previous=travel;
                    if(upright&&slideStart<0&&(float)typeof(GridPlayground).GetField("pushFlowProgress",flags).GetValue(b)>=.78f)slideStart=travel;
                    float bottom=float.PositiveInfinity;
                    foreach(var f in m.geometry.GetComponentsInChildren<MeshFilter>())foreach(var v in f.sharedMesh.vertices)bottom=Mathf.Min(bottom,f.transform.TransformPoint(v).y);
                    Check(Mathf.Abs(bottom)<.001f,"Ground roll lost support contact");
                }
                Check((m.transform.position-axis*b.cellSize).sqrMagnitude<.0001f&&Quaternion.Angle(m.Pose,final)<.01f,"Ground inertia changed logical result");checks++;
                if(upright){Check(slideStart>=.89f&&slideStart<.97f,"Falling bar has no forward slide after touchdown");checks++;}
                typeof(GridPlayground).GetMethod("StartBarRock",flags).Invoke(b,new object[]{m,new Vector3(dir.y,0,-dir.x)});
                float take=(float)typeof(GridPlayground).GetMethod("TakeBarRock",flags).Invoke(b,new object[]{m,new Vector3(dir.y,0,-dir.x)});
                Check(((System.Collections.IDictionary)typeof(GridPlayground).GetField("barRocks",flags).GetValue(b)).Count==0,"Next push failed to take over settling motion");checks++;
                m.transform.position=Vector3.zero;m.geometry.rotation=initial;MagnetVisuals.Ground(m);
                b.player.position=-axis*b.cellSize;tiles[2].blocked=true;
                typeof(GridPlayground).GetField("recordingPush",flags).SetValue(b,false);
                Check(!b.TryStep(dir),"Blocked destination accepted push");
                Check(b.player.GetComponent<PlayerPushPose>().HasBlockedFeedback,"Rejected magnet push omitted effort/shrug feedback");checks++;
                Check(m.transform.position==Vector3.zero&&b.player.position==-axis*b.cellSize&&!b.Busy,"Blocked feedback changed occupancy or locked movement");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int SkipSettlementCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var clock=typeof(BlackHoleTravel).GetField("settlingAnimation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var root=new GameObject("Skip final settlement");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
                var axis=new Vector3(dir.x,0,dir.y);var tiles=new List<GridTile>();
                for(int n=0;n<3;n++){var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=axis*(n*b.cellSize);tiles.Add(go.AddComponent<GridTile>());}
                tiles[2].blocked=true;b.tiles=tiles.ToArray();var portal=tiles[0].gameObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tiles[0];
                var cargo=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Bar,true,Quaternion.FromToRotation(Vector3.right,axis)*Quaternion.Euler(0,0,90),b.cellSize,root.transform);
                b.magnets=new[]{cargo};cargo.storedInPortal=portal;cargo.gameObject.SetActive(false);root.SetActive(true);
                var travel=root.AddComponent<BlackHoleTravel>();BlackHoleTravel.ExitPlan plan;
                Check(BlackHoleTravel.PlanExit(portal,dir,cargo,out plan)&&plan.kickRebound,"Skip fixture rejected");
                clock.SetValue(null,true);
                Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{portal,dir,cargo,plan}));
                Check(!b.Busy&&b.player.position==Vector3.zero,"Skipped rebound did not release player at hole");checks++;
                Check(cargo.gameObject.activeSelf&&!cargo.storedInPortal&&MagnetPiece.VerticalBar(cargo.Pose)&&(cargo.transform.position-axis*b.cellSize).sqrMagnitude<.0001f,"Skip lost final upright magnet state");checks++;
            }finally{clock.SetValue(null,false);UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static void Drain(System.Collections.IEnumerator routine){
        var stack=new Stack<System.Collections.IEnumerator>();stack.Push(routine);int guard=100000;
        while(stack.Count>0){
            Check(--guard>0,"Portal destination animation stalled");var current=stack.Peek();
            if(!current.MoveNext()){stack.Pop();continue;}
            if(current.Current is System.Collections.IEnumerator child&&!(child is CustomYieldInstruction))stack.Push(child);
        }
    }
    static int EmptySpaceLedgeCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var root=new GameObject("Empty-space ledge exit");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.magnets=new MagnetPiece[0];
                b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
                var ground=new GameObject("Portal shore");ground.transform.SetParent(root.transform);var tile=ground.AddComponent<GridTile>();b.tiles=new[]{tile};
                var portal=ground.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tile;
                root.SetActive(true);var travel=root.AddComponent<BlackHoleTravel>();BlackHoleTravel.ExitPlan plan;
                Check(BlackHoleTravel.PlanJourneyExit(portal,dir,null,out plan)&&plan.failedExit,"Empty-space exit lost return branch");
                var axis=new Vector3(dir.x,0,dir.y);bool outside=false,caught=false,launched=false;int guard=100000;
                var stack=new Stack<System.Collections.IEnumerator>();
                stack.Push((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("FailedPortalExit",flags).Invoke(travel,new object[]{portal,dir,null,Vector3.one}));
                while(stack.Count>0){
                    Check(--guard>0,"Ledge return stalled");var current=stack.Peek();
                    if(!current.MoveNext()){stack.Pop();continue;}
                    if(current.Current is System.Collections.IEnumerator child){stack.Push(child);continue;}
                    float distance=Vector3.Dot(b.player.position,axis);
                    outside|=distance>b.cellSize*.5f;
                    caught|=outside&&b.player.position.y<-.7f&&Vector3.Dot(b.player.forward,-axis)>.99f;
                    launched|=caught&&b.player.position.y>.2f&&distance<b.cellSize*.5f;
                }
                Check(outside&&caught&&launched,"Missing outward jump, turned ledge catch or inward leap");checks++;
                Check(b.player.position==Vector3.zero&&b.player.localScale==Vector3.zero&&!b.Busy,"Ledge return did not finish hidden in hole");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int OrdinaryMovementCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})for(int kind=0;kind<3;kind++){
            var root=new GameObject("Ordinary magnet arrival");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
                var axis=new Vector3(dir.x,0,dir.y);b.player.position=-axis*b.cellSize;
                var tiles=new List<GridTile>();
                for(int i=-1;i<=3;i++){var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=axis*(i*b.cellSize);tiles.Add(go.AddComponent<GridTile>());}
                b.tiles=tiles.ToArray();var target=tiles[kind==0?2:3].gameObject.AddComponent<BlackHolePortal>();target.board=b;target.tile=tiles[kind==0?2:3];
                var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                var near=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Bar,true,yaw,b.cellSize,root.transform);
                MagnetPiece far=null;
                if(kind==1)far=MagnetVisuals.Create(root.transform,axis*b.cellSize,MagnetShape.Bar,true,yaw,b.cellSize,root.transform);
                if(kind==2){MagnetVisuals.Product(near,MagnetProduct.Ring,b.cellSize,yaw,true);near.geometry.rotation=yaw*Quaternion.Euler(90,0,0);near.walkable=false;MagnetVisuals.Ground(near);}
                b.magnets=far?new[]{near,far}:new[]{near};root.SetActive(true);
                Check(b.TryStep(dir),"Ordinary push rejected kind="+kind+": "+b.LastRule);
                typeof(GridPlayground).GetMethod("FinishMovement",flags).Invoke(b,null);
                var received=far?far:near;
                Check(target.Cargo==received&&!received.gameObject.activeSelf&&!b.Busy,"Ordinary movement did not store magnet kind="+kind);checks++;
                Check((received.portalEntryPosition-axis*(kind==0?0:b.cellSize)).sqrMagnitude<.001f,"Wrong entry cell from ordinary movement");checks++;
                Check(b.player.position==Vector3.zero,"Ordinary arrival moved player into portal");checks++;
                Check(b.UndoStep()&&!target.Cargo&&received.gameObject.activeSelf,"Arrival undo did not restore magnet");checks++;
                // All callers see an occupied portal as blocked before moving.
                received.storedInPortal=target;received.gameObject.SetActive(false);
                var query=b.InspectMagnetDestination(target.Cell-dir,0,target.Cell,null);
                Check(query.kind==GridPlayground.MagnetLandingKind.Blocked,"Occupied portal admitted another material");checks++;
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int UprightEntryFlatExitCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})foreach(bool space in new[]{false,true}){
            var root=new GameObject("Upright entry and flat exit");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;
                var axis=new Vector3(dir.x,0,dir.y);
                b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);b.player.position=-axis*b.cellSize*2;
                var tiles=new List<GridTile>();
                for(int n=-2;n<=(space?0:1);n++){
                    var go=new GameObject("Shore");go.transform.SetParent(root.transform);go.transform.position=axis*(n*b.cellSize);
                    tiles.Add(go.AddComponent<GridTile>());
                }
                b.tiles=tiles.ToArray();var portal=tiles[2].gameObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tiles[2];
                var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                var cargo=MagnetVisuals.Create(root.transform,-axis*b.cellSize,MagnetShape.Bar,false,yaw*Quaternion.Euler(0,0,90),b.cellSize,root.transform);
                b.magnets=new[]{cargo};root.SetActive(true);
                Check(b.TryStep(dir),"Upright magnet could not be pushed into portal: "+b.LastRule);
                typeof(GridPlayground).GetMethod("FinishMovement",flags).Invoke(b,null);
                Check(portal.Cargo==cargo&&!cargo.gameObject.activeSelf&&!MagnetPiece.VerticalBar(cargo.Pose),"Entry did not store the rolled flat pose");checks++;
                var storedPose=cargo.Pose;
                BlackHoleTravel.ExitPlan plan;
                Check(BlackHoleTravel.PlanExit(portal,dir,cargo,out plan)&&!plan.kick&&plan.unstableLanding==space,"Flat exit was mistaken for an upright kick");checks++;
                var travel=root.AddComponent<BlackHoleTravel>();
                var stack=new Stack<System.Collections.IEnumerator>();
                stack.Push((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{portal,dir,cargo,plan}));
                bool cargoFirst=false,compressed=false,rebounded=false;float restY=0;int guard=100000;
                while(stack.Count>0){
                    Check(--guard>0,"Flat exit stalled");var current=stack.Peek();
                    if(!current.MoveNext()){stack.Pop();continue;}
                    if(current.Current is System.Collections.IEnumerator child&&!(child is CustomYieldInstruction)){stack.Push(child);continue;}
                    Check(Quaternion.Angle(cargo.Pose,storedPose)<=12.1f,"Flat cargo tumbled beyond its forward flight lean");
                    if(!cargoFirst&&cargo.gameObject.activeSelf&&(cargo.transform.position-axis*b.cellSize).sqrMagnitude<.0001f&&b.player.localScale==Vector3.zero){cargoFirst=true;restY=cargo.geometry.position.y;}
                    if(cargoFirst&&space){
                        compressed|=cargo.geometry.position.y<restY-b.cellSize*.2f;
                        rebounded|=compressed&&b.player.position.y>.3f;
                    }
                }
                Check(cargoFirst,"Player emerged before the flat magnet settled");checks++;
                Check(Quaternion.Angle(cargo.Pose,storedPose)<.01f,"Flight lean was not restored after landing");checks++;
                Check(cargo.gameObject.activeSelf&&!cargo.storedInPortal&&(cargo.transform.position-axis*b.cellSize).sqrMagnitude<.0001f,"Flat magnet did not remain in the first exit cell");checks++;
                Check(!b.Busy,"Flat exit left input locked");checks++;
                if(space){Check(compressed&&rebounded&&b.player.position==Vector3.zero,"Floating flat magnet did not compress and spring player back to hole");checks++;}
                else{Check(b.PlayerCell==dir&&b.player.localScale==Vector3.one,"Supported flat magnet did not retain player on landing");checks++;}
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
    static int DestinationCases(UnityEngine.SceneManagement.Scene scene){
        int checks=0;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})for(int kind=0;kind<8;kind++){
            var root=new GameObject("Portal destination fixture");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            try{
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
                var axis=new Vector3(dir.x,0,dir.y);var tiles=new List<GridTile>();
                for(int i=0;i<3;i++){
                    if((i==2&&(kind==1||kind==7))||(i==1&&kind>=5))continue;
                    var go=new GameObject("Floor");go.transform.SetParent(root.transform);go.transform.position=axis*(i*b.cellSize);tiles.Add(go.AddComponent<GridTile>());
                }
                b.tiles=tiles.ToArray();var source=tiles[0].gameObject.AddComponent<BlackHolePortal>();source.board=b;source.tile=tiles[0];
                BlackHolePortal second=null;
                if(kind==2||kind==4){second=tiles[2].gameObject.AddComponent<BlackHolePortal>();second.board=b;second.tile=tiles[2];}
                if(kind==3)tiles[2].blocked=true;
                var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                var cargo=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Bar,false,yaw*Quaternion.Euler(0,0,90),b.cellSize,root.transform);
                cargo.storedInPortal=source;cargo.gameObject.SetActive(false);b.magnets=new[]{cargo};
                if(kind==6)cargo.geometry.rotation=yaw;
                MagnetPiece occupied=null;
                if(kind==4){occupied=MagnetVisuals.Create(root.transform,axis*b.cellSize*2,MagnetShape.Bar,true,yaw,b.cellSize,root.transform);occupied.storedInPortal=second;occupied.gameObject.SetActive(false);b.magnets=new[]{cargo,occupied};}
                root.SetActive(true);var travel=root.AddComponent<BlackHoleTravel>();BlackHoleTravel.ExitPlan plan;
                Check(BlackHoleTravel.PlanJourneyExit(source,dir,cargo,out plan),"No selectable exit for destination "+kind);checks++;
                Check(plan.failedExit==(kind==4),"Wrong blocked-destination outcome "+kind);checks++;
                if(kind==3){
                    Check(plan.kickRebound,"Solid next cell did not plan a kick rebound");checks++;
                    var original=cargo.Pose;bool returned=false;float furthest=0,maxLean=0;
                    var stack=new Stack<System.Collections.IEnumerator>();int guard=100000;
                    stack.Push((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{source,dir,cargo,plan}));
                    while(stack.Count>0){
                        Check(--guard>0,"Blocked kick stalled");var current=stack.Peek();
                        if(!current.MoveNext()){stack.Pop();continue;}
                        if(current.Current is System.Collections.IEnumerator child&&!(child is CustomYieldInstruction)){stack.Push(child);continue;}
                        float along=Vector3.Dot(cargo.transform.position,axis);
                        furthest=Mathf.Max(furthest,along);
                        maxLean=Mathf.Max(maxLean,Quaternion.Angle(cargo.Pose,original));
                        returned|=furthest>b.cellSize*1.04f&&along<furthest-b.cellSize*.03f;
                    }
                    Check(returned&&furthest<=b.cellSize*1.081f&&maxLean>45&&maxLean<50.1f,"Magnet did not lean under the actor and spring upright");checks++;
                    Check(cargo.gameObject.activeSelf&&!cargo.storedInPortal&&Quaternion.Angle(cargo.Pose,original)<.01f
                        &&(cargo.transform.position-axis*b.cellSize).sqrMagnitude<.0001f,"Rebounded magnet did not remain upright in first cell");checks++;
                    Check(b.player.position==Vector3.zero&&!b.Busy,"Rebounded player did not return to hole");checks++;
                }else if(kind>=5){
                    Check(plan.unstableLanding&&plan.kick==(kind!=6),"Void should tip upright cargo and rebound the actor");checks++;
                    Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{source,dir,cargo,plan}));
                    Check(cargo.gameObject.activeSelf&&!cargo.storedInPortal&&(cargo.transform.position-axis*b.cellSize*(kind!=6?2:1)).sqrMagnitude<.001f,"Floating cargo was lost or did not advance a cell");checks++;
                    if(kind!=6)Check(!MagnetPiece.VerticalBar(cargo.Pose),"Upright floating cargo did not tip flat");
                    Check(b.player.position==Vector3.zero&&!b.Busy,"Unstable exit did not return player to hole");checks++;
                }else if(plan.failedExit){
                    Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("FailedPortalExit",flags).Invoke(travel,new object[]{source,dir,cargo,Vector3.one}));
                    Check(source.Cargo==cargo&&!cargo.gameObject.activeSelf&&!b.Busy,"Failure lost cargo or left board busy");checks++;
                    if(occupied)Check(second.Cargo==occupied,"Failure overwrote second portal cargo");
                    // Both sides may be blocked: fallback recovery on the mouth must complete.
                    Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("EjectPlayer",flags).Invoke(travel,new object[]{b.player,Vector3.zero,Vector3.zero,Vector3.one}));
                    Check(b.player.localScale==Vector3.one&&b.player.position==Vector3.zero,"Mouth recovery did not restore player");checks++;
                }else{
                    Drain((System.Collections.IEnumerator)typeof(BlackHoleTravel).GetMethod("Eject",flags).Invoke(travel,new object[]{source,dir,cargo,plan}));
                    Check(!MagnetPiece.VerticalBar(cargo.Pose)&&b.PlayerCell==dir&&!b.Busy,"Kick did not finish flat with player released");checks++;
                    Check(kind==2?second.Cargo==cargo&&!cargo.gameObject.activeSelf:cargo.gameObject.activeSelf&&!cargo.storedInPortal,"Wrong storage state "+kind);checks++;
                    if(kind==2){
                        Check((cargo.portalEntryPosition-axis*b.cellSize).sqrMagnitude<.001f,"Second portal lost entry position");
                        Check(BlackHoleTravel.PlanJourneyExit(second,dir,cargo,out plan),"Stored flat cargo cannot leave next portal");checks++;
                    }
                }
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        return checks;
    }
}
}
