using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class StarCollectionRegression {
    static int stage;static double deadline;static GridPlayground board;static Transform star;static GameObject holder;static Vector3 position;static bool background;
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
                position=board.player.position;
                holder=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Art/Collectibles/GoldenStar.prefab"));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder,board.gameObject.scene);holder.transform.position=position;
                star=holder.GetComponentInChildren<MeshFilter>().transform;
                board.SendMessage("NotifyLanding",board.PlayerCell);
                if(!board.Busy)throw new Exception("Collection did not lock input");stage=2;deadline=EditorApplication.timeSinceStartup+10;return;
            }
            if(stage==2){
                if(board.Busy)return;
                if(star.gameObject.activeSelf)throw new Exception("Collected star remains visible");
                if(board.player.position!=position)throw new Exception("Animation changed grid position");
                board.SendMessage("NotifyLanding",board.PlayerCell);
                if(board.Busy)throw new Exception("Repeated collection");
                File.WriteAllText("output/star-test.txt","PASS: actual golden star prefab collected through landing; movement locked during animation, star hidden, player position retained, no duplicate collection, input released.");Finish();
            }
        }catch(Exception ex){File.WriteAllText("output/star-test.txt","FAIL: "+ex);Finish();}
    }
    static void Finish(){if(holder)UnityEngine.Object.DestroyImmediate(holder);Application.runInBackground=background;stage=0;if(SessionState.GetBool("StarTest.OwnPlay",false)){SessionState.SetBool("StarTest.OwnPlay",false);EditorApplication.isPlaying=false;}}
}
}
