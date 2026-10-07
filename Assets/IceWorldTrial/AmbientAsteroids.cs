using UnityEngine;
using BetweenPoles.Generators;
namespace BetweenPoles {
 [ExecuteAlways,DefaultExecutionOrder(1250)] public sealed class AmbientAsteroids:MonoBehaviour {
  public GeneratedBackgroundFollower background;public CurvedTrialCamera world;
  [InspectorName("小行星实验室参数")] public PlanetStyle asteroidStyle;
  public Mesh quad;public Material material;
  [Range(1,16),InspectorName("常驻陨石数量")] public int count=7;
  MaterialPropertyBlock block;
  bool layoutReady;Vector2 skyCenter;float skyRadius,skyAspect;
  static bool ClipAxis(float origin,float direction,float min,float max,ref float lo,ref float hi){
   if(Mathf.Abs(direction)<.0001f)return origin>=min&&origin<=max;
   float a=(min-origin)/direction,b=(max-origin)/direction;if(a>b){float swap=a;a=b;b=swap;}lo=Mathf.Max(lo,a);hi=Mathf.Min(hi,b);return hi>lo;
  }
  // Scatter flight lines throughout the available sky, keeping the whole sprite clear of the planet.
  public static bool Flight(int seed,int index,int cycle,float aspect,Vector2 center,float radius,float size,out Vector2 start,out Vector2 end){
   start=end=Vector2.zero;var random=new System.Random(unchecked(seed*397+index*7919+cycle*104729));
   for(int attempt=0;attempt<400;attempt++){
    float angle=(float)random.NextDouble()*Mathf.PI*2;Vector2 normal=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));Vector2 direction=new Vector2(-normal.y,normal.x)*(random.Next(2)==0?1:-1);
    float skyWidth=Mathf.Max(.12f,aspect*.32f);
    Vector2 origin=center+normal*(radius+size*.5f+.035f+(float)random.NextDouble()*skyWidth);
    float lo=-10,hi=10;if(!ClipAxis(origin.x,direction.x,0,aspect,ref lo,ref hi)||!ClipAxis(origin.y,direction.y,0,1,ref lo,ref hi)||hi-lo<.18f)continue;
    lo=-10;hi=10;float margin=size+.05f;ClipAxis(origin.x,direction.x,-margin,aspect+margin,ref lo,ref hi);ClipAxis(origin.y,direction.y,-margin,1+margin,ref lo,ref hi);
    start=origin+direction*lo;end=origin+direction*hi;return true;
   }return false;
  }
  void OnEnable(){block=new MaterialPropertyBlock();layoutReady=false;Camera.onPreCull+=Render;}
  void OnDisable(){Camera.onPreCull-=Render;}
  void Render(Camera camera){
   if(!background||camera!=background.targetCamera||!asteroidStyle||!quad||!material)return;
   // Anchor trajectories to the persistent sky, not the moving gameplay planet.
   // Re-solving planet clearance every frame made recoil/camera motion reroute every asteroid.
   if(!layoutReady){
    skyAspect=camera.aspect;skyCenter=new Vector2(.5f*skyAspect,.5f);skyRadius=0;
    if(world&&world.planet){var p=camera.WorldToViewportPoint(world.planet.position);skyCenter=new Vector2(p.x*skyAspect,p.y);skyRadius=Vector2.Distance(camera.WorldToViewportPoint(world.planet.position+camera.transform.up*world.displayRadius),p);}
    layoutReady=true;
   }
   float clock=background.environment.phase,aspect=skyAspect;Vector2 center=skyCenter;float radius=skyRadius;
   float depth=Mathf.Min(camera.farClipPlane*.7f,150),height=camera.orthographic?camera.orthographicSize*2:2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
   var random=new System.Random(asteroidStyle.seed);
   for(int i=0;i<count;i++){
    float size=.16f+(float)random.NextDouble()*.06f;
    float duration=115+(float)random.NextDouble()*75,period=duration+8+(float)random.NextDouble()*18;
    float time=Mathf.Max(0,clock)+(float)random.NextDouble()*period;int cycle=Mathf.FloorToInt(time/period);float age=time-cycle*period;
    if(age>=duration)continue;Vector2 start,end;if(!Flight(asteroidStyle.seed,i,cycle,aspect,center,radius,size,out start,out end))continue;
    Vector2 direction=(end-start).normalized;
    Vector2 outward=new Vector2(-direction.y,direction.x);
    if(Vector2.Dot((start+end)*.5f-center,outward)<0)outward=-outward;
    // A gentle outward drift breaks ruler-straight motion without crossing the planet clearance.
    float bend=.012f*(1+Mathf.Sin(age*.045f+i*2.399f));
    Vector2 uv=Vector2.Lerp(start,end,age/duration)+outward*bend;uv.x/=aspect;
    Vector3 position=camera.ViewportToWorldPoint(new Vector3(uv.x,uv.y,depth));Quaternion rotation=camera.transform.rotation;
    // Keep the laboratory's connected silhouette; random seeds can split it into tiny fragments.
    PlanetStyleParameters.Apply(block,asteroidStyle.data.layers[0],asteroidStyle,clock*.05f);block.SetFloat("rotation",asteroidStyle.rotation*Mathf.Deg2Rad+clock*(i%2==0?.022f:-.017f)+i*1.2f);block.SetFloat("pixels",100);
    Graphics.DrawMesh(quad,Matrix4x4.TRS(position,rotation,Vector3.one*(size*height)),material,gameObject.layer,camera,0,block,UnityEngine.Rendering.ShadowCastingMode.Off,false);
   }
  }
 }
}
