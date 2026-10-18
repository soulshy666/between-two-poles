using UnityEngine;
namespace BetweenPoles {
// A deliberate chapter exit, separate from ordinary failed recoil flights.
public sealed class ChapterRecoilExit:MonoBehaviour {
    public string sourceRoom="muxk4c1ufa7uy";
    public int exitRow=18;
    public Vector2Int exitDirection=Vector2Int.left;
    public int exitColumn;
    public string destinationScene="Assets/BetweenPoles/Scenes/Chapter02_VerdantWorld.unity";
    public Vector2Int testCell=new Vector2Int(4,18);
    float preloadAfter;
    void OnEnable(){preloadAfter=Time.unscaledTime+8f;}
    void Update(){
        if(!Application.isPlaying||Time.unscaledTime<preloadAfter)return;
        preloadAfter=Time.unscaledTime+2f;
        var board=Board;
        if(!board||!board.enabled||board.Busy||board.OpeningCinematic||GameSaveSlots.Restoring
            ||ElementGameUI.Paused||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit
            ||ChapterRecoilTravel.Active||ChapterMapReturn.HasSuspendedGame
            ||gameObject.scene!=UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            ||ChapterAtlas.IsInspectionScene(gameObject.scene))return;
        ChapterRecoilTravel.Preload(this);
    }
    GridPlayground Board {get{return GetComponent<GridPlayground>();}}
    public bool Matches(Vector2Int from,Vector2Int direction){
        var tile=Board.TileAt(from);var owner=tile?tile.GetComponentInParent<IslandSurfaceAnchor>():null;
        return direction==exitDirection&&(exitDirection.x!=0?from.y==exitRow:from.x==exitColumn)
            &&owner&&owner.center&&owner.center.name==sourceRoom;
    }
    public bool JumpToFinalIsland(){return JumpToFinalIsland(Board);}
    // Debug navigation uses the current chapter's room numbering, not the ice exit cell.
    public static bool JumpToFinalIsland(GridPlayground board){
        if(!Application.isPlaying||!board||!board.enabled||board.Busy||board.OpeningCinematic
            ||ElementGameUI.Paused||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit||ChapterRecoilTravel.Active
            ||board.gameObject.scene!=UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            ||ChapterAtlas.IsInspectionScene(board.gameObject.scene))return false;
        var window=board.GetComponent<FiveIslandWindow>();
        if(!window)foreach(var candidate in FindObjectsOfType<FiveIslandWindow>())if(candidate.board==board){window=candidate;break;}

        FiveIslandWindow.Room last=null;int number=-1;
        for(int i=0;window&&i<window.rooms.Length;i++){
            var room=window.rooms[i];int rank=room.roomNumber>0?room.roomNumber:i+1;
            if(room.center&&rank>number){number=rank;last=room;}
        }
        // Older prototype chapters have a single anchored island and no room list.
        // Only accept that unambiguous fallback; never jump to an arbitrary island.
        if(last==null){
            Transform only=null;
            foreach(var tile in board.tiles){
                var anchor=tile?tile.GetComponentInParent<IslandSurfaceAnchor>():null;
                if(!anchor||!anchor.center)continue;
                if(only&&only!=anchor.center)return false;
                only=anchor.center;
            }
            if(!only)return false;
            last=new FiveIslandWindow.Room{center=only};
        }
        GridTile landing=null;Vector2Int landingCell=Vector2Int.zero;float distance=float.PositiveInfinity;
        var exit=board.GetComponent<ChapterRecoilExit>();
        foreach(var tile in board.tiles){
            if(!tile||!tile.gameObject.activeInHierarchy||tile.blocked||tile.goal
                ||tile.GetComponentInChildren<BlackHolePortal>(true))continue;
            var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();if(!owner||owner.center!=last.center)continue;
            var cell=new Vector2Int(Mathf.RoundToInt(tile.transform.position.x/board.cellSize),Mathf.RoundToInt(tile.transform.position.z/board.cellSize));
            if(board.MagnetAt(cell))continue;
            float score=(tile.transform.position-last.center.position).sqrMagnitude;
            if(exit&&cell==exit.testCell)score=-1;
            if(score<distance){distance=score;landing=tile;landingCell=cell;}
        }
        if(!landing)return false;
        board.player.SetPositionAndRotation(new Vector3(landingCell.x*board.cellSize,landing.surfaceHeight,landingCell.y*board.cellSize),Quaternion.LookRotation(Vector3.right));
        board.SendMessage("NotifyLanding",landingCell);return true;
    }
}
}
