using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
[Serializable] public sealed class GameSlotData {
    public int version=1;
    public bool hasCheckpoint;
    public string scene="Chapter01_IceWorld";
    public double seconds;
    public string[] explored=new string[0],discoveryAudio=new string[0];
    public GridPlayground.DiskState world;
}
// Files live outside Assets and the installation, so updates do not replace progress.
public sealed class GameSaveSlots:MonoBehaviour {
    static GameSaveSlots instance;
    static int activeSlot=-1;
    static GameSlotData current;
    static bool restoring;
    float nextSave;
    public static string LastError {get;private set;}
    public static bool Restoring {get{return restoring;}}
#if UNITY_EDITOR
    internal static string TestDirectory;
#endif
    public static string DirectoryPath {get{
#if UNITY_EDITOR
        if(!string.IsNullOrEmpty(TestDirectory))return TestDirectory;
#endif
        return Path.Combine(Application.persistentDataPath,"Saves");
    }}
    static string FilePath(int slot){if(slot<0||slot>2)throw new ArgumentOutOfRangeException("slot");return Path.Combine(DirectoryPath,"slot-"+(slot+1)+".json");}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){instance=null;activeSlot=-1;current=null;restoring=false;LastError=null;}
    static void Ensure(){if(instance)return;instance=new GameObject("三个存档 · 自动保存").AddComponent<GameSaveSlots>();DontDestroyOnLoad(instance.gameObject);}
    public static bool Exists(int slot){return File.Exists(FilePath(slot));}
    public static bool Read(int slot,out GameSlotData data){
        data=null;LastError=null;
        try{if(!Exists(slot))return false;data=JsonUtility.FromJson<GameSlotData>(File.ReadAllText(FilePath(slot)));if(data==null||data.version!=1||!ValidScene(data.scene)||(data.hasCheckpoint&&(data.world==null||data.world.pieces==null||data.world.authoredCount<=0)))throw new InvalidDataException("Unsupported save data");return true;}
        catch(Exception e){LastError="存档读取失败";Debug.LogWarning("Save read: "+e.Message);return false;}
    }
    static bool ValidScene(string scene){return scene=="Chapter01_IceWorld"||scene=="Chapter02_VerdantWorld"||scene=="Chapter03_RingWorld"||scene=="Chapter04_LavaWorld";}
    static void Write(int slot,GameSlotData data){
        Directory.CreateDirectory(DirectoryPath);string path=FilePath(slot),temp=path+".tmp";
        File.WriteAllText(temp,JsonUtility.ToJson(data));
        if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    }
    public static bool Delete(int slot){
        try{File.Delete(FilePath(slot));if(activeSlot==slot){activeSlot=-1;current=null;}LastError=null;return true;}
        catch(Exception e){LastError="删除失败";Debug.LogWarning("Save delete: "+e.Message);return false;}
    }
    public static bool Select(int slot,out string scene){
        scene=null;GameSlotData data;
        if(Exists(slot)){if(!Read(slot,out data))return false;}
        else{data=new GameSlotData();try{Write(slot,data);}catch(Exception e){LastError="存档保存失败";Debug.LogWarning(e.Message);return false;}}
        Ensure();activeSlot=slot;current=data;restoring=data.hasCheckpoint;scene=data.scene;
        SessionExploration.Import(data.explored);IslandDiscoveryAudio.ImportVisits(data.discoveryAudio);SessionPlayTime.Restore(data.seconds);
        ChapterAtlas.DestinationIsland=-1;ChapterAtlas.DestinationScene=null;ChapterAtlas.DestinationRoom=null;ChapterAtlas.ReturnChapter=-1;
        return true;
    }
    void OnEnable(){SceneManager.sceneLoaded+=Loaded;}
    void OnDisable(){SceneManager.sceneLoaded-=Loaded;}
    void Loaded(Scene scene,LoadSceneMode mode){
        if(scene.name=="Cover"){activeSlot=-1;current=null;restoring=false;return;}
        if(restoring&&current!=null&&scene.name==current.scene&&!ChapterAtlas.IsInspectionScene(scene))StartCoroutine(Apply(scene));
    }
    IEnumerator Apply(Scene scene){
        // Authored Start methods first configure room windows and the initial checkpoint.
        yield return null;yield return null;yield return null;
        GridPlayground board=null;foreach(var root in scene.GetRootGameObjects()){board=root.GetComponentInChildren<GridPlayground>(true);if(board)break;}
        try{if(!board)throw new InvalidDataException("Missing board");board.RestoreDiskState(current.world);SessionExploration.Import(current.explored);SessionPlayTime.Restore(current.seconds);}
        catch(Exception e){LastError="存档读取失败";Debug.LogError("Save restore: "+e);activeSlot=-1;}
        finally{restoring=false;nextSave=Time.unscaledTime+3;}
    }
    void Update(){if(Time.unscaledTime<nextSave)return;nextSave=Time.unscaledTime+3;SaveNow();}
    public static void SaveNow(){
        if(activeSlot<0||current==null||restoring)return;
        try{
            // Keep the last grounded checkpoint while a map/flight is being presented.
            if(!ChapterRecoilTravel.Active&&!BlackHoleTravel.InTransit&&!BlackHoleTravel.Selecting&&!ChapterMapReturn.HasSuspendedGame){
                foreach(var board in FindObjectsOfType<GridPlayground>()){
                    if(!board.enabled||!ValidScene(board.gameObject.scene.name)||ChapterAtlas.IsInspectionScene(board.gameObject.scene)||!board.CanSaveToDisk)continue;
                    current.world=board.CaptureDiskState();current.hasCheckpoint=true;current.scene=board.gameObject.scene.name;break;
                }
            }
            current.seconds=SessionPlayTime.Seconds;current.explored=SessionExploration.Export();current.discoveryAudio=IslandDiscoveryAudio.ExportVisits();
            Write(activeSlot,current);LastError=null;
        }catch(Exception e){LastError="存档保存失败";Debug.LogWarning("Save write: "+e.Message);}
    }
    void OnApplicationPause(bool value){if(value)SaveNow();}
    void OnApplicationQuit(){SaveNow();}
}
}
