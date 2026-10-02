using UnityEngine;
namespace BetweenPoles.Generators {
 [CreateAssetMenu(menuName="两极之间/主星球样式",fileName="主星球样式")]
 public sealed class PlanetStyle:ScriptableObject {
  public int version=1;
  [InspectorName("星球类型")] public string modelName;
  public int model,seed,pixels;
  public float rotation,speed;
  public Vector2 light;
  public bool dither;
  public ModelData data;
  public Shader[] surfaceShaders;
 }
 public static class PlanetStyleParameters {
  public static void Apply(MaterialPropertyBlock block,LayerData layer,PlanetStyle style,float phase){
   block.Clear();
   foreach(var p in layer.floats){if(p.name=="OCTAVES"||p.name=="octaves"||p.name=="n_colors")block.SetInt(p.name,(int)p.value);else block.SetFloat(p.name,p.value);}
   foreach(var p in layer.vectors)block.SetVector(p.name,p.value);
   foreach(var set in layer.colors){var colors=new Vector4[set.values.Length];for(int i=0;i<colors.Length;i++)colors[i]=QualitySettings.activeColorSpace==ColorSpace.Linear?(Vector4)set.values[i].linear:(Vector4)set.values[i];block.SetVectorArray(set.name,colors);}
   block.SetFloat("pixels",style.pixels*layer.scale);block.SetFloat("seed",.01f+(System.Math.Abs((long)style.seed)%1000)/100f);
   block.SetFloat("rotation",style.rotation*Mathf.Deg2Rad+layer.Get("rotation"));block.SetFloat("should_dither",style.dither?1:0);block.SetVector("light_origin",style.light);
   float ts=layer.Get("time_speed",.2f),size=layer.Get("size",5);float rate=ts==0?0:Mathf.Round(size)*2/ts*.02f;if(layer.shader=="Clouds")rate*=.5f;
   block.SetFloat("time",phase*rate);
  }
 }
}
