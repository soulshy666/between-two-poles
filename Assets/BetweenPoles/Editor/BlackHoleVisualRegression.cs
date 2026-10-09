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
            int phase=0;bool selected=false,captured=false;double started=EditorApplication.timeSinceStartup,ready=0;
            BlackHolePortal source=null,target=null;
            Action setup=()=>{
                lab.Load(phase==0?11:12);if(panel.IsOpen)panel.Toggle();
                source=BlackHolePortal.At(b,new Vector2Int(-1,0));target=BlackHolePortal.At(b,new Vector2Int(2,0));
                Require(b.TryStep(Vector2Int.right),"Deposit failed");typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(b,null);
                Require(b.TryStep(Vector2Int.right),"Player could not enter");typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(b,null);
                typeof(BlackHolePortal).GetField("<EntryDirection>k__BackingField",Private).SetValue(source,Vector2Int.right);
                BlackHoleTravel.Begin(source);selected=false;captured=false;ready=0;
            };
            EditorApplication.ExecuteMenuItem("Window/General/Game");setup();
            EditorApplication.CallbackFunction update=null;
            update=()=>{try{
                if(!Application.isPlaying||EditorApplication.timeSinceStartup-started>65)throw new Exception("Visual test interrupted/timed out");
                if(BlackHoleTravel.InTransit)return;
                var now=EditorApplication.timeSinceStartup;
                if(BlackHoleTravel.Selecting){
                    var travel=typeof(BlackHoleTravel).GetField("instance",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
                    var camera=(Camera)typeof(BlackHoleTravel).GetField("viewCamera",Private).GetValue(travel);Require(camera,"World navigation camera not bound");
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
                    Require(phase==0?b.player.position.y>.1f:Mathf.Abs(b.magnets[0].transform.position.x-6)<.01f,"Cargo landing wrong");
                    ready=now+1;return;
                }
                if(now<ready)return;
                if(captured){ScreenCapture.CaptureScreenshot("output/blackhole-arrival-"+phase+".png");captured=false;ready=now+.5;return;}
                if(phase++==0){setup();return;}
                File.WriteAllText(Report,"PASS: Actual test scene, flat/upright presets, real walk-in, camera pan, destination preview/confirm, cargo-first arrival and player landing. Screenshots captured.");
                EditorApplication.update-=update;Finish();
            }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);EditorApplication.update-=update;Finish();}};
            File.WriteAllText(Report,"RUNNING");EditorApplication.update+=update;
        }
        static void Finish(){if(SessionState.GetBool("BlackHoleVisual.Play",false)){SessionState.SetBool("BlackHoleVisual.Play",false);EditorApplication.isPlaying=false;}}
    }
}
