using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
[ExecuteAlways]
public sealed partial class CoverMenu:MonoBehaviour {
 public Texture2D background,solarMask,astronaut,title,startLabel,settingsLabel,exitLabel;
 public Texture2D startHover,settingsHover,exitHover,triangle,menuLine;
 readonly float[] hover=new float[3],hoverStart=new float[3];
 int previousSelection=-1;float hoverAge;
 Vector3 lastMouse;
 public Font font;
 public Shader backgroundShader;
 public bool pixelArtwork;
 Material backdrop;RenderTexture backgroundFrame;
 int selected;bool settings,exitPrompt,loading;
 float fade;Vector2 parallax;
 Vector2 astronautOffset,astronautVelocity;float astronautTilt;
 float AnimationTime {
  get {
#if UNITY_EDITOR
   if(!Application.isPlaying)return (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
   return Time.unscaledTime;
  }
 }
#if UNITY_EDITOR
 double nextPreviewFrame;
 void TickEditorPreview(){
  if(Application.isPlaying||!this||!isActiveAndEnabled)return;
  double now=UnityEditor.EditorApplication.timeSinceStartup;
  if(now<nextPreviewFrame)return;
  nextPreviewFrame=now+1.0/30.0;
  UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
  UnityEditor.SceneView.RepaintAll();
 }
#endif
 void OnEnable(){
  EnsurePreview();if(!Application.isPlaying)hover[0]=1;
#if UNITY_EDITOR
  UnityEditor.EditorApplication.update-=TickEditorPreview;
  UnityEditor.EditorApplication.update+=TickEditorPreview;
#endif
 }
 void EnsurePreview(){
  if(!background||!backgroundShader)return;
  if(!backdrop){backdrop=new Material(backgroundShader){hideFlags=HideFlags.HideAndDontSave};backdrop.SetTexture("_MapTex",solarMask);}
  if(!backgroundFrame)backgroundFrame=new RenderTexture(1376,768,0){hideFlags=HideFlags.HideAndDontSave};
  RenderBackground();
 }
 void ReleasePreview(){
  if(signalFrame){signalFrame.Release();if(Application.isPlaying)Destroy(signalFrame);else DestroyImmediate(signalFrame);signalFrame=null;}
  if(titleSignal){if(Application.isPlaying)Destroy(titleSignal);else DestroyImmediate(titleSignal);titleSignal=null;}
  if(backgroundFrame){backgroundFrame.Release();if(Application.isPlaying)Destroy(backgroundFrame);else DestroyImmediate(backgroundFrame);backgroundFrame=null;}
  if(backdrop){if(Application.isPlaying)Destroy(backdrop);else DestroyImmediate(backdrop);backdrop=null;}
 }
 void OnDisable(){
#if UNITY_EDITOR
  UnityEditor.EditorApplication.update-=TickEditorPreview;
#endif
  ReleasePreview();
 }
 void OnDestroy(){ReleasePreview();}
 void RenderBackground(){
  if(pixelArtwork)return;
  if(!backdrop||!backgroundFrame)return;
  var previous=RenderTexture.active;
  backdrop.SetFloat("_CoverTime",AnimationTime);
  backdrop.SetFloat("_PixelArtwork",pixelArtwork?1:0);
  backdrop.SetVector("_Parallax",Application.isPlaying?new Vector4(-parallax.x*.004f,-parallax.y*.004f,0,0):Vector4.zero);
  Graphics.Blit(background,backgroundFrame,backdrop);RenderTexture.active=previous;
 }
 void DrawCoverBackground(){
  var rect=new Rect(0,0,1376,768);
  // Draw the supplied artwork directly: an intermediate linear render texture
  // was making its sRGB colours too dark in the IMGUI presentation pass.
  if(pixelArtwork&&background){
   GUI.DrawTextureWithTexCoords(rect,background,new Rect(-parallax.x*.004f,parallax.y*.004f,1,1));
  }else GUI.DrawTexture(rect,backgroundFrame?backgroundFrame:(Texture)background);
 }
 void LateUpdate(){EnsurePreview();}
 void Update(){
  if(!Application.isPlaying)return;
  if(previousSelection!=selected){previousSelection=selected;hoverAge=0;System.Array.Copy(hover,hoverStart,3);}
  hoverAge+=Time.unscaledDeltaTime;
  float ease=MenuEase(Mathf.Clamp01(hoverAge/.25f));
  for(int i=0;i<3;i++)hover[i]=Mathf.Lerp(hoverStart[i],i==selected?1:0,ease);
  if(loading)return;
  var mouse=Input.mousePosition;
  if(!settings&&!exitPrompt&&!savePage&&(mouse-lastMouse).sqrMagnitude>.01f){
   float k=Mathf.Max(Screen.width/1376f,Screen.height/768f);
   float ox=Screen.width/1376f<Screen.height/768f?0:(Screen.width-1376*k)*.5f;
   var point=new Vector2((mouse.x-ox)/k,(Screen.height-mouse.y-(Screen.height-768*k)*.5f)/k);
   for(int i=0;i<3;i++)if(new Rect(30,330+i*106,350,92).Contains(point))selected=i;
  }
  float screenScale=Mathf.Max(Screen.width/1376f,Screen.height/768f);
  float screenLeft=Screen.width/1376f<Screen.height/768f?0:(Screen.width-1376*screenScale)*.5f;
  var coverMouse=new Vector2((mouse.x-screenLeft)/screenScale,(Screen.height-mouse.y-(Screen.height-768*screenScale)*.5f)/screenScale);
  UpdateAstronaut(coverMouse,!settings&&!exitPrompt&&!savePage&&mouse.x>=0&&mouse.x<=Screen.width&&mouse.y>=0&&mouse.y<=Screen.height);
  lastMouse=mouse;
  parallax=Vector2.Lerp(parallax,new Vector2(Mathf.Clamp01(Input.mousePosition.x/Screen.width)-.5f,.5f-Mathf.Clamp01(Input.mousePosition.y/Screen.height))*2,1-Mathf.Exp(-Time.unscaledDeltaTime*2.4f));
  if(savePage){UpdateSavePage();return;}
  if(settings||exitPrompt){if(Input.GetKeyDown(KeyCode.Escape)){settings=false;exitPrompt=false;}return;}
  if(Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.S))selected=(selected+1)%3;
  if(Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.W))selected=(selected+2)%3;
  if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))Activate(selected);
 }
 // Match the source page's cubic-bezier(.2,.8,.2,1).
 static float MenuEase(float x){
  float lo=0,hi=1,t=0;
  for(int i=0;i<12;i++){t=(lo+hi)*.5f;float u=1-t;float bx=3*u*u*t*.2f+3*u*t*t*.2f+t*t*t;if(bx<x)lo=t;else hi=t;}
  float v=1-t;return 3*v*v*t*.8f+3*v*t*t+t*t*t;
 }
 void Activate(int item){if(!Application.isPlaying||loading)return;if(item==0)OpenSavePage();else if(item==1)settings=true;else exitPrompt=true;}
 IEnumerator Begin(string scene){
  loading=true;
  for(float t=0;t<.55f;t+=Time.unscaledDeltaTime){fade=t/.55f;yield return null;}
  fade=1;Time.timeScale=1;
  yield return SceneManager.LoadSceneAsync(scene);
 }
 const string PixelGlyphs="两极之间开始游戏设置退出确定要吗？取消声音返回0123456789%−+› ";
 Texture2D pixelGlyphs,saveGlyphs;
 const string SaveGlyphs="选择存档新继续删除是否确认读取失败保存游戏返回取消确定吗？×0123456789› ";
 static int GlyphWidth(char c){return c<128||c=='−'||c=='›'?6:12;}
 void PixelText(Rect rect,string value,float maxScale,bool centered=true,bool shadow=true){
  if(!pixelGlyphs){pixelGlyphs=Resources.Load<Texture2D>("ElementUI/CoverPixelGlyphs");if(pixelGlyphs){pixelGlyphs.filterMode=FilterMode.Point;pixelGlyphs.wrapMode=TextureWrapMode.Clamp;}}
  if(!pixelGlyphs)return;
  int width=2;foreach(char c in value)width+=GlyphWidth(c);
  float k=Mathf.Max(1,Mathf.Floor(Mathf.Min(maxScale,Mathf.Min(rect.width/width,rect.height/15))));
  float x=Mathf.Round(centered?rect.center.x-width*k*.5f:rect.x),y=Mathf.Round(rect.center.y-15*k*.5f);
  var color=GUI.color;
  for(int pass=shadow?0:1;pass<2;pass++){
   GUI.color=pass==0?new Color(0,0,0,color.a*.7f):color;
   float at=x+(pass==0?k:0),top=y+(pass==0?k:0);
   foreach(char c in value){
    int index=PixelGlyphs.IndexOf(c),length=PixelGlyphs.Length;var atlas=pixelGlyphs;
    if(index<0){if(!saveGlyphs)saveGlyphs=Resources.Load<Texture2D>("ElementUI/SaveSlotGlyphs");atlas=saveGlyphs;index=SaveGlyphs.IndexOf(c);length=SaveGlyphs.Length;}
    if(index>=0&&atlas)GUI.DrawTextureWithTexCoords(new Rect(at,top,16*k,15*k),atlas,new Rect(index/(float)length,0,1f/length,1));at+=GlyphWidth(c)*k;
   }
  }
  GUI.color=color;
 }
 void ExitText(Rect rect,string value,int size){PixelText(rect,value,size/12f);}
 Texture2D signalTitle;Material titleSignal;RenderTexture signalFrame;
 void DrawPixelTitle(Rect rect){
  if(!signalTitle)signalTitle=Resources.Load<Texture2D>("ElementUI/CoverPixelTitle");
  if(!titleSignal){var shader=Resources.Load<Shader>("Shaders/CoverTitleSignal");if(shader)titleSignal=new Material(shader){hideFlags=HideFlags.HideAndDontSave};}
  if(!signalTitle||!titleSignal){PixelText(rect,"两极之间",20,false,false);return;}
  signalTitle.filterMode=FilterMode.Point;
  float k=Mathf.Max(1,Mathf.Floor(Mathf.Min(20,Mathf.Min(rect.width/50,rect.height/15))));
  int width=Mathf.RoundToInt(54*k),height=Mathf.RoundToInt(15*k);
  if(!signalFrame||signalFrame.width!=width||signalFrame.height!=height){
   if(signalFrame){signalFrame.Release();if(Application.isPlaying)Destroy(signalFrame);else DestroyImmediate(signalFrame);}
   signalFrame=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};signalFrame.Create();
  }
  if(Event.current.type!=EventType.Repaint)return;
  var previous=RenderTexture.active;titleSignal.SetFloat("_SignalTime",AnimationTime);
  Graphics.Blit(signalTitle,signalFrame,titleSignal);RenderTexture.active=previous;
  GUI.DrawTexture(new Rect(Mathf.Round(rect.x-2*k),Mathf.Round(rect.center.y-height*.5f),width,height),signalFrame);
 }
 Vector2 AstronautPosition(float t){return new Vector2(620+Mathf.Sin(t*.31f)*26+Mathf.Sin(t*.73f+1.3f)*11+parallax.x*16,345+Mathf.Sin(t*.47f)*22+Mathf.Sin(t*1.07f+.4f)*8+parallax.y*12);}
 void UpdateAstronaut(Vector2 mouse,bool interactive){
  var center=AstronautPosition(AnimationTime)+new Vector2(142.5f,97);
  var away=center-mouse;float distance=away.magnitude;
  float proximity=interactive?1-Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/280)):0;
  var target=(distance>1?away/distance:Vector2.up)*proximity*48;
  float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
  astronautOffset=Vector2.SmoothDamp(astronautOffset,target,ref astronautVelocity,.3f,250,dt);
  astronautTilt=Mathf.Lerp(astronautTilt,Mathf.Clamp(-away.x/180,-1,1)*proximity*16,1-Mathf.Exp(-dt*5));
 }
 void DrawAstronaut(float t){
  var before=GUI.matrix;var p=AstronautPosition(t)+astronautOffset;
  var pivot=p+new Vector2(142.5f,97);
  GUIUtility.RotateAroundPivot(Mathf.Sin(t*.23f)*9+Mathf.Sin(t*.61f+2)*3.5f+astronautTilt,pivot);
  float height=pixelArtwork?220:285f*astronaut.height/astronaut.width;
  float width=pixelArtwork?height*astronaut.width/astronaut.height:285;
  var rect=new Rect(pivot.x-width*.5f,pivot.y-height*.5f,width,height);
  GUI.DrawTexture(rect,astronaut);
  GUI.matrix=before;
 }
 bool ExitChoice(Rect rect,string value,int size=26){
  bool hot=rect.Contains(Event.current.mousePosition);
  ExitText(rect,(hot?"›  ":"")+value,size);
  return GUI.Button(rect,GUIContent.none,GUIStyle.none);
 }
 void OnGUI(){
  if(!background||!astronaut||!title)return;
  EnsurePreview();
  var matrix=GUI.matrix;var color=GUI.color;
  float scale=Mathf.Max(Screen.width/1376f,Screen.height/768f);
  GUI.matrix=Matrix4x4.TRS(new Vector3(Screen.width/1376f<Screen.height/768f?0:(Screen.width-1376*scale)*.5f,(Screen.height-768*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
  float t=AnimationTime;
  DrawCoverBackground();
  DrawAstronaut(t);
  DrawPixelTitle(new Rect(24,60,title.width*.5f,title.height*.5f));
  var labels=new[]{startLabel,settingsLabel,exitLabel};
  var highlights=new[]{startHover,settingsHover,exitHover};
  if(!(savePage||(!Application.isPlaying&&previewSavePage)))for(int i=0;i<3;i++){
   float h=hover[i];
   var rect=new Rect(44+12*h,330+i*106,320,92);
   if(!settings&&!exitPrompt&&!savePage&&!loading&&GUI.Button(rect,GUIContent.none,GUIStyle.none)){selected=i;Activate(i);}
   GUI.color=Color.Lerp(new Color(.80f,.85f,.88f),Color.white,h);PixelText(new Rect(rect.x,rect.y+6,320,75),new[]{"开始游戏","设置","退出"}[i],5,false);
   GUI.color=new Color(1,1,1,Mathf.Lerp(.75f,1,h));GUI.DrawTexture(new Rect(rect.x+14,rect.y+80.5f,230+70*h,1.5f),menuLine);
   GUI.color=new Color(1,1,1,h);GUI.DrawTexture(new Rect(rect.x-14-8*(1-h),rect.y+32,10,14),triangle);GUI.color=Color.white;
  }
  if(savePage||(!Application.isPlaying&&previewSavePage))DrawSavePage();
  if(settings||exitPrompt){
   if(settings){
    ExitText(new Rect(428,260,520,75),"设置",48);
    ExitText(new Rect(508,355,360,75),"声音  "+Mathf.RoundToInt(AudioListener.volume*100)+"%",48);
    if(ExitChoice(new Rect(388,355,120,75),"−",48))AudioListener.volume=Mathf.Clamp01(AudioListener.volume-.1f);
    if(ExitChoice(new Rect(868,355,120,75),"+",48))AudioListener.volume=Mathf.Clamp01(AudioListener.volume+.1f);
    if(ExitChoice(new Rect(568,465,240,75),"返回",48))settings=false;
   }
   else{
    ExitText(new Rect(428,310,520,60),"确定要退出游戏吗？",32);
    if(ExitChoice(new Rect(490,410,160,48),"取消"))exitPrompt=false;
    if(ExitChoice(new Rect(720,410,165,48),"确定")){
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying=false;
#else
    Application.Quit();
#endif
   }}
  }
  GUI.matrix=matrix;if(fade>0){GUI.color=new Color(0,0,0,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);}GUI.color=color;
 }
#if UNITY_EDITOR
 void OnDrawGizmos(){
  if(Application.isPlaying||!background||!astronaut||!title)return;
  var view=UnityEditor.SceneView.currentDrawingSceneView;if(!view)return;
  EnsurePreview();
  UnityEditor.Handles.BeginGUI();
  var old=GUI.matrix;var color=GUI.color;
  float width=view.position.width,height=view.position.height-24;
  float scale=Mathf.Min(width/1376f,height/768f);
  GUI.matrix=Matrix4x4.TRS(new Vector3((width-1376*scale)*.5f,(height-768*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
  GUI.color=Color.white;
  DrawCoverBackground();
  float t=AnimationTime;
  DrawAstronaut(t);
  DrawPixelTitle(new Rect(24,60,title.width*.5f,title.height*.5f));
  var labels=new[]{startHover?startHover:startLabel,settingsLabel,exitLabel};
  if(previewSavePage)DrawSavePage();
  else for(int i=0;i<3;i++)if(labels[i]){
   float x=44+(i==0?12:0),y=330+i*106;
   GUI.color=i==0?Color.white:new Color(.80f,.85f,.88f);PixelText(new Rect(x,y+6,320,75),new[]{"开始游戏","设置","退出"}[i],5,false);GUI.color=Color.white;
   if(menuLine)GUI.DrawTexture(new Rect(x+14,y+80.5f,i==0?300:230,1.5f),menuLine);
   if(i==0&&triangle)GUI.DrawTexture(new Rect(x-14,y+32,10,14),triangle);
  }
  GUI.matrix=old;GUI.color=color;UnityEditor.Handles.EndGUI();
 }
#endif

}
}
