using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace BetweenPoles {
[Serializable] public class ChapterMapIsland {
    public string id;
    public bool hasBlackHole;
    public Vector2Int blackHoleCell;
    public Vector2 position;
    public Vector2Int[] cells;
    public Vector2Int[] rocks;
    public Vector2Int[] magnets;
    public int[] neighbors;
    public Vector3 spawn;
}
[Serializable] public class ChapterMapWorld {
    public string title,scene;
    public Transform planet;
    public float hitRadius=1.4f;
    public Color sea,land,accent;
    public Texture2D surfaceBackground;
    public GameObject spaceBackground;
    public int start;
    public ChapterMapIsland[] islands;
}
// The star chart is a location overview, with no nested island-selection screen.
public sealed partial class ChapterAtlas : MonoBehaviour {
    public Camera showcaseCamera;
    public ChapterMapWorld[] chapters;
    public static int ReturnChapter=-1,DestinationIsland=-1;
    public static string DestinationScene,DestinationRoom;
    public int CurrentChapter {get;private set;}=-1;
    Canvas canvas;RectTransform root,locator;Font font;
    Texture2D portrait,arrow;bool returning;
    public static string Key(string scene,string room){return "ChapterAtlas.visited.v1."+scene+"."+room;}
    void Start(){
        if(!showcaseCamera)showcaseCamera=GetComponent<Camera>();
        chartCameraPosition=showcaseCamera.transform.position;chartCameraSize=showcaseCamera.orthographicSize;
        CurrentChapter=Mathf.Clamp(ReturnChapter,0,chapters.Length-1);
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);
        BuildUI();CacheChartPlanets();if(ChartArrivalFrame)StartCoroutine(ArriveAtChart());
    }
    void Update(){
        if(Input.GetKeyDown(KeyCode.M)){ReturnToGame();return;}
        if(chartTransitioning){if(Input.GetKeyDown(KeyCode.Escape))ReturnToGame();return;}
        if(IsInspecting){if(!inspectionLoading){if(inspectionNavigation){inspectionNavigation.TickInspection();if(inspectionNavigation.PlayerReturnRequested){ReturnToGame();return;}}if(Input.GetKeyDown(KeyCode.Escape))CloseInspection();}return;}
        if(Input.GetKeyDown(KeyCode.Escape)){ReturnToGame();return;}
        if(Input.GetMouseButtonDown(0)&&(!EventSystem.current||!EventSystem.current.IsPointerOverGameObject()))ClickPlanet(Input.mousePosition);
    }

    void LateUpdate(){
        if(!locator||CurrentChapter<0||CurrentChapter>=chapters.Length||!chapters[CurrentChapter].planet)return;
        var p=showcaseCamera.WorldToViewportPoint(chapters[CurrentChapter].planet.position);
        var size=root.rect.size;
        locator.anchoredPosition=new Vector2((p.x-.5f)*size.x,Mathf.Min((p.y-.5f)*size.y+115,size.y*.5f-110));
    }
    public void ReturnToGame(){
        if(returning)return;if(IsInspecting){returnAfterInspection=true;if(!inspectionLoading)StartCoroutine(FinishInspectionView());return;}returning=true;
        if(ChapterMapReturn.HasSuspendedGame){ChapterMapReturn.ResumeGame();return;}
        var scene=chapters[CurrentChapter].scene;
        if(Application.CanStreamedLevelBeLoaded(scene))SceneManager.LoadSceneAsync(scene);else returning=false;
    }
    RectTransform Box(string name,Transform parent,Vector2 position,Vector2 size){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;}
    Text Label(string value,Transform parent,Vector2 position,Vector2 size,int fontSize){var r=Box(value,parent,position,size);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=fontSize;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.92f,.96f,.95f);t.raycastTarget=false;return t;}
    void BuildUI(){
        {var e=new GameObject("星际航图输入",typeof(EventSystem),typeof(StandaloneInputModule));SceneManager.MoveGameObjectToScene(e,gameObject.scene);}
        var go=new GameObject("星际航图界面",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));SceneManager.MoveGameObjectToScene(go,gameObject.scene);
        canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;root=(RectTransform)go.transform;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        Label("两极之间  /  星际航图",root,new Vector2(-480,390),new Vector2(520,55),28);
        for(int i=0;i<chapters.Length;i++){
            int chapter=i;var tab=Box("检视"+chapters[i].title,root,new Vector2(-510+i*340,-345),new Vector2(305,48));
            tab.gameObject.AddComponent<Image>().color=new Color(.07f,.13f,.18f,.95f);var chapterButton=tab.gameObject.AddComponent<Button>();chapterButton.onClick.AddListener(()=>OpenInspection(chapter));PixelGameUI.StyleButton(chapterButton);
            Label("0"+(i+1)+"  "+chapters[i].title,tab,Vector2.zero,new Vector2(305,48),22);
        }
        Label("点击星球，检视已经探索的小岛",root,new Vector2(0,-402),new Vector2(900,40),20);
        string text=ChapterMapReturn.HasSuspendedGame?"返回岛屿（M / Esc）":"进入游戏";
        var button=Box(text,root,new Vector2(645,385),new Vector2(220,52));button.gameObject.AddComponent<Image>().color=new Color(.07f,.13f,.18f,.95f);
        var returnButton=button.gameObject.AddComponent<Button>();returnButton.onClick.AddListener(ReturnToGame);PixelGameUI.StyleButton(returnButton);Label(text,button,Vector2.zero,new Vector2(220,52),21);
        locator=Box("主角当前位置",root,Vector2.zero,new Vector2(240,150));
        portrait=CreatePlayerPortrait();var icon=Box("主角形象",locator,new Vector2(0,30),new Vector2(64,80)).gameObject.AddComponent<RawImage>();icon.texture=portrait;icon.raycastTarget=false;
        arrow=new Texture2D(9,7,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};var pixels=new Color[63];
        for(int y=0;y<7;y++)for(int x=0;x<9;x++)if(y<5?Mathf.Abs(x-4)<=y:Mathf.Abs(x-4)<=1)pixels[y*9+x]=new Color(1,.2f,.25f,1);
        arrow.SetPixels(pixels);arrow.Apply();var pointer=Box("指向当前星球",locator,new Vector2(0,-31),new Vector2(36,28)).gameObject.AddComponent<RawImage>();pointer.texture=arrow;pointer.raycastTarget=false;
        Label("你在："+chapters[CurrentChapter].title,locator,new Vector2(0,91),new Vector2(280,34),22);
    }
    Texture2D CreatePlayerPortrait(){
        var definition=Resources.Load<SharedPlayerDefinition>("SharedPlayerDefinition");
        if(!definition||!definition.prefab)return Texture2D.whiteTexture;
        var holder=new GameObject("主角标识临时摄影棚");holder.transform.position=new Vector3(10000,10000,10000);
        var materials=new List<Material>();var renderers=new List<Renderer>();var rt=new RenderTexture(64,80,24){filterMode=FilterMode.Point};var active=RenderTexture.active;
        try{
            foreach(var mesh in definition.prefab.GetComponentsInChildren<MeshFilter>(true)){
                var source=mesh.GetComponent<MeshRenderer>();if(!source||!mesh.sharedMesh)continue;
                var part=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));part.layer=30;part.transform.SetParent(holder.transform,false);
                var matrix=definition.prefab.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
                part.transform.localPosition=matrix.GetColumn(3);part.transform.localRotation=matrix.rotation;part.transform.localScale=matrix.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                var copies=new Material[source.sharedMaterials.Length];
                for(int i=0;i<copies.Length;i++){var m=new Material(Shader.Find("Unlit/Color"));m.color=source.sharedMaterials[i]&&source.sharedMaterials[i].HasProperty("_Color")?source.sharedMaterials[i].color:Color.white;copies[i]=m;materials.Add(m);}
                var renderer=part.GetComponent<MeshRenderer>();renderer.sharedMaterials=copies;renderers.Add(renderer);
            }
            var bounds=new Bounds();bool hasBounds=false;
            foreach(var mesh in holder.GetComponentsInChildren<MeshFilter>())foreach(var vertex in mesh.sharedMesh.vertices){var point=mesh.transform.TransformPoint(vertex);if(!hasBounds){bounds=new Bounds(point,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(point);}
            var cameraObject=new GameObject("主角标识相机");cameraObject.transform.SetParent(holder.transform,false);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=bounds.size.y*.68f;camera.aspect=.8f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<30;
            camera.transform.position=bounds.center+new Vector3(2,1,5).normalized*8;camera.transform.LookAt(bounds.center);camera.targetTexture=rt;rt.Create();camera.Render();
            RenderTexture.active=rt;var image=new Texture2D(64,80,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};image.ReadPixels(new Rect(0,0,64,80),0,0);image.Apply();return image;
        }finally{RenderTexture.active=active;rt.Release();Destroy(rt);holder.SetActive(false);Destroy(holder);foreach(var m in materials)Destroy(m);}
    }
    void OnDestroy(){ReleaseChartFeedback();if(portrait&&portrait!=Texture2D.whiteTexture)Destroy(portrait);if(arrow)Destroy(arrow);if(font)Destroy(font);if(canvas)Destroy(canvas.gameObject);}
}
}
