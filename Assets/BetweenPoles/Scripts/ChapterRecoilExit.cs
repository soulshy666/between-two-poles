using UnityEngine;
namespace BetweenPoles {
// A deliberate chapter exit, separate from ordinary failed recoil flights.
public sealed class ChapterRecoilExit:MonoBehaviour {
    public string sourceRoom="muxk4c1ufa7uy";
    public int exitRow=18;
    public string destinationScene="Assets/BetweenPoles/Scenes/Chapter02_VerdantWorld.unity";
    public Vector2Int testCell=new Vector2Int(4,18);
    GridPlayground Board {get{return GetComponent<GridPlayground>();}}
    public bool Matches(Vector2Int from,Vector2Int direction){
        var tile=Board.TileAt(from);var owner=tile?tile.GetComponentInParent<IslandSurfaceAnchor>():null;
        return direction==Vector2Int.left&&from.y==exitRow&&owner&&owner.center&&owner.center.name==sourceRoom;
    }
    public bool JumpToFinalIsland(){
        var board=Board;if(board.Busy||BlackHoleTravel.InTransit)return false;
        var tile=board.TileAt(testCell);if(!tile||tile.blocked||board.MagnetAt(testCell))return false;
        board.player.SetPositionAndRotation(new Vector3(testCell.x*board.cellSize,tile.surfaceHeight,testCell.y*board.cellSize),Quaternion.LookRotation(Vector3.right));
        board.SendMessage("NotifyLanding",testCell);return true;
    }
    void OnGUI(){if(ChapterRecoilTravel.Active)return;
        if(!Debug.isDebugBuild)return;var old=GUI.matrix;float scale=Mathf.Max(.7f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        GUI.enabled=!Board.Busy&&!BlackHoleTravel.InTransit;
        if(GUI.Button(new Rect(22,74,200,38),"测试：冰雪末岛"))JumpToFinalIsland();
        GUI.enabled=true;GUI.matrix=old;
    }
}
}
