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
            Directory.CreateDirectory("output");File.WriteAllText("output/portal-repulsion.txt","PASS: "+checks+" checks; four directions, stone/high platform, upright return kick, safe return validation, unchanged receiver, free repulsion, same-portal kick onto connected wide bar.");
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
}
}
