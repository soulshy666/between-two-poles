using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
// Runs only in the mechanism lab during Play mode; never saves scene changes.
[InitializeOnLoad]
public sealed class PortalRingDemo : EditorWindow {
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static string status="选择一个场景，自动演示推入、选洞和出洞。";
    static bool running;
    static readonly string[] Cases={"立环撞石头，玩家向下出洞","同向入洞，圆环独立反弹","滚动圆环撞石头后反向出洞","圆环落入太空，玩家落到环上","出洞撞第二个圆环，继续入洞","出口固定组合磁铁反弹","目标黑洞已有磁铁","地面连锁撞击，将第二个圆环送入洞","滚动圆环撞高台墙面，弹回洞中"};
    static PortalRingDemo(){
        EditorApplication.delayCall+=()=>{
            if(!File.Exists("output/portal-ring-demo.run"))return;
            File.Delete("output/portal-ring-demo.run");Open();StartDemo(-1);
        };
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("PortalRingDemo.Pending",false)){
                SessionState.SetBool("PortalRingDemo.Pending",false);
                double ready=EditorApplication.timeSinceStartup+1;
                EditorApplication.CallbackFunction tick=null;
                tick=()=>{if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=tick;StartDemo(SessionState.GetInt("PortalRingDemo.Case",-1));};
                EditorApplication.update+=tick;
            }
            if(state==PlayModeStateChange.ExitingPlayMode)running=false;
        };
    }
    [MenuItem("两极之间/测试/圆环黑洞实机演示")]
    public static void Open(){GetWindow<PortalRingDemo>("圆环黑洞演示").Show();}
    void OnGUI(){
        EditorGUILayout.HelpBox(status,MessageType.Info);
        EditorGUI.BeginDisabledGroup(running||BlackHoleTravel.InTransit||BlackHoleTravel.Selecting);
        if(GUILayout.Button("连续演示全部场景"))StartDemo(-1);
        for(int i=0;i<Cases.Length;i++)if(GUILayout.Button((i+1)+". "+Cases[i]))StartDemo(i);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.LabelField("只在运行模式生成临时布局，停止运行即可恢复。",EditorStyles.wordWrappedLabel);
    }
    static void StartDemo(int which){
        if(running)return;
        if(!EditorApplication.isPlaying){SessionState.SetInt("PortalRingDemo.Case",which);SessionState.SetBool("PortalRingDemo.Pending",true);EditorApplication.isPlaying=true;return;}
        var lab=UnityEngine.Object.FindObjectOfType<MagnetTestLayouts>();
        if(!lab){status="请先打开磁铁机制测试场景。";return;}
        running=true;lab.StartCoroutine(Run(lab,which));
    }
    static IEnumerator Run(MagnetTestLayouts lab,int which){
        var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Demonstrate(lab,which));
        try{
            while(stack.Count>0){
                var current=stack.Peek();bool next=false;Exception failure=null;
                try{next=current.MoveNext();}catch(Exception ex){failure=ex;}
                if(failure!=null){status="演示失败："+failure.Message;File.WriteAllText("output/portal-ring-demo-result.txt",failure.ToString());Debug.LogException(failure);yield break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current.Current is IEnumerator child){stack.Push(child);continue;}
                yield return current.Current;
            }
            status="演示完成，可单独重播任一场景，或在当前布局中继续操作。";
            File.WriteAllText("output/portal-ring-demo-result.txt","PASS: real-time Play mode demonstration completed: "+(which<0?Cases.Length:1)+" scenarios.");
        }finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();running=false;foreach(var window in Resources.FindObjectsOfTypeAll<PortalRingDemo>())window.Repaint();}
    }
    static IEnumerator Demonstrate(MagnetTestLayouts lab,int which){
        for(int mode=which<0?0:which;mode<(which<0?Cases.Length:which+1);mode++){
            var b=lab.board;lab.Load(10);
            foreach(var p in MagnetTestLayouts.Portals(b)){p.gameObject.SetActive(false);UnityEngine.Object.Destroy(p.gameObject);}
            yield return null;
            var root=b.TileAt(Vector2Int.zero).transform.parent;
            var portal=lab.PlacePortal(b.TileAt(Vector2Int.zero));
            bool rolling=mode==2||mode==4||mode==7||mode==8;
            var ring=MagnetVisuals.Create(root,new Vector3(-b.cellSize,0,0),MagnetShape.Horseshoe,true,Quaternion.identity,b.cellSize,lab.center);
            var pose=rolling?Quaternion.Euler(90,0,0):Quaternion.identity;
            MagnetVisuals.Product(ring,MagnetProduct.Ring,b.cellSize,pose,true);ring.geometry.rotation=pose;MagnetVisuals.Ground(ring);ring.walkable=!rolling;
            b.magnets=new[]{ring};b.player.position=new Vector3(-2*b.cellSize,0,0);b.player.rotation=Quaternion.LookRotation(Vector3.right);
            if(mode<=2||mode==8){
                var tile=b.TileAt(Vector2Int.right);tile.blocked=mode!=8;tile.rockStyle=2;tile.rockTheme=1;
                if(mode==8)tile.surfaceHeight=b.LayerHeight;
                var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);stone.name="调试高台";stone.transform.SetParent(tile.transform,false);
                float height=mode==8?tile.surfaceHeight:1;
                stone.transform.localPosition=Vector3.up*height*.5f;stone.transform.localScale=new Vector3(b.cellSize*.96f,height,b.cellSize*.96f);
                stone.GetComponent<Renderer>().sharedMaterial=tile.GetComponentInChildren<Renderer>().sharedMaterial;
                UnityEngine.Object.Destroy(stone.GetComponent<Collider>());
                var binding=stone.AddComponent<IslandSurfaceAnchor>();binding.center=lab.center;binding.Apply();
                IceRockVisuals.Apply(b);
            }
            if(mode==3){string message;lab.EditTile(Vector2Int.right,false,out message);}
            BlackHolePortal occupied=null;
            if(mode==4||mode==5||mode==6||mode==7){
                int x=mode==7?-1:mode==4?2:mode==5?1:3;
                if(mode==4||mode==6)occupied=lab.PlacePortal(b.TileAt(new Vector2Int(mode==4?4:3,0)));
                var other=MagnetVisuals.Create(root,new Vector3(x*b.cellSize,0,0),MagnetShape.Horseshoe,false,Quaternion.identity,b.cellSize,lab.center);
                var product=mode==5?MagnetProduct.CrossU:MagnetProduct.Ring;
                var otherPose=mode==4||mode==7?Quaternion.Euler(90,0,0):Quaternion.identity;
                MagnetVisuals.Product(other,product,b.cellSize,otherPose,true);other.geometry.rotation=otherPose;MagnetVisuals.Ground(other);
                if(mode==6){other.storedInPortal=occupied;other.gameObject.SetActive(false);}
                b.magnets=new[]{ring,other};
                if(mode==7){ring.transform.position=Vector3.left*3*b.cellSize;b.player.position=Vector3.left*4*b.cellSize;}
            }
            b.CaptureInitialState();var panel=lab.GetComponent<MagnetDebugPanel>();if(panel&&panel.IsOpen)panel.Toggle();
            status=Cases[mode];lab.instruction=status;foreach(var window in Resources.FindObjectsOfTypeAll<PortalRingDemo>())window.Repaint();
            yield return new WaitForSecondsRealtime(1);
            yield return Step(b,Vector2Int.right);
            if(mode==7){
                float timeout=Time.realtimeSinceStartup+15;
                while(!b.magnets[1].storedInPortal){if(Time.realtimeSinceStartup>timeout)throw new Exception("地面连锁圆环未进入黑洞");yield return null;}
                if(portal.Cargo!=b.magnets[1]||b.magnets[1].gameObject.activeSelf||b.magnets[1].portalRollSpeed<=0)throw new Exception("地面连锁入洞状态错误");
                yield return new WaitForSecondsRealtime(2);
                ScreenCapture.CaptureScreenshot("output/portal-ring-demo-8.png");yield return new WaitForEndOfFrame();yield return null;continue;
            }
            if(mode!=1&&mode!=3){yield return Step(b,Vector2Int.up);yield return Step(b,Vector2Int.right);yield return Step(b,Vector2Int.down);}
            else yield return Step(b,Vector2Int.right);
            float deadline=Time.realtimeSinceStartup+30;
            while(!BlackHoleTravel.Selecting||BlackHoleTravel.InTransit){if(Time.realtimeSinceStartup>deadline)throw new Exception("未进入黑洞选择状态");yield return null;}
            yield return new WaitForSecondsRealtime(1);
            var travel=(BlackHoleTravel)typeof(BlackHoleTravel).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            var select=typeof(BlackHoleTravel).GetMethod("SelectTestPortal",Private);
            if(mode==6){if((bool)select.Invoke(travel,new object[]{occupied}))throw new Exception("已有磁铁的目标洞未被拒绝");yield return new WaitForSecondsRealtime(2);}
            if(!(bool)select.Invoke(travel,new object[]{portal}))throw new Exception("演示出口未接受选择");
            deadline=Time.realtimeSinceStartup+45;
            while(BlackHoleTravel.Selecting||BlackHoleTravel.InTransit){if(Time.realtimeSinceStartup>deadline)throw new Exception("出洞未完成");yield return null;}
            var expected=mode==1||mode==3?Vector2Int.right:Vector2Int.down;
            if(b.PlayerCell!=expected||b.Busy)throw new Exception("玩家出洞方向或忙碌状态错误："+Cases[mode]);
            if(mode==0||mode==1||mode==5){
                if(ring.storedInPortal||Mathf.Abs(ring.transform.position.x+b.cellSize)>.01f||MagnetPiece.FlatU(ring.Pose))throw new Exception("立环未反向出洞并保持竖立");
            }
            if(mode==2||mode==3||mode==8){
                if(!ring.walkable||!MagnetPiece.FlatU(ring.Pose)||ring.storedInPortal)throw new Exception("圆环未完成太空悬浮");
                if(mode==3&&Mathf.Abs(ring.transform.position.x-b.cellSize)>.01f)throw new Exception("悬浮圆环落点错误");
            }
            if(mode==4&&occupied.Cargo!=b.magnets[1])throw new Exception("碰撞传递未送入第二个黑洞");
            if(mode==6&&occupied.Cargo!=b.magnets[1])throw new Exception("目标洞原有磁铁被覆盖");
            yield return new WaitForSecondsRealtime(2);
            ScreenCapture.CaptureScreenshot("output/portal-ring-demo-"+(mode+1)+".png");
            yield return new WaitForEndOfFrame();yield return null;
        }
    }
    static IEnumerator Step(GridPlayground board,Vector2Int direction){
        if(!board.TryStep(direction))throw new Exception("演示移动被拒绝："+direction);
        float deadline=Time.realtimeSinceStartup+15;
        while(board.Busy){if(Time.realtimeSinceStartup>deadline)throw new Exception("推行动画超时");yield return null;}
        yield return new WaitForSecondsRealtime(.2f);
    }
}
}
