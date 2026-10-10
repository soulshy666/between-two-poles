using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
    [InitializeOnLoad] static class BlackHoleVisualRegression {
        const string Request="output/blackhole-visual.run",Report="output/blackhole-visual-result.txt";
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static BlackHoleVisualRegression(){
            EditorApplication.delayCall+=()=>{if(File.Exists(Request)){File.Delete(Request);Run();}};
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("BlackHoleVisual.Play",false)){
                double start=EditorApplication.timeSinceStartup;EditorApplication.CallbackFunction tick=null;
                tick=()=>{if(EditorApplication.timeSinceStartup-start<1)return;EditorApplication.update-=tick;Run();};EditorApplication.update+=tick;
            }};
        }
        static void Require(bool ok,string error){if(!ok)throw new Exception(error);}
        [MenuItem("两极之间/测试/黑洞实际场景镜头回归")]
        public static void Run(){
            if(!Application.isPlaying){SessionState.SetBool("BlackHoleVisual.Play",true);EditorApplication.isPlaying=true;return;}
            var lab=UnityEngine.Object.FindObjectOfType<MagnetTestLayouts>();
            if(!lab){File.WriteAllText(Report,"FAIL: Open the mechanism test scene before running this visual regression.");Finish();return;}
            var b=lab.board;var panel=b.GetComponent<MagnetDebugPanel>();
            int phase=0;bool selected=false,captured=false,climbed=false,flightCaptured=false,proneCaptured=false,entryCaptured=false;double started=EditorApplication.timeSinceStartup,ready=0;
            float climbPeak=0;bool climbCaptured=false;
            BlackHolePortal source=null,target=null;
            Action setup=()=>{
                lab.Load(11+phase);if(panel.IsOpen)panel.Toggle();
                source=BlackHolePortal.At(b,new Vector2Int(-1,0));target=BlackHolePortal.At(b,new Vector2Int(2,0));
                Require(b.TryStep(Vector2Int.right),"Deposit failed");typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(b,null);
                Require(b.TryStep(Vector2Int.right),"Player could not enter");
                typeof(BlackHolePortal).GetField("<EntryDirection>k__BackingField",Private).SetValue(source,Vector2Int.right);
                selected=false;captured=false;climbed=false;flightCaptured=false;proneCaptured=false;entryCaptured=false;climbPeak=0;climbCaptured=false;ready=0;
            };
            EditorApplication.ExecuteMenuItem("Window/General/Game");setup();
            EditorApplication.CallbackFunction update=null;
            update=()=>{try{
                if(!Application.isPlaying||EditorApplication.timeSinceStartup-started>130)throw new Exception("Visual test interrupted/timed out");
                if(!entryCaptured&&!selected&&b.player.position.x<0&&b.player.position.y>.3f){ScreenCapture.CaptureScreenshot("output/blackhole-jump-"+phase+".png");entryCaptured=true;}
                if(selected&&b.player.localScale.sqrMagnitude>.1f){
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
                    if(!selected){Require((bool)typeof(BlackHoleTravel).GetMethod("SelectTestPortal",Private).Invoke(travel,new object[]{target,false}),"Confirmation rejected");selected=true;ready=0;}
                    return;
                }
                Require(selected,"Travel skipped selection");
                if(ready==0){
                    Require(b.PlayerCell==new Vector2Int(3,0),"Player landing wrong");
                    Require(b.player.Find("Visual").localScale.sqrMagnitude>.01f,"Visual scale not restored after arrival");
                    if(phase==0)Require(b.player.position.y>.1f,"Flat landing wrong");
                    if(phase==1)Require(Mathf.Abs(b.magnets[0].transform.position.x-6)<.01f,"Kick landing wrong");
                    if(phase==2)Require(b.magnets[1].product==MagnetProduct.WideBar&&Mathf.Abs(b.player.position.y-.24f)<.01f,"Merged landing wrong");
                    if(phase==3)Require(Mathf.Abs(b.magnets[0].transform.position.x+3)<.01f&&Mathf.Abs(b.player.position.y-.55f)<.01f,"Bounce landing wrong");
                    ready=now+1;return;
                }
                if(now<ready)return;
                if(captured){ScreenCapture.CaptureScreenshot("output/blackhole-arrival-"+phase+".png");captured=false;ready=now+.5;return;}
                if(phase>=2&&!climbed){Require(b.TryStep(Vector2Int.right),"Step onto platform rejected");climbed=true;ready=now+1.2;return;}
                if(phase>=2)Require(climbPeak>b.LayerHeight+.15f&&climbCaptured&&!b.Busy&&b.PlayerCell==new Vector2Int(4,0)&&Mathf.Abs(b.player.position.y-b.LayerHeight)<.01f,"Real platform ascent failed");
                if(phase++<3){setup();return;}
                File.WriteAllText(Report,"PASS: Actual test scene, flat/upright/merge/stone-bounce presets, real walk-in, camera pan, destination preview/confirm, cargo-first arrival and player landing. Screenshots captured.");
                EditorApplication.update-=update;Finish();
            }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);EditorApplication.update-=update;Finish();}};
            File.WriteAllText(Report,"RUNNING");EditorApplication.update+=update;
        }
        static void Finish(){if(SessionState.GetBool("BlackHoleVisual.Play",false)){SessionState.SetBool("BlackHoleVisual.Play",false);EditorApplication.isPlaying=false;}}
    }
}
// Refresh visual verification after animation changes.
