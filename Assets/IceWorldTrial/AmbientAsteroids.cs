using UnityEngine;
using BetweenPoles.Generators;
namespace BetweenPoles {
 [ExecuteAlways,DefaultExecutionOrder(1250)] public sealed class AmbientAsteroids:MonoBehaviour {
  public GeneratedBackgroundFollower background;public CurvedTrialCamera world;
  [InspectorName("小行星实验室参数")] public PlanetStyle asteroidStyle;
  public Mesh quad;public Material material;
  [Range(1,16),InspectorName("常驻陨石数量")] public int count=7;
  MaterialPropertyBlock block;
  void OnEnable(){block=new MaterialPropertyBlock();Camera.onPreCull+=Render;}
  void OnDisable(){Camera.onPreCull-=Render;}
  void Render(Camera camera){
   if(!background||camera!=background.targetCamera||!asteroidStyle||!quad||!material)return;
   float clock=background.environment.phase,aspect=camera.aspect;Vector2 center=new Vector2(.5f*aspect,.5f);float radius=0;
   if(world&&world.planet){var p=camera.WorldToViewportPoint(world.planet.position);center=new Vector2(p.x*aspect,p.y);radius=Vector2.Distance(camera.WorldToViewportPoint(world.planet.position+camera.transform.up*world.displayRadius),p);}
   float depth=Mathf.Min(camera.farClipPlane*.7f,150),height=camera.orthographic?camera.orthographicSize*2:2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
   var random=new System.Random(asteroidStyle.seed);var occupied=new Vector2[count];int placed=0;
   for(int i=0;i<count;i++){
    float size=.045f+(float)random.NextDouble()*.040f;Vector2 uv=Vector2.zero;bool found=false;
    for(int k=0;k<100;k++){uv=new Vector2(.05f+(float)random.NextDouble()*.9f,.06f+(float)random.NextDouble()*.88f);Vector2 metric=new Vector2(uv.x*aspect,uv.y);if(Vector2.Distance(metric,center)<radius+size+.04f)continue;bool crowded=false;for(int j=0;j<placed;j++)if(Vector2.Distance(metric,occupied[j])<.10f)crowded=true;if(crowded)continue;occupied[placed++]=metric;found=true;break;}
    if(!found)continue;uv+=new Vector2(Mathf.Sin(clock*.12f+i*2)*.006f,Mathf.Cos(clock*.16f+i)*.009f);
    Vector3 position=camera.ViewportToWorldPoint(new Vector3(uv.x,uv.y,depth));Quaternion rotation=camera.transform.rotation;
    PlanetStyleParameters.Apply(block,asteroidStyle.data.layers[0],asteroidStyle,clock*.05f);block.SetFloat("seed",.2f+(i*1.73f+asteroidStyle.seed*.01f)%9);block.SetFloat("rotation",clock*(i%2==0?.022f:-.017f)+i*1.2f);block.SetFloat("pixels",48);
    Graphics.DrawMesh(quad,Matrix4x4.TRS(position,rotation,Vector3.one*(size*height)),material,gameObject.layer,camera,0,block,UnityEngine.Rendering.ShadowCastingMode.Off,false);
   }
  }
 }
}
