using UnityEngine;
namespace BetweenPoles {
// Small mesh particles share the game's spherical-world shader and island binding.
public sealed class MagnetContactSpark:MonoBehaviour {
    const int SparkCount=9;
    const float FlashSize=.30f,SparkSize=.13f,BurstSpread=1.4f;
    Transform[] particles;Renderer[] renderers;Vector3[] velocities;Material material;float age;
    public bool Reusable {get;private set;}
    public static MagnetContactSpark Spawn(Vector3 point,MaterialPropertyBlock block,Transform owner,bool reusable){
        var root=new GameObject("Magnet contact sparks");root.SetActive(false);
        // The board owns its pool, so unloading a chapter also releases its effects.
        root.transform.SetParent(owner,true);
        var effect=root.AddComponent<MagnetContactSpark>();effect.Reusable=reusable;
        effect.Build();effect.Play(point,block);return effect;
    }
    void Build(){
        material=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));
        material.color=new Color(3f,1.8f,.3f);material.SetFloat("_Painted",0);material.SetFloat("_Snow",0);material.SetFloat("_Grid",0);
        particles=new Transform[SparkCount+1];velocities=new Vector3[particles.Length];renderers=new Renderer[particles.Length];
        for(int i=0;i<particles.Length;i++){
            var g=new GameObject("Contact spark");g.transform.SetParent(transform,false);
            g.AddComponent<MeshFilter>().sharedMesh=MagnetVisuals.PrimitiveMesh(PrimitiveType.Cube);
            var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderers[i]=renderer;
            renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*300);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            particles[i]=g.transform;
            float angle=i*2.399963f;
            velocities[i]=i==0?Vector3.zero:new Vector3(Mathf.Cos(angle),.35f+(i%4)*.16f,Mathf.Sin(angle))*(1.2f+(i%5)*.28f)*BurstSpread;
            g.transform.localScale=Vector3.one*(i==0?FlashSize:SparkSize);
        }
    }
    public void Play(Vector3 point,MaterialPropertyBlock block){
        age=0;transform.position=point;material.color=new Color(3f,1.8f,.3f);
        for(int i=0;i<particles.Length;i++){
            particles[i].localPosition=Vector3.zero;
            particles[i].localScale=Vector3.one*(i==0?FlashSize:SparkSize);
            // Replace the previous island's mapping as well as its colour overrides.
            renderers[i].SetPropertyBlock(block);
        }
        gameObject.SetActive(true);
    }
    public void Stop(){gameObject.SetActive(false);if(!Reusable)Destroy(gameObject);}
    void Update(){
        age+=Time.deltaTime;float p=Mathf.Clamp01(age/.28f);
        for(int i=0;i<particles.Length;i++){
            particles[i].localPosition=velocities[i]*age+Vector3.down*(age*age*2);
            particles[i].localScale=Vector3.one*(i==0?FlashSize*Mathf.Max(0,1-age/.075f):SparkSize*(1-p));
        }
        material.color=Color.Lerp(new Color(3,1.8f,.3f),new Color(1,.22f,.015f),p);
        if(age>=.28f)Stop();
    }
    void OnDestroy(){if(material)Destroy(material);}
}
}
