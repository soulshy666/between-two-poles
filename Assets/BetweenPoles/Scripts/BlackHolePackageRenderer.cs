using UnityEngine;
namespace BetweenPoles {
// Refract the captured game view with the supplied package's distortion texture.
public sealed class BlackHolePackageRenderer : MonoBehaviour {
    RenderTexture output;
    Material refraction;
    public void Initialize(){
        refraction=new Material(Resources.Load<Material>("BlackHoleTransit"));
        var prefab=Resources.Load<GameObject>("BlackHolePackage");
        var renderer=prefab.GetComponent<ParticleSystemRenderer>();
        refraction.SetTexture("_FlowTex",renderer.sharedMaterial.GetTexture("_DistortTex"));
    }
    public RenderTexture Render(RenderTexture source,float progress,bool flip){
        if(!output||output.width!=source.width||output.height!=source.height){
            if(output){output.Release();Destroy(output);}
            output=new RenderTexture(source.width,source.height,0,RenderTextureFormat.ARGB32);
            output.Create();
        }
        refraction.SetFloat("_Progress",progress);
        refraction.SetFloat("_FlipY",flip?1:0);
        refraction.SetFloat("_Aspect",(float)source.width/source.height);
        refraction.SetVector("_Center",new Vector4(.5f,.5f,0,0));
        Graphics.Blit(source,output,refraction);
        return output;
    }
    void OnDestroy(){if(output){output.Release();Destroy(output);}if(refraction)Destroy(refraction);}
}
}
