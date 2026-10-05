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
    public int start;
    public ChapterMapIsland[] islands;
}
public sealed class ChapterAtlas : MonoBehaviour {
    public Camera showcaseCamera;
    public ChapterMapWorld[] chapters;
    public static int ReturnChapter=-1, DestinationIsland=-1;
    public static string DestinationScene,DestinationRoom;
    public bool IsMapOpen {get;private set;}
    public bool IsTransitioning {get;private set;}
    public int CurrentChapter {get;private set;}=-1;
    Canvas canvas; CanvasGroup group; RectTransform root; RawImage art;
    Button enter; RectTransform selection; GameObject mapPanel,spacePanel;
    readonly List<GameObject> pins=new List<GameObject>();
    readonly List<Texture2D> owned=new List<Texture2D>();
    Vector3 cameraStart; float sizeStart; int selected; Font font;
    public static string Key(string scene,string room){return "ChapterAtlas.visited.v1."+scene+"."+room;}
    public bool IsUnlocked(int chapter,int island){var c=chapters[chapter];return island==c.start||PlayerPrefs.GetInt(Key(c.scene,c.islands[island].id),0)==1;}
    public bool CanSelectIsland(int chapter,int island){var c=chapters[chapter];if(island<0||island>=c.islands.Length)return false;return BlackHoleTravel.Selecting ? c.islands[island].hasBlackHole && PlayerPrefs.GetInt(Key(c.scene,c.islands[island].id),0)==1 && BlackHoleTravel.IsAvailable(c.scene,c.islands[island].id) : IsUnlocked(chapter,island);}
    void Start(){
        if(!showcaseCamera)showcaseCamera=GetComponent<Camera>();
        cameraStart=showcaseCamera.transform.position;sizeStart=showcaseCamera.orthographicSize;
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);
        BuildUI();
        if(ReturnChapter>=0&&ReturnChapter<chapters.Length){int c=ReturnChapter;ReturnChapter=-1;OpenMap(c);}
    }
    void OnDestroy(){foreach(var t in owned)if(t)Destroy(t);if(font)Destroy(font);if(canvas)Destroy(canvas.gameObject);}
    void Update(){
        if(BlackHoleTravel.InTransit)return;
        if(Input.GetKeyDown(KeyCode.Escape)&&!IsTransitioning){if(IsMapOpen)CloseMap();else if(BlackHoleTravel.Selecting)BlackHoleTravel.Cancel();}
        if(IsMapOpen||IsTransitioning)return;
        if(Input.GetMouseButtonDown(0)&&(!EventSystem.current||!EventSystem.current.IsPointerOverGameObject())){
            Vector3 p=showcaseCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x,Input.mousePosition.y,30));
            for(int i=0;i<chapters.Length;i++)if(chapters[i].planet&&Vector2.Distance(p,chapters[i].planet.position)<chapters[i].hitRadius){OpenMap(i);break;}
        }
    }
    public void OpenMap(int chapter){if(IsTransitioning||IsMapOpen||chapter<0||chapter>=chapters.Length)return;CurrentChapter=chapter;StartCoroutine(EnterMap());}
    IEnumerator EnterMap(){
        IsTransitioning=true;spacePanel.SetActive(false);var c=chapters[CurrentChapter];
        Vector3 end=c.planet.position;end.z=cameraStart.z;
        for(float t=0;t<1.35f;t+=Time.unscaledDeltaTime){float p=Mathf.SmoothStep(0,1,t/1.35f);showcaseCamera.transform.position=Vector3.Lerp(cameraStart,end,p);showcaseCamera.orthographicSize=Mathf.Exp(Mathf.Lerp(Mathf.Log(sizeStart),Mathf.Log(.55f),p));yield return null;}
        RenderMap();mapPanel.SetActive(true);group.alpha=0;IsMapOpen=true;
        for(float t=0;t<.45f;t+=Time.unscaledDeltaTime){group.alpha=Mathf.SmoothStep(0,1,t/.45f);yield return null;}
        group.alpha=1;IsTransitioning=false;
    }
    public void CloseMap(){if(IsTransitioning||!IsMapOpen)return;StartCoroutine(LeaveMap());}
    IEnumerator LeaveMap(){
        IsTransitioning=true;
        for(float t=0;t<.3f;t+=Time.unscaledDeltaTime){group.alpha=1-t/.3f;yield return null;}
        mapPanel.SetActive(false);IsMapOpen=false;
        Vector3 start=showcaseCamera.transform.position;float size=showcaseCamera.orthographicSize;
        for(float t=0;t<.85f;t+=Time.unscaledDeltaTime){float p=Mathf.SmoothStep(0,1,t/.85f);showcaseCamera.transform.position=Vector3.Lerp(start,cameraStart,p);showcaseCamera.orthographicSize=Mathf.Exp(Mathf.Lerp(Mathf.Log(size),Mathf.Log(sizeStart),p));yield return null;}
        showcaseCamera.transform.position=cameraStart;showcaseCamera.orthographicSize=sizeStart;spacePanel.SetActive(true);IsTransitioning=false;
    }
    RectTransform Box(string name,Transform parent,Vector2 position,Vector2 size){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;}
    Text Label(string value,Transform parent,Vector2 position,Vector2 size,int fontSize,TextAnchor alignment){var r=Box(value,parent,position,size);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=fontSize;t.alignment=alignment;t.color=new Color(.92f,.96f,.95f);t.raycastTarget=false;return t;}
    Button Button(string name,Transform parent,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action){var r=Box(name,parent,position,size);var img=r.gameObject.AddComponent<Image>();img.color=new Color(.07f,.13f,.18f,.95f);var b=r.gameObject.AddComponent<Button>();var colors=b.colors;colors.highlightedColor=new Color(.65f,.9f,.9f);colors.pressedColor=new Color(.4f,.7f,.8f);colors.disabledColor=new Color(.35f,.35f,.35f);b.colors=colors;b.onClick.AddListener(action);Label(name,r,Vector2.zero,size,21,TextAnchor.MiddleCenter);return b;}
    void BuildUI(){
        if(!EventSystem.current){var e=new GameObject("章节地图输入",typeof(EventSystem),typeof(StandaloneInputModule));SceneManager.MoveGameObjectToScene(e,gameObject.scene);}
        var go=new GameObject("章节导航",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));SceneManager.MoveGameObjectToScene(go,gameObject.scene);canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;root=(RectTransform)go.transform;
        spacePanel=Box("星际导航",root,Vector2.zero,new Vector2(1600,900)).gameObject;
        Label("两极之间  /  星际航图",spacePanel.transform,new Vector2(-480,390),new Vector2(520,55),28,TextAnchor.MiddleLeft);
        Label("选择一颗章节星球，展开它的世界地图",spacePanel.transform,new Vector2(0,-400),new Vector2(900,48),22,TextAnchor.MiddleCenter);
        for(int i=0;i<chapters.Length;i++){int index=i;Button("0"+(i+1)+"  "+chapters[i].title,spacePanel.transform,new Vector2(-510+i*340,-345),new Vector2(305,48),()=>OpenMap(index));}
        if(BlackHoleTravel.Selecting)Button("取消穿梭",spacePanel.transform,new Vector2(645,385),new Vector2(220,52),BlackHoleTravel.Cancel);
        mapPanel=Box("展开的世界地图",root,Vector2.zero,new Vector2(1600,900)).gameObject;group=mapPanel.AddComponent<CanvasGroup>();
        var ar=Box("地表与迷雾",mapPanel.transform,Vector2.zero,new Vector2(1800,1100));art=ar.gameObject.AddComponent<RawImage>();art.raycastTarget=false;
        Button("← 星际航图",mapPanel.transform,new Vector2(645,385),new Vector2(220,52),CloseMap);
        enter=Button(BlackHoleTravel.Selecting?"穿梭至黑洞 →":"进入小岛 →",mapPanel.transform,new Vector2(635,-370),new Vector2(245,60),EnterIsland);
        selection=Box("选中小岛",mapPanel.transform,Vector2.zero,new Vector2(16,5));var marker=selection.gameObject.AddComponent<Image>();marker.color=new Color(.9f,.95f,.78f);marker.raycastTarget=false;
        mapPanel.SetActive(false);
    }
    void RenderMap(){
        var c=chapters[CurrentChapter];foreach(var pin in pins)Destroy(pin);pins.Clear();
        if(art.texture){owned.Remove((Texture2D)art.texture);Destroy(art.texture);}var texture=DrawMap(c);owned.Add(texture);art.texture=texture;
        for(int i=0;i<c.islands.Length;i++){int index=i;var island=c.islands[i];Vector2 pos=new Vector2((island.position.x-.5f)*1800,(island.position.y-.5f)*1100);bool open=CanSelectIsland(CurrentChapter,i);
            if(open){
                var hit=Box("选择小岛 "+i,mapPanel.transform,pos,new Vector2(200,195));var img=hit.gameObject.AddComponent<Image>();img.color=Color.clear;var button=hit.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>{SelectIsland(index);if(BlackHoleTravel.Selecting)EnterIsland();});pins.Add(hit.gameObject);
            }
        }
        selected=-1;selection.gameObject.SetActive(false);enter.interactable=false;
        for(int i=0;i<c.islands.Length;i++)if(CanSelectIsland(CurrentChapter,i)){SelectIsland(i);break;}
    }
    public void SelectIsland(int island){if(!IsMapOpen&& !IsTransitioning)return;if(island<0||island>=chapters[CurrentChapter].islands.Length||!CanSelectIsland(CurrentChapter,island))return;selected=island;selection.gameObject.SetActive(true);var position=chapters[CurrentChapter].islands[island].position;selection.anchoredPosition=new Vector2((position.x-.5f)*1800,(position.y-.5f)*1100-118);enter.interactable=true;}
    public void EnterIsland(){if(!IsMapOpen||IsTransitioning||!CanSelectIsland(CurrentChapter,selected))return;if(BlackHoleTravel.Selecting){var c=chapters[CurrentChapter];BlackHoleTravel.Choose(c.scene,c.islands[selected].id);return;}StartCoroutine(LoadIsland());}
    public void ShowPortalBlocked(){if(enter)enter.GetComponentInChildren<Text>().text="出洞方向被挡住";}
    IEnumerator LoadIsland(){
        IsTransitioning=true;enter.interactable=false;enter.GetComponentInChildren<Text>().text="进入中…";
        var c=chapters[CurrentChapter];DestinationIsland=selected;DestinationRoom=c.islands[selected].id;DestinationScene=c.scene;ReturnChapter=CurrentChapter;
        if(!Application.CanStreamedLevelBeLoaded(c.scene)){enter.GetComponentInChildren<Text>().text="暂不可用";IsTransitioning=false;yield break;}
        var load=SceneManager.LoadSceneAsync(c.scene);if(load==null){enter.GetComponentInChildren<Text>().text="暂不可用";IsTransitioning=false;yield break;}while(!load.isDone)yield return null;
    }
    Texture2D DrawMap(ChapterMapWorld c){
        const int w=600,h=366;var pixels=new Color[w*h];
        if(c.surfaceBackground&&c.surfaceBackground.width==w&&c.surfaceBackground.height==h)pixels=c.surfaceBackground.GetPixels();
        else for(int i=0;i<pixels.Length;i++)pixels[i]=c.sea;
        Action<int,int,Color> dot=(x,y,col)=>{if(x>=0&&x<w&&y>=0&&y<h){col.a=1;pixels[y*w+x]=col;}};
        for(int i=0;i<c.islands.Length;i++){
            var island=c.islands[i];int cx=(int)(island.position.x*w),cy=(int)(island.position.y*h);
            if(IsUnlocked(CurrentChapter,i)||(BlackHoleTravel.Selecting&&CanSelectIsland(CurrentChapter,i))){
                var occupied=new HashSet<Vector2Int>(island.cells);
                // Three-pixel rock sides and a narrow top bevel give the same slab
                // silhouette as the playable islands, rather than a flat grid stamp.
                foreach(var cell in island.cells){int x=cx+cell.x*7,y=cy+cell.y*7;for(int yy=-3;yy<=3;yy++)for(int xx=-3;xx<=3;xx++){dot(x+xx+1,y+yy-4,c.land*.34f);dot(x+xx,y+yy-3,c.land*.62f);}}
                foreach(var cell in island.cells){int x=cx+cell.x*7,y=cy+cell.y*7;
                    for(int yy=-3;yy<=3;yy++)for(int xx=-3;xx<=3;xx++){
                        int px=x+xx,py=y+yy;float patch=Mathf.Floor(Mathf.PerlinNoise(px*.075f,py*.075f)*5)/4;
                        Color top=Color.Lerp(c.land*.81f,c.land*1.09f,patch);
                        if(xx==-3||yy==3)top*=.9f;
                        if(CurrentChapter==3){float crack=Mathf.Abs(Mathf.PerlinNoise(px*.11f,py*.11f)-.5f);if(crack<.025f)top=Color.Lerp(top,new Color(.95f,.56f,.30f),.6f);}
                        if(yy==3&&!occupied.Contains(cell+Vector2Int.up))top=c.land*1.13f;
                        if(xx==-3&&!occupied.Contains(cell+Vector2Int.left))top=c.land*1.04f;
                        dot(px,py,top);
                    }
                }
                foreach(var rock in island.rocks){int rx=cx+rock.x*7,ry=cy+rock.y*7;for(int yy=-2;yy<=2;yy++)for(int xx=-2;xx<=2;xx++)dot(rx+xx,ry+yy,c.land*(xx>0?.40f:.52f));for(int xx=-2;xx<=1;xx++)dot(rx+xx,ry+2,c.land*.67f);}
                for(int m=0;m<island.magnets.Length;m++){var cell=island.magnets[m];if(Array.IndexOf(island.cells,cell)<0)continue;for(int xx=-3;xx<=3;xx++)dot(cx+cell.x*7+xx,cy+cell.y*7,m%2==0?new Color(.95f,.16f,.25f):new Color(.13f,.65f,.95f));}
                if(island.hasBlackHole){int bx=cx+island.blackHoleCell.x*7,by=cy+island.blackHoleCell.y*7;for(int y=-3;y<=3;y++)for(int x=-3;x<=3;x++){float r=Mathf.Sqrt(x*x+y*y);if(r>3.5f)continue;float arm=Mathf.Sin(Mathf.Atan2(y,x)*3+r*2);dot(bx+x,by+y,r<1.5f?new Color(.025f,.005f,.06f):Color.Lerp(new Color(.13f,.03f,.25f),new Color(.65f,.3f,.9f),(arm+1)*.5f));}}
            }else {
                for(int y=-40;y<=40;y++)for(int x=-52;x<=52;x++){
                    float radial=x*x/(48f*48)+y*y/(31f*31);float n=Mathf.PerlinNoise((x+110+i*80)*.11f,(y+80)*.11f);if(radial> .76f+n*.48f)continue;
                    float layers=Mathf.Floor((n*.6f+(1-radial)*.4f)*5)/5;Color fog=Color.Lerp(c.sea,new Color(.54f,.64f,.7f),.30f+layers*.38f);dot(cx+x,cy+y,fog);
                }
            }
        }
        var tex=new Texture2D(w,h,TextureFormat.RGBA32,false);tex.name=c.title+"地图";tex.filterMode=FilterMode.Point;tex.wrapMode=TextureWrapMode.Clamp;tex.SetPixels(pixels);tex.Apply();return tex;
    }
}
}
