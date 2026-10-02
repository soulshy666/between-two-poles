using UnityEngine;
using BetweenPoles.Generators;
namespace BetweenPoles {
 [ExecuteAlways,DefaultExecutionOrder(1200)]
 public sealed class SkyStreaks:MonoBehaviour {
  public GeneratedBackgroundFollower background;
  public CurvedTrialCamera world;
  public Renderer layer;
  struct Flight {public int cycle;public Vector2 start,end;public float duration;public bool visible;}
  Flight[] flights=new Flight[3];
  Vector4[] heads=new Vector4[3],styles=new Vector4[3];
  MaterialPropertyBlock block;
  void OnEnable(){for(int i=0;i<3;i++)flights[i].cycle=int.MinValue;Camera.onPreCull+=BeforeCamera;}
  void OnDisable(){Camera.onPreCull-=BeforeCamera;}
  void BeforeCamera(Camera cam){if(background&&cam==background.targetCamera)Refresh();}
  void LateUpdate(){Refresh();}
  static bool Clip(float origin,float direction,float min,float max,ref float lo,ref float hi){
   if(Mathf.Abs(direction)<.0001f)return origin>=min&&origin<=max;
   float a=(min-origin)/direction,b=(max-origin)/direction;if(a>b){float t=a;a=b;b=t;}lo=Mathf.Max(lo,a);hi=Mathf.Min(hi,b);return hi>lo;
  }
  // Reserve the complete approach/explosion plus a quiet gap on both sides.
  // Decide for the whole flight so a comet is never cut off halfway through.
  public static bool HasClearCollisionWindow(float start,float duration,bool collisions,float period,float offset){
   if(!collisions)return true;
   float collisionClock=start+offset;if(collisionClock<0)return false;
   period=Mathf.Max(8,period);float phase=Mathf.Repeat(collisionClock,period);
   return phase>=5.8f+1.5f&&phase+duration<=period-1.5f;
  }
  public void Refresh(){
   if(!background||!background.environment||!background.targetCamera||!layer)return;
   var cam=background.targetCamera;var lab=background.environment;float aspect=cam.aspect;
   Vector2 center=new Vector2(aspect*.5f,.5f);float radius=0;
   if(world&&world.planet){Vector3 p=cam.WorldToViewportPoint(world.planet.position);center=new Vector2(p.x*aspect,p.y);float r=world.displayRadius>0?world.displayRadius:world.planet.lossyScale.x*10;radius=Vector2.Distance(cam.WorldToViewportPoint(world.planet.position+cam.transform.up*r),p);}
   for(int i=0;i<3;i++){
    float period=i==2?23:10+i*3,clock=Mathf.Max(0,lab.phase)+i*4.7f;int cycle=Mathf.FloorToInt(clock/period);float age=clock-cycle*period;
    if(flights[i].cycle!=cycle){
     var random=new System.Random(unchecked(lab.seed*193+cycle*6151+i*271));var flight=new Flight{cycle=cycle,duration=i==2?6:1.4f+(float)random.NextDouble()*.8f};
     for(int attempt=0;attempt<160;attempt++){
      float angle=(float)random.NextDouble()*Mathf.PI*2;Vector2 normal=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
      Vector2 direction=new Vector2(-normal.y,normal.x)*(random.Next(2)==0?1:-1);
      Vector2 origin=center+normal*(radius+.045f+(float)random.NextDouble()*.12f);float lo=-5,hi=5;
      if(!Clip(origin.x,direction.x,0,aspect,ref lo,ref hi)||!Clip(origin.y,direction.y,0,1,ref lo,ref hi)||hi-lo<.28f)continue;
      flight.start=origin+direction*(lo-.12f);flight.end=origin+direction*(hi+.25f);flight.visible=true;break;
     }flights[i]=flight;
    }
    var f=flights[i];Vector2 dir=(f.end-f.start).normalized,head=Vector2.LerpUnclamped(f.start,f.end,age/f.duration);
    heads[i]=new Vector4(head.x,head.y,dir.x,dir.y);
    float fade=f.visible&&age<f.duration?Mathf.SmoothStep(0,1,age/.18f)*Mathf.Clamp01((f.duration-age)/.3f):0;
    float flightStart=cycle*period-i*4.7f;
    if(!HasClearCollisionWindow(flightStart,f.duration,lab.meteorCollision,lab.meteorPeriod,lab.meteorOffset))fade=0;
    styles[i]=new Vector4(i==2?.23f:.13f,i==2?.007f:.0024f,fade,i==2?1:0);
   }
   if(block==null)block=new MaterialPropertyBlock();layer.GetPropertyBlock(block);
   block.SetVectorArray("_Heads",heads);block.SetVectorArray("_Styles",styles);block.SetFloat("_Clock",lab.phase);block.SetFloat("_Aspect",aspect);
   block.SetVector("_Planet",new Vector4(center.x,center.y,radius,0));
   block.SetVector("_Resolution",new Vector4(cam.pixelWidth,cam.pixelHeight,0,0));layer.SetPropertyBlock(block);
   layer.transform.localScale=new Vector3(5f*lab.width/lab.height,5,1);
  }
 }
}
