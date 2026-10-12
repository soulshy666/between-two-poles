using UnityEngine;

namespace BetweenPoles {
// Frame the current island, not the midpoint between both islands or each player step.
[RequireComponent(typeof(Camera))]
public sealed class IslandCamera : MonoBehaviour {
    public Transform player;
    public Transform[] islandCenters;
    public GridPlayground board;
    public static readonly Vector3 DefaultViewingOffset = new Vector3(0,15.4f,-12f);
    [Tooltip("约52度俯角，露出人物正面，保持左右方向端正。")]
    public Vector3 viewingOffset = DefaultViewingOffset;
    public float transitionSeconds = .65f;
    [Range(0f,.05f),Tooltip("吸合震动幅度，占一格宽度的比例。")]
    public float mergeShakeCellRatio=.01f;
    [Range(.05f,.4f),Tooltip("吸合震动持续时间。")]
    public float mergeShakeSeconds=.15f;
    float mergeShakeStart,mergeShakeAmplitude;
    Vector3 mergeShakeDirection,unshakenPosition;
    bool mergeShakeActive,shakeApplied;
    Vector3 focus, velocity;
    int current;
    public Transform CurrentIsland {get{return islandCenters!=null&&current>=0&&current<islandCenters.Length?islandCenters[current]:null;}}
    void OnEnable(){if(board)board.Landed+=OnLanded;}
    void OnDisable(){if(board)board.Landed-=OnLanded;CancelMergeShake();}
    public void PlayMergeShake(Vector3 direction,float cellSize){
        if(!Application.isPlaying||!isActiveAndEnabled)return;
        RestoreShakePosition();
        // Screen-plane translation only: never rotate the camera or change its focus.
        direction=Vector3.ProjectOnPlane(direction,transform.forward);
        mergeShakeDirection=direction.sqrMagnitude>.0001f?direction.normalized:transform.right;
        mergeShakeAmplitude=Mathf.Max(0,cellSize)*Mathf.Clamp(mergeShakeCellRatio,0,.05f);
        mergeShakeStart=Time.time;mergeShakeActive=mergeShakeAmplitude>0;
    }
    public void CancelMergeShake(){mergeShakeActive=false;RestoreShakePosition();}
    void RestoreShakePosition(){if(shakeApplied){transform.position=unshakenPosition;shakeApplied=false;}}
    // The project uses the built-in renderer. Apply only during rendering, after
    // island/curved-camera framing, and restore before gameplay reads the camera.
    void OnPreCull(){
        RestoreShakePosition();
        if(!mergeShakeActive)return;
        float t=(Time.time-mergeShakeStart)/Mathf.Max(.01f,mergeShakeSeconds);
        if(t>=1){mergeShakeActive=false;return;}
        float pulse=Mathf.Cos(t*Mathf.PI*3f)*(1-t)*(1-t);
        unshakenPosition=transform.position;shakeApplied=true;
        transform.position+=mergeShakeDirection*(mergeShakeAmplitude*pulse);
    }
    void OnPostRender(){RestoreShakePosition();}
    void OnLanded(GridTile tile){
        if(!tile||!tile.gameObject.activeInHierarchy||islandCenters==null)return;
        var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();
        if(owner&&owner.center){for(int i=0;i<islandCenters.Length;i++)if(islandCenters[i]==owner.center){current=i;return;}}
        current=Nearest();
    }
    void Start() {
        if(islandCenters==null||islandCenters.Length==0)return;
        current=Nearest();
        if(board)foreach(var tile in board.tiles)if(tile&&tile.gameObject.activeInHierarchy){Vector3 d=tile.transform.position-player.position;d.y=0;if(d.sqrMagnitude<.1f){OnLanded(tile);break;}}
        focus=islandCenters[current].position;Apply();
    }
    public void SelectCurrentIsland(){current=Nearest();}
    public void FocusIsland(Vector3 point){PreviewNavigation(point);focus=point;velocity=Vector3.zero;Apply();}
    public void PreviewNavigation(Vector3 point){
        if(islandCenters==null)return;float best=float.PositiveInfinity;
        for(int i=0;i<islandCenters.Length;i++)if(islandCenters[i]){float d=(islandCenters[i].position-point).sqrMagnitude;if(d<best){best=d;current=i;}}
    }
    // Coordinate-frame handoff only; normal island tracking and easing stay in charge.
    public void RebaseFocus(Vector3 delta){focus+=delta;Apply();}
    int Nearest() {
        int selected=0;float best=float.PositiveInfinity;
        for(int i=0;i<islandCenters.Length;i++){
            if(!islandCenters[i])continue;
            float d=(islandCenters[i].position-player.position).sqrMagnitude;
            if(d<best){best=d;selected=i;}
        }
        return selected;
    }
    void LateUpdate() {
        if(!player||islandCenters==null||islandCenters.Length==0)return;
        // The landing event changes the target once; movement on a bridge never selects an island.
        var curved=GetComponent<CurvedTrialCamera>();
        var target=board&&board.RecoilFlying?board.RecoilCameraFocus:curved?curved.DisplayFocus(islandCenters[current].position):islandCenters[current].position;
        focus=Vector3.SmoothDamp(focus,target,ref velocity,board&&board.RecoilFlying?.12f:transitionSeconds);
        Apply();
    }
    public void ApplyView(Vector3 target){
        // Migrate the previous overhead preset while keeping yaw and roll at zero.
        if((viewingOffset-new Vector3(0,17.7f,-8.25f)).sqrMagnitude<.001f
            ||(viewingOffset-new Vector3(0,13.05f,-14.5f)).sqrMagnitude<.001f
            ||(Mathf.Abs(viewingOffset.x)<.001f&&Mathf.Abs(viewingOffset.z)<.001f))viewingOffset=DefaultViewingOffset;
        transform.position=target+viewingOffset;
        transform.rotation=Quaternion.LookRotation(-viewingOffset,Mathf.Abs(viewingOffset.normalized.y)>.999f?Vector3.forward:Vector3.up);
    }
    void Apply(){ApplyView(focus);}
}
}
