using UnityEngine;
namespace BetweenPoles {
 [ExecuteAlways,DefaultExecutionOrder(100)] public class CurvedTrialCamera:MonoBehaviour {
  public IslandCamera island;
  public Transform planet;
  public bool PlanarChapter {get{return island&&island.board&&island.board.gameObject.scene.name.StartsWith("Chapter0");}}
  public Vector3 editFocus=new Vector3(-3.75f,0,0);
  [Range(5.5f,12),InspectorName("最小观察范围")] public float planningSize=7.4f;
  [InspectorName("随主岛大小扩大视野")] public bool fitIsland=true;
  [Min(10),InspectorName("星球半径")] public float planetRadius=10;
  [InspectorName("星球与主岛画面中心对齐")] public bool centeredGlobe;
  public float displayRadius;
  [Min(1),InspectorName("星球背景显示倍率")] public float globeDisplayScale=1.12f;
  [InspectorName("星球背景上下偏移")] public float globeVerticalOffset=0f;
  [Range(1,1.5f),InspectorName("岛屿与格子显示倍率")] public float islandDisplayScale=1.2f;
  [Min(.05f),InspectorName("镜头缩放缓动时间")] public float zoomSmoothTime=.55f;
  [System.NonSerialized] public bool fixedDisplayOrigin;
  [System.NonSerialized] public Vector3 displayOrigin;
  [System.NonSerialized] public Vector3 curveOrigin;
  [System.NonSerialized] public Vector3 rotationOrigin;
  public Vector3 DisplayFocus(Vector3 world){return fixedDisplayOrigin?displayOrigin+(world-displayOrigin)*Mathf.Max(1,islandDisplayScale*(island&&island.board&&island.board.GetComponent<FiveIslandWindow>()?1.1f:1f)):world;}
  bool settlingFlight;
  float settleElapsed;
  Vector3 settleOrigin,settleCurve,settlePlanet;
  Quaternion settleRotation;
  Transform settleIsland;
  public bool SettlingFlight {get{return settlingFlight;}}
  public void ResetSurfaceFrame(){flatInitialized=false;}
  public void ResumeFlight(){if(settlingFlight){settlingFlight=false;fixedDisplayOrigin=false;}}
  // Both ordinary recoil landings and chapter landings leave the airborne frame
  // through this one continuous presentation transition.
  public void SettleFlight(Vector3 origin,Transform destination){
   if(!fixedDisplayOrigin){fixedDisplayOrigin=true;displayOrigin=origin;curveOrigin=origin;}
   settleOrigin=displayOrigin;settleCurve=curveOrigin;settleIsland=destination;
   settlePlanet=planet?planet.position:Vector3.zero;settleRotation=planet?planet.rotation:Quaternion.identity;
   settleElapsed=0;settlingFlight=true;
  }
  float fittedSize,nextFitTime;Transform fittedCenter;
  float zoomVelocity;
  bool zoomInitialized;
  Vector4 flatBounds;
  bool flatInitialized;
  readonly Vector4[] anchors=new Vector4[16];
  void OnEnable(){Apply(editFocus);}
  void OnDisable(){Shader.SetGlobalFloat("_IslandPlanar",0);Shader.SetGlobalFloat("_IslandRigidLayout",0);Shader.SetGlobalFloat("_IslandFlatEnabled",0);flatInitialized=false;}
  void UpdateFlatIsland(FiveIslandWindow window,Vector3 focus){
   bool valid=island&&island.board&&island.islandCenters!=null&&island.islandCenters.Length>0;
   if(!valid){Shader.SetGlobalFloat("_IslandFlatEnabled",0);flatInitialized=false;return;}
   var center=island.CurrentIsland;
   if(window&&window.enabled&&window.rooms.Length>0)center=window.rooms[Mathf.Clamp(window.CurrentRoom,0,window.rooms.Length-1)].center;
   else if(!Application.isPlaying)foreach(var candidate in island.islandCenters)if(candidate&&(!center||(candidate.position-focus).sqrMagnitude<(center.position-focus).sqrMagnitude))center=candidate;
   float minX=float.PositiveInfinity,minZ=minX,maxX=float.NegativeInfinity,maxZ=maxX;
   float pad=island.board.cellSize*.5f;
   foreach(var tile in island.board.tiles){
    if(!tile||!tile.gameObject.activeInHierarchy)continue;
    var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();if(!owner||owner.center!=center)continue;
    var p=tile.transform.position;minX=Mathf.Min(minX,p.x-pad);maxX=Mathf.Max(maxX,p.x+pad);minZ=Mathf.Min(minZ,p.z-pad);maxZ=Mathf.Max(maxZ,p.z+pad);
   }
   if(float.IsInfinity(minX)){Shader.SetGlobalFloat("_IslandFlatEnabled",0);return;}
   var target=new Vector4(minX,minZ,maxX,maxZ);
   if(!Application.isPlaying||!flatInitialized)flatBounds=target;
   else flatBounds=Vector4.Lerp(flatBounds,target,1-Mathf.Exp(-Time.deltaTime/Mathf.Max(.05f,island.transitionSeconds*.3f)));
   flatInitialized=true;Shader.SetGlobalVector("_IslandFlatBounds",flatBounds);Shader.SetGlobalFloat("_IslandFlatEnabled",1);
  }
  // OnEnable can run before the island window and camera select the spawn island.
  // Recompute the first frame after their Start methods, using the normal zoom rule.
  void Start(){
   if(!Application.isPlaying||!island)return;
   zoomInitialized=false;
   Apply(island.enabled?transform.position-island.viewingOffset:editFocus);
  }
  void LateUpdate(){if(!island)return;
   if(settlingFlight&&settleIsland){
    if(island.CurrentIsland)settleIsland=island.CurrentIsland;
    settleElapsed+=Time.deltaTime;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(settleElapsed/Mathf.Max(.8f,island.transitionSeconds*2)));
    var previousOrigin=displayOrigin;
    displayOrigin=Vector3.Lerp(settleOrigin,settleIsland.position,t);curveOrigin=Vector3.Lerp(settleCurve,settleIsland.position,t);
    // Changing the render origin is a coordinate conversion, not a camera pan.
    // Convert camera and globe together in the same frame before easing continues.
    float scale=islandDisplayScale*(island.board&&island.board.GetComponent<FiveIslandWindow>()?1.1f:1f);
    var delta=(displayOrigin-previousOrigin)*(1-scale);
    island.RebaseFocus(delta);settlePlanet+=delta;
    if(t>=1&&(transform.position-island.viewingOffset-settleIsland.position).sqrMagnitude<.000001f){settlingFlight=false;fixedDisplayOrigin=false;}
   }
   Vector3 focus=Application.isPlaying&&island.enabled?transform.position-island.viewingOffset:editFocus;Apply(focus);}
  public static Vector3 SphereNormal(Vector3 point,float radius=10){float d=new Vector2(point.x,point.z).magnitude;return d<.001f?Vector3.up:new Vector3(point.x/d*Mathf.Sin(d/radius),Mathf.Cos(d/radius),point.z/d*Mathf.Sin(d/radius));}
  public void Apply(Vector3 focus){
   // Share the physical camera offset in edit mode and during island transitions.
   if(island)island.ApplyView(focus);
   bool planar=PlanarChapter;
   Shader.SetGlobalFloat("_IslandPlanar",planar?1:0);
   if(planar&&planet&&planet.gameObject.activeSelf)planet.gameObject.SetActive(false);
   Shader.SetGlobalFloat("_IceRadius",planetRadius);
   var layoutWindow=island&&island.board?island.board.GetComponent<FiveIslandWindow>():null;
   if(planar)Shader.SetGlobalFloat("_IslandFlatEnabled",0);else UpdateFlatIsland(layoutWindow,focus);
   bool edgeLayout=layoutWindow&&layoutWindow.enabled;
   bool recoil=island&&island.board&&island.board.RecoilFlying;
   Vector3 renderFocus=fixedDisplayOrigin?displayOrigin:recoil?island.board.RecoilOrigin:focus;
   float displayScale=islandDisplayScale*(edgeLayout?1.1f:1f);
   Shader.SetGlobalFloat("_IslandDisplayScale",displayScale);
   Shader.SetGlobalVector("_IceCurveFocus",fixedDisplayOrigin?curveOrigin:renderFocus);
   Shader.SetGlobalVector("_IceFocus",new Vector4(renderFocus.x,0,renderFocus.z,0));
   var window=island&&island.board?island.board.GetComponent<FiveIslandWindow>():null;
   var centers=window&&window.VisibleCenters!=null?window.VisibleCenters:(island?island.islandCenters:null);
   int count=centers!=null?Mathf.Min(16,centers.Length):0;
   for(int i=0;i<count;i++)anchors[i]=centers[i].position;
   // Keep every connected island, magnet and the player in the same rigid frame.
   // Independent rim placement changes shore separation and stretches bridge meshes.
   // The camera pans over the fixed board while only the background globe rotates.
   Shader.SetGlobalFloat("_IslandRigidLayout",edgeLayout||recoil||fixedDisplayOrigin||Shader.GetGlobalFloat("_IslandFlatEnabled")>.5f?1:0);
   Shader.SetGlobalFloat("_IslandEdgeLayout",0);
   Shader.SetGlobalVectorArray("_IslandAnchors",anchors);Shader.SetGlobalInt("_IslandCount",count);
   Vector3 rotationFocus=(fixedDisplayOrigin?displayOrigin+(focus-displayOrigin)/Mathf.Max(1,displayScale):focus)-rotationOrigin;
   Quaternion rotation=planar?Quaternion.identity:Quaternion.FromToRotation(SphereNormal(rotationFocus,planetRadius),Vector3.up);Shader.SetGlobalMatrix("_IceRotation",Matrix4x4.Rotate(rotation));
   float visibleRadius=displayRadius>0?displayRadius*globeDisplayScale:planetRadius;
   if(planet&&!planar&&!recoil&&(!fixedDisplayOrigin||settlingFlight)){
    Vector3 destination=centeredGlobe?focus+transform.forward*(planetRadius+1.5f):new Vector3(focus.x,-planetRadius-.15f,focus.z);
    if(displayRadius>0)destination=focus+transform.forward*(visibleRadius+1.5f)+transform.up*globeVerticalOffset;
    float follow=settlingFlight?Mathf.SmoothStep(0,1,Mathf.Clamp01(settleElapsed/Mathf.Max(.8f,island.transitionSeconds*2))):1;
    planet.position=Vector3.Lerp(settlingFlight?settlePlanet:planet.position,destination,follow);planet.rotation=Quaternion.Slerp(settlingFlight?settleRotation:planet.rotation,rotation,follow);planet.localScale=Vector3.one*(visibleRadius/10);
   }
   Shader.SetGlobalFloat("_IceDiskRadius",!planar&&displayRadius>0?visibleRadius:0);
   if(planet)Shader.SetGlobalVector("_IceDiskCenter",planet.position);
   Shader.SetGlobalVector("_IceDiskRight",transform.right);Shader.SetGlobalVector("_IceDiskUp",transform.up);
   var camera=GetComponent<Camera>();if(camera){
    // Flat chapters no longer need the wide margin reserved for the globe.
    float size=planningSize*(planar?.8f:1f);
    float framingMargin=planar?1.4f:2.8f;
    if(fitIsland&&count>0&&island.board){
     int selected=0;for(int j=window?count:1;j<count;j++)if(((Vector3)anchors[j]-focus).sqrMagnitude<((Vector3)anchors[selected]-focus).sqrMagnitude)selected=j;
     Vector3 center=anchors[selected];
     bool cached=Application.isPlaying&&fittedCenter==centers[selected]&&Time.unscaledTime<nextFitTime;
     if(cached)size=fittedSize;
     else {
     foreach(var tile in island.board.tiles){if(!tile||!tile.gameObject.activeInHierarchy)continue;Vector3 p=tile.transform.position;int owner=0;for(int j=1;j<count;j++)if((p-(Vector3)anchors[j]).sqrMagnitude<(p-(Vector3)anchors[owner]).sqrMagnitude)owner=j;var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();if(binding&&binding.center){if(window&&binding.center!=centers[selected])continue;for(int j=0;j<count;j++)if(centers[j]==binding.center){owner=j;break;}}if(owner!=selected)continue;
      Vector3 delta=(p-center)*displayScale;float pad=island.board.cellSize*.72f*displayScale;
      size=Mathf.Max(size,Mathf.Abs(Vector3.Dot(transform.up,delta))+pad+framingMargin,(Mathf.Abs(Vector3.Dot(transform.right,delta))+pad+framingMargin)/Mathf.Max(.5f,camera.aspect));
     }
     }
     if(!cached){fittedSize=size;fittedCenter=centers[selected];nextFitTime=Time.unscaledTime+.25f;}
     }
    // Start at the correct framing; subsequent island changes ease both ways.
    if(!Application.isPlaying||!zoomInitialized){
     camera.orthographicSize=size;zoomVelocity=0;zoomInitialized=Application.isPlaying;
    }else{
     camera.orthographicSize=Mathf.SmoothDamp(camera.orthographicSize,size,ref zoomVelocity,
      Mathf.Max(.05f,zoomSmoothTime),Mathf.Infinity,Mathf.Min(Time.deltaTime,.05f));
    }
   }
  }
 }
}

