using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles {
// Explicit, non-saving cover QA. Never selects or deletes a real save slot.
[InitializeOnLoad]
public static class CoverMobileRegression {
 const string Key="CoverMobileRegression.Active",Folder="output/mobile-cover";
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static int stage,checks;static double next,deadline;
 static EditorWindow view;
 static CoverMobileRegression(){EditorApplication.update+=Tick;}
 public static void Run(){
  Directory.CreateDirectory(Folder);MathChecks();
  EditorSceneManager.OpenScene("Assets/BetweenPoles/Scenes/Cover.unity");
  SetSize(1600,720);SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
 }
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static object Call(CoverMenu cover,string name,params object[] args)=>typeof(CoverMenu).GetMethod(name,Private).Invoke(cover,args);
 static void Flag(CoverMenu cover,string name,bool value)=>typeof(CoverMenu).GetField(name,Private).SetValue(cover,value);
 static void SetSize(int width,int height){
  var assembly=typeof(EditorWindow).Assembly;var type=assembly.GetType("UnityEditor.GameView");view=EditorWindow.GetWindow(type);
  var sizes=assembly.GetType("UnityEditor.GameViewSizes").GetProperty("instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.FlattenHierarchy).GetValue(null);
  var getGroup=sizes.GetType().GetMethod("GetGroup");var group=getGroup.Invoke(sizes,new[]{Enum.Parse(getGroup.GetParameters()[0].ParameterType,"Android")});
  var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
   new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),width,height,"Cover QA "+width+"x"+height},null);
  group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
  int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
  type.GetProperty("selectedSizeIndex",Private|BindingFlags.Public).SetValue(view,count-1);view.Show();view.Focus();
 }
 static void Tick(){
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  try{
   MobileUI.PreviewInEditor=true;Application.runInBackground=true;
   if(deadline==0){deadline=EditorApplication.timeSinceStartup+100;next=EditorApplication.timeSinceStartup+5;}
   if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Cover QA timed out");
   if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
   var cover=UnityEngine.Object.FindObjectOfType<CoverMenu>();Check(cover,"Cover missing");
   switch(stage++){
    case 0:Validate(cover);Shot("wide-cover");break;
    case 1:Flag(cover,"settings",true);break;
    case 2:Validate(cover);Shot("wide-settings");break;
    case 3:Flag(cover,"settings",false);Call(cover,"OpenSavePage");break;
    case 4:Validate(cover);Shot("wide-saves");break;
    case 5:Flag(cover,"savePage",false);SetSize(1280,720);break;
    case 6:Validate(cover);Shot("16x9-cover");break;
    case 7:SetSize(2400,1080);break;
    case 8:Validate(cover);Shot("1080p-cover");break;
    case 9:
     ((RenderTexture)typeof(CoverMenu).GetField("mobileCover",Private).GetValue(cover)).Release();
     ((RenderTexture)typeof(CoverMenu).GetField("mobileFiltered",Private).GetValue(cover)).Release();break;
    case 10:Validate(cover);Shot("recreated-textures");cover.crtEnabled=false;break;
    case 11:Validate(cover,false);Shot("crt-disabled");cover.crtEnabled=true;break;
    default:
     File.WriteAllText(Folder+"/regression.txt","PASS: "+checks+" checks; mobile shader support, owned render targets, visible pixels, 16:9 and 20:9 aspect fill, 1080p bounded textures, settings/save page rendering, lost render texture recreation, CRT input inverse mapping. Editor GPU validation; Android device confirmation still required.");
     SessionState.SetBool(Key,false);EditorApplication.Exit(0);break;
   }
  }catch(Exception e){SessionState.SetBool(Key,false);File.WriteAllText(Folder+"/regression.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
 static void Shot(string name){ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");}
 static void Validate(CoverMenu cover,bool crt=true){
  Check((bool)typeof(CoverMenu).GetProperty("MobileCRTReady",Private).GetValue(cover),"Mobile compositor not available");
  Check((bool)typeof(CoverMenu).GetProperty("UseCRT",Private).GetValue(cover)==crt,"CRT enable state mismatch");
  var raw=(RenderTexture)typeof(CoverMenu).GetField("mobileCover",Private).GetValue(cover);
  var filtered=(RenderTexture)typeof(CoverMenu).GetField("mobileFiltered",Private).GetValue(cover);
  Check(raw&&filtered&&raw!=filtered&&raw.IsCreated()&&filtered.IsCreated(),"Render textures lost or aliased");
  Check(raw.width<=1600&&raw.height<=1600&&Mathf.Abs(raw.width/(float)raw.height-Screen.width/(float)Screen.height)<.005f,"Render target size/aspect mismatch");
  var previous=RenderTexture.active;var image=new Texture2D(raw.width,raw.height,TextureFormat.RGB24,false);
  try{
   RenderTexture.active=crt?filtered:raw;image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();
   var pixels=image.GetPixels32();int lit=0;foreach(var pixel in pixels)if(pixel.r+pixel.g+pixel.b>60)lit++;
   Check(lit>pixels.Length*.15f,"Rendered cover is black");
   var edge=image.GetPixel(Mathf.RoundToInt(image.width*.04f),Mathf.RoundToInt(image.height*.3f));
   Check(edge.r+edge.g+edge.b>.06f,"Left side has the previous letterbox bar");
  }finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
  var layout=(Rect)Call(cover,"CoverRect");float scale=layout.width/1376f;
  foreach(var local in new[]{new Vector2(200,380),new Vector2(200,480),new Vector2(200,580),new Vector2(448,390),new Vector2(928,390),new Vector2(350,627),new Vector2(535,375)}){
   var wanted=layout.position+local*scale;
   var target=new Vector2(wanted.x/Screen.width,wanted.y/Screen.height);
   var distorted=crt?Inverse(target,.06f+.22f*cover.crtStrength):target;
   var screen=new Vector2(distorted.x*Screen.width,distorted.y*Screen.height);
   var mapped=(Vector2)Call(cover,"CRTContentPoint",(screen-layout.position)/scale);
   Check(Vector2.Distance(mapped,local)<.02f,"Displayed button and touch coordinate do not match");
  }
 }
 static Vector2 Inverse(Vector2 target,float curvature){
  var d=target-Vector2.one*.5f;float lo=0,hi=2;
  for(int i=0;i<32;i++){float k=(lo+hi)*.5f;var p=d*k;float projected=k*(1+curvature*p.sqrMagnitude)*(1-curvature*.25f);if(projected<1)lo=k;else hi=k;}
  return Vector2.one*.5f+d*((lo+hi)*.5f);
 }
 static void MathChecks(){
  checks=0;var method=typeof(CoverMenu).GetMethod("MobileBackgroundUV",BindingFlags.Static|BindingFlags.NonPublic);
  foreach(var screen in new[]{new Vector2(1600,720),new Vector2(1280,720),new Vector2(2400,1080),new Vector2(2048,1536)}){
   var source=new Vector2(1376,768);var uv=(Rect)method.Invoke(null,new object[]{source,screen});
   Check(uv.x>=0&&uv.y>=0&&uv.xMax<=1.001f&&uv.yMax<=1.001f,"Background sampling outside artwork");
   Check(Mathf.Abs(source.x*uv.width/(source.y*uv.height)-screen.x/screen.y)<.001f,"Background stretched instead of proportionally cropped");
  }
  foreach(var path in new[]{"Shaders/CoverCompose","Shaders/CoverCRTMobile"}){
   var shader=Resources.Load<Shader>(path);Check(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Missing/unsupported mobile shader "+path);
  }
 }
}
}
