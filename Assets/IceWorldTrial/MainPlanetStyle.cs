using UnityEngine;
using BetweenPoles.Generators;
namespace BetweenPoles {
 [ExecuteAlways] public sealed class MainPlanetStyle:MonoBehaviour {
  [InspectorName("导出的主星球样式")] public PlanetStyle style;
  [InspectorName("游玩相机")] public Camera targetCamera;
  [InspectorName("地表自动流动（关闭时只随切岛转动）")] public bool animateSurface;
  [InspectorName("云层动画")] public bool animateClouds=true;
  Renderer original;MeshFilter mesh;Material[] materials;PlanetStyle loaded;bool originalEnabled;
  MaterialPropertyBlock block;
  void OnEnable(){block=new MaterialPropertyBlock();original=GetComponent<Renderer>();mesh=GetComponent<MeshFilter>();if(original)originalEnabled=original.enabled;Camera.onPreCull+=Render;}
  void OnDisable(){Camera.onPreCull-=Render;Release();if(original)original.forceRenderingOff=false;}
  void Release(){if(materials!=null)foreach(var m in materials)if(m)PixelGeneratorLab.Release(m);materials=null;loaded=null;}
  public void Rebuild(){Release();}
  void Render(Camera camera){
   if(camera!=targetCamera||!original||!mesh)return;
   if(!style){original.forceRenderingOff=false;return;}
   if(style!=loaded){Release();if(style.data==null||style.surfaceShaders==null||style.surfaceShaders.Length!=style.data.layers.Length)return;
    materials=new Material[style.surfaceShaders.Length];for(int i=0;i<materials.Length;i++){if(!style.surfaceShaders[i]){Release();return;}materials[i]=new Material(style.surfaceShaders[i]){hideFlags=HideFlags.HideAndDontSave,renderQueue=1900+i};}loaded=style;
   }
   if(materials==null)return;original.forceRenderingOff=true;
   float clock=Time.realtimeSinceStartup;
#if UNITY_EDITOR
   if(!Application.isPlaying)clock=(float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
   for(int i=0;i<materials.Length;i++){var layer=style.data.layers[i];if(!layer.visible)continue;float phase=animateSurface||(animateClouds&&layer.shader=="Clouds")?clock*style.speed:0;
    PlanetStyleParameters.Apply(block,layer,style,phase);
    Graphics.DrawMesh(mesh.sharedMesh,transform.localToWorldMatrix,materials[i],gameObject.layer,camera,0,block,UnityEngine.Rendering.ShadowCastingMode.Off,false);
   }
  }
 }
}
