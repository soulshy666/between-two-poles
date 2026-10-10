using UnityEngine;
namespace BetweenPoles {
 [ExecuteAlways] public class IslandSurfaceAnchor:MonoBehaviour {
  public Transform center;
  public GridPlayground ground;
  [Range(0,1)] public float flatten;
  public Vector3 visualOffset;
  Vector3 trackedPosition;GridPlayground trackedGround;bool tracked;
  Vector3 last;Vector4 lastFocus;Renderer[] renderers;MaterialPropertyBlock block;
  void OnEnable(){renderers=GetComponentsInChildren<Renderer>(true);block=new MaterialPropertyBlock();Apply();}
  void LateUpdate(){if(ground&&(!tracked||trackedGround!=ground||trackedPosition!=transform.position)){tracked=true;trackedGround=ground;trackedPosition=transform.position;Vector3 p=transform.position;foreach(var tile in ground.tiles){if(!tile||!tile.gameObject.activeInHierarchy)continue;Vector3 q=tile.transform.position;if(Mathf.RoundToInt(p.x/ground.cellSize)==Mathf.RoundToInt(q.x/ground.cellSize)&&Mathf.RoundToInt(p.z/ground.cellSize)==Mathf.RoundToInt(q.z/ground.cellSize)){var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();if(binding&&binding.center)center=binding.center;break;}}}if(center&&(center.position!=last||((flatten!=0||visualOffset!=Vector3.zero)&&Shader.GetGlobalVector("_IceFocus")!=lastFocus)))Apply();}
  public void Apply(){if(!center)return;if(renderers==null)renderers=GetComponentsInChildren<Renderer>(true);if(block==null)block=new MaterialPropertyBlock();last=center.position;lastFocus=Shader.GetGlobalVector("_IceFocus");float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(Vector2.Distance(new Vector2(last.x,last.z),new Vector2(lastFocus.x,lastFocus.z))/6));foreach(var r in renderers){if(!r)continue;r.GetPropertyBlock(block);block.SetVector("_ExplicitIsland",new Vector4(last.x,last.y,last.z,1));block.SetFloat("_IslandFlatten",flatten*weight);block.SetVector("_IslandViewOffset",visualOffset*weight);r.SetPropertyBlock(block);}}
 }
}
