using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class StarCollectionRegression {
    static int stage;static double deadline;static GridPlayground board;static Transform star;static GameObject holder;static Vector3 position,starStart;static bool background,sawEarlyPickup;
    static StarCollectionRegression(){EditorApplication.update+=Tick;}
    [MenuItem("两极之间/验证星星收集动画")]
    static void Run(){File.WriteAllText("output/star-test.run","");}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try{
            if(stage==0){
                if(!File.Exists("output/star-test.run"))return;
                if(!EditorApplication.isPlaying){SessionState.SetBool("StarTest.OwnPlay",true);EditorApplication.isPlaying=true;return;}
                File.Delete("output/star-test.run");stage=1;deadline=EditorApplication.timeSinceStartup+40;background=Application.runInBackground;Application.runInBackground=true;
            }
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout at "+stage);
            if(stage==1){
                board=UnityEngine.Object.FindObjectOfType<GridPlayground>();if(!board||board.Busy||board.OpeningCinematic)return;
                Vector2Int direction=Vector2Int.zero;
                foreach(var candidate in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
                    var tile=board.TileAt(board.PlayerCell+candidate);
                    if(tile&&!tile.blocked&&!board.MagnetAt(board.PlayerCell+candidate)&&Mathf.Abs(tile.surfaceHeight-board.player.position.y)<.01f&&!tile.GetComponentInChildren<BlackHolePortal>()){direction=candidate;break;}
                }
                if(direction==Vector2Int.zero)throw new Exception("Need an empty neighboring floor cell");
                position=board.player.position+new Vector3(direction.x*board.cellSize,0,direction.y*board.cellSize);sawEarlyPickup=false;
                holder=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Art/Collectibles/GoldenStar.prefab"));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder,board.gameObject.scene);holder.transform.position=position;
                star=holder.GetComponentInChildren<MeshFilter>().transform;
                starStart=star.position;
                var step=typeof(GridPlayground).GetMethod("TryStep",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);
                if(!(bool)step.Invoke(board,new object[]{direction}))throw new Exception("Could not step toward star");
                if(!board.Busy)throw new Exception("Collection did not lock input");stage=2;deadline=EditorApplication.timeSinceStartup+10;return;
            }
            if(stage==2){
                if(board.Busy){
                    float distance=Vector3.Distance(board.player.position,position);
                    if(distance>board.cellSize*.65f&&Vector3.Distance(star.position,starStart)>.001f)throw new Exception("Star moved before player reached it");
                    if(distance>.1f&&Vector3.Distance(star.position,starStart)>.02f)sawEarlyPickup=true;return;
                }
                if(!sawEarlyPickup)throw new Exception("Star was not picked up before reaching destination");
                if(star.gameObject.activeSelf)throw new Exception("Collected star remains visible");
                if(board.player.position!=position)throw new Exception("Animation changed grid position");
                board.SendMessage("NotifyLanding",board.PlayerCell);
                if(board.Busy)throw new Exception("Repeated collection");
                File.WriteAllText("output/star-test.txt","PASS: walking toward golden star starts pickup before destination; animation locks input, finishes on target cell, hides star, prevents duplicate collection and releases input.");Finish();
            }
        }catch(Exception ex){File.WriteAllText("output/star-test.txt","FAIL: "+ex);Finish();}
    }
    static void Finish(){if(holder)UnityEngine.Object.DestroyImmediate(holder);Application.runInBackground=background;stage=0;if(SessionState.GetBool("StarTest.OwnPlay",false)){SessionState.SetBool("StarTest.OwnPlay",false);EditorApplication.isPlaying=false;}}
}
}
