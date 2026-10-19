using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace BetweenPoles {
public static class MobileAdaptationRegression {
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static Touch T(int id,TouchPhase phase,float x,float y,float dx=0,float dy=0)=>new Touch{fingerId=id,phase=phase,position=new Vector2(x,y),deltaPosition=new Vector2(dx,dy)};
    [MenuItem("Between Poles/Tests/Mobile adaptation")]
    public static void Run(){
        checks=0;Directory.CreateDirectory("output/mobile-adaptation");
        try{
            MobileControlRegression.Run();
            GestureCases();LayoutCases();AudioCases();
            File.WriteAllText("output/mobile-adaptation/controls.txt","PASS: "+checks+" assertions; tap, drag, pinch, finger identity, UI exclusion, cancellation, focus-reset, safe-area fitting, persistent volume. Device validation still required.");
            Debug.Log("Mobile adaptation controls PASS: "+checks);
        }catch(Exception e){File.WriteAllText("output/mobile-adaptation/controls.txt","FAIL: "+e);throw;}
    }
    static void GestureCases(){
        var g=new MapTouchGesture();Func<Vector2,bool> ui=p=>p.x<100&&p.y>500;
        Action<Touch[]> step=t=>g.Step(t,ui,12);
        step(new[]{T(3,TouchPhase.Began,200,200)});Check(!g.Tapped,"Touch down must not select a portal");
        step(new[]{T(3,TouchPhase.Ended,202,201)});Check(g.Tapped,"Small release selects portal");
        step(new[]{T(4,TouchPhase.Began,200,200)});step(new[]{T(4,TouchPhase.Moved,240,200,40)});
        Check(g.Panning&&g.Previous==new Vector2(200,200),"Drag delta is applied from original point");
        step(new[]{T(4,TouchPhase.Moved,200,200,-40)});step(new[]{T(4,TouchPhase.Ended,200,200)});
        Check(!g.Tapped,"Dragging away and back must not become a tap");
        step(Array.Empty<Touch>());step(new[]{T(1,TouchPhase.Began,200,200)});
        step(new[]{T(1,TouchPhase.Stationary,200,200),T(8,TouchPhase.Began,300,200)});Check(g.ZoomRatio==1,"Second finger down must not jump zoom");
        step(new[]{T(8,TouchPhase.Moved,350,200,50),T(1,TouchPhase.Moved,150,200,-50)});
        Check(Mathf.Abs(g.ZoomRatio-.5f)<.001f&&!g.Tapped,"Pinch ratio survives touch array reordering");
        step(new[]{T(1,TouchPhase.Ended,150,200),T(8,TouchPhase.Stationary,350,200)});
        step(new[]{T(8,TouchPhase.Moved,360,200,10)});Check(!g.Panning&&!g.Tapped,"Pinch to one finger cannot pan or select");
        step(new[]{T(8,TouchPhase.Ended,360,200)});Check(!g.Tapped,"Final pinch release cannot select");
        step(new[]{T(2,TouchPhase.Began,300,200)});step(new[]{T(2,TouchPhase.Ended,300,200)});Check(g.Tapped,"New tap works immediately after pinch release");
        step(new[]{T(2,TouchPhase.Began,20,600)});step(new[]{T(2,TouchPhase.Moved,200,300)});Check(!g.Panning,"UI-owned finger cannot drag the map");
        step(new[]{T(2,TouchPhase.Stationary,200,300),T(5,TouchPhase.Began,300,300)});
        step(new[]{T(2,TouchPhase.Moved,150,300,-50),T(5,TouchPhase.Moved,350,300,50)});Check(g.ZoomRatio==1,"UI-owned gesture cannot become a pinch");
        step(Array.Empty<Touch>());step(new[]{T(2,TouchPhase.Began,200,200)});step(new[]{T(2,TouchPhase.Canceled,200,200)});Check(!g.Tapped,"Cancellation cannot select");
        step(Array.Empty<Touch>());step(new[]{T(2,TouchPhase.Began,200,200)});g.Reset(true);step(new[]{T(2,TouchPhase.Ended,200,200)});Check(!g.Tapped,"Focus/scene reset cancels selection");
        step(Array.Empty<Touch>());step(new[]{T(9,TouchPhase.Moved,200,200)});Check(!g.Panning&&!g.Tapped,"No adoption of a finger held before map opened");
        step(Array.Empty<Touch>());step(new[]{T(9,TouchPhase.Began,300,600)});step(new[]{T(9,TouchPhase.Ended,300,600)});Check(g.Tapped,"Top area outside actual UI remains interactive");
    }
    static void LayoutCases(){
        foreach(var safe in new[]{new Rect(70,0,1490,720),new Rect(0,38,2340,1000),new Rect(40,20,720,360),new Rect(0,0,1920,1080)}){
            var fit=MobileUI.Fit(safe,new Vector2(1376,768));
            Check(fit.xMin>=safe.xMin-.01f&&fit.yMin>=safe.yMin-.01f&&fit.xMax<=safe.xMax+.01f&&fit.yMax<=safe.yMax+.01f,"Menu clipped outside safe area");
            Check(Mathf.Abs(fit.width/fit.height-1376f/768f)<.001f,"Menu aspect distorted");
        }
        Check(CoverMenu.HasPixelText("继续游戏"),"Resume caption requires portable glyphs");
        Check(Resources.Load<Texture2D>("ElementUI/SaveSlotGlyphs"),"Missing save/resume glyph atlas");
        foreach(var theme in new[]{"ice","grass","earth","fire"})foreach(var icon in new[]{"undo-normal","return","chart","pause-normal","sound-normal"})
            Check(Resources.Load<Texture2D>("ElementUI/"+theme+"-"+icon),"Missing mobile control art: "+theme+"-"+icon);
    }
    static void AudioCases(){
        const string key="BetweenPoles.MasterVolume",audible="BetweenPoles.AudibleVolume";
        bool had=PlayerPrefs.HasKey(key),hadAudible=PlayerPrefs.HasKey(audible);float saved=PlayerPrefs.GetFloat(key),savedAudible=PlayerPrefs.GetFloat(audible),original=AudioListener.volume;
        var field=typeof(GameAudioSettings).GetField("audible",BindingFlags.NonPublic|BindingFlags.Static);float previous=(float)field.GetValue(null);
        try{
            GameAudioSettings.SetVolume(.4f);GameAudioSettings.ToggleMute();Check(AudioListener.volume==0,"Mute failed");
            GameAudioSettings.ToggleMute();Check(Mathf.Abs(AudioListener.volume-.4f)<.001f,"Mute did not restore user volume");
            AudioListener.volume=1;typeof(GameAudioSettings).GetMethod("Load",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            Check(Mathf.Abs(AudioListener.volume-.4f)<.001f,"Volume not restored from preferences");
            GameAudioSettings.SetVolume(2);Check(AudioListener.volume==1,"Volume exceeds upper limit");
            GameAudioSettings.SetVolume(-1);Check(AudioListener.volume==0,"Volume below lower limit");
        }finally{
            if(had)PlayerPrefs.SetFloat(key,saved);else PlayerPrefs.DeleteKey(key);
            if(hadAudible)PlayerPrefs.SetFloat(audible,savedAudible);else PlayerPrefs.DeleteKey(audible);
            PlayerPrefs.Save();AudioListener.volume=original;field.SetValue(null,previous);
        }
    }
    public static void Mechanics(){
        Run();
        MobileMechanismAudit.Run();
    }
}
}
