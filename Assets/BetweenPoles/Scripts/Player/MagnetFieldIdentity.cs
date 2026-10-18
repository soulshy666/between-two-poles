using UnityEngine;
namespace BetweenPoles {
// Bounded visual-only particles; no colliders or changes to the magnet mesh.
[DefaultExecutionOrder(1100)]
public sealed class MagnetFieldIdentity:MonoBehaviour {
 MagnetPiece piece;Transform geometry;Renderer reference;
 float nextBind;
 [SerializeField,HideInInspector] ParticleSystem particles;ParticleSystemRenderer particleRenderer;
 static Material sharedMaterial;static int materialUsers;
 Material materialLease;Color particleColor;MaterialPropertyBlock block;
 void AcquireMaterial(){
  if(materialLease&&materialLease==sharedMaterial)return;
  if(!sharedMaterial)sharedMaterial=new Material(Shader.Find("BetweenPoles/PaintedIceTrial")){name="磁极微粒 · 共享辉光",hideFlags=HideFlags.HideAndDontSave};
  materialLease=sharedMaterial;materialUsers++;
 }
 void ReleaseMaterial(){
  if(!materialLease)return;
  if(particleRenderer&&particleRenderer.sharedMaterial==materialLease)particleRenderer.sharedMaterial=null;
  materialLease=null;
  if(--materialUsers==0){
   if(Application.isPlaying)Destroy(sharedMaterial);else DestroyImmediate(sharedMaterial);
   sharedMaterial=null;
  }
 }
 void OnEnable(){geometry=null;reference=null;block=new MaterialPropertyBlock();}
 void LateUpdate(){
  if(!piece)piece=GetComponent<MagnetPiece>();
  if(!piece||!piece.geometry)return;
  if(!piece.NeedsDynamicEffects){
   if(particles&&particles.isPlaying)particles.Pause(true);
   return;
  }
  if(geometry!=piece.geometry||block==null||((!reference||!particles)&&Time.unscaledTime>=nextBind)){nextBind=Time.unscaledTime+.5f;Bind();}
  if(!reference||!particles)return;
  // Keep the same curved-world/island mapping as the magnet, including portal transitions.
  reference.GetPropertyBlock(block);block.SetFloat("_MagnetSpark",.65f);block.SetColor("_Color",particleColor);particleRenderer.SetPropertyBlock(block);
  if(!particles.isPlaying&&geometry.gameObject.activeInHierarchy)particles.Play();
 }
 void Bind(){
  geometry=piece.geometry;if(block==null)block=new MaterialPropertyBlock();
  reference=null;var bounds=new Bounds();bool hasBounds=false;
  foreach(var r in geometry.GetComponentsInChildren<MeshRenderer>(true)){
   if(MagnetVisuals.IsMagneticEffect(r)||!r.sharedMaterial||!r.sharedMaterial.HasProperty("_Color"))continue;
   var shader=r.sharedMaterial.shader;
   if(!r.sharedMaterial.HasProperty("_MagnetSpark")&&(!shader||shader.name!="BetweenPoles/ChapterIslandSurface"))continue;
   Color color=r.sharedMaterial.color;bool red=color.r>color.b*1.4f;bool blue=color.b>color.r*1.4f;
   if(!red&&!blue)continue;
   if(!reference)reference=r;
   var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;
   // Gameplay inflates mesh.bounds for its curved-world renderer: use real vertices.
   if(!f.sharedMesh.isReadable)continue;
   var toGeometry=geometry.worldToLocalMatrix*r.transform.localToWorldMatrix;
   foreach(var vertex in MagnetVisuals.CachedVertices(f.sharedMesh)){var v=toGeometry.MultiplyPoint3x4(vertex);if(!hasBounds){bounds=new Bounds(v,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(v);}

  }
  if(!reference)return;
  // Reuse our local effect after an editor domain reload as well as after assembly.
  if(!particles){var existing=geometry.Find("磁极微粒");if(existing)particles=existing.GetComponent<ParticleSystem>();}
  if(particles)particleRenderer=particles.GetComponent<ParticleSystemRenderer>();
  if(!particles){
   var go=new GameObject("磁极微粒");go.layer=gameObject.layer;particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   particleRenderer=go.GetComponent<ParticleSystemRenderer>();particleRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;particleRenderer.receiveShadows=false;
  }
  // One material for every magnet; the per-renderer block keeps red/blue independent.
  AcquireMaterial();particleRenderer.sharedMaterial=materialLease;
  particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   // Decorative particles resume immediately; never replay time spent off screen.
   var main=particles.main;main.playOnAwake=false;main.loop=true;main.maxParticles=10;main.startLifetime=2.6f;main.startSpeed=.025f;main.startSize=new ParticleSystem.MinMaxCurve(.07f,.10f);main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Shape;main.cullingMode=ParticleSystemCullingMode.Pause;
   var emission=particles.emission;emission.rateOverTime=3f;
   var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,0),new Keyframe(.25f,1),new Keyframe(.7f,.7f),new Keyframe(1,0)));
   var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.y=.075f;velocity.x=.018f;velocity.z=.012f;velocity.orbitalY=.25f;
  particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);particles.transform.SetParent(geometry,false);particles.transform.localPosition=bounds.center;
  particleColor=reference.sharedMaterial.color.r>reference.sharedMaterial.color.b?new Color(.95f,.035f,.065f):new Color(.025f,.35f,.95f);
  var shape=particles.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=bounds.size+new Vector3(.18f,.12f,.18f);
  particles.Play();
 }
 void OnDisable(){if(particles)particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ReleaseMaterial();}
 void OnDestroy(){ReleaseMaterial();if(particles)Destroy(particles.gameObject);}
}
}
