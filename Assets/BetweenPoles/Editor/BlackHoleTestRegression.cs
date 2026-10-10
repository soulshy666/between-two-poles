using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
    [InitializeOnLoad] static partial class BlackHoleTestRegression {
        const string Request="output/blackhole-test.run",Report="output/blackhole-test-result.txt";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static BlackHoleTestRegression(){
            EditorApplication.delayCall+=()=>{if(File.Exists(Request)){File.Delete(Request);Run();}};
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("BlackHoleTest.OwnPlay",false)){
                double ready=EditorApplication.timeSinceStartup+.5;EditorApplication.CallbackFunction wait=null;
                wait=()=>{if(EditorApplication.timeSinceStartup<ready||Time.unscaledDeltaTime<=0)return;EditorApplication.update-=wait;Run();};EditorApplication.update+=wait;
            }};
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        [MenuItem("两极之间/测试/黑洞测试编辑器回归")]
        public static void Run(){
            if(!Application.isPlaying){SessionState.SetBool("BlackHoleTest.OwnPlay",true);EditorApplication.isPlaying=true;return;}
            var root=new GameObject("Black hole isolated regression");root.SetActive(false);int checks=0;bool asynchronous=false;
            try{
                checks+=CargoCases();
                checks+=ExitObstacleCases();
                var board=root.AddComponent<GridPlayground>();board.enabled=false;board.magnets=new MagnetPiece[0];
                var player=new GameObject("Player");player.transform.SetParent(root.transform);board.player=player.transform;
                var center=new GameObject("Center");center.transform.SetParent(root.transform);
                var lab=root.AddComponent<MagnetTestLayouts>();lab.enabled=false;lab.board=board;lab.center=center.transform;
                var tiles=new System.Collections.Generic.List<GridTile>();
                for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++){
                    var go=new GameObject("Tile");go.transform.SetParent(root.transform);go.transform.position=new Vector3(x*1.5f,0,z*1.5f);
                    var tile=go.AddComponent<GridTile>();var binding=go.AddComponent<IslandSurfaceAnchor>();binding.center=root.transform;tiles.Add(tile);
                }
                board.tiles=tiles.ToArray();var portal=lab.PlacePortal(board.TileAt(Vector2Int.zero));
                Check(portal&&lab.PlacePortal(portal.tile)==portal,"Duplicate portal placement");checks++;
                root.SetActive(true);
                foreach(var direction in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left}){
                    GridTile exit;Vector3 position;
                    Check(portal.TryExitLanding(direction,out exit,out position)&&exit==board.TileAt(direction),"Wrong directional exit");checks++;
                    var held=board.tiles;board.tiles=Array.FindAll(held,t=>t!=exit);
                    Check(!portal.TryExitLanding(direction,out exit,out position),"Void exit allowed");checks++;board.tiles=held;
                    exit=board.TileAt(direction);exit.surfaceHeight=3;
                    Check(portal.TryExitLanding(direction,out exit,out position)&&Mathf.Abs(position.y-3)<.001f,"Raised landing lost height");checks++;exit.surfaceHeight=0;
                }
                var m=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Bar,true,Quaternion.identity,1.5f,root.transform);board.magnets=new[]{m};
                Check(!portal.CanArrive,"Occupied portal allowed arrival");checks++;m.gameObject.SetActive(false);board.magnets=new MagnetPiece[0];
                var panel=root.AddComponent<MagnetDebugPanel>();panel.Toggle();
                typeof(BlackHolePortal).GetField("armed",Private).SetValue(portal,true);
                board.enabled=true;typeof(BlackHolePortal).GetMethod("Update",Private).Invoke(portal,null);board.enabled=false;
                Check(!BlackHoleTravel.Selecting,"Editing triggered travel");checks++;
                typeof(MagnetDebugPanel).GetField("selected",Private).SetValue(panel,Vector2Int.one);
                typeof(MagnetDebugPanel).GetField("shape",Private).SetValue(panel,10);
                Check(panel.Place()&&MagnetTestLayouts.Portals(board).Length==2,"Panel did not place portal");checks++;
                typeof(MagnetDebugPanel).GetField("shape",Private).SetValue(panel,8);Check(panel.Place()&&MagnetTestLayouts.Portals(board).Length==1,"Panel did not remove portal");checks++;
                lab.Load(10);Check(MagnetTestLayouts.Portals(board).Length==2&&board.PlayerCell==new Vector2Int(-2,0),"Preset invalid");checks++;
                lab.Load(0);Check(MagnetTestLayouts.Portals(board).Length==0,"Old portals leaked between presets");checks++;
                lab.Load(10);panel.Toggle();
                var source=board.TileAt(new Vector2Int(-1,0)).GetComponentInChildren<BlackHolePortal>();
                var target=board.TileAt(new Vector2Int(2,0)).GetComponentInChildren<BlackHolePortal>();
                typeof(BlackHolePortal).GetField("<EntryDirection>k__BackingField",Private).SetValue(source,Vector2Int.right);
                board.player.position=source.tile.transform.position;
                int phase=0;bool chosen=false;double started=EditorApplication.timeSinceStartup;
                EditorApplication.CallbackFunction tick=null;
                tick=()=>{
                    try{
                        if(!Application.isPlaying||EditorApplication.timeSinceStartup-started>90)throw new Exception("Travel timed out or Play mode stopped");
                        if(BlackHoleTravel.InTransit)return;
                        if(BlackHoleTravel.Selecting){
                            if(!chosen){
                                var travel=typeof(BlackHoleTravel).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                                Check((bool)typeof(BlackHoleTravel).GetMethod("SelectTestPortal",Private).Invoke(travel,new object[]{phase==1||phase==4?source:target,phase==1||phase==4}),"Destination selection rejected");chosen=true;
                            }return;
                        }
                        Check(chosen,"Travel ended before selection");
                        Check(board.PlayerCell==(phase==1||phase==4?source.Cell-Vector2Int.right:target.Cell+Vector2Int.right),"Incorrect travel/cancel landing");checks++;
                        Check(Vector3.Dot(board.player.forward,(phase==1||phase==4)?Vector3.left:Vector3.right)>.99f,"Travel/cancel facing opposite to exit direction");checks++;
                        if(phase==2)Check(board.player.position.y>0&&!source.Cargo,"Flat cargo runtime landing failed");
                        if(phase==3)Check(!MagnetPiece.VerticalBar(board.magnets[0].Pose)&&Mathf.Abs(board.magnets[0].transform.position.x-6)<.001f,"Upright cargo runtime kick failed");
                        if(phase==4)Check(source.Cargo==board.magnets[0]&&!source.Cargo.gameObject.activeSelf,"Cancel lost stored cargo");
                        if(phase++<4){
                            chosen=false;
                            if(phase>=2){
                                lab.Load(phase==3?12:11);
                                source=board.TileAt(new Vector2Int(-1,0)).GetComponentInChildren<BlackHolePortal>();target=board.TileAt(new Vector2Int(2,0)).GetComponentInChildren<BlackHolePortal>();
                                Check(board.TryStep(Vector2Int.right),"Live cargo deposit failed");typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(board,null);
                                typeof(BlackHolePortal).GetField("<EntryDirection>k__BackingField",Private).SetValue(source,Vector2Int.right);
                            }
                            board.player.position=source.tile.transform.position;BlackHoleTravel.Begin(source);return;
                        }
                        File.WriteAllText(Report,"PASS: "+checks+" checks: placement/removal, duplicates, four directions, void/elevated exits, occupied portal, editor suppression, presets, full local travel and cancel with screen effects.");
                        EditorApplication.update-=tick;Cleanup(root);
                    }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);EditorApplication.update-=tick;Cleanup(root);}
                };
                asynchronous=true;File.WriteAllText(Report,"RUNNING: "+checks+" checks passed; verifying travel and cancel animations.");
                BlackHoleTravel.Begin(source);EditorApplication.update+=tick;
            }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);}
            finally{
                if(!asynchronous)Cleanup(root);
            }
        }
        static void Cleanup(GameObject root){
            UnityEngine.Object.DestroyImmediate(root);
            if(SessionState.GetBool("BlackHoleTest.OwnPlay",false)){SessionState.SetBool("BlackHoleTest.OwnPlay",false);EditorApplication.isPlaying=false;}
        }
    }
}
