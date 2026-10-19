using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
// Explicit QA entry point; never saves scene changes or touches player save slots.
[InitializeOnLoad]
public static class MobilePlaytestCapture {
    const string Key="MobilePlaytestCapture.Run";
    static double next,deadline;
    static int stage;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static BlackHoleTravel travel;
    static BlackHolePortal target;
    static MobilePlaytestCapture(){EditorApplication.update+=Tick;}
    public static void Run(){
        MobileAdaptationRegression.Run();
        EditorSceneManager.OpenScene("Assets/BetweenPoles/Scenes/Cover.unity");
        var assembly=typeof(EditorWindow).Assembly;
        var type=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=sizesType.GetProperty("instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.FlattenHierarchy).GetValue(null);
        var getGroup=sizesType.GetMethod("GetGroup");var groupType=getGroup.GetParameters()[0].ParameterType;
        var group=getGroup.Invoke(sizes,new[]{Enum.Parse(groupType,"Android")});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var modeType=assembly.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.Parse(modeType,"FixedResolution"),1600,720,"Mobile QA 1600x720"},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,count-1);
        view.Show();view.Focus();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Shot(string name){ScreenCapture.CaptureScreenshot("output/mobile-adaptation/"+name+".png");}
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            if(deadline==0){deadline=EditorApplication.timeSinceStartup+180;next=EditorApplication.timeSinceStartup+5;}
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Mobile visual QA timeout");
            MobileUI.PreviewInEditor=true;Application.runInBackground=true;
            if(EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+2;
            var cover=UnityEngine.Object.FindObjectOfType<CoverMenu>();
            var board=UnityEngine.Object.FindObjectOfType<GridPlayground>();
            switch(stage){
                case 0:Shot("cover");break;
                case 1:typeof(CoverMenu).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(cover,true);break;
                case 2:Shot("settings");break;
                case 3:typeof(CoverMenu).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(cover,false);typeof(CoverMenu).GetMethod("OpenSavePage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cover,null);break;
                case 4:Shot("save-slots");break;
                case 5:SceneManager.LoadScene("Chapter01_IceWorld");next+=5;break;
                case 6:var intro=UnityEngine.Object.FindObjectOfType<IceCrashIntro>();if(intro)intro.Skip();next+=3;break;
                case 7:Shot("gameplay");break;
                case 8:typeof(ElementGameUI).GetMethod("SetPaused",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(board.GetComponent<ElementGameUI>(),new object[]{true});break;
                case 9:Shot("pause-volume");break;
                case 10:typeof(ElementGameUI).GetMethod("SetPaused",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(board.GetComponent<ElementGameUI>(),new object[]{false});board.GetComponent<ChapterMapReturn>().OpenLocalOverview();next+=4;break;
                case 11:Shot("local-map");break;
                case 12:board.GetComponent<ChapterMapReturn>().ReturnToMap();next+=7;break;
                case 13:ChartChecks();Shot("star-chart");break;
                case 14:EditorSceneManager.LoadSceneInPlayMode("Assets/BetweenPoles/Scenes/磁铁机制测试.unity",new LoadSceneParameters(LoadSceneMode.Single));next+=3;break;
                case 15:
                    var lab=UnityEngine.Object.FindObjectOfType<MagnetTestLayouts>();lab.Load(10);board=lab.board;
                    var panel=board.GetComponent<MagnetDebugPanel>();if(panel&&panel.IsOpen)panel.Toggle();
                    Require(board.TryStep(Vector2Int.right),"Cannot enter portal fixture");next+=6;break;
                case 16:
                    travel=(BlackHoleTravel)typeof(BlackHoleTravel).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                    Require(travel&&BlackHoleTravel.Selecting&&!(bool)Read("busy"),"Portal selection did not open");
                    target=BlackHolePortal.At(board,new Vector2Int(2,0));Require(target,"Missing destination fixture");
                    NavigationChecks();Shot("portal-map");break;
                case 17:
                    var point=(Vector3)Call("NavigationScreenPoint",target.transform.position);
                    Require(point.z>0&&Screen.safeArea.Contains(point)&&!MobileUI.OverUI(point),"Portal target is not tappable on screen");
                    TouchStep(TouchPhase.Began,point);TouchStep(TouchPhase.Ended,point);
                    Require((BlackHolePortal)Read("testDestination")==target,"Screen tap did not select destination");next+=10;break;
                case 18:
                    Require(!BlackHoleTravel.Selecting&&!BlackHoleTravel.InTransit&&board.PlayerCell==new Vector2Int(3,0),"Touch-selected portal did not finish at expected exit");
                    Shot("portal-touch-arrival");
                    File.WriteAllText("output/mobile-adaptation/portal-touch.txt","PASS: actual mechanism test scene; walk into portal, screen-space pan and pinch, UI gesture exclusion, no selection on drag/pinch release, unchanged portal positions, screen-space tap chooses destination, existing travel completes at expected exit. Synthetic touch samples in Editor; device testing still required.");break;
                default:SessionState.SetBool(Key,false);Debug.Log("Mobile visual QA captured");EditorApplication.Exit(0);return;
            }
            stage++;
        }catch(Exception e){SessionState.SetBool(Key,false);File.WriteAllText("output/mobile-adaptation/visual-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static object Read(string field)=>typeof(BlackHoleTravel).GetField(field,Private).GetValue(travel);
    static object Call(string method,params object[] args)=>typeof(BlackHoleTravel).GetMethod(method,Private).Invoke(travel,args);
    static Touch Finger(int id,TouchPhase phase,Vector2 point,Vector2 delta=default)=>new Touch{fingerId=id,phase=phase,position=point,deltaPosition=delta};
    static void TouchStep(TouchPhase phase,Vector2 point)=>Call("ProcessNavigationTouches",new object[]{new[]{Finger(1,phase,point)}});
    static void NavigationChecks(){
        var original=target.transform.position;var focus=(Vector3)Read("viewFocus");
        var center=new Vector2(Screen.width*.5f,Screen.height*.5f);
        TouchStep(TouchPhase.Began,center);TouchStep(TouchPhase.Moved,center+new Vector2(100,0));TouchStep(TouchPhase.Ended,center+new Vector2(100,0));
        Require(((Vector3)Read("viewFocus")-focus).sqrMagnitude>.01f,"Touch pan did not move view focus");
        Require(Read("testDestination")==null,"Pan released into unintended teleport");
        float size=(float)Read("viewSize");
        Call("ProcessNavigationTouches",new object[]{new[]{Finger(1,TouchPhase.Began,center-new Vector2(50,0)),Finger(2,TouchPhase.Began,center+new Vector2(50,0))}});
        Call("ProcessNavigationTouches",new object[]{new[]{Finger(1,TouchPhase.Moved,center-new Vector2(100,0),new Vector2(-50,0)),Finger(2,TouchPhase.Moved,center+new Vector2(100,0),new Vector2(50,0))}});
        Require((float)Read("viewSize")<size,"Pinch did not zoom map in");
        Call("ProcessNavigationTouches",new object[]{new[]{Finger(1,TouchPhase.Ended,center-new Vector2(100,0)),Finger(2,TouchPhase.Ended,center+new Vector2(100,0))}});
        Require(Read("testDestination")==null,"Pinch release triggered teleport");
        var safe=MobileUI.SafeRect;float k=ElementGameUI.K;
        var undo=new Vector2(safe.xMax-15*k,Screen.height-safe.y-15*k);
        Require(MobileUI.OverUI(undo),"Undo button not excluded from map input");
        var beforeUI=(Vector3)Read("viewFocus");
        TouchStep(TouchPhase.Began,undo);TouchStep(TouchPhase.Moved,center);TouchStep(TouchPhase.Ended,center);
        Require((Vector3)Read("viewFocus")==beforeUI&&Read("testDestination")==null,"UI gesture leaked into map");
        Require(target.transform.position==original,"Map gestures moved the portal object");
        typeof(BlackHoleTravel).GetField("viewFocus",Private).SetValue(travel,focus);
        typeof(BlackHoleTravel).GetField("viewSize",Private).SetValue(travel,size);
        Call("ApplyNavigation");
    }
    static void ChartChecks(){
        var chart=UnityEngine.Object.FindObjectOfType<ChapterAtlas>();var camera=chart.showcaseCamera;
        var position=camera.transform.position;float size=camera.orthographicSize;
        var center=new Vector2(Screen.width*.5f,Screen.height*.5f);
        var method=typeof(ChapterAtlas).GetMethod("ProcessChartTouches",Private);
        Action<Touch[]> step=t=>method.Invoke(chart,new object[]{t});
        step(Array.Empty<Touch>());step(new[]{Finger(1,TouchPhase.Began,center)});
        step(new[]{Finger(1,TouchPhase.Moved,center+new Vector2(100,0))});
        float expected=100f/Screen.width*2*size*camera.aspect;
        Require(Mathf.Abs(Vector3.Distance(position,camera.transform.position)-expected)<.01f,"Chart pan used render texture pixels instead of phone screen pixels");
        step(new[]{Finger(1,TouchPhase.Ended,center+new Vector2(100,0))});
        Require(!chart.IsInspecting,"Chart drag selected a planet");
        step(new[]{Finger(1,TouchPhase.Began,center-new Vector2(50,0)),Finger(2,TouchPhase.Began,center+new Vector2(50,0))});
        step(new[]{Finger(1,TouchPhase.Moved,center-new Vector2(100,0),new Vector2(-50,0)),Finger(2,TouchPhase.Moved,center+new Vector2(100,0),new Vector2(50,0))});
        Require(Mathf.Abs(camera.orthographicSize-size*.5f)<.01f,"Chart pinch zoom ratio incorrect");
        step(Array.Empty<Touch>());camera.transform.position=position;camera.orthographicSize=size;
        File.WriteAllText("output/mobile-adaptation/chart-touch.txt","PASS: actual chart scene, screen-normalized pan with pixel render texture, proportional pinch zoom, no planet selection on drag release. Synthetic Editor input, device test pending.");
    }
}
}
