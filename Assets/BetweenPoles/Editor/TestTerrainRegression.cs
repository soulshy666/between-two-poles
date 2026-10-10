using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class TestTerrainRegression {
    const string Request="output/test-terrain.run",Report="output/test-terrain-result.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static TestTerrainRegression(){
        EditorApplication.delayCall+=()=>{if(File.Exists(Request)){File.Delete(Request);if(!Application.isPlaying){SessionState.SetBool("TerrainTest.Play",true);EditorApplication.isPlaying=true;}else Run();}};
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TerrainTest.Play",false)){
            double ready=EditorApplication.timeSinceStartup+1;EditorApplication.CallbackFunction tick=null;
            tick=()=>{if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=tick;Run();};EditorApplication.update+=tick;
        }};
    }
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Run(){
        try{
            var lab=UnityEngine.Object.FindObjectOfType<MagnetTestLayouts>();Check(lab,"Open mechanism test scene");lab.Load(0);var board=lab.board;
            string message;int count=board.tiles.Length;var cell=new Vector2Int(5,0);
            Check(lab.EditTile(cell,true,out message)&&board.tiles.Length==count+1,"Add failed: "+message);
            var added=board.TileAt(cell);Check(added&&added.surfaceHeight==0&&added.GetComponent<IslandSurfaceAnchor>().center==lab.center,"New tile support/owner wrong");
            Check(!lab.EditTile(cell,true,out message)&&board.tiles.Length==count+1,"Duplicate tile created");
            Check(!lab.EditTile(board.PlayerCell,false,out message),"Deleted player floor");
            Check(!lab.EditTile(new Vector2Int(-1,0),false,out message),"Deleted occupied magnet floor");
            Check(lab.EditTile(new Vector2Int(4,0),false,out message)&&!board.TileAt(new Vector2Int(4,0)),"Delete failed");
            board.player.position=new Vector3(6,0,1.5f);board.CaptureInitialState();
            Check(!board.TryStep(Vector2Int.down),"Player walked into deleted gap");
            Check(lab.EditTile(new Vector2Int(4,0),true,out message),"Recreate failed");
            Check(board.TryStep(Vector2Int.down),"New tile not walkable");typeof(GridPlayground).GetMethod("FinishMovement",Private).Invoke(board,null);
            Check(board.PlayerCell==new Vector2Int(4,0),"Walking destination wrong");
            board.ResetPuzzle();Check(board.TileAt(cell)&&board.TileAt(new Vector2Int(4,0)),"Reset lost custom layout");
            var portal=lab.PlacePortal(added);Check(!lab.EditTile(cell,false,out message),"Deleted black hole floor");portal.gameObject.SetActive(false);UnityEngine.Object.DestroyImmediate(portal.gameObject);
            Check(lab.EditTile(cell,false,out message)&&!board.TileAt(cell),"Deletion after clearing portal failed");
            Check(lab.EditTile(new Vector2Int(6,1),true,out message),"Detached island tile failed");
            var panel=board.GetComponent<MagnetDebugPanel>();if(!panel.IsOpen)panel.Toggle();typeof(MagnetDebugPanel).GetField("section",Private).SetValue(panel,2);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            double ready=EditorApplication.timeSinceStartup+.5;bool captured=false;EditorApplication.CallbackFunction tick=null;
            tick=()=>{if(EditorApplication.timeSinceStartup<ready)return;if(!captured){ScreenCapture.CaptureScreenshot("output/test-terrain-panel.png");captured=true;ready=EditorApplication.timeSinceStartup+.5;return;}
                EditorApplication.update-=tick;File.WriteAllText(Report,"PASS: add/delete/recreate, duplicates, detached tile, player/magnet/portal guards, movement across new floor, deleted gap blocked, reset preserves custom terrain; UI captured.");Finish();};EditorApplication.update+=tick;
        }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);Debug.LogException(ex);Finish();}
    }
    static void Finish(){if(SessionState.GetBool("TerrainTest.Play",false)){SessionState.SetBool("TerrainTest.Play",false);EditorApplication.isPlaying=false;}}
}
}
