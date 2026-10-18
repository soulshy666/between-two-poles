using UnityEngine;
namespace BetweenPoles {
 [ExecuteAlways] public class IslandSurfaceAnchor:MonoBehaviour {
  static readonly System.Collections.Generic.HashSet<IslandSurfaceAnchor> activeAnchors=new System.Collections.Generic.HashSet<IslandSurfaceAnchor>();
  bool displayBoundsApplied;
  static int boundsRevision;
  public Transform center;
  public GridPlayground ground;
  [Range(0,1)] public float flatten;
  public Vector3 visualOffset;
  Vector3 trackedPosition;GridPlayground trackedGround;bool tracked;
  Vector3 last;Vector4 lastFocus;Renderer[] renderers;MaterialPropertyBlock block;
  sealed class DisplayBoundsState {
   public Renderer renderer;
   public bool valid;
   public Matrix4x4 matrix;
   public Bounds local;
   public Vector3 focus,offset;
   public float scale;
   public int revision;
  }
  DisplayBoundsState[] boundsStates;
  void OnEnable(){unchecked{boundsRevision++;}activeAnchors.Add(this);renderers=GetComponentsInChildren<Renderer>(true);block=new MaterialPropertyBlock();
   var states=new System.Collections.Generic.List<DisplayBoundsState>();
   foreach(var r in renderers)if(r is MeshRenderer||r is SkinnedMeshRenderer)states.Add(new DisplayBoundsState{renderer=r});
   boundsStates=states.ToArray();Apply();}
  void OnDisable(){activeAnchors.Remove(this);RestoreDisplayBounds();}
  void RestoreDisplayBounds(){
   if(boundsStates!=null)foreach(var state in boundsStates)state.valid=false;
   if(!displayBoundsApplied||renderers==null)return;
   unchecked{boundsRevision++;}
   foreach(var r in renderers)if(r)r.ResetBounds();
   displayBoundsApplied=false;
  }
  static bool Finite(Vector3 v){return !float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);}
  // Vertex shaders move the displayed geometry without moving its Transform.
  // Match CPU frustum bounds to that same affine frame immediately before culling.
  public static void UpdatePlanarCullingBounds(){
   bool planar=Shader.GetGlobalFloat("_IslandPlanar")>.5f;
   Vector3 focus=Shader.GetGlobalVector("_IceFocus");
   float scale=Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"));
   if(!Finite(focus)||float.IsNaN(scale)||float.IsInfinity(scale))return;
   foreach(var anchor in activeAnchors){
    if(!anchor)continue;
    if(!planar){anchor.RestoreDisplayBounds();continue;}
    if(anchor.boundsStates==null)continue;
    foreach(var state in anchor.boundsStates){
     var r=state.renderer;
     // Particle/trail bounds belong to their simulation, not the source mesh.
     if(!r||!r.enabled||!r.gameObject.activeInHierarchy){state.valid=false;continue;}
     var matrix=r.localToWorldMatrix;
     var local=r.localBounds;
     r.GetPropertyBlock(anchor.block);
     Vector3 offset=anchor.block.GetFloat("_RecoilFlying")>.5f?(Vector3)anchor.block.GetVector("_RecoilDisplayOffset"):Vector3.zero;
     // Exact comparisons: even a small camera drift, deformation or render-only
     // hover offset must invalidate this cache before the next cull.
     if(state.valid&&state.revision==boundsRevision&&state.matrix.Equals(matrix)&&state.local.Equals(local)&&state.focus.Equals(focus)
        &&state.scale.Equals(scale)&&state.offset.Equals(offset))continue;
     state.valid=false;
     var axisX=new Vector3(matrix.m00,matrix.m10,matrix.m20);
     var axisY=new Vector3(matrix.m01,matrix.m11,matrix.m21);
     var axisZ=new Vector3(matrix.m02,matrix.m12,matrix.m22);
     if(!Finite(axisX)||!Finite(axisY)||!Finite(axisZ)||axisX.sqrMagnitude<1e-12f||axisY.sqrMagnitude<1e-12f||axisZ.sqrMagnitude<1e-12f){r.ResetBounds();continue;}
     if(!Finite(local.center)||!Finite(local.extents)){r.ResetBounds();r.ResetLocalBounds();continue;}
     var x=matrix.MultiplyVector(new Vector3(local.extents.x,0,0));
     var y=matrix.MultiplyVector(new Vector3(0,local.extents.y,0));
     var z=matrix.MultiplyVector(new Vector3(0,0,local.extents.z));
     var extent=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*scale;
     var position=focus+(matrix.MultiplyPoint3x4(local.center)-focus)*scale+offset;
     if(!Finite(position)||!Finite(extent)||extent.x>1000000||extent.y>1000000||extent.z>1000000){r.ResetBounds();continue;}
     r.bounds=new Bounds(position,extent*2+Vector3.one*.02f);
     state.matrix=matrix;state.local=local;state.focus=focus;state.scale=scale;state.offset=offset;state.revision=boundsRevision;state.valid=true;
    }
    anchor.displayBoundsApplied=true;
   }
  }
  void LateUpdate(){if(ground&&(!tracked||trackedGround!=ground||trackedPosition!=transform.position)){tracked=true;trackedGround=ground;trackedPosition=transform.position;Vector3 p=transform.position;foreach(var tile in ground.tiles){if(!tile||!tile.gameObject.activeInHierarchy)continue;Vector3 q=tile.transform.position;if(Mathf.RoundToInt(p.x/ground.cellSize)==Mathf.RoundToInt(q.x/ground.cellSize)&&Mathf.RoundToInt(p.z/ground.cellSize)==Mathf.RoundToInt(q.z/ground.cellSize)){var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();if(binding&&binding.center)center=binding.center;break;}}}if(center&&(center.position!=last||((flatten!=0||visualOffset!=Vector3.zero)&&Shader.GetGlobalVector("_IceFocus")!=lastFocus)))Apply();}
  public void Apply(){if(!center)return;if(renderers==null)renderers=GetComponentsInChildren<Renderer>(true);if(block==null)block=new MaterialPropertyBlock();last=center.position;lastFocus=Shader.GetGlobalVector("_IceFocus");float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(Vector2.Distance(new Vector2(last.x,last.z),new Vector2(lastFocus.x,lastFocus.z))/6));foreach(var r in renderers){if(!r)continue;r.GetPropertyBlock(block);block.SetVector("_ExplicitIsland",new Vector4(last.x,last.y,last.z,1));block.SetFloat("_IslandFlatten",flatten*weight);block.SetVector("_IslandViewOffset",visualOffset*weight);r.SetPropertyBlock(block);}}
 }
}
