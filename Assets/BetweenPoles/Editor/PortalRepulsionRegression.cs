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
            checks+=WideBarKickCases(scene);
            checks+=DestinationCases(scene);
            checks+=UprightEntryFlatExitCases(scene);
            checks+=OrdinaryMovementCases(scene);
            checks+=EmptySpaceLedgeCases(scene);
            checks+=SkipSettlementCases(scene);
            checks+=GroundBarInertiaCases(scene);
            Directory.CreateDirectory("output");File.WriteAllText("output/portal-repulsion.txt","PASS: "+checks+" checks; four directions; stone/high platform rebound; wide-bar support and visibility; kick onto ground/space/second portal; occupied portal rejection with animated return; cargo retention and mouth recovery.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/portal-repulsion.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
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
                    if(upright&&slideStart<0&&Quaternion.Angle(m.Pose,final)<.01f)slideStart=travel;
                    float bottom=float.PositiveInfinity;
                    foreach(var f in m.geometry.GetComponentsInChildren<MeshFilter>())foreach(var v in f.sharedMesh.vertices)bottom=Mathf.Min(bottom,f.transform.TransformPoint(v).y);
                    Check(Mathf.Abs(bottom)<.001f,"Ground roll lost support contact");
                }
                Check((m.transform.position-axis*b.cellSize).sqrMagnitude<.0001f&&Quaternion.Angle(m.Pose,final)<.01f,"Ground inertia changed logical result");checks++;
                if(upright){Check(slideStart>=.89f&&slideStart<.97f,"Falling bar has no forward slide after touchdown");checks++;}
                typeof(GridPlayground).GetMethod("StartBarRock",flags).Invoke(b,new object[]{m,new Vector3(dir.y,0,-dir.x)});
                float take=(float)typeof(GridPlayground).GetMethod("TakeBarRock",flags).Invoke(b,new object[]{m,new Vector3(dir.y,0,-dir.x)});
                Check(((System.Collections.IDictionary)typeof(GridPlayground).GetField("barRocks",flags).GetValue(b)).Count==0,"Next push failed to take over settling motion");checks++;
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
