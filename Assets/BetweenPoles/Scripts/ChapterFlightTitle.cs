using UnityEngine;
namespace BetweenPoles {
// Pixel sprites, score and timing come from 两极之间-开场动画-像素飞入版.html.
// The overlay follows the live magnet; it never replaces or steers the game camera.
public sealed class ChapterFlightTitle:MonoBehaviour {
    const float Beat=60f/93.24f,Drop=8*Beat,LetterDuration=1.15f;
    const float Width=1672,Height=1002,TitleY=222;
    public const float CruiseSeconds=4.5f;
    static readonly Texture2D[] letters=new Texture2D[4];
    static Texture2D subtitle;
    static AudioClip score;
    static ChapterFlightTitle activeTitle;
    public static bool HoldsMusic {get{return activeTitle&&activeTitle.running;}}
    readonly Texture2D[] silhouettes=new Texture2D[4],highlights=new Texture2D[4];
    readonly Color32[][] original=new Color32[4][],working=new Color32[4][];
    readonly Color[][] sourceColors=new Color[4][];
    readonly int[][] visiblePixels=new int[4][];
    readonly float[] previousFlash={-1,-1,-1,-1};
    readonly float[] previousSweep={float.NaN,float.NaN,float.NaN,float.NaN};
    static readonly int[] subtitleSteps={2,7,12,18,24,29,34,39,42,48,54,59,62,67,72,77,82,87};
    static readonly Color Red=new Color32(240,59,76,255),Blue=new Color32(50,147,206,255);
    static readonly Color LightRed=new Color32(255,138,149,255),LightBlue=new Color32(143,208,255,255);
    MagnetPiece magnet;CurvedTrialCamera view;AudioSource sound;
    MaterialPropertyBlock properties;
    float started;int highlightFrame=-1;bool running;
    GridPlayground landedBoard;Vector3 landedPosition;
    int landedFrame;float dismissAt=-1;
    Texture2D magnetGlow;
    public bool AwaitingDismissal {get{return running&&landedBoard&&dismissAt<0;}}
    public float Opacity {get{return dismissAt<0?1:1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-dismissAt)/1.8f));}}
    public void Landed(GridPlayground board){
        landedBoard=board;landedPosition=board.player.position;landedFrame=Time.frameCount;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject,board.gameObject.scene);
    }
    public void Cancel(){Stop();Destroy(gameObject);}
    void Dismiss(){if(dismissAt<0)dismissAt=Time.unscaledTime;}

    public float Elapsed {get{return running?Time.time-started:0;}}
    public static void Preload(){
        for(int i=0;i<4;i++)if(!letters[i])letters[i]=Resources.Load<Texture2D>("ChapterFlightTitle/letter-"+i);
        if(!subtitle)subtitle=Resources.Load<Texture2D>("ChapterFlightTitle/subtitle");
        if(!score)score=Resources.Load<AudioClip>("ChapterFlightTitle/flight-score");
        if(score)score.LoadAudioData();
    }
    public void Begin(MagnetPiece actor,CurvedTrialCamera camera){
        Preload();magnet=actor;view=camera;properties=new MaterialPropertyBlock();
        for(int i=0;i<4;i++)if(letters[i]){
            original[i]=letters[i].GetPixels32();working[i]=new Color32[original[i].Length];
            sourceColors[i]=new Color[original[i].Length];
            var visible=new System.Collections.Generic.List<int>();
            for(int j=0;j<original[i].Length;j++){
                sourceColors[i][j]=original[i][j];working[i][j]=original[i][j];
                if(original[i][j].a>0)visible.Add(j);
            }
            visiblePixels[i]=visible.ToArray();
            silhouettes[i]=NewTexture(letters[i]);highlights[i]=NewTexture(letters[i]);
            var tint=(Color32)(i<2?Red:Blue);var pixels=new Color32[original[i].Length];
            for(int j=0;j<pixels.Length;j++){pixels[j]=tint;pixels[j].a=original[i][j].a;}
            silhouettes[i].SetPixels32(pixels);silhouettes[i].Apply();
        }
        magnetGlow=new Texture2D(32,16,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        var glowPixels=new Color[32*16];
        for(int y=0;y<16;y++)for(int x=0;x<32;x++){
            float r=Mathf.Sqrt(Mathf.Pow((x-15.5f)/16,2)+Mathf.Pow((y-7.5f)/8,2));
            float a=Mathf.Floor(Mathf.Pow(Mathf.Clamp01(1-r),1.4f)*6)/6;
            glowPixels[y*32+x]=new Color(1,1,1,a);
        }
        magnetGlow.SetPixels(glowPixels);magnetGlow.Apply();
        started=Time.time;running=true;activeTitle=this;
        sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;
        sound.clip=score;sound.volume=1;if(score)sound.Play();
    }
    static Texture2D NewTexture(Texture2D source){return new Texture2D(source.width,source.height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};}
    public void Stop(){running=false;if(activeTitle==this)activeTitle=null;if(sound)sound.Stop();}
    void OnDisable(){Stop();}
    void OnDestroy(){if(magnetGlow)Destroy(magnetGlow);for(int i=0;i<4;i++){if(silhouettes[i])Destroy(silhouettes[i]);if(highlights[i])Destroy(highlights[i]);}}
    void Update(){
        if(!running)return;float t=Elapsed;
        if(sound)sound.volume=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(11.5f,13.5f,t));
        // Flight input never dismisses the title. Wait for a fresh click or actual
        // player movement after touchdown, then fade without blocking gameplay.
        if(landedBoard&&Time.frameCount>landedFrame&&dismissAt<0){
            if(Input.GetMouseButtonDown(0)||(landedBoard.player.position-landedPosition).sqrMagnitude>.0004f)Dismiss();
        }
        if(dismissAt>=0&&Opacity<=0){Stop();Destroy(gameObject);return;}
        int frame=Mathf.FloorToInt(t*30);if(frame==highlightFrame)return;highlightFrame=frame;
        for(int i=0;i<4;i++)if(letters[i]){
            float end=Drop-(3-i)*Beat,flash=Mathf.Clamp01(1-(t-end)/.14f);
            if(t<end)flash=0;
            float sweep=t-(Drop+.9f),p=Mathf.Repeat(sweep,3.6f)/1.1f;
            float x=208+(1464-208)*p;
            // Between sweeps the title is unchanged. Do not recalculate and
            // upload four textures for the entire time it awaits interaction.
            float sweepKey=sweep>=0&&p<1?x:float.PositiveInfinity;
            if(previousFlash[i]==flash&&previousSweep[i]==sweepKey)continue;
            previousFlash[i]=flash;previousSweep[i]=sweepKey;
            int width=letters[i].width,lastRow=-1;float sweepX=0;
            foreach(int index in visiblePixels[i]){
                int y=index/width,px=index-y*width;var c=sourceColors[i][index];
                if(y!=lastRow){lastRow=y;float rowY=TitleY-129+(42-y)*6;sweepX=Snap(x+(rowY-TitleY)*.55f,5);}
                float wx=836+(i-1.5f)*226-129+px*6;
                float dx=wx-sweepX;
                float shine=sweep>=0&&p<1&&((dx>=0&&dx<30)||(dx>=45&&dx<55))?.85f:0;
                float alpha=c.a;c=Color.Lerp(c,Color.white,Mathf.Max(flash,shine));c.a=alpha;working[i][index]=c;
            }
            highlights[i].SetPixels32(working[i]);highlights[i].Apply();
        }
    }
    static float Snap(float x,float grid){return Mathf.Round(x/grid)*grid;}
    static Vector2 Bezier(Vector2 a,Vector2 b,Vector2 c,float t){return (1-t)*(1-t)*a+2*(1-t)*t*b+t*t*c;}
    static float End(int i){return Drop-(3-i)*Beat;}
    static Vector2 Center(int i){return new Vector2(836+(i-1.5f)*226,TitleY);}
    static Vector3 Flight(int i,float t,Vector2 magnet,Rect viewport){
        float k=Mathf.Clamp01((t-(End(i)-LetterDuration))/LetterDuration),z=1+13*Mathf.Pow(1-k,1.3f);
        // The magnetic field bows the incoming path toward the live magnet before
        // the glyph settles into its title position, retaining the original beat.
        Vector2 control=Vector2.Lerp(Center(i),magnet,.55f);
        control.x+=(i%2==1?-1:1)*65;
        // Use the actual screen bounds in title coordinates, including wide/tall
        // viewport margins. The entire glyph starts outside, not at a visible star.
        Vector2 origin=i==0?new Vector2(viewport.xMin-180,viewport.yMin-120)
            :i==3?new Vector2(viewport.xMax+180,viewport.yMin-120)
            :new Vector2(viewport.center.x+(i==1?-230:230),viewport.yMin-220);
        float approach=(1/z-1f/14)/(1-1f/14);
        Vector2 p=Bezier(origin,control,Center(i),approach);
        return new Vector3(p.x,p.y,1.22f/z);
    }
    static void Image(Texture2D image,Vector2 center,float pixelScale,Color tint){
        if(!image)return;float size=43*Mathf.Max(1,Mathf.Round(pixelScale));GUI.color=tint;
        GUI.DrawTexture(new Rect(Mathf.Round(center.x-size/2),Mathf.Round(center.y-size/2),size,size),image);
    }
    static void Square(Vector2 p,float size,Color color){GUI.color=color;GUI.DrawTexture(new Rect(Snap(p.x,5),Snap(p.y,5),size,size),Texture2D.whiteTexture);}
    static void Attraction(Vector2 glyph,Vector2 magnet,int i,Color tint,float t,float strength){
        Vector2 middle=Vector2.Lerp(glyph,magnet,.5f)+Vector2.right*(i%2==0?-32:32);
        // A few accelerating motes always travel from the letter toward the magnet.
        // No static outlines or hard white strips on the magnet itself.
        for(int n=0;n<5;n++){
            float p=Mathf.Repeat(t*1.15f+n*.2f+i*.13f,1);
            for(int tail=2;tail>=0;tail--){
                float u=Mathf.Max(0,p-tail*.035f);float eased=u*u;
                var c=tint;c.a=strength*Mathf.Sin(p*Mathf.PI)*(.38f-tail*.10f);
                Square(Bezier(glyph,middle,magnet,eased),tail==0?5:3,c);
            }
        }
    }
    Vector2 MagnetPoint(float scale,Vector2 margin){
        if(!magnet||!view)return new Vector2(836,510);
        var renderer=magnet.GetComponentInChildren<Renderer>();if(renderer)renderer.GetPropertyBlock(properties);
        Vector3 focus=Shader.GetGlobalVector("_IceFocus"),offset=properties.GetVector("_RecoilDisplayOffset");
        var world=focus+(magnet.transform.position-focus)*Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"))+offset;
        var p=view.GetComponent<Camera>().WorldToViewportPoint(world);
        return (new Vector2(p.x*Screen.width,(1-p.y)*Screen.height)-margin)/scale;
    }
    void OnGUI(){
        if(!running)return;float t=Elapsed,fade=Opacity;
        float scale=Mathf.Min(Screen.width/Width,Screen.height/Height);Vector2 margin=new Vector2((Screen.width-Width*scale)/2,(Screen.height-Height*scale)/2);
        var viewport=new Rect(-margin.x/scale,-margin.y/scale,Screen.width/scale,Screen.height/scale);
        var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;
        GUI.depth=-80;GUI.matrix=Matrix4x4.TRS(margin,Quaternion.identity,Vector3.one*scale);
        try{
            Vector2 m=MagnetPoint(scale,margin);float charge=End(0)-LetterDuration-.55f;
            float glow=Mathf.Clamp01((t-charge)/.4f)*Mathf.Clamp01((Drop+.4f-t)/.4f);
            if(glow>0&&magnetGlow){
                Color c=magnet&&magnet.north?LightRed:LightBlue;
                c.a=glow*(.22f+.045f*Mathf.Sin(t*3.5f))*fade;
                GUI.color=c;GUI.DrawTexture(new Rect(m.x-88,m.y-38,176,76),magnetGlow);
            }
            for(int i=0;i<4;i++){
                float end=End(i),start=end-LetterDuration;if(t<start)continue;
                var f=Flight(i,Mathf.Min(t,end),m,viewport);
                if(t>=start+.25f&&t<=end+.15f){
                    Color c=i<2?Red:Blue;c.a=Mathf.Clamp01((t-start-.25f)/.25f)*Mathf.Clamp01((end+.15f-t)/.15f)*.8f*fade;
                    Attraction(new Vector2(f.x,f.y+258*f.z*.3f),m,i,i<2?LightRed:LightBlue,t,c.a);
                }
                if(t<end){
                    float progress=(t-start)/LetterDuration;
                    if(progress<.3f){float a=1-progress/.3f,r=Mathf.Round(3+a*3)*3;GUI.color=new Color(1,1,1,a*(.6f+.4f*Mathf.Sin(t*40))*fade);GUI.DrawTexture(new Rect(Snap(f.x,3)-r,Snap(f.y,3)-1,r*2+3,3),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(Snap(f.x,3),Snap(f.y,3)-r-1,3,r*2+3),Texture2D.whiteTexture);}
                    for(int g=3;g>=1;g--){if(t-g*.045f<start)continue;var ghost=Flight(i,t-g*.045f,m,viewport);Image(silhouettes[i],ghost,6*ghost.z,new Color(1,1,1,(.42f-g*.11f)*fade));}
                    Image(letters[i],f,6*f.z,new Color(1,1,1,fade));
                }else{
                    float dt=t-end;
                    if(dt<.7f)for(int j=0;j<8;j++){
                        float angle=j/8f*Mathf.PI*2+i*.23f,life=.45f+(j%3)*.1f,a=1-dt/life;if(a<=0)continue;
                        float distance=82+(260+(j%4)*65)*(1-Mathf.Exp(-dt*6))/6;
                        var c=a>.5f?Color.white:i<2?LightRed:LightBlue;c.a=fade;
                        Square(Center(i)+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*distance,a>.4f?12:6,c);
                    }
                }
            }
            if(t>=Drop+.2f){
                var a=Center(1)+new Vector2(258*.28f,-129+258*.12f);var b=Center(2)+new Vector2(-258*.28f,-129+258*.12f);var mid=(a+b)/2+Vector2.down*95;
                for(int n=0;n<=18;n++){if(Mathf.Repeat(n-t*14,6)>=3)continue;var p=Bezier(a,mid,b,n/18f);float alpha=Mathf.Clamp01((t-Drop-.2f)/.5f)*fade;Square(p-Vector2.one*6,24,new Color(6/255f,14/255f,32/255f,alpha));var c=n<9?LightRed:LightBlue;c.a=alpha;Square(p,12,c);}
            }
            // Foreground pass: every settled glyph covers ALL incoming letters,
            // trails and particles, regardless of its index in the title.
            for(int i=0;i<4;i++)if(t>=End(i)){
                float dt=t-End(i),bounce=dt<.5f?1+.22f*Mathf.Exp(-dt*9)*Mathf.Cos(dt*28):1;
                float dy=t>Drop+.3f?Mathf.Round(Mathf.Sin((t-Drop)*2.1f+i*1.3f)*1.2f)*6:0;
                Image(highlights[i],Center(i)+Vector2.up*dy,6*bounce,new Color(1,1,1,fade));
            }
            if(subtitle&&t>=Drop+.45f){int n=Mathf.Min(subtitleSteps.Length-1,Mathf.FloorToInt((t-Drop-.45f)/.045f));float width=subtitleSteps[n]+1;GUI.color=new Color(1,1,1,fade);GUI.DrawTextureWithTexCoords(new Rect(836-subtitle.width*2.5f,TitleY+146,width*5,50),subtitle,new Rect(0,0,width/subtitle.width,1));}
        }finally{GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;}
    }
}
}
