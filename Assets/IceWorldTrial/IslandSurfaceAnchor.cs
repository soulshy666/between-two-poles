using UnityEngine;
namespace BetweenPoles {
 [ExecuteAlways] public class IslandSurfaceAnchor:MonoBehaviour {
  static readonly System.Collections.Generic.HashSet<IslandSurfaceAnchor> activeAnchors=new System.Collections.Generic.HashSet<IslandSurfaceAnchor>();
  bool displayBoundsApplied;
  public Transform center;
  public GridPlayground ground;
  [Range(0,1)] public float flatten;
  public Vector3 visualOffset;
  Vector3 trackedPosition;GridPlayground trackedGround;bool tracked;
  Vector3 last;Vector4 lastFocus;Renderer[] renderers;MaterialPropertyBlock block;
  void OnEnable(){activeAnchors.Add(this);renderers=GetComponentsInChildren<Renderer>(true);block=new MaterialPropertyBlock();Apply();}
  void OnDisable(){activeAnchors.Remove(this);RestoreDisplayBounds();}
  void RestoreDisplayBounds(){
   if(!displayBoundsApplied||renderers==null)return;
   foreach(var r in renderers)if(r)r.ResetBounds();
   displayBoundsApplied=false;
  }
  // Vertex shaders move the displayed geometry without moving its Transform.
  // Match CPU frustum bounds to that same affine frame immediately before culling.
  public static void UpdatePlanarCullingBounds(){
   bool planar=Shader.GetGlobalFloat("_IslandPlanar")>.5f;
   Vector3 focus=Shader.GetGlobalVector("_IceFocus");
   float scale=Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"));
   foreach(var anchor in activeAnchors){
    if(!anchor)continue;
    if(!planar){anchor.RestoreDisplayBounds();continue;}
    if(anchor.renderers==null)continue;
    foreach(var r in anchor.renderers){
     if(!r)continue;
     var local=r.localBounds;var matrix=r.localToWorldMatrix;
     var x=matrix.MultiplyVector(new Vector3(local.extents.x,0,0));
     var y=matrix.MultiplyVector(new Vector3(0,local.extents.y,0));
     var z=matrix.MultiplyVector(new Vector3(0,0,local.extents.z));
     var extent=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*scale;
     r.GetPropertyBlock(anchor.block);
     Vector3 offset=anchor.block.GetFloat("_RecoilFlying")>.5f?(Vector3)anchor.block.GetVector("_RecoilDisplayOffset"):Vector3.zero;
     var position=focus+(matrix.MultiplyPoint3x4(local.center)-focus)*scale+offset;
     r.bounds=new Bounds(position,extent*2+Vector3.one*.02f);
    }
    anchor.displayBoundsApplied=true;
   }
  }
  void LateUpdate(){if(ground&&(!tracked||trackedGround!=ground||trackedPosition!=transform.position)){tracked=true;trackedGround=ground;trackedPosition=transform.position;Vector3 p=transform.position;foreach(var tile in ground.tiles){if(!tile||!tile.gameObject.activeInHierarchy)continue;Vector3 q=tile.transform.position;if(Mathf.RoundToInt(p.x/ground.cellSize)==Mathf.RoundToInt(q.x/ground.cellSize)&&Mathf.RoundToInt(p.z/ground.cellSize)==Mathf.RoundToInt(q.z/ground.cellSize)){var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();if(binding&&binding.center)center=binding.center;break;}}}if(center&&(center.position!=last||((flatten!=0||visualOffset!=Vector3.zero)&&Shader.GetGlobalVector("_IceFocus")!=lastFocus)))Apply();}
  public void Apply(){if(!center)return;if(renderers==null)renderers=GetComponentsInChildren<Renderer>(true);if(block==null)block=new MaterialPropertyBlock();last=center.position;lastFocus=Shader.GetGlobalVector("_IceFocus");float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(Vector2.Distance(new Vector2(last.x,last.z),new Vector2(lastFocus.x,lastFocus.z))/6));foreach(var r in renderers){if(!r)continue;r.GetPropertyBlock(block);block.SetVector("_ExplicitIsland",new Vector4(last.x,last.y,last.z,1));block.SetFloat("_IslandFlatten",flatten*weight);block.SetVector("_IslandViewOffset",visualOffset*weight);r.SetPropertyBlock(block);}}
 }
}
