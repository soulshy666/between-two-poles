using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetweenPoles.Generators {
 [ExecuteAlways] public class PixelGeneratorLab : MonoBehaviour {
  [InspectorName("太空背景模式")] public bool spaceMode;
  [InspectorName("预览相机")] public Camera previewCamera;
  [InspectorName("原版天体参数")] public TextAsset originalCatalog;
  [HideInInspector] public Catalog catalog;
  [HideInInspector] public List<VisualBinding> visuals=new List<VisualBinding>();
  [Range(0,11),InspectorName("天体类型")] public int model=2;
  [InspectorName("随机种子")] public int seed=795;
  [Range(12,5000),InspectorName("像素精度")] public int pixels=100;
  [Range(-180,180),InspectorName("倾角")] public float rotation;
  [Range(-3,3),InspectorName("动画速度")] public float speed=1;
  [InspectorName("光源位置")] public new Vector2 light=new Vector2(.3f,.3f);
  [InspectorName("像素抖动")] public bool dither=true;
  [InspectorName("暂停")] public bool paused;
  [InspectorName("中文面板")] public bool showPanel=true;
  [HideInInspector] public bool standaloneAsset;
  public static Action<PixelGeneratorLab,bool> ExportUnityRequested;
  public static Action<PixelGeneratorLab> ExportStyleRequested;
  [Header("太空图层与动画")]
  [InspectorName("星星")] public bool stars=true;
  [InspectorName("星尘")] public bool dust=true;
  [InspectorName("星云")] public bool nebula=true;
  [InspectorName("行星")] public bool planets=true;
  [InspectorName("透明背景")] public bool transparent;
  [InspectorName("平铺")] public bool tile;
  [InspectorName("像素模式")] public bool pixelArt=true;
  [InspectorName("减弱背景")] public bool quiet;
  [InspectorName("边缘留空")] public bool clearEdges;
  [Range(0,1),InspectorName("星星密度")] public float density=.13f;
  [Range(0,3),InspectorName("星云流动")] public float flow=.5f;
  [Range(0,.1f),InspectorName("星云扭曲")] public float warp=.009f;
  [Range(0,1),InspectorName("行星漂浮")] public float drift=.25f;
  [Range(0,1),InspectorName("星星闪烁")] public float twinkle=1;
  [InspectorName("陨石对撞")] public bool meteorCollision=true;
  [Range(8,60),InspectorName("对撞间隔（秒）")] public float meteorPeriod=28;
  [Range(.2f,2),InspectorName("对撞效果大小")] public float meteorScale=1;
  [InspectorName("碰撞位置（屏幕归一化）")] public Vector2 meteorCenter=new Vector2(.19f,.27f);
  [HideInInspector] public float meteorOffset;
  [InspectorName("背景颜色")] public Color background=new Color(.025f,.035f,.075f,1);
  [InspectorName("太空调色板")] public Color[] spaceColors;
  [InspectorName("导出宽度")] public int width=640;
  [InspectorName("导出高度")] public int height=360;
  [InspectorName("动画帧数")] public int frames=48;
  [InspectorName("动画时长（秒）")] public float duration=8;
  [InspectorName("图集列数")] public int columns=8;
  [InspectorName("图集间距")] public int margin;
  [HideInInspector] public float phase;
  [NonSerialized] public float exportCycle=-1;
  MaterialPropertyBlock block;
  Texture2D paletteTexture;
  Color[] paletteCache;
  Font font;
  double last;
  int tab,selectedColor;
  Vector2 scroll;
  string hex="",paletteText="",seedText="",status="设置会实时更新；可保存方案以保留播放时的修改。";
  public string Status {get{return status;}set{status=value;}}
  GeneratorExport export;
  public bool IsExporting {get{return export!=null;}}
  public string ExportDirectory {get{return Path.GetFullPath(Path.Combine(Application.dataPath,"../PixelGeneratorExports",spaceMode?"太空背景":"像素星球"));}}
  void OnEnable(){last=Clock();EnsureData();Apply();
#if UNITY_EDITOR
   UnityEditor.EditorApplication.update+=EditorTick;
#endif
  }
  void OnDisable(){
#if UNITY_EDITOR
   UnityEditor.EditorApplication.update-=EditorTick;
#endif
   if(export!=null){export.Cancel();export=null;}Release(paletteTexture);paletteTexture=null;Release(font);font=null;
  }
  public static void Release(UnityEngine.Object obj){if(!obj)return;if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
  double Clock(){
#if UNITY_EDITOR
   if(!Application.isPlaying)return UnityEditor.EditorApplication.timeSinceStartup;
#endif
   return Time.realtimeSinceStartupAsDouble;
  }
#if UNITY_EDITOR
  void EditorTick(){if(this==null||Application.isPlaying||Clock()-last<1.0/30)return;Tick();UnityEditor.EditorApplication.QueuePlayerLoopUpdate();UnityEditor.SceneView.RepaintAll();}
#endif
  void Update(){if(Application.isPlaying)Tick();}
  void Tick(){double now=Clock();if(export!=null){try{if(export.Step()){status="导出完成："+export.OutputPath;export.Dispose();export=null;}}catch(Exception e){status="导出失败："+e.Message;export.Cancel();export=null;Debug.LogException(e);}}else if(!paused)phase+=(float)Math.Min(.1,now-last)*speed;last=now;Apply();}
  void OnValidate(){pixels=Mathf.Clamp(pixels,12,5000);width=Mathf.Clamp(width,100,3000);height=Mathf.Clamp(height,100,3000);EnsureData();}
  public void EnsureData(){if((catalog==null||catalog.models==null)&&originalCatalog)catalog=JsonUtility.FromJson<Catalog>(originalCatalog.text);if(spaceColors==null||spaceColors.Length!=8)SpacePreset(0);}
  public void SpacePreset(int index){string[][] sets={new[]{"080d24","171b42","29234f","3e326c","594591","7764bb","a297df","e4edff"},new[]{"061b20","0c313b","174e58","206a70","338d8f","66b7b0","a0d7c8","edffd8"},new[]{"210b25","3b1636","612743","873a50","b45059","d57466","efb68c","fff1be"}};spaceColors=new Color[8];for(int i=0;i<8;i++)ColorUtility.TryParseHtmlString("#"+sets[Mathf.Clamp(index,0,2)][i],out spaceColors[i]);background=spaceColors[0];hex="";}
  public void SelectModel(int i){model=Mathf.Clamp(i,0,11);selectedColor=0;hex="";Apply();MarkDirty();}
  public void Randomize(){seed=UnityEngine.Random.Range(1,1000000);seedText=seed.ToString();Apply();MarkDirty();}
  public void MarkDirty(){
#if UNITY_EDITOR
   if(!Application.isPlaying){UnityEditor.EditorUtility.SetDirty(this);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);}
#endif
  }
  static Vector4 Linear(Color c){return QualitySettings.activeColorSpace==ColorSpace.Linear?(Vector4)c.linear:(Vector4)c;}
  Texture2D Palette(){
   if(!paletteTexture){paletteTexture=new Texture2D(8,1,TextureFormat.RGBA32,false,false);paletteTexture.filterMode=FilterMode.Point;paletteTexture.wrapMode=TextureWrapMode.Clamp;paletteCache=null;}
   bool changed=paletteCache==null;for(int i=0;i<8&&!changed;i++)changed=paletteCache[i]!=spaceColors[i];
   if(changed){paletteTexture.SetPixels(spaceColors);paletteTexture.Apply(false);paletteCache=(Color[])spaceColors.Clone();}return paletteTexture;
  }
  public void Apply(){
   EnsureData();if(block==null)block=new MaterialPropertyBlock();if(!standaloneAsset&&previewCamera)previewCamera.backgroundColor=spaceMode&&!transparent?background:new Color(.025f,.03f,.055f,1);
   // Cover the whole Game view without stretching the artwork. A different
   // preview aspect crops the edges; Capture still uses the requested export size.
   if(!standaloneAsset&&spaceMode&&previewCamera&&previewCamera.targetTexture==null){
    previewCamera.ResetAspect();
    float imageAspect=(float)Mathf.Max(1,width)/Mathf.Max(1,height);
    previewCamera.orthographicSize=2.5f*Mathf.Min(1f,imageAspect/Mathf.Max(.01f,previewCamera.aspect));
    previewCamera.transform.position=new Vector3(0,0,-10);
   }
   foreach(var v in visuals){if(!v.renderer)continue;block.Clear();
    if(!spaceMode){
     bool active=v.model==model&&catalog.models[v.model].layers[v.layer].visible;v.renderer.gameObject.SetActive(active);if(!active)continue;
     var d=catalog.models[v.model].layers[v.layer];
     foreach(var p in d.floats){if(p.name=="OCTAVES"||p.name=="octaves"||p.name=="n_colors")block.SetInt(p.name,(int)p.value);else block.SetFloat(p.name,p.value);}
     foreach(var p in d.vectors)block.SetVector(p.name,p.value);
     foreach(var c in d.colors){var colors=new Vector4[c.values.Length];for(int i=0;i<colors.Length;i++)colors[i]=Linear(c.values[i]);block.SetVectorArray(c.name,colors);}
     block.SetFloat("pixels",pixels*d.scale);block.SetFloat("seed",.01f+(Math.Abs((long)seed)%1000)/100f);
     block.SetFloat("rotation",rotation*Mathf.Deg2Rad+d.Get("rotation"));block.SetFloat("should_dither",dither?1:0);block.SetVector("light_origin",new Vector4(light.x,light.y,0,0));
     float ts=d.Get("time_speed",.2f),size=d.Get("size",5);float rate=ts==0?0:Mathf.Round(size)*2/ts*.02f;
     if(d.shader=="Clouds")rate*=.5f;if(d.shader=="Galaxy")rate*=2;if(d.shader=="BlackHoleRing")rate=1.2566f;
     float timeValue=phase*rate;
     if(exportCycle>=0){timeValue=ts==0?0:exportCycle*Mathf.Round(size)*2/ts;
      if(d.shader=="Star")timeValue=exportCycle/Mathf.Max(.0001f,ts);
      if(d.shader=="Galaxy")timeValue=exportCycle*Mathf.PI*2*ts;
      if(d.shader=="BlackHoleRing")timeValue=exportCycle*314.15f*ts*.5f;
      if(d.shader=="Asteroids")block.SetFloat("rotation",rotation*Mathf.Deg2Rad+exportCycle*Mathf.PI*2);
     }
     block.SetFloat("time",timeValue);
    }else{
     int role=v.layer;
     if(role==-1||role==100){
      v.renderer.gameObject.SetActive(role==-1?!transparent:meteorCollision);
      block.SetVector("background_color",Linear(background));block.SetFloat("_Clock",phase+meteorOffset);
      block.SetFloat("_Aspect",(float)width/height);block.SetFloat("_EventPeriod",meteorPeriod);block.SetFloat("_CollisionScale",meteorScale);
      block.SetVector("_CollisionCenter",new Vector4(meteorCenter.x,meteorCenter.y,0,0));block.SetVector("_Pixels",new Vector4(width,height,0,0));
      v.renderer.transform.localScale=new Vector3(5f*width/height,5,1);v.renderer.SetPropertyBlock(block);continue;
     }
     bool active=role==0?dust:role==1?nebula:role<4?stars:(planets&&role-4<Math.Abs((long)seed)%5&&(tile||v.wrap==Vector2.zero));
     v.renderer.gameObject.SetActive(active);if(!active)continue;
     block.SetTexture("colorscheme",Palette());block.SetFloat("seed",1+(Math.Abs((long)seed+role*137)%900)/100f);
     block.SetFloat("pixels",pixelArt?(role>=4?96:width):5000);block.SetFloat("pixel_art",pixelArt?1:0);block.SetFloat("should_dither",dither?1:0);
     block.SetFloat("should_tile",tile?1:0);block.SetFloat("reduce_background",quiet?1:0);block.SetFloat("clear_edges",clearEdges?1:0);block.SetFloat("edge_clear_margin",.1f);
     block.SetVector("uv_correct",new Vector4(tile?Mathf.Max(1,Mathf.Round((float)width/Math.Min(width,height))):(float)width/Math.Min(width,height),tile?Mathf.Max(1,Mathf.Round((float)height/Math.Min(width,height))):(float)height/Math.Min(width,height),0,0));block.SetVector("background_color",Linear(background));
     block.SetFloat("clockTime",phase);block.SetVector("motion",new Vector4(flow*(role==0?.0015f:.0006f),flow*.0003f,0,0));block.SetFloat("warpAmount",warp);
     block.SetFloat("size",role==0?10:tile?6:5);block.SetInt("OCTAVES",role==0?8:3);block.SetVector("light_origin",new Vector4(.18f,.25f,0,0));
     block.SetFloat("density",role==3?density*.55f:density);block.SetFloat("twinkle",twinkle);
     block.SetVector("starGrid",role==3?new Vector4(8,5,0,0):new Vector4(32,20,0,0));block.SetFloat("starFrames",role==3?6:16);block.SetFloat("starScale",role==3?3.5f:3.8f);
     if(role>=4){
      var random=new System.Random(unchecked(seed*31+role*137));float scale=.45f+(float)random.NextDouble()*.9f;float aspect=(float)width/height;
      float safe=clearEdges?.32f:.46f;Vector3 origin=new Vector3(((float)random.NextDouble()*2-1)*5*aspect*safe,((float)random.NextDouble()*2-1)*5*safe,-role*.02f);
      float t=phase*.22f+role*2.1f;Vector3 p=origin+new Vector3(Mathf.Sin(t)*drift*.23f,Mathf.Cos(t*.8f)*drift*.15f,0);
      if(tile){p.x=Mathf.Repeat(p.x+2.5f*aspect,5*aspect)-2.5f*aspect;p.y=Mathf.Repeat(p.y+2.5f,5)-2.5f;}
      p+=new Vector3(v.wrap.x*5*aspect,v.wrap.y*5,0);v.renderer.transform.localPosition=p;v.renderer.transform.localScale=Vector3.one*scale;
     }
     else {var s=v.renderer.transform.localScale;s.x=5f*width/height;s.y=5f;v.renderer.transform.localScale=s;}
    }
    v.renderer.SetPropertyBlock(block);
   }
  }
  public List<Color> GetColors(){var list=new List<Color>();if(spaceMode){list.AddRange(spaceColors);return list;}if(catalog==null)return list;foreach(var l in catalog.models[model].layers)foreach(var c in l.colors)list.AddRange(c.values);return list;}
  public void SetColors(IList<Color> colors){int k=0;if(spaceMode){for(int i=0;i<8;i++)spaceColors[i]=colors[i];}else foreach(var l in catalog.models[model].layers)foreach(var c in l.colors)for(int i=0;i<c.values.Length;i++)c.values[i]=colors[k++];Apply();MarkDirty();}
  public void RandomColors(){var colors=GetColors();float h=UnityEngine.Random.value;for(int i=0;i<colors.Count;i++){float shift=(i%4)*.025f;colors[i]=Color.HSVToRGB(Mathf.Repeat(h+shift,1),.45f+(i%3)*.12f,Mathf.Lerp(.95f,.18f,(i%4)/3f));}SetColors(colors);hex="";}
  public void ResetColors(){if(spaceMode)SpacePreset(0);else{var original=JsonUtility.FromJson<Catalog>(originalCatalog.text);var current=catalog.models[model];for(int i=0;i<current.layers.Length;i++)current.layers[i].colors=original.models[model].layers[i].colors;}hex="";Apply();MarkDirty();}
  public string ColorText(){var c=GetColors();return string.Join("\n",c.ConvertAll(x=>"#"+ColorUtility.ToHtmlStringRGBA(x)).ToArray());}
  public bool ImportColors(string text){string[] parts=text.Split(new[]{'\n','\r',',',';',' ','\t'},StringSplitOptions.RemoveEmptyEntries);var expected=GetColors().Count;if(parts.Length!=expected){status="需要 "+expected+" 个颜色，当前有 "+parts.Length+" 个。";return false;}var colors=new List<Color>();foreach(var p in parts){Color c;if(!ColorUtility.TryParseHtmlString(p.StartsWith("#")?p:"#"+p,out c)){status="无效颜色："+p;return false;}colors.Add(c);}SetColors(colors);hex="";status="已应用全部自定义颜色。";return true;}
  public void SaveSettings(){Directory.CreateDirectory(ExportDirectory);var s=new GeneratorSettings{model=model,seed=seed,width=width,height=height,pixels=pixels,rotation=rotation,speed=speed,light=light,dither=dither,catalog=catalog,spaceColors=spaceColors,background=background,stars=stars,dust=dust,nebula=nebula,planets=planets,transparent=transparent,tile=tile,pixelArt=pixelArt,quiet=quiet,clearEdges=clearEdges,density=density,flow=flow,warp=warp,drift=drift,twinkle=twinkle,meteorCollision=meteorCollision,meteorPeriod=meteorPeriod,meteorScale=meteorScale,meteorCenter=meteorCenter};string p=Path.Combine(ExportDirectory,"方案_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".json");File.WriteAllText(p,JsonUtility.ToJson(s,true));status="已保存："+p;}
  public void LoadSettings(string path){var s=JsonUtility.FromJson<GeneratorSettings>(File.ReadAllText(path));if(s.catalog==null||s.catalog.models==null||s.catalog.models.Length!=12||s.spaceColors==null||s.spaceColors.Length!=8)throw new FormatException("不是有效的实验室方案");catalog=s.catalog;model=Mathf.Clamp(s.model,0,11);seed=s.seed;pixels=Mathf.Clamp(s.pixels,12,5000);width=Mathf.Clamp(s.width,100,3000);height=Mathf.Clamp(s.height,100,3000);rotation=s.rotation;speed=s.speed;light=s.light;dither=s.dither;spaceColors=s.spaceColors;background=s.background;stars=s.stars;dust=s.dust;nebula=s.nebula;planets=s.planets;transparent=s.transparent;tile=s.tile;pixelArt=s.pixelArt;quiet=s.quiet;clearEdges=s.clearEdges;density=s.density;flow=s.flow;warp=s.warp;drift=s.drift;twinkle=s.twinkle;meteorCollision=s.meteorCollision;meteorPeriod=s.meteorPeriod>0?s.meteorPeriod:28;meteorScale=s.meteorScale>0?s.meteorScale:1;meteorCenter=s.meteorCenter;selectedColor=0;hex="";seedText=seed.ToString();Apply();MarkDirty();status="已载入方案。";}
  public void BeginExport(string kind){if(export!=null)return;try{export=new GeneratorExport(this,kind);status="正在导出…";}catch(Exception e){status=e.Message;}}
  public void CancelExport(){if(export==null)return;export.Cancel();export=null;status="已取消导出，未完成的文件保留为 .partial。";}
  public Texture2D Capture(){
   int w=spaceMode?width:Mathf.RoundToInt(pixels*catalog.models[model].relativeScale),h=spaceMode?height:w;
   if(w>SystemInfo.maxTextureSize||h>SystemInfo.maxTextureSize)throw new InvalidOperationException("当前设备不支持这么大的导出尺寸。");
   Apply();var cam=previewCamera;var oldTarget=cam.targetTexture;var oldPos=cam.transform.position;float oldSize=cam.orthographicSize,oldAspect=cam.aspect;var oldColor=cam.backgroundColor;var oldActive=RenderTexture.active;
   var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32);Texture2D image=null;
   try{cam.targetTexture=rt;cam.aspect=(float)w/h;cam.transform.position=new Vector3(0,0,-10);cam.orthographicSize=2.5f;cam.backgroundColor=spaceMode&&!transparent?background:Color.clear;cam.Render();RenderTexture.active=rt;image=new Texture2D(w,h,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();return image;}
   catch{Release(image);throw;}
   finally{cam.targetTexture=oldTarget;cam.transform.position=oldPos;cam.orthographicSize=oldSize;cam.aspect=oldAspect;cam.backgroundColor=oldColor;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);}
  }
  float Slider(string name,float value,float min,float max){GUILayout.Label(name+"  "+value.ToString("0.##"),GUILayout.Height(25));return GUILayout.HorizontalSlider(value,min,max,GUILayout.Height(19));}
  int Number(string label,int value,int min,int max){GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(145),GUILayout.Height(25));int n;string t=GUILayout.TextField(value.ToString(),GUILayout.Height(25));GUILayout.EndHorizontal();return int.TryParse(t,out n)?Mathf.Clamp(n,min,max):value;}
  void OnGUI(){if(standaloneAsset||!showPanel)return;if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei"},16);var oldFont=GUI.skin.font;GUI.skin.font=font;Matrix4x4 old=GUI.matrix;float scale=Mathf.Min(Screen.width/1200f,Screen.height/800f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
   GUILayout.BeginArea(new Rect(18,18,335,764),GUI.skin.box);GUILayout.Label(spaceMode?"像素太空 · 动态背景实验室":"像素星球 · 完整功能实验室",new GUIStyle(GUI.skin.label){fontSize=19},GUILayout.Height(34));
   tab=GUILayout.Toolbar(tab,new[]{"生成","颜色","图层","导出"},GUILayout.Height(32));scroll=GUILayout.BeginScrollView(scroll);
   GUI.enabled=!IsExporting;
   if(tab==0)DrawGenerate();if(tab==1)DrawColors();if(tab==2)DrawLayers();if(tab==3)DrawExport();GUI.enabled=true;
   GUILayout.EndScrollView();if(export!=null){GUILayout.Label("导出进度 "+export.Progress,GUILayout.Height(26));if(GUILayout.Button("取消导出",GUILayout.Height(28)))CancelExport();}
   GUILayout.Label(status,new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12},GUILayout.Height(52));GUILayout.EndArea();GUI.matrix=old;GUI.skin.font=oldFont;
   // Original interaction: drag in the preview to move the light.
   var e=Event.current;if(!spaceMode&&!IsExporting&&(e.type==EventType.MouseDrag||e.type==EventType.MouseDown)&&e.button==0&&e.mousePosition.x>365*scale){Vector3 screen=new Vector3(e.mousePosition.x,Screen.height-e.mousePosition.y,10);Vector3 world=previewCamera.ScreenToWorldPoint(screen);light=new Vector2(Mathf.Clamp01(world.x/5+.5f),Mathf.Clamp01(.5f-world.y/5));Apply();MarkDirty();}
  }
  void DrawGenerate(){
   if(!spaceMode){var names=Array.ConvertAll(catalog.models,x=>x.name);int next=GUILayout.SelectionGrid(model,names,2,GUILayout.Height(180));if(next!=model)SelectModel(next);pixels=Number("像素精度",pixels,12,5000);rotation=Slider("倾角",rotation,-180,180);light.x=Slider("光源左右",light.x,-.5f,1.5f);light.y=Slider("光源上下",light.y,-.5f,1.5f);dither=GUILayout.Toggle(dither,"像素抖动过渡",GUILayout.Height(26));}
   else{width=Number("背景宽度",width,100,3000);height=Number("背景高度",height,100,3000);density=Slider("星星密度",density,0,.5f);flow=Slider("星云流动速度",flow,0,3);warp=Slider("星云扭曲幅度",warp,0,.05f);drift=Slider("行星漂浮幅度",drift,0,1);twinkle=Slider("闪烁强度",twinkle,0,1);meteorCollision=GUILayout.Toggle(meteorCollision,"陨石对撞",GUILayout.Height(26));if(meteorCollision){meteorPeriod=Slider("对撞间隔（秒）",meteorPeriod,8,60);meteorScale=Slider("陨石效果大小",meteorScale,.2f,2);if(GUILayout.Button("立即预演陨石对撞",GUILayout.Height(30))){meteorOffset=-phase;paused=false;}}}
   speed=Slider("动画速度",speed,-3,3);GUILayout.BeginHorizontal();GUILayout.Label("种子",GUILayout.Width(45),GUILayout.Height(26));if(seedText=="")seedText=seed.ToString();seedText=GUILayout.TextField(seedText,GUILayout.Height(26));int n;if(int.TryParse(seedText,out n))seed=n;GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();if(GUILayout.Button("随机生成",GUILayout.Height(32)))Randomize();if(GUILayout.Button(paused?"继续动画":"暂停动画",GUILayout.Height(32)))paused=!paused;GUILayout.EndHorizontal();Apply();
  }
  void DrawColors(){
   if(spaceMode){GUILayout.BeginHorizontal();if(GUILayout.Button("蓝紫"))SpacePreset(0);if(GUILayout.Button("青绿"))SpacePreset(1);if(GUILayout.Button("暖红"))SpacePreset(2);GUILayout.EndHorizontal();}
   var colors=GetColors();selectedColor=Mathf.Clamp(selectedColor,0,colors.Count-1);
   GUILayout.Label("点击色块编辑颜色（支持透明度）",GUILayout.Height(28));
   for(int start=0;start<colors.Count;start+=6){GUILayout.BeginHorizontal();for(int i=start;i<Math.Min(start+6,colors.Count);i++){Rect swatch=GUILayoutUtility.GetRect(44,32,GUILayout.Width(44),GUILayout.Height(32));var tint=GUI.color;GUI.color=new Color(colors[i].r,colors[i].g,colors[i].b,1);GUI.DrawTexture(swatch,Texture2D.whiteTexture);GUI.color=tint;var style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter};style.normal.textColor=colors[i].grayscale>.5f?Color.black:Color.white;if(GUI.Button(swatch,(selectedColor==i?"● ":"")+(i+1),style)){selectedColor=i;hex="";}}GUILayout.EndHorizontal();}
   Color c=colors[selectedColor];GUILayout.Label("颜色 "+(selectedColor+1),GUILayout.Height(28));Color previous=c;
   c.r=Slider("红 R",c.r,0,1);c.g=Slider("绿 G",c.g,0,1);c.b=Slider("蓝 B",c.b,0,1);c.a=Slider("透明度 A",c.a,0,1);
   if(c!=previous){colors[selectedColor]=c;SetColors(colors);hex="";}
   if(hex=="")hex="#"+ColorUtility.ToHtmlStringRGBA(c);GUILayout.BeginHorizontal();hex=GUILayout.TextField(hex,GUILayout.Height(27));if(GUILayout.Button("应用",GUILayout.Width(55),GUILayout.Height(27))){Color parsed;if(ColorUtility.TryParseHtmlString(hex,out parsed)){colors[selectedColor]=parsed;SetColors(colors);}else status="请输入 #RRGGBB 或 #RRGGBBAA。";}GUILayout.EndHorizontal();
   if(spaceMode&&GUILayout.Button("将此颜色设为底色",GUILayout.Height(28)))background=colors[selectedColor];
   GUILayout.BeginHorizontal();if(GUILayout.Button("随机配色",GUILayout.Height(28)))RandomColors();if(GUILayout.Button("恢复原色",GUILayout.Height(28)))ResetColors();GUILayout.EndHorizontal();
   GUILayout.Label("整套颜色导入 / 导出",GUILayout.Height(27));paletteText=GUILayout.TextArea(paletteText,GUILayout.Height(82));GUILayout.BeginHorizontal();if(GUILayout.Button("复制当前颜色")){paletteText=ColorText();GUIUtility.systemCopyBuffer=paletteText;}if(GUILayout.Button("粘贴"))paletteText=GUIUtility.systemCopyBuffer;if(GUILayout.Button("应用整套"))ImportColors(paletteText);GUILayout.EndHorizontal();
  }
  void DrawLayers(){if(spaceMode){stars=GUILayout.Toggle(stars,"星星 / 星芒",GUILayout.Height(30));dust=GUILayout.Toggle(dust,"星尘",GUILayout.Height(30));nebula=GUILayout.Toggle(nebula,"星云",GUILayout.Height(30));planets=GUILayout.Toggle(planets,"远处行星",GUILayout.Height(30));transparent=GUILayout.Toggle(transparent,"透明背景（导出）",GUILayout.Height(30));tile=GUILayout.Toggle(tile,"平铺（含跨边缘行星）",GUILayout.Height(30));pixelArt=GUILayout.Toggle(pixelArt,"像素风格",GUILayout.Height(30));quiet=GUILayout.Toggle(quiet,"减弱背景亮度",GUILayout.Height(30));clearEdges=GUILayout.Toggle(clearEdges,"边缘留空",GUILayout.Height(30));GUILayout.Label("行星数量、大小和位置由种子决定，可能生成没有行星的背景。",new GUIStyle(GUI.skin.label){wordWrap=true},GUILayout.Height(60));}else{foreach(var l in catalog.models[model].layers)l.visible=GUILayout.Toggle(l.visible,LayerName(l.name),GUILayout.Height(32));}Apply();}
  public static string LayerName(string s){var map=new Dictionary<string,string>{{"Ground","地表"},{"Cloud2","第二层云"},{"Lakes","冰湖"},{"LavaRivers","熔岩河流"},{"Water","海洋"},{"Land","大陆"},{"Cloud","云层"},{"Clouds","云层"},{"Planet","星球"},{"Craters","陨石坑"},{"Ring","星环"},{"GasLayers","气态条带"},{"BlackHole","黑洞核心"},{"Disk","吸积盘"},{"Galaxy","星系"},{"Star","恒星表面"},{"Blobs","日珥"},{"StarFlares","耀斑"},{"Rivers","河流 / 熔岩"},{"LandRivers","地表与河流"},{"Asteroid","小行星"}};return map.ContainsKey(s)?map[s]:s;}
  void DrawExport(){
#if UNITY_EDITOR
   GUILayout.Label("直接用于 Unity 场景",GUILayout.Height(28));
   if(!spaceMode&&GUILayout.Button("导出主星球样式（游玩场景使用）",GUILayout.Height(36)))RequestStyleExport();
   if(GUILayout.Button("导出 Unity 预制体（直接拖入场景）",GUILayout.Height(36)))RequestUnityExport(false);
   if(GUILayout.Button("导出 Unity 资源包（用于其他项目）",GUILayout.Height(36)))RequestUnityExport(true);
   GUILayout.Space(12);
#endif
   if(GUILayout.Button("导出当前 PNG",GUILayout.Height(36)))BeginExport("png");frames=Number("动画帧数",frames,2,600);duration=Slider("动画时长（秒）",duration,.2f,30);columns=Number("图集列数",columns,1,100);margin=Number("图集像素间距",margin,0,32);
   if(GUILayout.Button("导出动画 GIF",GUILayout.Height(34)))BeginExport("gif");if(GUILayout.Button("导出动画图集 PNG",GUILayout.Height(34)))BeginExport("sheet");if(GUILayout.Button("保存全部设置与颜色",GUILayout.Height(34)))SaveSettings();
#if UNITY_EDITOR
   if(GUILayout.Button("载入已保存方案",GUILayout.Height(34))){string p=UnityEditor.EditorUtility.OpenFilePanel("载入生成器方案",ExportDirectory,"json");if(p!=""){try{LoadSettings(p);}catch(Exception e){status=e.Message;}}}
   if(GUILayout.Button("打开导出文件夹",GUILayout.Height(32))){Directory.CreateDirectory(ExportDirectory);UnityEditor.EditorUtility.RevealInFinder(ExportDirectory);}
#endif
   GUILayout.Label("星球按原版完整周期采样；太空按当前动画采样。导出后恢复预览，GIF 为 256 色。",new GUIStyle(GUI.skin.label){wordWrap=true},GUILayout.Height(65));
  }
  public void RequestUnityExport(bool package){try{if(ExportUnityRequested==null){status="Unity 导出工具尚未初始化，请等待脚本编译完成。";return;}ExportUnityRequested(this,package);}catch(Exception e){status="Unity 导出失败："+e.Message;Debug.LogException(e);}}
  public void RequestStyleExport(){try{if(ExportStyleRequested==null){status="请等待 Unity 编辑器完成编译。";return;}ExportStyleRequested(this);}catch(Exception e){status=e.Message;}}
 }
}
