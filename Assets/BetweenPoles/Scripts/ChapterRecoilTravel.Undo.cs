using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
public sealed partial class ChapterRecoilTravel {
    static ChapterRecoilTravel activeFlight;
    public static int UndoInputFrame {get;private set;}=-1;
    GridPlayground arrivalBoard;
    readonly List<GameObject> movedRoots=new List<GameObject>();
    readonly List<GameObject> suspendedSourceRoots=new List<GameObject>();
    string savedView,savedIsland;
    Vector3 savedCameraPosition,savedDisplayOrigin,savedCurveOrigin,savedRotationOrigin;
    Quaternion savedCameraRotation;
    bool savedFixedOrigin;
    Transform undoSun;
    Vector3 undoSunPosition,undoSunScale;
    Quaternion undoSunRotation;

    void CaptureUndoView(){
        // Serialized settings include the original planet and island references;
        // coordinate-frame fields are intentionally nonserialized and saved separately.
        savedView=JsonUtility.ToJson(view);savedIsland=JsonUtility.ToJson(view.island);
        savedCameraPosition=view.transform.position;savedCameraRotation=view.transform.rotation;
        savedFixedOrigin=view.fixedDisplayOrigin;savedDisplayOrigin=view.displayOrigin;
        savedCurveOrigin=view.curveOrigin;savedRotationOrigin=view.rotationOrigin;
        flightBackground=VisibleBackground(source.gameObject.scene);
        if(flightBackground){
            flightBackground.EnsureData();originalBackground=flightBackground.background;
            originalPalette=(Color[])flightBackground.spaceColors.Clone();
            undoSun=FindBackgroundSun(flightBackground);
            if(undoSun){undoSunPosition=undoSun.localPosition;undoSunScale=undoSun.localScale;undoSunRotation=undoSun.localRotation;}
        }
    }
    void Update(){
        if(activeFlight==this&&Active&&Time.timeScale>0&&Input.GetKeyDown(KeyCode.Z))TryUndoFlight();
    }
    void OnGUI(){
        if(MobileUI.Enabled&&!TelevisionSceneTransition.Busy&&activeFlight==this&&Active&&Time.timeScale>0)
            if(MobileUI.ActionIcon("flight-undo","undo",true))TryUndoFlight();
    }
    public static bool TryUndoFlight(){
        if(!Active||!activeFlight||!activeFlight.source||activeFlight.source.UndoCount==0)return false;
        UndoInputFrame=Time.frameCount;
        activeFlight.RestoreDeparture();return true;
    }
    void RestoreDeparture(){
        if(flightTitle)flightTitle.Cancel();
        StopAllCoroutines();
        if(arrivalBoard){arrivalBoard.StopAllCoroutines();arrivalBoard.ReleaseChapterFlight();arrivalBoard.enabled=false;}
        var scene=source.gameObject.scene;
        // Actors and shared presentation must leave the destination before it unloads.
        view.island.enabled=false;
        foreach(var root in movedRoots)if(root)SceneManager.MoveGameObjectToScene(root,scene);
        if(magnet&&magnet.gameObject.scene!=scene){magnet.transform.SetParent(null,true);SceneManager.MoveGameObjectToScene(magnet.gameObject,scene);}
        if(arrivalBoard&&destination.IsValid()&&destination.isLoaded)
            foreach(var root in destination.GetRootGameObjects())root.SetActive(false);
        JsonUtility.FromJsonOverwrite(savedView,view);
        JsonUtility.FromJsonOverwrite(savedIsland,view.island);
        view.ResumeFlight();view.fixedDisplayOrigin=savedFixedOrigin;view.displayOrigin=savedDisplayOrigin;
        view.curveOrigin=savedCurveOrigin;view.rotationOrigin=savedRotationOrigin;
        view.ResetSurfaceFrame();view.ResetZoomVelocity();
        view.transform.SetPositionAndRotation(savedCameraPosition,savedCameraRotation);
        view.GetComponent<Camera>().orthographicSize=FlightSize;
        if(flightBackground&&originalPalette!=null){flightBackground.background=originalBackground;flightBackground.spaceColors=(Color[])originalPalette.Clone();}
        if(undoSun){undoSun.localPosition=undoSunPosition;undoSun.localScale=undoSunScale;undoSun.localRotation=undoSunRotation;}
        SceneManager.SetActiveScene(scene);
        foreach(var root in suspendedSourceRoots)if(root)root.SetActive(true);
        source.enabled=true;SharedPlayer.Bind(source,true);
        if(magnet){var anchor=magnet.GetComponent<IslandSurfaceAnchor>();if(anchor)anchor.ground=source;}
        view.island.enabled=true;
        // Clear the flight owner before invoking normal undo, avoiding recursion.
        activeFlight=null;Active=false;FlightView=null;releasedControl=true;
        source.UndoStep();
        view.island.SelectCurrentIsland();
        if(view.island.CurrentIsland)view.island.FocusIsland(view.DisplayFocus(view.island.CurrentIsland.position));
        view.Apply(view.transform.position-view.island.viewingOffset);
        if(changedLoadingPriority){Application.backgroundLoadingPriority=previousLoadingPriority;changedLoadingPriority=false;}
        // Loading cannot be cancelled. Keep its hide callback until completion,
        // then unload only the flight's destination. Block duplicate preloads meanwhile.
        prepared=this;StartCoroutine(ReleaseCancelledDestination());
    }
    IEnumerator ReleaseCancelledDestination(){
        if(loading!=null)while(!loading.isDone)yield return null;
        RestoreLoadingPriority();
        sleepingRoots.Clear();
        SceneManager.sceneLoaded-=HideDestination;
        if(destination.IsValid()&&destination.isLoaded&&destination!=SceneManager.GetActiveScene()){
            var unload=SceneManager.UnloadSceneAsync(destination);if(unload!=null)yield return unload;
        }
        if(prepared==this)prepared=null;
        Destroy(gameObject);
    }
}
}
