using System;
using UnityEngine;
namespace BetweenPoles.Generators {
 [Serializable] public class Scalar { public string name; public float value; }
 [Serializable] public class VectorParam { public string name; public Vector4 value; }
 [Serializable] public class ColorSet { public string name; public Color[] values; }
 [Serializable] public class LayerData { public string name,shader; public float scale=1; public bool visible=true; public Scalar[] floats; public VectorParam[] vectors; public ColorSet[] colors;
  public float Get(string key,float fallback=0){if(floats!=null)foreach(var p in floats)if(p.name==key)return p.value;return fallback;}
 }
 [Serializable] public class ModelData {public string name;public float relativeScale=1;public LayerData[] layers;}
 [Serializable] public class Catalog {public ModelData[] models;}
 [Serializable] public class VisualBinding {public Renderer renderer;public int model,layer;public Vector3 origin;public Vector2 wrap;}
 [Serializable] public class GeneratorSettings {
  public bool meteorCollision=true;public float meteorPeriod=28,meteorScale=1;public Vector2 meteorCenter=new Vector2(.19f,.27f);
  public int model,seed,width,height,pixels;public float rotation,speed;public Vector2 light;
  public bool dither;public Catalog catalog;public Color[] spaceColors;public Color background;
  public bool stars,dust,nebula,planets,transparent,tile,pixelArt,quiet,clearEdges;public float density,flow,warp,drift,twinkle;
 }
}
