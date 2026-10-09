using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    IslandCamera navigationCamera;
    CurvedTrialCamera navigationCurve;
    Camera viewCamera;
    FiveIslandWindow navigationWindow;
    bool cameraEnabled,windowEnabled,curveFit;
    float savedSize,viewSize;
    Vector3 savedFocus,viewFocus;
    readonly Dictionary<Renderer,bool> navigationRenderers=new Dictionary<Renderer,bool>();
    void BeginNavigation(GridPlayground board){
        foreach(var root in board.gameObject.scene.GetRootGameObjects())foreach(var camera in root.GetComponentsInChildren<IslandCamera>())if(camera.board==board)navigationCamera=camera;
        if(!navigationCamera)return;
        viewCamera=navigationCamera.GetComponent<Camera>();navigationCurve=navigationCamera.GetComponent<CurvedTrialCamera>();
        cameraEnabled=navigationCamera.enabled;viewFocus=navigationCamera.transform.position-navigationCamera.viewingOffset;
        navigationCamera.enabled=false;
        if(navigationCurve){savedFocus=navigationCurve.editFocus;savedSize=navigationCurve.planningSize;curveFit=navigationCurve.fitIsland;navigationCurve.fitIsland=false;}
        else savedSize=viewCamera.orthographicSize;
        viewSize=Mathf.Max(savedSize,viewCamera.orthographicSize)*1.5f;
        navigationWindow=board.GetComponent<FiveIslandWindow>();if(navigationWindow){windowEnabled=navigationWindow.enabled;navigationWindow.enabled=false;foreach(var room in navigationWindow.rooms)foreach(var r in room.surfaces)ShowForNavigation(r);}
        foreach(var m in board.magnets)if(m&&m.gameObject.activeInHierarchy)foreach(var r in m.GetComponentsInChildren<Renderer>())ShowForNavigation(r);
        ApplyNavigation();
    }
    void ShowForNavigation(Renderer renderer){if(!renderer)return;if(!navigationRenderers.ContainsKey(renderer))navigationRenderers.Add(renderer,renderer.enabled);renderer.enabled=true;}
    void ApplyNavigation(){
        if(!navigationCamera)return;
        navigationCamera.PreviewNavigation(viewFocus);
        if(navigationCurve){navigationCurve.editFocus=viewFocus;navigationCurve.planningSize=viewSize;navigationCurve.Apply(viewFocus);}
        else{navigationCamera.ApplyView(viewFocus);viewCamera.orthographicSize=viewSize;}
    }
    void UpdateNavigation(){
        if(!navigationCamera)return;
        float x=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
        float z=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
        viewFocus+=new Vector3(x,0,z)*viewSize*Time.unscaledDeltaTime;
        if(Input.GetMouseButton(1)||Input.GetMouseButton(2))viewFocus+=new Vector3(-Input.GetAxisRaw("Mouse X"),0,-Input.GetAxisRaw("Mouse Y"))*viewSize*.04f;
        viewSize=Mathf.Clamp(viewSize-Input.mouseScrollDelta.y*1.5f,5,80);ApplyNavigation();
        if(Input.GetKeyDown(KeyCode.Return)&&previewPortal)SelectTestPortal(previewPortal,false);
    }
    void EndNavigation(){
        foreach(var item in navigationRenderers)if(item.Key)item.Key.enabled=item.Value;navigationRenderers.Clear();
        if(navigationCurve){navigationCurve.editFocus=savedFocus;navigationCurve.planningSize=savedSize;navigationCurve.fitIsland=curveFit;}
        if(navigationCamera){navigationCamera.enabled=cameraEnabled;if(!navigationCurve&&viewCamera)viewCamera.orthographicSize=savedSize;}
        if(navigationWindow)navigationWindow.enabled=windowEnabled;
        navigationCamera=null;navigationCurve=null;navigationWindow=null;viewCamera=null;
    }
    void OnGUI(){
        if(testPortals==null||busy||!Selecting)return;
        var style=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};
        GUI.Box(new Rect(16,16,Mathf.Min(Screen.width-32,790),106),"");
        GUI.Label(new Rect(30,24,750,30),"选择传送黑洞 · WASD / 方向键移动镜头 · 右键拖动 · 滚轮缩放",style);
        GUI.Label(new Rect(30,58,750,60),testMessage,style);
        int row=0;
        foreach(var portal in testPortals){
            if(!portal||portal==origin)continue;ExitPlan plan;bool valid=PlanExit(portal,entryDirection,origin.Cargo,out plan);
            Rect rect;
            if(viewCamera){
                var point=viewCamera.WorldToViewportPoint(CrashColorReveal.DisplayPoint(portal.transform.position));
                if(point.z<0||point.x<0||point.x>1||point.y<0||point.y>1)continue;
                rect=new Rect(point.x*Screen.width-78,(1-point.y)*Screen.height-24,156,48);
            }else rect=new Rect(30,145+row++*55,260,46);
            var color=GUI.backgroundColor;GUI.backgroundColor=valid?(previewPortal==portal?Color.green:new Color(.65f,.4f,1)):Color.gray;
            if(GUI.Button(rect,"黑洞 ("+portal.Cell.x+", "+portal.Cell.y+")\n"+(valid?"选择出口":"出口受阻"))){previewPortal=portal;testMessage=valid?"已选择出口。确认后磁铁先出来，玩家随后出来。":"该出口空间不足，请选择其他黑洞。";}
            GUI.backgroundColor=color;
        }
        ExitPlan selectedPlan;GUI.enabled=previewPortal&&PlanExit(previewPortal,entryDirection,origin.Cargo,out selectedPlan);
        if(GUI.Button(new Rect(Screen.width/2-200,Screen.height-76,250,52),"确认传送（Enter）"))SelectTestPortal(previewPortal,false);
        GUI.enabled=true;if(GUI.Button(new Rect(Screen.width/2+65,Screen.height-76,170,52),"取消返回（Esc）"))SelectTestPortal(origin,true);
    }
}
}
