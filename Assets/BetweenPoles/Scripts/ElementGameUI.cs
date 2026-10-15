using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
// Canvas exports from 四元素-像素UI.html are the source of truth, including rasterized text.
[ExecuteAlways,DefaultExecutionOrder(-300),DisallowMultipleComponent]
public sealed class ElementGameUI:MonoBehaviour {
    public enum EditorPanel { HUD, Pause }
    [Header("编辑状态预览")]
    public bool showInSceneView=true;
    public EditorPanel editorPanel;
    static readonly Dictionary<string,Texture2D> assets=new Dictionary<string,Texture2D>();
    static readonly string[] themes={"ice","grass","earth","fire"};
    static float audibleVolume=1;
    public static bool Paused {get;private set;}
    GridPlayground board;ChapterMapReturn map;float previousTimeScale=1;bool ownsPause;
    string toast,hovered;float toastUntil;AudioSource audioSource;
    readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    public const float UIScale=1.5f;
    public static int BaseK {get{return Mathf.Clamp(Mathf.FloorToInt(Screen.width/460f+.5f),2,4);}}
    public static Rect SafeRect {get{var safe=Application.isMobilePlatform?Screen.safeArea:new Rect(0,0,Screen.width,Screen.height);return new Rect(safe.x,Screen.height-safe.yMax,safe.width,safe.height);}}
    public static float K {get{return Application.isMobilePlatform?Mathf.Clamp(Mathf.Min(SafeRect.height/150f,SafeRect.width/160f),1.5f,8f):BaseK*UIScale;}}
    public static float TextScale {get{return Application.isMobilePlatform?K*.75f:Mathf.Max(1,BaseK-1)*UIScale;}}
    public static string Theme {
        get{string name=SceneManager.GetActiveScene().name;for(int i=0;i<4;i++)if(name.Contains("Chapter0"+(i+1)))return themes[i];return themes[Mathf.Clamp(ChapterAtlas.ReturnChapter,0,3)];}
    }
    public static Texture2D Asset(string name){
        string key=Theme+"-"+name;Texture2D texture;
        if(!assets.TryGetValue(key,out texture)||!texture){texture=Resources.Load<Texture2D>("ElementUI/"+key);if(texture){texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;}assets[key]=texture;}
        return texture;
    }
    public static void Draw(Rect rect,string name){var texture=Asset(name);if(texture)GUI.DrawTexture(rect,texture);}
    public static Texture2D TextTexture(string text,bool light=false){
        string key=System.BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(text)).Replace("-","").ToLowerInvariant();
        return Asset("text-"+key+(light?"-light":""));
    }
    public static Color Ink {get{Color color;ColorUtility.TryParseHtmlString(Theme=="ice"?"#45697d":Theme=="grass"?"#4b6a3a":Theme=="earth"?"#7a5538":"#2f2331",out color);return color;}}
    public static void Panel(Rect rect){
        var texture=Asset("panel");if(!texture)return;
        float k=TextScale,left=2*k,right=2*k,top=2*k,bottom=5*k;
        float[] xs={rect.x,rect.x+left,rect.xMax-right,rect.xMax},ys={rect.y,rect.y+top,rect.yMax-bottom,rect.yMax};
        float[] us={0,2f/texture.width,1-2f/texture.width,1},vs={1,1-2f/texture.height,5f/texture.height,0};
        for(int y=0;y<3;y++)for(int x=0;x<3;x++)GUI.DrawTextureWithTexCoords(new Rect(xs[x],ys[y],xs[x+1]-xs[x],ys[y+1]-ys[y]),texture,new Rect(us[x],vs[y+1],us[x+1]-us[x],vs[y]-vs[y+1]));
    }
    public static bool NavigationButton(string asset,bool right,System.Action action){
        var texture=Asset(asset);if(!texture)return false;float k=K,t=TextScale;
        var safe=SafeRect;
        var rect=new Rect(right?safe.xMax-6*k-texture.width*t:safe.x+6*k,safe.y+6*k,texture.width*t,texture.height*t);
        if(rect.Contains(Event.current.mousePosition)&&Input.GetMouseButton(0))rect.y+=t;
        GUI.DrawTexture(rect,texture);if(GUI.Button(rect,GUIContent.none,GUIStyle.none)){action();return true;}return false;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState(){Paused=false;assets.Clear();}
    void Awake(){Initialize();}
    void OnEnable(){Initialize();}
    void Initialize(){board=GetComponent<GridPlayground>();map=GetComponent<ChapterMapReturn>();if(!Application.isPlaying||audioSource)return;audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.ignoreListenerPause=true;}
    bool HUDReady {get{return gameObject.scene==SceneManager.GetActiveScene()&&board&&board.enabled&&!board.OpeningCinematic&&!ChapterRecoilTravel.Active&&!BlackHoleTravel.Selecting&&!BlackHoleTravel.InTransit;}}
    bool Available {get{return HUDReady&&!board.Busy;}}
    void SetPaused(bool value){if(value==Paused)return;if(value){previousTimeScale=Time.timeScale;Time.timeScale=0;}else Time.timeScale=previousTimeScale;ownsPause=value;Paused=value;}
    void OnDisable(){if(ownsPause)SetPaused(false);}
    void OnDestroy(){foreach(var clip in clips.Values)if(clip)Destroy(clip);}
    void Update(){
        if(!Application.isPlaying)return;
        if(gameObject.scene!=SceneManager.GetActiveScene())return;
        if(!map)map=GetComponent<ChapterMapReturn>();
        if(Paused){if(Input.GetKeyDown(KeyCode.Tab))SetPaused(false);else if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.P))SetPaused(false);return;}
        if(!Available||map&&map.MapSessionActive)return;
        if(Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.Keypad3))ChapterRecoilExit.JumpToFinalIsland(board);
        if(Input.GetKeyDown(KeyCode.M))Activate("sound");
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.P))Activate("pause");
        // Undo/reset key handling remains in GridPlayground; avoid executing either action twice.
        if(Input.GetKeyDown(KeyCode.Z))Notify("undo");
        if(Input.GetKeyDown(KeyCode.R))Notify("reset");
    }
    void Notify(string action){
        Blip(action);
        if(action=="undo"||action=="reset"){toastUntil=0;return;}
        toast=action;toastUntil=Time.unscaledTime+1.6f;
    }
    void Activate(string action){
        if(!Application.isPlaying)return;
        if(action=="map"){if(Paused)SetPaused(false);map.OpenLocalOverview();Blip("map");return;}
        if(action=="sound"){if(AudioListener.volume>0){audibleVolume=AudioListener.volume;AudioListener.volume=0;}else AudioListener.volume=audibleVolume;Notify(AudioListener.volume>0?"sound":"mute");return;}
        if(action=="pause"){SetPaused(!Paused);Blip("click");return;}
        if(!Available||Paused)return;
        if(action=="undo"){board.UndoStep();Notify(action);}
        if(action=="reset"){board.ResetCurrentIsland();Notify(action);}
    }
    void Blip(string kind){
        if(!Application.isPlaying||!audioSource)return;
        if(AudioListener.volume<=0)return;AudioClip clip;
        if(!clips.TryGetValue(kind,out clip)){
            float[] notes=kind=="hover"?new[]{1320f}:kind=="sound"?new[]{660f,990f}:kind=="map"?new[]{392f,523f,659f}:new[]{880f,1320f};
            float duration=kind=="hover"?.03f:notes.Length*.05f;var samples=new float[Mathf.CeilToInt((duration+.01f)*22050)];
            for(int i=0;i<samples.Length;i++){float time=i/22050f;int note=Mathf.Min(notes.Length-1,(int)(time/.045f));samples[i]=time>=duration?0:Mathf.Sign(Mathf.Sin(time*notes[note]*Mathf.PI*2))*(kind=="hover"?.025f:.05f);}
            clip=AudioClip.Create("Pixel UI "+kind,samples.Length,1,22050,false);clip.SetData(samples,0);clips[kind]=clip;
        }
        audioSource.PlayOneShot(clip);
    }
    void Icon(string action,float x,float y,bool enabled=true,bool? appearanceEnabled=null){
        float k=K;var rect=new Rect(x,y,19*k,21*k);bool hot=rect.Contains(Event.current.mousePosition);
        string icon=action=="sound"&&AudioListener.volume<=0?"mute":action;
        // A single walking step temporarily blocks actions, but must not flash the HUD.
        bool visuallyEnabled=appearanceEnabled??enabled;
        string state=!visuallyEnabled?"off":hot&&Input.GetMouseButton(0)?"press":hot?"hover":"normal";
        Draw(rect,icon+"-"+state);
        if(action=="undo"){int frame=(int)(Time.unscaledTime/.18f)%2;Draw(new Rect(x-k,y-3*k,5*k,6*k),"deco-"+frame);}
        bool old=GUI.enabled;GUI.enabled=enabled;
        if(GUI.Button(rect,GUIContent.none,GUIStyle.none))Activate(action);GUI.enabled=old;
        if(hot&&visuallyEnabled){
            if(hovered!=action&&Event.current.type==EventType.Repaint){hovered=action;Blip("hover");}
            var tip=Asset("tip-"+icon+"-k"+BaseK);float t=TextScale;if(tip){float left=action=="map"?x:action=="pause"?rect.xMax-tip.width*t:rect.center.x-tip.width*t/2f;GUI.DrawTexture(new Rect(left,rect.yMax+k,tip.width*t,tip.height*t),tip);}
        }
    }
    void OnGUI(){
        if(!Application.isPlaying){if(gameObject.scene==SceneManager.GetActiveScene())DrawEditorPreview(Screen.width,Screen.height);return;}
        if(gameObject.scene!=SceneManager.GetActiveScene()||!board||ChapterAtlas.IsInspectionScene(gameObject.scene)||ChapterRecoilTravel.Active||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit||board.OpeningCinematic)return;
        if(map&&map.MapSessionActive)return;
        GUI.depth=-20;float k=K;
        if(Paused){
            var pauseSafe=SafeRect;
            GUI.BeginGroup(pauseSafe);
            DrawPausePanel(pauseSafe.width,pauseSafe.height,k,true);
            GUI.EndGroup();
            return;
        }
        var safe=SafeRect;float top=safe.y+6*k;
        Icon("map",safe.x+6*k,top,Available||Paused,HUDReady||Paused);
        float x=safe.xMax-6*k-85*k;
        Icon("undo",x,top,Available&&!Paused,HUDReady&&!Paused);Icon("reset",x+21*k,top,Available&&!Paused,HUDReady&&!Paused);
        Draw(new Rect(x+39*k,top+6*k,7*k,6*k),"bridge");
        Icon("sound",x+45*k,top);Icon("pause",x+66*k,top);
        if(Time.unscaledTime<toastUntil){var t=Asset("toast-"+toast);float tk=TextScale;if(t)GUI.DrawTexture(new Rect(safe.xMax-6*k-t.width*tk,safe.y+50*k,t.width*tk,t.height*tk),t);}
    }
    bool returningToMenu;
    void DrawPausePanel(float width,float height,float k,bool interactive){
        var panel=Asset("pause-panel");
        float panelHeight=panel?panel.height*k:60*k;
        float top=(height-panelHeight-46*k)*.5f;
        if(panel)GUI.DrawTexture(new Rect((width-panel.width*k)*.5f,top,panel.width*k,panelHeight),panel);
        float buttonWidth=70*k,buttonHeight=18*k;
        var timeRect=new Rect((width-110*k)*.5f,top+panelHeight+3*k,110*k,15*k);
        Panel(timeRect);
        GUI.Label(timeRect,"游玩时长  "+SessionPlayTime.Display,PixelGameUI.Text(Mathf.RoundToInt(5*k),TextAnchor.MiddleCenter));
        var menu=new Rect((width-buttonWidth)*.5f,top+panelHeight+22*k,buttonWidth,buttonHeight);
        bool old=GUI.enabled;GUI.enabled=interactive&&!returningToMenu;
        bool clicked=PixelGameUI.Button(menu,"返回主菜单");GUI.enabled=old;
        if(!interactive)return;
        if(clicked){
            returningToMenu=true;Blip("click");StartCoroutine(ReturnToMenu());
            return;
        }
        // Do not let the new button's press leak into click-anywhere-to-resume.
        if(Event.current.type==EventType.MouseUp&&Event.current.button==0&&!menu.Contains(Event.current.mousePosition)){
            SetPaused(false);Event.current.Use();
        }
    }
    System.Collections.IEnumerator ReturnToMenu(){
        yield return ChapterRecoilTravel.CancelPreloadForMenu();
        SetPaused(false);
        yield return SceneManager.LoadSceneAsync("Cover",LoadSceneMode.Single);
    }
    void DrawEditorPreview(float width,float height){
        float k=Mathf.Clamp(Mathf.FloorToInt(width/460f+.5f),2,4)*UIScale;
        var old=GUI.color;var matrix=GUI.matrix;GUI.color=Color.white;
        try{
            if(editorPanel==EditorPanel.Pause){
                DrawPausePanel(width,height,k,false);
            }
            Draw(new Rect(6*k,6*k,19*k,21*k),"map-normal");
            float x=width-91*k;
            Draw(new Rect(x,6*k,19*k,21*k),"undo-normal");Draw(new Rect(x-k,3*k,5*k,6*k),"deco-0");
            Draw(new Rect(x+21*k,6*k,19*k,21*k),"reset-normal");Draw(new Rect(x+39*k,12*k,7*k,6*k),"bridge");
            Draw(new Rect(x+45*k,6*k,19*k,21*k),"sound-normal");Draw(new Rect(x+66*k,6*k,19*k,21*k),"pause-normal");
        }finally{GUI.color=old;GUI.matrix=matrix;}
    }
#if UNITY_EDITOR
    void OnDrawGizmos(){
        if(Application.isPlaying||!showInSceneView||gameObject.scene!=SceneManager.GetActiveScene())return;
        var view=UnityEditor.SceneView.currentDrawingSceneView;if(!view)return;
        UnityEditor.Handles.BeginGUI();
        try{DrawEditorPreview(view.position.width,view.position.height);}finally{UnityEditor.Handles.EndGUI();}
    }
    void OnValidate(){UnityEditor.EditorApplication.QueuePlayerLoopUpdate();UnityEditor.SceneView.RepaintAll();}
#endif
}
}
