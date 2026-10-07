using UnityEngine;
namespace BetweenPoles {
// Small mesh particles share the game's spherical-world shader and island binding.
public sealed class MagnetContactSpark:MonoBehaviour {
    const int SparkCount=9;
    const float FlashSize=.30f,SparkSize=.13f,BurstSpread=1.4f;
    Transform[] particles;Vector3[] velocities;Material material;float age;
    public static MagnetContactSpark Spawn(Vector3 point,MaterialPropertyBlock block){
        var root=new GameObject("Magnet contact sparks");root.transform.position=point;
        var effect=root.AddComponent<MagnetContactSpark>();effect.Build(block);return effect;
    }
    void Build(MaterialPropertyBlock block){
        material=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));
        material.color=new Color(3f,1.8f,.3f);material.SetFloat("_Painted",0);material.SetFloat("_Snow",0);material.SetFloat("_Grid",0);
        particles=new Transform[SparkCount+1];velocities=new Vector3[particles.Length];
        for(int i=0;i<particles.Length;i++){
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Contact spark";g.transform.SetParent(transform,false);
            var collider=g.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=g.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.SetPropertyBlock(block);
            renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*300);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            particles[i]=g.transform;
            float angle=i*2.399963f;
            velocities[i]=i==0?Vector3.zero:new Vector3(Mathf.Cos(angle),.35f+(i%4)*.16f,Mathf.Sin(angle))*(1.2f+(i%5)*.28f)*BurstSpread;
            g.transform.localScale=Vector3.one*(i==0?FlashSize:SparkSize);
        }
    }
    void Update(){
        age+=Time.deltaTime;float p=Mathf.Clamp01(age/.28f);
        for(int i=0;i<particles.Length;i++){
            particles[i].localPosition=velocities[i]*age+Vector3.down*(age*age*2);
            particles[i].localScale=Vector3.one*(i==0?FlashSize*Mathf.Max(0,1-age/.075f):SparkSize*(1-p));
        }
        material.color=Color.Lerp(new Color(3,1.8f,.3f),new Color(1,.22f,.015f),p);
        if(age>=.28f){gameObject.SetActive(false);Destroy(gameObject);}
    }
    void OnDestroy(){if(material)Destroy(material);}
}
}
