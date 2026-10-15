using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    IslandCamera navigationCamera;
    CurvedTrialCamera navigationCurve;
    Camera viewCamera;
    FiveIslandWindow navigationWindow;
    bool cameraEnabled,windowEnabled,curveFit,dragging;
    float savedSize,viewSize,savedFarClip,navigationZoomVelocity,navigationSmoothTime;
    Vector3 savedViewingOffset;
    Vector3 savedFocus,viewFocus,pressPosition,lastMouse;
    readonly Dictionary<Renderer,bool> navigationRenderers=new Dictionary<Renderer,bool>();
    GridPlayground inspectionBoard;
    Vector3? inspectionPosition;
    float navigationStartSize;
    public bool PlayerReturnRequested {get;private set;}
    bool playerReturnPressed;
    public void BeginInspection(GridPlayground board,bool preserveCurrentView=false){
        inspectionBoard=board;
        Vector3 position;
        inspectionPosition=ChapterMapReturn.TrySuspendedPosition(board.gameObject.scene.path,out position)?(Vector3?)position:null;
        BeginNavigation(board);
        // Local inspection inherits the island framing; player position is only for the marker.
        if(!preserveCurrentView&&inspectionPosition.HasValue){viewFocus=inspectionPosition.Value;ApplyNavigation();}
    }

    public System.Collections.IEnumerator PullBackInspection(){
        if(!viewCamera)yield break;float start=viewCamera.orthographicSize;
        navigationSmoothTime=.1f;if(navigationCurve)navigationCurve.navigationZoomSmoothTime=.1f;
        for(float age=0;age<.55f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)*AnimationSpeed){
            viewSize=Mathf.Lerp(start,Mathf.Min(80,start*1.65f),Mathf.SmoothStep(0,1,age/.55f));ApplyNavigation();yield return null;
        }
    }
    public System.Collections.IEnumerator ReturnInspectionToPlayer(){
        if(!viewCamera||!inspectionBoard||!inspectionPosition.HasValue)yield break;
        Vector3 startFocus=viewFocus;
        Vector3 target=navigationCamera.CurrentIsland?navigationCamera.CurrentIsland.position:inspectionPosition.Value;
        float startSize=viewCamera.orthographicSize,endSize=savedSize;
        if(navigationCurve){
            navigationCurve.fitIsland=curveFit;navigationCurve.planningSize=savedSize;navigationCurve.Apply(target);
            endSize=navigationCurve.TargetZoomSize;
            navigationCurve.fitIsland=false;
        }
        for(float age=0;age<.85f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)){
            float t=Mathf.SmoothStep(0,1,age/.85f);
            viewFocus=Vector3.Lerp(startFocus,target,t);
            viewSize=Mathf.Exp(Mathf.Lerp(Mathf.Log(startSize),Mathf.Log(endSize),t));
            ApplyNavigation();viewCamera.orthographicSize=viewSize;
            if(navigationCurve)navigationCurve.ResetZoomVelocity();
            yield return null;
        }
        viewFocus=target;viewSize=endSize;ApplyNavigation();viewCamera.orthographicSize=endSize;
        if(navigationCurve)navigationCurve.ResetZoomVelocity();
    }
    public Texture2D CaptureInspectionFrame(){
        if(!viewCamera)return null;
        var pixel=viewCamera.GetComponent<PixelWorldCamera>();var buffer=pixel?pixel.Buffer:null;
        if(!buffer)return null;
        var previous=RenderTexture.active;
        try{RenderTexture.active=buffer;var image=new Texture2D(buffer.width,buffer.height,TextureFormat.RGB24,false){filterMode=FilterMode.Point};image.ReadPixels(new Rect(0,0,buffer.width,buffer.height),0,0);image.Apply();return image;}
        finally{RenderTexture.active=previous;}
    }
    public void TickInspection(){PlayerReturnRequested=false;if(inspectionBoard)UpdateNavigation();}
    public void FinishInspection(){EndNavigation();inspectionBoard=null;inspectionPosition=null;PlayerReturnRequested=false;playerReturnPressed=false;}
    bool VisitedRoom(string room){
        if(inspectionBoard)return SessionExploration.GetInt(ChapterAtlas.Key(inspectionBoard.gameObject.scene.path,room),0)>0;
        if(!origin)return false;
        return room==origin.Room||SessionExploration.GetInt(ChapterAtlas.Key(origin.gameObject.scene.path,room),0)>0;
    }
    void BeginNavigation(GridPlayground board,bool mapPresentation=true){
        navigationRenderers.Clear();
        foreach(var camera in FindObjectsOfType<IslandCamera>())if(camera.enabled&&camera.board==board){navigationCamera=camera;break;}
        if(!navigationCamera)throw new System.InvalidOperationException("黑洞总览找不到当前关卡镜头");
        viewCamera=navigationCamera.GetComponent<Camera>();navigationCurve=navigationCamera.GetComponent<CurvedTrialCamera>();
        navigationStartSize=viewCamera.orthographicSize;
        savedViewingOffset=navigationCamera.viewingOffset;savedFarClip=viewCamera.farClipPlane;navigationZoomVelocity=0;navigationSmoothTime=1.4f;
        cameraEnabled=navigationCamera.enabled;viewFocus=navigationCamera.transform.position-navigationCamera.viewingOffset;
        navigationCamera.enabled=false;
        if(navigationCurve){savedFocus=navigationCurve.editFocus;savedSize=navigationCurve.planningSize;curveFit=navigationCurve.fitIsland;navigationCurve.fitIsland=false;navigationCurve.navigationZoomSmoothTime=1.4f;navigationCurve.ResetZoomVelocity();}
        else savedSize=viewCamera.orthographicSize;
        viewSize=mapPresentation?(inspectionBoard?viewCamera.orthographicSize*1.8f:Mathf.Max(12f,viewCamera.orthographicSize*1.15f)):viewCamera.orthographicSize;
        navigationWindow=board.GetComponent<FiveIslandWindow>();
        if(navigationWindow){
            windowEnabled=navigationWindow.enabled;navigationWindow.enabled=false;
            if(mapPresentation)foreach(var room in navigationWindow.rooms)foreach(var renderer in room.surfaces)SetNavigationVisible(renderer,true);
        }
        if(mapPresentation)foreach(var m in board.magnets)if(m&&m.gameObject.activeInHierarchy){
            // Map appearance is restored when navigation closes.
            bool show=true;
            foreach(var renderer in m.GetComponentsInChildren<Renderer>())SetNavigationVisible(renderer,show);
        }
        if(mapPresentation){BeginUnknownIslands(board);BeginMapLighting(board);}
        dragging=false;playerReturnPressed=false;PlayerReturnRequested=false;ApplyNavigation();
    }
    void SetNavigationVisible(Renderer renderer,bool show){
        if(!renderer)return;if(!navigationRenderers.ContainsKey(renderer))navigationRenderers.Add(renderer,renderer.enabled);renderer.enabled=show;
    }
    void ApplyNavigation(){
        if(!navigationCamera)return;
        if(Input.GetMouseButtonDown(0))playerReturnPressed=HitInspectionPlayer(Input.mousePosition);
        if(Input.GetMouseButton(1)||Input.GetMouseButton(2))playerReturnPressed=false;
        // Orthographic zoom widens the ground footprint without moving the lens.
        // Retreat along the same viewing direction so its near plane cannot slice that footprint.
        float depth=savedViewingOffset.magnitude+Mathf.Max(0,viewCamera.orthographicSize-navigationStartSize)*1.5f;
        navigationCamera.viewingOffset=savedViewingOffset.normalized*depth;
        viewCamera.farClipPlane=Mathf.Max(savedFarClip,depth+Mathf.Max(viewSize,viewCamera.orthographicSize)*2f+50f);
        if(navigationCurve){navigationCurve.editFocus=viewFocus;navigationCurve.planningSize=viewSize/(navigationCurve.PlanarChapter?.8f:1f);navigationCurve.Apply(viewFocus);}
        else{navigationCamera.ApplyView(viewFocus);viewCamera.orthographicSize=Mathf.SmoothDamp(viewCamera.orthographicSize,viewSize,ref navigationZoomVelocity,navigationSmoothTime,Mathf.Infinity,Mathf.Min(Time.deltaTime,.05f));}
    }
    bool GroundPoint(Vector3 mouse,out Vector3 point){
        point=Vector3.zero;float distance;var ray=viewCamera.ViewportPointToRay(new Vector3(mouse.x/Screen.width,mouse.y/Screen.height,0));
        if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out distance))return false;point=ray.GetPoint(distance);return true;
    }
    void UpdateNavigation(){
        if(!navigationCamera)return;
        if(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2)){pressPosition=lastMouse=Input.mousePosition;dragging=false;}
        if(Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2)){
            var mouse=Input.mousePosition;
            if((mouse-pressPosition).sqrMagnitude>36)dragging=true;
            Vector3 before,after;
            if((Input.GetMouseButton(0)?dragging:true)&&GroundPoint(lastMouse,out before)&&GroundPoint(mouse,out after))viewFocus+=(before-after)*.4f/Mathf.Max(1f,Shader.GetGlobalFloat("_IslandDisplayScale"));
            lastMouse=mouse;
        }
        ApplyNavigationScroll(Input.mouseScrollDelta.y);ApplyNavigation();
        if(inspectionBoard){
            if(Input.GetMouseButtonUp(0)){
                PlayerReturnRequested=playerReturnPressed&&!dragging&&(Input.mousePosition-pressPosition).sqrMagnitude<=36&&HitInspectionPlayer(Input.mousePosition);
                playerReturnPressed=false;
            }
            return; // Inspection never selects or activates a portal.
        }
        if(Input.GetMouseButtonUp(0)&&!dragging&&Input.mousePosition.y<Screen.height-88){
            BlackHolePortal selected=null;float best=38*38;
            foreach(var portal in testPortals){if(!portal)continue;
                var screen=NavigationScreenPoint(portal.transform.position);
                float d=((Vector2)screen-(Vector2)Input.mousePosition).sqrMagnitude;
                if(screen.z>0&&d<best){selected=portal;best=d;}
            }
            if(selected)SelectTestPortal(selected);
        }
    }
    bool HitInspectionPlayer(Vector2 mouse){
        if(!inspectionBoard||!inspectionPosition.HasValue||!viewCamera)return false;
        var point=new Vector2(mouse.x,Screen.height-mouse.y);
        float scale=Mathf.Max(.7f,Screen.height/900f);
        // Leave the navigation buttons and header to their own UI handlers.
        if(point.y<80*scale||new Rect(12*scale,12*scale,230*scale,184*scale).Contains(point))return false;
        Vector2 marker,direction;bool offscreen;
        if(!OriginIndicator(out marker,out direction,out offscreen)||offscreen)return false;
        var player=NavigationScreenPoint(inspectionPosition.Value);
        float radius=52*scale;
        return (point-new Vector2(player.x,Screen.height-player.y)).sqrMagnitude<=radius*radius
            ||new Rect(marker.x-48,marker.y-48,96,112).Contains(point);
    }
    void ApplyNavigationScroll(float scroll){
        if(Mathf.Abs(scroll)<.001f||!viewCamera)return;
        // The first wheel input takes control from the slow entrance zoom.
        if(navigationSmoothTime>.2f){viewSize=viewCamera.orthographicSize;navigationZoomVelocity=0;if(navigationCurve)navigationCurve.ResetZoomVelocity();}
        navigationSmoothTime=.1f;
        if(navigationCurve)navigationCurve.navigationZoomSmoothTime=navigationSmoothTime;
        viewSize=Mathf.Clamp(viewSize-scroll*1.5f,4,80);
        // Wheel input only changes this map; the chart is opened explicitly by its button.
    }
    Vector3 NavigationScreenPoint(Vector3 world){
        // The world camera renders into a small pixel buffer, not the GUI-sized screen.
        var viewport=viewCamera.WorldToViewportPoint(CrashColorReveal.DisplayPoint(world));
        return new Vector3(viewport.x*Screen.width,viewport.y*Screen.height,viewport.z);
    }
    bool OriginIndicator(out Vector2 position,out Vector2 direction,out bool offscreen){
        position=direction=Vector2.zero;offscreen=false;
        if(!viewCamera||(!inspectionPosition.HasValue&&!origin))return false;
        var screen=NavigationScreenPoint(inspectionPosition.HasValue?inspectionPosition.Value:origin.transform.position);
        var target=new Vector2(screen.x,Screen.height-screen.y);
        offscreen=screen.z<=0||target.x<0||target.x>Screen.width||target.y<0||target.y>Screen.height;
        if(!offscreen){position=new Vector2(Mathf.Clamp(target.x,48,Screen.width-48),Mathf.Clamp(target.y-64,48,Screen.height-64));direction=(target-position).normalized;return true;}
        var center=new Vector2(Screen.width*.5f,Screen.height*.5f);
        direction=target-center;if(screen.z<=0&&!viewCamera.orthographic)direction=-direction;
        if(direction.sqrMagnitude<.001f)direction=Vector2.up;
        direction.Normalize();
        // Keep the arrow and its caption inside the viewport, clear of navigation buttons.
        float left=48,right=Screen.width-48,top=116,bottom=Screen.height-94;
        float tx=Mathf.Abs(direction.x)<.001f?float.PositiveInfinity:((direction.x>0?right:left)-center.x)/direction.x;
        float ty=Mathf.Abs(direction.y)<.001f?float.PositiveInfinity:((direction.y>0?bottom:top)-center.y)/direction.y;
        position=center+direction*Mathf.Min(tx,ty);return true;
    }
    readonly Texture2D[] originArrows=new Texture2D[8];
    Texture2D OriginArrowTexture(int heading){
        if(originArrows[heading])return originArrows[heading];
        const int size=17;var filled=new bool[size*size];
        float angle=heading*45*Mathf.Deg2Rad,sin=Mathf.Sin(angle),cos=Mathf.Cos(angle);
        // Rasterize the rotated solid shape onto the destination grid. Rotating isolated
        // source blocks left holes and overlapping pixels at diagonal headings.
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float dx=x-8,dy=y-8;
            float localX=dx*cos+dy*sin,localY=-dx*sin+dy*cos;
            filled[y*size+x]=(localY>=-4.5f&&localY<=.5f&&Mathf.Abs(localX)<=localY+4.6f)
                ||(localY>=0&&localY<=5.5f&&Mathf.Abs(localX)<=1.35f);
        }
        var colors=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            bool border=false;
            if(!filled[y*size+x])for(int oy=-1;oy<=1;oy++)for(int ox=-1;ox<=1;ox++){
                int nx=x+ox,ny=y+oy;
                if(nx>=0&&nx<size&&ny>=0&&ny<size&&filled[ny*size+nx])border=true;
            }
            colors[(size-1-y)*size+x]=filled[y*size+x]?new Color(1f,.2f,.25f,1):border?new Color(.06f,.08f,.1f,1):Color.clear;
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Black hole position arrow "+heading,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels(colors);texture.Apply();originArrows[heading]=texture;return texture;
    }
    void OnDestroy(){ReleaseMapLighting();EndUnknownIslands();foreach(var texture in originArrows)if(texture){if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);}}
    void DrawOriginIndicator(){
        if(Event.current.type!=EventType.Repaint)return;
        Vector2 position,direction;bool offscreen;if(!OriginIndicator(out position,out direction,out offscreen))return;
        int heading=((Mathf.RoundToInt(Mathf.Atan2(direction.x,-direction.y)*Mathf.Rad2Deg/45f)%8)+8)%8;
        var old=GUI.color;GUI.color=Color.white;
        GUI.DrawTexture(new Rect(Mathf.Round(position.x)-42,Mathf.Round(position.y)-42,85,85),OriginArrowTexture(heading));
        GUI.color=Color.white;

        GUI.color=old;
    }
    void EndNavigation(){
        mapIslands.Clear();
        EndUnknownIslands();
        foreach(var item in navigationRenderers)if(item.Key)item.Key.enabled=item.Value;navigationRenderers.Clear();
        if(viewCamera)viewCamera.farClipPlane=savedFarClip;
        if(navigationCamera)navigationCamera.viewingOffset=savedViewingOffset;
        if(navigationCurve){navigationCurve.navigationZoomSmoothTime=0;navigationCurve.editFocus=savedFocus;navigationCurve.planningSize=savedSize;navigationCurve.fitIsland=curveFit;}
        if(navigationCamera){navigationCamera.enabled=cameraEnabled;if(!navigationCurve&&viewCamera)viewCamera.orthographicSize=savedSize;}
        if(navigationWindow){navigationWindow.enabled=windowEnabled;navigationWindow.Show(navigationWindow.CurrentRoom);}
        navigationCamera=null;navigationCurve=null;navigationWindow=null;viewCamera=null;
    }
    void FocusExit(BlackHolePortal portal){
        foreach(var camera in FindObjectsOfType<IslandCamera>())if(camera.enabled&&camera.board==portal.board){
            camera.SelectCurrentIsland();if(camera.CurrentIsland)camera.FocusIsland(camera.CurrentIsland.position);
            var curved=camera.GetComponent<CurvedTrialCamera>();if(curved)curved.Apply(camera.transform.position-camera.viewingOffset);
        }
    }
    System.Collections.IEnumerator OpenPortalMap(){
        float start=viewCamera.orthographicSize,target=viewSize;
        // Finish entry before enabling destination selection. Own both ends of this
        // tween so the normal camera's SmoothDamp does not add a second delay.
        for(float age=0;age<1.05f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)*AnimationSpeed){
            float t=Mathf.SmoothStep(0,1,age/1.05f);
            viewSize=Mathf.Exp(Mathf.Lerp(Mathf.Log(start),Mathf.Log(target),t));
            ApplyNavigation();viewCamera.orthographicSize=viewSize;
            if(navigationCurve)navigationCurve.ResetZoomVelocity();yield return null;
        }
        viewSize=target;ApplyNavigation();viewCamera.orthographicSize=target;
        if(navigationCurve)navigationCurve.ResetZoomVelocity();
    }
    System.Collections.IEnumerator WaitForExitView(BlackHolePortal portal){
        // Keep the currently panned map pose until the destination framing is known.
        // Automatic rebounds need camera framing only, never a new exit-selection map.
        if(!navigationCamera)BeginNavigation(portal.board,false);
        var rig=navigationCamera;var camera=viewCamera;var curve=navigationCurve;
        Vector3 startFocus=viewFocus;float startSize=camera.orthographicSize;
        rig.SelectCurrentIsland();
        Vector3 target=rig.CurrentIsland?rig.CurrentIsland.position:portal.transform.position;
        if(navigationWindow)for(int i=0;i<navigationWindow.rooms.Length;i++)if(navigationWindow.rooms[i].center==rig.CurrentIsland){navigationWindow.Show(i);break;}
        float endSize=savedSize;
        if(curve){
            curve.fitIsland=curveFit;curve.planningSize=savedSize;curve.Apply(target);
            endSize=curve.TargetZoomSize;curve.fitIsland=false;curve.ResetZoomVelocity();
        }
        try{
            for(float age=0;age<1.1f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)*AnimationSpeed){
                float t=Mathf.SmoothStep(0,1,age/1.1f);
                // Restore scene brightness together with the return-camera movement.
                mapLightingExitWeight=1f-t;
                viewFocus=Vector3.Lerp(startFocus,target,t);
                viewSize=Mathf.Exp(Mathf.Lerp(Mathf.Log(startSize),Mathf.Log(endSize),t));
                ApplyNavigation();camera.orthographicSize=viewSize;
                if(curve)curve.ResetZoomVelocity();yield return null;
            }
        }finally{
            mapLightingExitWeight=0f;
            EndNavigation();
            if(rig){rig.FocusIsland(target);if(curve){curve.Apply(target);curve.ResetZoomVelocity();}if(camera)camera.orthographicSize=endSize;}
        }
    }
    void OnGUI(){
        DrawUnknownIslands();
        if(inspectionBoard){if(inspectionPosition.HasValue)DrawOriginIndicator();return;}
        if(testPortals==null||busy||!Selecting)return;
        DrawOriginIndicator();
    }
}
}
