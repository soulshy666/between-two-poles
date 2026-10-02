using UnityEngine;
using BetweenPoles.Generators;

namespace BetweenPoles {
    // Select once per event, in camera space; never teleport an ongoing collision.
    [ExecuteAlways, DefaultExecutionOrder(1100)]
    public sealed class VisibleMeteorCollisions : MonoBehaviour {
        public GeneratedBackgroundFollower background;
        public CurvedTrialCamera world;
        int cycle=int.MinValue;
        Vector2 selected, previous;
        float angle, eventScale=1;
        bool hasLocation;
        Renderer effect;
        MaterialPropertyBlock block;
        void OnEnable(){cycle=int.MinValue;Camera.onPreCull+=BeforeCamera;}
        void OnDisable(){Camera.onPreCull-=BeforeCamera;}
        void LateUpdate(){Refresh();}
        void BeforeCamera(Camera camera){if(background&&camera==background.targetCamera)Refresh();}
        Vector2 Viewport(Vector2 uv,Camera camera){return camera.WorldToViewportPoint(effect.transform.TransformPoint(new Vector3(uv.x-.5f,uv.y-.5f,0)));}
        bool Clear(Vector2 uv,float radius,Camera camera) {
            Vector2 v=Viewport(uv,camera);
            float margin=Vector2.Distance(Viewport(uv+Vector2.up*radius,camera),v);
            if(v.x<margin/camera.aspect+.015f||v.x>1-margin/camera.aspect-.015f||v.y<margin+.015f||v.y>1-margin-.015f)return false;
            if(!world||!world.planet)return true;
            Vector3 p=camera.WorldToViewportPoint(world.planet.position);
            float r=world.displayRadius>0?world.displayRadius:world.planet.lossyScale.x*10;
            float projected=Vector2.Distance(camera.WorldToViewportPoint(world.planet.position+camera.transform.up*r),p);
            Vector2 d=v-new Vector2(p.x,p.y);d.x*=camera.aspect;
            return d.magnitude>projected+margin+.025f;
        }
        public void Refresh(){
            if(!background||!background.environment||!background.targetCamera)return;
            var lab=background.environment;var camera=background.targetCamera;
            if(!effect)foreach(var visual in lab.visuals)if(visual.layer==100){effect=visual.renderer;break;}
            if(!effect)return;
            float time=Mathf.Max(0,lab.phase+lab.meteorOffset);
            int current=Mathf.FloorToInt(time/Mathf.Max(8,lab.meteorPeriod));
            float radius=.19f*eventScale;
            if(current!=cycle){
                cycle=current;hasLocation=false;
                var random=new System.Random(unchecked(lab.seed*397+current*7919));
                float best=-1;
                for(int size=0;size<5&&!hasLocation;size++){
                eventScale=lab.meteorScale*Mathf.Pow(.8f,size);radius=.19f*eventScale;
                for(int i=0;i<384;i++){
                    Vector2 screen=new Vector2(.04f+(float)random.NextDouble()*.92f,.04f+(float)random.NextDouble()*.92f);
                    float depth=Vector3.Dot(effect.transform.position-camera.transform.position,camera.transform.forward);
                    Vector3 local=effect.transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(screen.x,screen.y,depth)));
                    Vector2 uv=new Vector2(local.x+.5f,local.y+.5f);
                    if(!Clear(uv,radius,camera))continue;
                    float candidateAngle=(float)random.NextDouble()*Mathf.PI*2;
                    // Keep the final approach on the same clear side of the planet.
                    Vector2 offset=new Vector2(-.25f*(float)lab.width/lab.height,.37f)*(.35f*eventScale);
                    float cs=Mathf.Cos(candidateAngle),sn=Mathf.Sin(candidateAngle);
                    offset=new Vector2(cs*offset.x-sn*offset.y,sn*offset.x+cs*offset.y);offset.x/=(float)lab.width/lab.height;
                    if(!Clear(uv+offset,.055f*eventScale,camera))continue;
                    float score=Vector2.Distance(screen,previous)+(float)random.NextDouble()*.25f;
                    if(score<=best)continue;best=score;selected=uv;angle=candidateAngle;hasLocation=true;
                }}
                if(hasLocation)previous=Viewport(selected,camera);
            }
            if(block==null)block=new MaterialPropertyBlock();effect.GetPropertyBlock(block);
            block.SetVector("_CollisionCenter",selected);block.SetFloat("_CollisionAngle",angle);
            block.SetFloat("_CollisionScale",eventScale);
            block.SetFloat("_HideEvent",hasLocation&&Clear(selected,radius,camera)?0:1);
            effect.SetPropertyBlock(block);
        }
    }
}
