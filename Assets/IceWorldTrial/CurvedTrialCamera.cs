using UnityEngine;
namespace BetweenPoles {
 [ExecuteAlways,DefaultExecutionOrder(100)] public class CurvedTrialCamera:MonoBehaviour {
  public IslandCamera island;
  public Transform planet;
  public Vector3 editFocus=new Vector3(-3.75f,0,0);
  [Range(5.5f,12),InspectorName("最小观察范围")] public float planningSize=7.4f;
  [InspectorName("随主岛大小扩大视野")] public bool fitIsland=true;
  [Min(10),InspectorName("星球半径")] public float planetRadius=10;
  [InspectorName("星球与主岛画面中心对齐")] public bool centeredGlobe;
  public float displayRadius;
  readonly Vector4[] anchors=new Vector4[16];
  void OnEnable(){Apply(editFocus);}
  void LateUpdate(){if(!island)return;Vector3 focus=Application.isPlaying&&island.enabled?transform.position-island.viewingOffset:editFocus;Apply(focus);}
  public static Vector3 SphereNormal(Vector3 point,float radius=10){float d=new Vector2(point.x,point.z).magnitude;return d<.001f?Vector3.up:new Vector3(point.x/d*Mathf.Sin(d/radius),Mathf.Cos(d/radius),point.z/d*Mathf.Sin(d/radius));}
  public void Apply(Vector3 focus){
   Shader.SetGlobalFloat("_IceRadius",planetRadius);
   Shader.SetGlobalVector("_IceFocus",new Vector4(focus.x,0,focus.z,0));
   int count=island&&island.islandCenters!=null?Mathf.Min(16,island.islandCenters.Length):0;
   for(int i=0;i<count;i++)anchors[i]=island.islandCenters[i].position;
   Shader.SetGlobalVectorArray("_IslandAnchors",anchors);Shader.SetGlobalInt("_IslandCount",count);
   Quaternion rotation=Quaternion.FromToRotation(SphereNormal(focus,planetRadius),Vector3.up);Shader.SetGlobalMatrix("_IceRotation",Matrix4x4.Rotate(rotation));
   if(planet){planet.position=centeredGlobe?focus+transform.forward*(planetRadius+1.5f):new Vector3(focus.x,-planetRadius-.15f,focus.z);planet.localScale=Vector3.one*((displayRadius>0?displayRadius:planetRadius)/10);planet.rotation=rotation;
    if(displayRadius>0)planet.position=focus+transform.forward*(displayRadius+1.5f)-transform.up*1.5f;}
   Shader.SetGlobalFloat("_IceDiskRadius",displayRadius>0?displayRadius:0);
   if(planet)Shader.SetGlobalVector("_IceDiskCenter",planet.position);
   Shader.SetGlobalVector("_IceDiskRight",transform.right);Shader.SetGlobalVector("_IceDiskUp",transform.up);
   var camera=GetComponent<Camera>();if(camera){
    float size=planningSize;
    if(fitIsland&&count>0&&island.board){
     int selected=0;for(int j=1;j<count;j++)if(((Vector3)anchors[j]-focus).sqrMagnitude<((Vector3)anchors[selected]-focus).sqrMagnitude)selected=j;
     Vector3 center=anchors[selected];
     foreach(var tile in island.board.tiles){if(!tile||!tile.gameObject.activeInHierarchy)continue;Vector3 p=tile.transform.position;int owner=0;for(int j=1;j<count;j++)if((p-(Vector3)anchors[j]).sqrMagnitude<(p-(Vector3)anchors[owner]).sqrMagnitude)owner=j;var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();if(binding&&binding.center){for(int j=0;j<count;j++)if(island.islandCenters[j]==binding.center){owner=j;break;}}if(owner!=selected)continue;
      Vector3 delta=p-center;float pad=island.board.cellSize*.72f;
      size=Mathf.Max(size,Mathf.Abs(Vector3.Dot(transform.up,delta))+pad+2.2f,(Mathf.Abs(Vector3.Dot(transform.right,delta))+pad+2.2f)/Mathf.Max(.5f,camera.aspect));
     }
    }
    camera.orthographicSize=size;
   }
  }
 }
}

