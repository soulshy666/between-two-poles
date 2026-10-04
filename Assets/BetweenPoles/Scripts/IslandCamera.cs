using UnityEngine;

namespace BetweenPoles {
// Frame the current island, not the midpoint between both islands or each player step.
[RequireComponent(typeof(Camera))]
public sealed class IslandCamera : MonoBehaviour {
    public Transform player;
    public Transform[] islandCenters;
    public GridPlayground board;
    public static readonly Vector3 DefaultViewingOffset = new Vector3(1.5f,15,-12.5f);
    [Tooltip("斜俯视并略偏向侧面，便于辨认竖立磁铁的形状。")]
    public Vector3 viewingOffset = DefaultViewingOffset;
    public float transitionSeconds = .65f;
    Vector3 focus, velocity;
    int current;
    void OnEnable(){if(board)board.Landed+=OnLanded;}
    void OnDisable(){if(board)board.Landed-=OnLanded;}
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
        focus=Vector3.SmoothDamp(focus,islandCenters[current].position,ref velocity,transitionSeconds);
        Apply();
    }
    public void ApplyView(Vector3 target){
        transform.position=target+viewingOffset;
        transform.rotation=Quaternion.LookRotation(-viewingOffset,Vector3.up);
    }
    void Apply(){ApplyView(focus);}
}
}
