using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class PushResponseRegression {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Request="output/push-response.run",Report="output/push-response.txt";
    static IEnumerator test;static GameObject root;static Scene scene;static GridPlayground b;
    static double deadline;static int frame;static bool background;
    static readonly double LoadedAt=EditorApplication.timeSinceStartup;
    static PushResponseRegression(){EditorApplication.update+=Tick;}
    static object Call(string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Flags).Invoke(b,args);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup-LoadedAt<1)return;
        if(test==null){
            if(!File.Exists(Request))return;
            if(!EditorApplication.isPlaying){SessionState.SetBool("PushResponse.OwnPlay",true);EditorApplication.isPlaying=true;return;}
            if(!Application.isPlaying)return;
            File.Delete(Request);background=Application.runInBackground;Application.runInBackground=true;
            test=Run();deadline=EditorApplication.timeSinceStartup+35;frame=-1;
        }
        if(frame==Time.frameCount)return;frame=Time.frameCount;
        try{
            Check(EditorApplication.timeSinceStartup<deadline,"Test timeout");
            if(test.MoveNext())return;
            File.WriteAllText(Report,"PASS: flat-bar scoop and upright-bar shove on real astronaut mesh; pose distinction and seamless style handoff; continuous bar/U pushes; shared trajectory and gait; smooth release; collision; per-push undo; direction change; repulsion; assembly cleanup. Preview: output/bar-push-poses.png");
        }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);Debug.LogException(ex);}
        test=null;if(root)UnityEngine.Object.Destroy(root);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);
        Application.runInBackground=background;
        if(SessionState.GetBool("PushResponse.OwnPlay",false)){SessionState.SetBool("PushResponse.OwnPlay",false);EditorApplication.isPlaying=false;}
    }
    static void Setup(MagnetShape shape,bool second=false,bool opposite=false){
        if(root)UnityEngine.Object.Destroy(root);
        root=new GameObject("Push response test");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
        b=root.AddComponent<GridPlayground>();b.enabled=false;
        b.player=new GameObject("Actor").transform;b.player.SetParent(root.transform);b.player.position=Vector3.zero;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Art/AstronautV1/AstronautVisual.prefab");
        b.playerVisual=UnityEngine.Object.Instantiate(prefab,b.player,false).transform;b.playerVisual.name="Visual";
        b.tiles=new GridTile[18];int n=0;
        for(int z=0;z<2;z++)for(int x=0;x<9;x++){
            var t=new GameObject("Tile");t.transform.SetParent(root.transform);t.transform.position=new Vector3(x*1.5f,0,z*1.5f);b.tiles[n++]=t.AddComponent<GridTile>();
        }
        var m=MagnetVisuals.Create(root.transform,Vector3.right*1.5f,shape,true,Quaternion.identity,1.5f,root.transform);
        b.magnets=second?new[]{m,MagnetVisuals.Create(root.transform,Vector3.right*3,shape,!opposite,Quaternion.identity,1.5f,root.transform)}:new[]{m};
        root.SetActive(true);b.CaptureInitialState();
    }
    static void Tap(Vector2Int direction){Call("PumpMovementInput",direction,true);}
    static bool Animating(){return ((IList)typeof(GridPlayground).GetField("pushPresentations",Flags).GetValue(b)).Count>0;}
    static void Pump(){Call("PumpMovementInput",Vector2Int.zero,false);}
    static void ValidateStances(){
        Setup(MagnetShape.Bar);
        var pose=b.player.GetComponent<PlayerPushPose>();pose.End();
        var cameraObject=new GameObject("Pose preview camera");cameraObject.transform.SetParent(root.transform);
        var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.8f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.20f);camera.cullingMask=1<<31;
        camera.transform.position=new Vector3(2,1.2f,2.5f);camera.transform.LookAt(new Vector3(0,.5f,.12f));
        foreach(var t in b.player.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
        var lightObject=new GameObject("Pose preview light");lightObject.transform.SetParent(root.transform);
        var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(35,-30,0);
        var atlas=new Texture2D(960,640,TextureFormat.RGB24,false);
        var rt=new RenderTexture(320,320,24);camera.targetTexture=rt;var active=RenderTexture.active;
        float lowHand=0,highHand=0;
        try{
            for(int row=0;row<2;row++){
                pose.End();PlayerPushPose.BeginFlowPush(b.player,b.cellSize,row==0?PlayerPushPose.FlowPushStyle.LiftBar:PlayerPushPose.FlowPushStyle.ShoveBar);
                for(int col=0;col<3;col++){
                    float target=new[]{.22f,.52f,.86f}[col];
                    float previous=col==0?0:new[]{.22f,.52f,.86f}[col-1];
                    for(float p=previous+.01f;p<target+.005f;p+=.01f)pose.AdvanceFlowPush(.01f*b.cellSize,.0046f,p);
                    if(col==0){
                        var mesh=(Mesh)typeof(PlayerPushPose).GetField("posed",Flags).GetValue(pose);
                        var filter=(MeshFilter)typeof(PlayerPushPose).GetField("body",Flags).GetValue(pose);
                        var arms=(bool[])typeof(PlayerPushPose).GetField("arms",Flags).GetValue(pose);
                        float minimum=float.MaxValue;var verts=mesh.vertices;
                        for(int i=0;i<verts.Length;i++)if(arms[i])minimum=Mathf.Min(minimum,filter.transform.TransformPoint(verts[i]).y);
                        if(row==0)lowHand=minimum;else highHand=minimum;
                    }
                    camera.Render();RenderTexture.active=rt;atlas.ReadPixels(new Rect(0,0,320,320),col*320,(1-row)*320);
                }
                var before=b.playerVisual.localRotation;
                var body=(Mesh)typeof(PlayerPushPose).GetField("posed",Flags).GetValue(pose);var beforeVertices=body.vertices;
                PlayerPushPose.BeginFlowPush(b.player,b.cellSize,row==0?PlayerPushPose.FlowPushStyle.ShoveBar:PlayerPushPose.FlowPushStyle.LiftBar);
                pose.AdvanceFlowPush(0,0,0);
                Check(Quaternion.Angle(before,b.playerVisual.localRotation)<.001f,"Style change snapped torso");
                var afterVertices=body.vertices;
                for(int i=0;i<beforeVertices.Length;i++)Check(Vector3.Distance(beforeVertices[i],afterVertices[i])<.00001f,"Style change snapped limbs");
            }
            Check(lowHand<highHand-.12f,"Flat-bar hands do not reach below standing-bar hands");
            atlas.Apply();File.WriteAllBytes("output/bar-push-poses.png",atlas.EncodeToPNG());
        }finally{RenderTexture.active=active;camera.targetTexture=null;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(atlas);pose.End();}
    }
    static IEnumerator Run(){
        scene=SceneManager.CreateScene("PushResponseValidation-"+Guid.NewGuid().ToString("N"));
        ValidateStances();
        // Exercise real keyboard routing in all eight perpendicular orientations.
        foreach(var first in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            foreach(int sign in new[]{-1,1}){
                Setup(MagnetShape.Bar);
                b.magnets[0].gameObject.SetActive(false);b.magnets=new MagnetPiece[0];
                var origin=new Vector2Int(4,first.y<0?1:0);
                if(first.x!=0)origin.y=sign<0?1:0;
                var second=first.x!=0?new Vector2Int(0,sign):new Vector2Int(sign,0);
                b.player.position=new Vector3(origin.x*b.cellSize,0,origin.y*b.cellSize);
                b.CaptureInitialState();yield return null;yield return null;
                Tap(first);yield return null;
                var before=b.player.position;Tap(second);yield return null;
                var delta=b.player.position-before;
                var incoming=new Vector3(first.x,0,first.y);var outgoing=new Vector3(second.x,0,second.y);
                Check(Vector3.Dot(delta,incoming)>Vector3.Dot(delta,outgoing)*2,"Walking turn cut diagonally at entry: "+first+" -> "+second+" before="+before+" delta="+delta+" dt="+Time.deltaTime);
                Check(Vector3.Dot(delta,outgoing)>0,"Walking curve did not respond on next frame");
                while(b.Busy){
                    before=b.player.position;yield return null;
                    Check(Vector3.Distance(before,b.player.position)<=b.cellSize/b.walkSeconds*1.5f*Time.deltaTime+.004f,"Walking curve exceeded speed cap");
                }
                Check(b.PlayerCell==origin+first+second,"Walking curve landed in wrong cell");
                Check(b.UndoStep()&&b.PlayerCell==origin+first,"Walking curve lost corner undo");
                Check(b.UndoStep()&&b.PlayerCell==origin,"Walking curve lost origin undo");
            }
        }
        foreach(var shape in new[]{MagnetShape.Bar,MagnetShape.Horseshoe}){
            Setup(shape);float began=Time.time;Check(b.TryStep(Vector2Int.right),"First push rejected");
            while(b.Busy)yield return null;float normal=Time.time-began;
            Check(b.UndoStep()&&b.PlayerCell==Vector2Int.zero,"Initial undo failed");
            began=Time.time;Tap(Vector2Int.right);
            for(int i=0;i<2;i++)yield return null;
            Check(!(bool)Call("CanInterruptPush",Vector2Int.right,false),"Held input can interrupt same direction");
            var position=b.player.position;
            var pose=b.player.GetComponent<PlayerPushPose>();
            var stance=b.playerVisual.localRotation;
            float gait=(float)typeof(PlayerPushPose).GetField("flowDistance",Flags).GetValue(pose);
            Tap(Vector2Int.right);
            Check(b.player.position==position,"Tap moved player before next frame");
            Check(Quaternion.Angle(stance,b.playerVisual.localRotation)<.001f,"Tap snapped the visible stance");
            Check((float)typeof(PlayerPushPose).GetField("flowDistance",Flags).GetValue(pose)==gait,"Tap reset gait phase");
            bool crossed=false;
            while(b.Busy){
                Pump();yield return null;
                Check(!Animating(),"Ordinary push allocated replay geometry");
                float normalPushSeconds=shape==MagnetShape.Bar?b.barPushSeconds:b.uRollSeconds;
                Check((float)typeof(GridPlayground).GetField("pushFlowSpeed",Flags).GetValue(b)<=b.maxPushSpeedMultiplier/normalPushSeconds+.0001f,"Push exceeded configured speed cap");
                Check(Mathf.Abs(b.magnets[0].transform.position.x-b.player.position.x-b.cellSize)<.001f,"Player and magnet travel diverged");
                float distance=(float)typeof(PlayerPushPose).GetField("flowDistance",Flags).GetValue(pose);
                Check(Mathf.Abs(distance-b.player.position.x)<.001f,"Push gait restarted or diverged from distance");
                if(b.player.position.x>b.cellSize&&b.Busy){
                    crossed=true;
                    Check(Quaternion.Angle(b.playerVisual.localRotation,Quaternion.identity)>8,"Body stood upright between pushes");
                }
                if(b.Busy)Check(b.player.position.x>position.x+.00001f,"Idle frame between pushes");
                position=b.player.position;
            }
            Check(crossed,"Second tap was not chained");
            Check(Time.time-began<normal*1.85f,"Fresh taps did not improve throughput");
            Check(b.PlayerCell==new Vector2Int(2,0)&&b.MagnetAt(new Vector2Int(3,0))==b.magnets[0],"Two taps did not produce two pushes");
            Check(b.UndoCount==2&&!Animating(),"Undo count or animation cleanup wrong");
            Check((bool)typeof(PlayerPushPose).GetField("flowPushing",Flags).GetValue(pose),"Push pose snapped to rest on stop");
            float releaseEnd=Time.time+.18f;while(Time.time<releaseEnd)yield return null;
            Check(!(bool)typeof(PlayerPushPose).GetField("flowPushing",Flags).GetValue(pose),"Push pose never settled");
            Check(Quaternion.Angle(b.playerVisual.localRotation,Quaternion.identity)<.001f,"Visual did not return to rest");
            Check(b.UndoStep()&&b.PlayerCell==Vector2Int.right&&b.MagnetAt(new Vector2Int(2,0)),"First undo not one push");
            Check(b.UndoStep()&&b.PlayerCell==Vector2Int.zero&&b.MagnetAt(Vector2Int.right),"Second undo not original");
        }
        Setup(MagnetShape.Bar);b.tiles[3].blocked=true;Tap(Vector2Int.right);yield return null;Tap(Vector2Int.right);
        while(b.Busy){Pump();yield return null;}Pump();
        Check(!b.Busy&&b.UndoCount==1&&b.MagnetAt(new Vector2Int(2,0)),"Rapid push passed obstacle");
        Setup(MagnetShape.Bar);Tap(Vector2Int.right);
        int taps=1;float nextTap=Time.time+.25f;
        while(b.Busy||taps<5){
            if(taps<5&&Time.time>=nextTap){Tap(Vector2Int.right);taps++;nextTap=Time.time+.25f;}
            Pump();yield return null;
        }
        Check(b.PlayerCell==new Vector2Int(5,0)&&b.UndoCount==5&&!Animating(),"Five rapid taps lost steps or undo records");
        for(int i=4;i>=0;i--)Check(b.UndoStep()&&b.PlayerCell==new Vector2Int(i,0),"Continuous chain undo skipped a cell");
        Tap(Vector2Int.right);yield return null;Tap(Vector2Int.right);yield return null;
        Check(b.UndoStep()&&!b.Busy&&!Animating(),"Undo during flowing push did not cancel playback");
        var undone=b.player.position;yield return null;yield return null;
        Check(b.player.position==undone,"Flow coroutine moved player after undo");
        foreach(var shape in new[]{MagnetShape.Bar,MagnetShape.Horseshoe}){
            Setup(shape);Tap(Vector2Int.right);yield return null;
            float angle=Quaternion.Angle(b.player.rotation,Quaternion.LookRotation(Vector3.forward));
            var inputPosition=b.player.position;
            Tap(Vector2Int.up);
            Check(Quaternion.Angle(b.player.rotation,Quaternion.LookRotation(Vector3.forward))<angle,"Turn did not respond on input frame");
            Check(b.player.position.z>inputPosition.z&&b.player.position.x<b.cellSize,"New direction did not move immediately before old cell centre");
            var firstDelta=b.player.position-inputPosition;
            Check(firstDelta.x>firstDelta.z*2,"Turn began as a diagonal shortcut instead of following the old direction");
            Check(Vector3.Distance(inputPosition,b.player.position)<=b.cellSize/b.walkSeconds*1.5f*Time.deltaTime+.001f,"Input teleported visible player");
            Check(b.UndoCount==2,"New logical step was not committed independently");
            bool walkedBeforeMagnetFinished=false;
            while(b.Busy){
                var before=b.player.position;Pump();yield return null;
                var delta=b.player.position-before;
                if(b.player.position.z>0&&Animating())walkedBeforeMagnetFinished=true;
                Check(delta.magnitude<=b.cellSize/b.walkSeconds*1.5f*Time.deltaTime+.015f,"Turn exceeded movement cap: distance="+delta.magnitude+" dt="+Time.deltaTime+" from="+before+" to="+b.player.position);
            }
            Check(walkedBeforeMagnetFinished,"Player waited for magnet presentation before turning");
            Check(b.PlayerCell==Vector2Int.one,"Turn during push lost input");
            Check(b.UndoStep()&&b.PlayerCell==Vector2Int.right&&!Animating(),"Turn undo failed to clean independent animation");
            Check(b.UndoStep()&&b.PlayerCell==Vector2Int.zero,"Push undo after turn failed");
        }
        // Block the inside corner: blending must fall back to the cardinal path.
        Setup(MagnetShape.Bar);b.tiles[9].blocked=true;
        Tap(Vector2Int.right);yield return null;Tap(Vector2Int.up);
        Check(Mathf.Abs(b.player.position.z)<.00001f,"Turn cut across blocked inner corner");
        while(b.Busy){Pump();yield return null;
            Check(Mathf.Abs(b.player.position.z)<.00001f||Mathf.Abs(b.player.position.x-b.cellSize)<.00001f,"Visible path crossed obstacle");
        }
        Check(b.PlayerCell==Vector2Int.one,"Safe corner fallback missed destination");
        Setup(MagnetShape.Bar);b.tiles[10].blocked=true;
        Tap(Vector2Int.right);yield return null;Tap(Vector2Int.up);
        while(b.Busy){Pump();yield return null;}Pump();
        Check(b.PlayerCell==Vector2Int.right&&b.UndoCount==1,"Blocked new direction created a step");
        Setup(MagnetShape.Bar);Tap(Vector2Int.right);yield return null;Tap(Vector2Int.up);
        Check(b.UndoStep()&&b.PlayerCell==Vector2Int.right&&!Animating(),"Mid-blend undo did not restore logical corner");
        var restored=b.player.position;yield return null;yield return null;
        Check(b.player.position==restored,"Blend kept running after undo");
        Setup(MagnetShape.Bar,true);Tap(Vector2Int.right);yield return null;Tap(Vector2Int.right);
        while(b.Busy){Pump();yield return null;}Pump();while(b.Busy){Pump();yield return null;}
        Check(b.PlayerCell==new Vector2Int(2,0)&&b.MagnetAt(new Vector2Int(3,0))&&b.MagnetAt(new Vector2Int(4,0)),"Repulsion continuation failed");
        Setup(MagnetShape.Bar,true,true);Tap(Vector2Int.right);yield return null;Tap(Vector2Int.right);
        while(b.Busy){Pump();yield return null;}Pump();
        Check(!b.Busy&&b.magnets[1].product==MagnetProduct.WideBar&&b.PlayerCell==Vector2Int.right,"Assembly continuation bypassed product rules");
        Check(b.UndoStep()&&b.magnets[0].gameObject.activeSelf&&b.magnets[1].product==MagnetProduct.None&&!Animating(),"Assembly undo failed");
    }
}
}
