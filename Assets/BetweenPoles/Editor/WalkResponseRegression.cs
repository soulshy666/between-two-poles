using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class WalkResponseRegression {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Request="output/walk-response.run",Report="output/walk-response.txt";
    static WalkResponseRegression(){EditorApplication.update+=Poll;}
    static void Poll(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        File.Delete(Request);Run();
    }
    static object Call(GridPlayground board,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Private).Invoke(board,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static bool AdvanceCardinal(IEnumerator move,GridPlayground board){
        var before=board.player.position;bool running=move.MoveNext();var after=board.player.position;
        var delta=after-before;
        Check(Mathf.Abs(delta.x)<.00001f||Mathf.Abs(delta.z)<.00001f,"Diagonal displacement during a turn");
        Check(Mathf.Abs(after.x/board.cellSize-Mathf.Round(after.x/board.cellSize))<.00001f
            ||Mathf.Abs(after.z/board.cellSize-Mathf.Round(after.z/board.cellSize))<.00001f,"Player left grid centre lines");
        return running;
    }
    [MenuItem("两极之间/验证行走即时反向")]
    static void Run(){
        // An inactive, isolated preview scene never changes the user's live board.
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            var root=new GameObject("Walk response regression");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var board=root.AddComponent<GridPlayground>();
            board.player=new GameObject("Player").transform;board.player.SetParent(root.transform);
            var cells=new[]{Vector2Int.zero,Vector2Int.right,new Vector2Int(2,0),Vector2Int.up,Vector2Int.one,new Vector2Int(2,1)};
            board.magnets=new MagnetPiece[0];board.tiles=new GridTile[cells.Length];
            for(int i=0;i<cells.Length;i++){
                var tile=new GameObject("Tile "+i);tile.transform.SetParent(root.transform);
                tile.transform.position=new Vector3(cells[i].x*board.cellSize,0,cells[i].y*board.cellSize);board.tiles[i]=tile.AddComponent<GridTile>();
            }
            // Use the existing deterministic 60 Hz movement clock.
            typeof(GridPlayground).GetField("recordingPush",Private).SetValue(board,true);
            int landings=0;GridTile landed=null;board.Landed+=tile=>{landings++;landed=tile;};
            foreach(int frames in new[]{1,5,11}){
                board.player.SetPositionAndRotation(Vector3.zero,Quaternion.LookRotation(Vector3.left));
                landings=0;landed=null;
                var move=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);
                for(int i=0;i<frames;i++)Check(move.MoveNext()&&move.Current==null,"Walk waited for a turn before moving");
                var before=board.player.position;
                Check(before.x>0&&before.x<board.cellSize,"First-frame movement or intermediate position invalid");
                float facingBefore=Quaternion.Angle(board.player.rotation,Quaternion.LookRotation(Vector3.left));
                Check(board.TryStep(Vector2Int.left),"Reverse input rejected");
                Check(board.player.position==before,"Reverse teleported the player");
                Check(Quaternion.Angle(board.player.rotation,Quaternion.LookRotation(Vector3.left))<facingBefore||facingBefore<.01f,"Reverse facing was delayed");
                move.MoveNext();Check(board.player.position.x<before.x,"Reverse continued toward the old destination");
                int guard=100;while(move.MoveNext())Check(--guard>0,"Reversed walk never finished");
                Check(board.player.position==Vector3.zero&&!board.Busy,"Reverse did not finish at the origin");
                Check(landings==1&&landed==board.tiles[0],"Reverse notified the wrong landing tile");
            }
            var repeat=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);
            for(int i=0;i<6;i++)repeat.MoveNext();
            Check(board.TryStep(Vector2Int.left),"First repeated reversal rejected");repeat.MoveNext();
            var pivot=board.player.position;
            Check(board.TryStep(Vector2Int.right),"Second repeated reversal rejected");repeat.MoveNext();
            Check(board.player.position.x>pivot.x,"Latest direction did not win");
            int remaining=100;while(repeat.MoveNext())Check(--remaining>0,"Repeated reversal never finished");
            Check(board.player.position==board.tiles[1].transform.position&&!board.Busy,"Repeated reversal landed incorrectly");
            board.player.position=Vector3.zero;
            var turn=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);turn.MoveNext();turn.MoveNext();
            var turnStart=board.player.position;
            Check(board.TryStep(Vector2Int.up),"Open-ground turn rejected");
            Check(board.player.position==turnStart,"Turn teleported the player");
            AdvanceCardinal(turn,board);Check(board.player.position.z==turnStart.z,"Turn cut diagonally before reaching the centre");
            remaining=100;while(AdvanceCardinal(turn,board))Check(--remaining>0,"Turn never finished");
            Check(board.PlayerCell==Vector2Int.one&&!board.Busy,"Turn missed target centre");
            board.player.position=Vector3.zero;board.tiles[3].blocked=true;
            var blocked=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);blocked.MoveNext();
            board.TryStep(Vector2Int.up);blocked.MoveNext();
            Check(board.player.position.z==0,"Turn cut through the blocked corner");
            Call(board,"ClearMovementInput");remaining=100;while(blocked.MoveNext())Check(--remaining>0,"Blocked turn never settled");
            board.tiles[3].blocked=false;board.player.position=Vector3.zero;
            var continuous=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);continuous.MoveNext();
            Call(board,"PumpMovementInput",Vector2Int.right,false);
            var pose=board.player.GetComponent<PlayerPushPose>();
            bool crossed=false;remaining=100;
            while(continuous.MoveNext()){
                Check(--remaining>0,"Continuous walk never stopped");
                if(board.player.position.x>=board.cellSize){crossed=true;Check((bool)typeof(PlayerPushPose).GetField("walking",Private).GetValue(pose),"Gait reset between cells");}
            }
            Check(crossed&&board.player.position==board.tiles[2].transform.position&&!board.Busy,"Buffered walk did not stop at the second cell");
            board.player.position=Vector3.zero;
            var spam=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);AdvanceCardinal(spam,board);
            var directions=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down,Vector2Int.up};
            foreach(var direction in directions){
                Check(board.Busy,"Rapid-input fixture ended early");
                board.TryStep(direction);AdvanceCardinal(spam,board);
            }
            remaining=100;while(AdvanceCardinal(spam,board))Check(--remaining>0,"Rapid-input walk never settled");
            Check(!board.Busy&&Vector3.Distance(board.player.position,new Vector3(board.PlayerCell.x*board.cellSize,0,board.PlayerCell.y*board.cellSize))<.00001f,"Rapid inputs did not settle at a cell centre");
            int normalFrames=0,tapFrames=0;
            foreach(bool fresh in new[]{false,true}){
                Call(board,"ClearMovementInput");board.player.position=Vector3.zero;
                var accelerated=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);accelerated.MoveNext();
                var beforeTap=board.player.position;
                Call(board,"PumpMovementInput",Vector2Int.right,fresh);
                Check(board.player.position==beforeTap,"Fresh tap teleported player");
                float gaitBefore=(float)typeof(PlayerPushPose).GetField("walkDistance",Private).GetValue(pose);
                AdvanceCardinal(accelerated,board);
                float travelled=Vector3.Distance(beforeTap,board.player.position);
                Check(travelled<=board.cellSize/board.walkSeconds*board.maxWalkSpeedMultiplier/60f+.00001f,"Walking exceeded speed cap");
                float gaitAfter=(float)typeof(PlayerPushPose).GetField("walkDistance",Private).GetValue(pose);
                Check(Mathf.Abs(gaitAfter-gaitBefore-travelled)<.00001f,"Gait did not follow accelerated movement");
                if(fresh)Check(travelled>board.cellSize/board.walkSeconds/60f*1.1f,"Fresh tap did not accelerate");
                else Check(Mathf.Abs(travelled-board.cellSize/board.walkSeconds/60f)<.00001f,"Held input accelerated");
                int frames=1;while(AdvanceCardinal(accelerated,board))Check(++frames<100,"Tap walk stuck");
                Check(board.PlayerCell==new Vector2Int(2,0)&&!board.Busy,"Tap did not finish exactly two steps");
                if(fresh)tapFrames=frames;else normalFrames=frames;
            }
            Check(tapFrames<normalFrames,"Fresh tap throughput unchanged");
            Call(board,"ClearMovementInput");board.player.position=Vector3.zero;board.tiles[2].blocked=true;
            var wall=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);wall.MoveNext();
            Call(board,"PumpMovementInput",Vector2Int.right,true);
            remaining=100;while(AdvanceCardinal(wall,board))Check(--remaining>0,"Accelerated wall approach stuck");
            Check(board.PlayerCell==Vector2Int.right&&!board.Busy,"Accelerated walk crossed obstacle");
            board.tiles[2].blocked=false;Call(board,"ClearMovementInput");
            board.player.position=Vector3.zero;board.CaptureInitialState();
            var undoWalk=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);undoWalk.MoveNext();
            Call(board,"PumpMovementInput",Vector2Int.right,true);
            remaining=100;while(undoWalk.MoveNext())Check(--remaining>0,"Undo setup stuck");
            Check(board.UndoCount==1&&board.UndoStep()&&board.PlayerCell==Vector2Int.right,"Accelerated continuation lost per-cell undo");
            File.WriteAllText(Report,"PASS: same-direction fresh taps accelerate; held timing unchanged; gait tracks actual distance; no teleport or obstacle bypass; per-cell undo; reversals; cardinal turns; uninterrupted gait; exact landing and unlock.");
        }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);Debug.LogException(ex);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
