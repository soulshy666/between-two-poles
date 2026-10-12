using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
    [InitializeOnLoad] static class BlackHoleVisualRegression {
        const string Request="output/blackhole-visual.run",Report="output/blackhole-visual-result.txt";
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static BlackHoleVisualRegression(){
            EditorApplication.playModeStateChanged+=s=>{
                if(s!=PlayModeStateChange.EnteredEditMode)return;
                string previous=SessionState.GetString("BlackHoleVisual.Scene","");
                if(string.IsNullOrEmpty(previous))return;
                SessionState.EraseString("BlackHoleVisual.Scene");EditorSceneManager.OpenScene(previous);
            };
            EditorApplication.delayCall+=()=>{if(File.Exists(Request)){File.Delete(Request);Run();}};
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("BlackHoleVisual.Play",false)){
                double start=EditorApplication.timeSinceStartup;EditorApplication.CallbackFunction tick=null;
                tick=()=>{if(EditorApplication.timeSinceStartup-start<1)return;EditorApplication.update-=tick;Run();};EditorApplication.update+=tick;
            }};
        }
        static void Require(bool ok,string error){if(!ok)throw new Exception(error);}
        [MenuItem("两极之间/测试/黑洞实际场景镜头回归")]
        public static void Run(){
            if(!Application.isPlaying){
                if(!UnityEngine.Object.FindObjectOfType<MagnetTestLayouts>()){
                    var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    if(active.isDirty){File.WriteAllText(Report,"FAIL: Save the open scene before the visual test switches scenes.");return;}
                    SessionState.SetString("BlackHoleVisual.Scene",active.path);
                    EditorSceneManager.OpenScene("Assets/BetweenPoles/Scenes/磁铁机制测试.unity");
                }
                SessionState.SetBool("BlackHoleVisual.Play",true);EditorApplication.isPlaying=true;return;
            }
            var lab=UnityEngine.Object.FindObjectOfType<MagnetTestLayouts>();
            if(!lab){File.WriteAllText(Report,"FAIL: Open the mechanism test scene before running this visual regression.");Finish();return;}
            var b=lab.board;var panel=b.GetComponent<MagnetDebugPanel>();
            int phase=0;bool selected=false,captured=false,climbed=false,flightCaptured=false,proneCaptured=false,entryCaptured=false;double started=EditorApplication.timeSinceStartup,ready=0;
            float climbPeak=0;bool climbCaptured=false;
            bool cargoFlightSeen=false,headSeen=false,hitSeen=false,returnFlightSeen=false,tumbleSeen=false;
            BlackHolePortal source=null,target=null;
            Action setup=()=>{
                lab.Load(phase<4?11+phase:phase<6||phase==7?13:11);if(panel.IsOpen)panel.Toggle();
                if(phase==4||phase==5){
                    b.magnets[1].geometry.rotation=Quaternion.Euler(0,90,0);MagnetVisuals.Ground(b.magnets[1]);
                    if(phase==4){var gap=b.TileAt(new Vector2Int(3,0));gap.gameObject.SetActive(false);b.tiles=b.tiles.Where(t=>t!=gap).ToArray();}
                }
                if(phase==7){
                    b.magnets[0].north=b.magnets[1].north;
                    foreach(var renderer in b.magnets[0].geometry.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=b.magnets[1].geometry.GetComponentInChildren<Renderer>().sharedMaterial;
                    b.magnets[0].geometry.rotation=Quaternion.Euler(0,90,0);MagnetVisuals.Ground(b.magnets[0]);
                    var ground=b.TileAt(new Vector2Int(4,0));ground.surfaceHeight=0;
                }
                source=BlackHolePortal.At(b,new Vector2Int(-1,0));target=BlackHolePortal.At(b,new Vector2Int(2,0));
                if(phase==6)target=source;
                // Keep the exit fixtures' intended arrival poses, except the stone
                // case which now explicitly covers flat -> upright entry -> return kick.
                if(phase!=3){b.magnets[0].geometry.rotation=Quaternion.AngleAxis(-90,Vector3.back)*b.magnets[0].Pose;MagnetVisuals.Ground(b.magnets[0]);}
                float rollDegrees;var expectedArrival=MagnetPiece.Rolled(b.magnets[0].shape,b.magnets[0].Pose,Vector2Int.right,out rollDegrees);
                Require(b.TryStep(Vector2Int.right),"Deposit failed");typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(b,null);
                Require(Quaternion.Angle(b.magnets[0].Pose,expectedArrival)<.1f,"Portal entry skipped roll");
                Require(b.TryStep(Vector2Int.right),"Player could not enter");
                typeof(BlackHolePortal).GetField("<EntryDirection>k__BackingField",Private).SetValue(source,Vector2Int.right);
                selected=false;captured=false;climbed=false;flightCaptured=false;proneCaptured=false;entryCaptured=false;climbPeak=0;climbCaptured=false;ready=0;cargoFlightSeen=false;
            };
            EditorApplication.ExecuteMenuItem("Window/General/Game");setup();
            EditorApplication.CallbackFunction update=null;
            update=()=>{try{
                if(!Application.isPlaying||EditorApplication.timeSinceStartup-started>130)throw new Exception("Visual test interrupted/timed out");
                if(selected&&BlackHoleTravel.InTransit&&b.player.localScale.sqrMagnitude<.0001f){
                    var cargo=b.magnets[0];
                    if(cargo&&cargo.gameObject.activeInHierarchy&&!cargo.storedInPortal&&cargo.geometry.position.y-cargo.transform.position.y>.15f){
                        foreach(var camera in UnityEngine.Object.FindObjectsOfType<IslandCamera>())if(camera.enabled&&camera.board==b){
                            var curved=camera.GetComponent<CurvedTrialCamera>();
                            Require(!curved||!curved.enabled||curved.FramingSettled,"Cargo emerged before normal camera framing settled");
                        }
                        var renderers=cargo.geometry.GetComponentsInChildren<Renderer>();
                        Require(renderers.Length>0,"Cargo has no visual renderers");
                        foreach(var renderer in renderers)Require(renderer.enabled,"Cargo throw invisible after destination preview");
                        cargoFlightSeen=true;
                    }
                }
                if(phase==3&&selected&&BlackHoleTravel.InTransit){
                    var cargo=b.magnets[0];
                    if(!tumbleSeen&&cargo.gameObject.activeInHierarchy&&cargo.geometry.position.y-cargo.transform.position.y>1.5f&&Mathf.Abs(Vector3.Dot(cargo.geometry.up,Vector3.up))<.7f){
                        tumbleSeen=true;ScreenCapture.CaptureScreenshot("output/blackhole-bounce-tumble.png");
                    }
                    if(b.player.localScale.sqrMagnitude>2.9f&&b.player.position.x>2&&b.player.position.y<-.15f&&b.player.position.y>-.65f&&cargo.geometry.position.y-cargo.transform.position.y>.7f&&!headSeen){
                        headSeen=true;ScreenCapture.CaptureScreenshot("output/blackhole-bounce-head.png");
                    }
                    if(headSeen&&b.player.localScale.sqrMagnitude>.1f&&b.player.localScale.sqrMagnitude<1.8f&&b.player.position.x>2&&!hitSeen){
                        hitSeen=true;ScreenCapture.CaptureScreenshot("output/blackhole-bounce-hit.png");
                    }
                    if(b.player.localScale.sqrMagnitude>.1f&&b.player.position.x>2){
                        if(b.player.localScale.sqrMagnitude>2.9f)Require(b.player.position.y<-.15f,"Player fully emerged before rebound impact");
                        float playerTop=float.NegativeInfinity,cargoBottom=float.PositiveInfinity;
                        foreach(var filter in b.player.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh&&filter.sharedMesh.isReadable)
                            foreach(var vertex in filter.sharedMesh.vertices)playerTop=Mathf.Max(playerTop,filter.transform.TransformPoint(vertex).y);
                        foreach(var filter in cargo.geometry.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh&&filter.sharedMesh.isReadable)
                            foreach(var vertex in filter.sharedMesh.vertices)cargoBottom=Mathf.Min(cargoBottom,filter.transform.TransformPoint(vertex).y);
                        Require(cargoBottom>=playerTop,"Rebounding cargo penetrated player mesh");
                        if(b.player.localScale.sqrMagnitude<1.8f){
                            Require(cargoBottom>=target.tile.surfaceHeight-.001f,"Absorbed magnet penetrated ground");
                            Require(cargo.geometry.localScale.sqrMagnitude<1.8f,"Magnet did not shrink with player");
                        }
                    }
                    if(hitSeen&&b.player.localScale.sqrMagnitude<.0001f&&cargo.gameObject.activeInHierarchy&&cargo.transform.position.x<0&&cargo.geometry.position.y-cargo.transform.position.y>.15f){
                        if(!returnFlightSeen)ScreenCapture.CaptureScreenshot("output/blackhole-bounce-return.png");returnFlightSeen=true;
                    }
                }
                if(!entryCaptured&&!selected&&b.player.position.x<0&&b.player.position.y>.3f){ScreenCapture.CaptureScreenshot("output/blackhole-jump-"+phase+".png");entryCaptured=true;}
                if(selected&&b.player.localScale.sqrMagnitude>.1f){
                    if(b.player.position.y>1.05f)Require(cargoFlightSeen,"Player emerged before visible cargo flight");
                    if(!flightCaptured&&b.player.position.y>1.05f){ScreenCapture.CaptureScreenshot("output/blackhole-flail-"+phase+".png");flightCaptured=true;}
                    var visual=b.player.Find("Visual");
                    if(!proneCaptured&&visual&&Mathf.Abs(Vector3.Dot(visual.up,Vector3.up))<.4f&&b.player.position.y<.8f){ScreenCapture.CaptureScreenshot("output/blackhole-fall-"+phase+".png");proneCaptured=true;}
                }
                if(!selected&&BlackHoleTravel.InTransit&&b.PlayerCell==source.Cell&&b.player.localScale.sqrMagnitude>.01f){
                    var swallowed=b.player.Find("Visual");Require(swallowed&&swallowed.localScale.sqrMagnitude<.0001f,"Head remains visible during absorption");
                }
                if(BlackHoleTravel.InTransit||(!selected&&!BlackHoleTravel.Selecting&&b.Busy))return;
                if(climbed){climbPeak=Mathf.Max(climbPeak,b.player.position.y);
                    if(!climbCaptured&&b.player.position.y>b.LayerHeight+.15f){ScreenCapture.CaptureScreenshot("output/blackhole-platform-jump-"+phase+".png");climbCaptured=true;}}
                var now=EditorApplication.timeSinceStartup;
                if(BlackHoleTravel.Selecting){
                    var travel=typeof(BlackHoleTravel).GetField("instance",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
                    var camera=(Camera)typeof(BlackHoleTravel).GetField("viewCamera",Private).GetValue(travel);Require(camera,"World navigation camera not bound");
                    Require(b.player.localScale.sqrMagnitude<.0001f,"Player visible during portal selection");
                    if(ready==0){
                        var focus=(Vector3)typeof(BlackHoleTravel).GetField("viewFocus",Private).GetValue(travel);
                        typeof(BlackHoleTravel).GetField("viewFocus",Private).SetValue(travel,focus+Vector3.right*1.5f);
                        typeof(BlackHoleTravel).GetMethod("ApplyNavigation",Private).Invoke(travel,null);
                        typeof(BlackHoleTravel).GetField("previewPortal",Private).SetValue(travel,target);ready=now+1;return;
                    }
                    if(now<ready)return;
                    if(!captured){ScreenCapture.CaptureScreenshot("output/blackhole-navigation-"+phase+".png");captured=true;ready=now+.5;return;}
                    if(!selected){
                        if(phase==7){
                            var ground=b.TileAt(new Vector2Int(4,0));ground.blocked=true;
                            BlackHoleTravel.ExitPlan blocked;
                            Require(!BlackHoleTravel.PlanExit(target,Vector2Int.right,source.Cargo,out blocked),"Displacement entered blocked cell");
                            ground.blocked=false;
                        }
                        Require((bool)typeof(BlackHoleTravel).GetMethod("SelectTestPortal",Private).Invoke(travel,new object[]{target,false}),"Confirmation rejected");
                        // Simulate the entry island being culled during destination selection.
                        foreach(var renderer in source.Cargo.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                        selected=true;ready=0;
                    }
                    return;
                }
                Require(selected,"Travel skipped selection");
                if(ready==0){
                    Require(b.PlayerCell==new Vector2Int(phase==6||phase==3?-2:3,0),"Player landing wrong");
                    Require(b.player.Find("Visual").localScale.sqrMagnitude>.01f,"Visual scale not restored after arrival");
                    if(phase==0)Require(b.player.position.y>.1f,"Flat landing wrong");
                    if(phase==1)Require(Mathf.Abs(b.magnets[0].transform.position.x-6)<.01f,"Kick landing wrong");
                    if(phase==2)Require(b.magnets[1].product==MagnetProduct.WideBar&&Mathf.Abs(b.player.position.y-.24f)<.01f,"Merged landing wrong");
                    if(phase==3)Require(tumbleSeen&&headSeen&&hitSeen&&returnFlightSeen&&Mathf.Abs(b.magnets[0].transform.position.x+4.5f)<.01f&&Mathf.Abs(b.player.position.y)<.01f&&!MagnetPiece.VerticalBar(b.magnets[0].Pose)&&b.player.forward.x<-.95f,"Upright bounce return kick wrong");
                    if(phase==4||phase==5){
                        Require(b.magnets[1].product==(phase==4?MagnetProduct.Cross:MagnetProduct.WideBar),"Perpendicular bars produced wrong exit product");
                        GridTile surface;Vector3 top;
                        Require(BlackHolePortal.TrySurface(b,new Vector2Int(3,0),out surface,out top)&&(b.player.position-top).sqrMagnitude<.0001f,"Player did not land on assembled magnet");
                    }
                    if(phase==7){
                        Require(!b.magnets[0].combined&&!b.magnets[1].combined,"Same poles merged");
                        Require(Mathf.Abs(b.magnets[0].transform.position.x-4.5f)<.01f&&Mathf.Abs(b.magnets[1].transform.position.x-6)<.01f&&b.player.position.y>.1f,"Exit displacement or player landing wrong");
                    }
                    if(phase==6){
                        Require(!source.Cargo&&!b.magnets[0].storedInPortal&&b.magnets[0].gameObject.activeInHierarchy,"Return left cargo stored in portal");
                        Require(Mathf.Abs(b.magnets[0].transform.position.x+3)<.01f&&b.player.position.y>.1f&&b.player.forward.x<-.95f,"Return position/facing wrong");
                    }
                    ready=now+1;return;
                }
                if(now<ready)return;
                if(captured){ScreenCapture.CaptureScreenshot("output/blackhole-arrival-"+phase+".png");captured=false;ready=now+.5;return;}
                if(phase==2&&!climbed){Require(b.TryStep(Vector2Int.right),"Step onto platform rejected");climbed=true;ready=now+1.2;return;}
                if(phase==2)Require(climbPeak>b.LayerHeight+.15f&&climbCaptured&&!b.Busy&&b.PlayerCell==new Vector2Int(4,0)&&Mathf.Abs(b.player.position.y-b.LayerHeight)<.01f,"Real platform ascent failed");
                if(phase++<7){setup();return;}
                File.WriteAllText(Report,"PASS: Actual test scene, flat/upright/merge/stone-bounce presets, real walk-in, camera pan, destination preview/confirm, cargo-first arrival and player landing; perpendicular bars form cross over gap / wide bar on land; clicking origin returns cargo then player; same-pole exit displacement. Screenshots captured.");
                EditorApplication.update-=update;Finish();
            }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);EditorApplication.update-=update;Finish();}};
            File.WriteAllText(Report,"RUNNING");EditorApplication.update+=update;
        }
        static void Finish(){if(SessionState.GetBool("BlackHoleVisual.Play",false)){SessionState.SetBool("BlackHoleVisual.Play",false);EditorApplication.isPlaying=false;}}
    }
}
// Refresh visual verification after animation changes.
// Verify same-pole portal displacement.
