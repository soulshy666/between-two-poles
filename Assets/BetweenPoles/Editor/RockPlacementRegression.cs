using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class RockPlacementRegression {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static RockPlacementRegression(){
        EditorApplication.delayCall+=()=>{if(File.Exists("output/rock-placement.run")){File.Delete("output/rock-placement.run");SessionState.SetBool("RockPlacement.Run",true);if(EditorApplication.isPlaying)Run();else EditorApplication.isPlaying=true;}};
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("RockPlacement.Run",false))EditorApplication.delayCall+=Run;};
    }
    static void Run(){
        var scene=SceneManager.CreateScene("RockPlacementValidation");var root=new GameObject("Rock placement check");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);
        try{
            var board=root.AddComponent<GridPlayground>();board.magnets=new MagnetPiece[0];
            var player=new GameObject("actor");player.transform.SetParent(root.transform);player.transform.position=Vector3.left*3;board.player=player.transform;
            var tileObj=new GameObject("tile");tileObj.transform.SetParent(root.transform);var tile=tileObj.AddComponent<GridTile>();board.tiles=new[]{tile};
            var panel=root.AddComponent<MagnetDebugPanel>();root.SetActive(true);board.enabled=false;
            typeof(MagnetDebugPanel).GetField("board",Flags).SetValue(panel,board);typeof(MagnetDebugPanel).GetField("shape",Flags).SetValue(panel,9);
            for(int theme=0;theme<5;theme++)for(int shape=0;shape<3;shape++){
                typeof(MagnetDebugPanel).GetField("rockStyle",Flags).SetValue(panel,shape);typeof(MagnetDebugPanel).GetField("rockTheme",Flags).SetValue(panel,theme);
                if(!panel.Place())throw new Exception("Placement rejected");
                var visual=tile.transform.Find("调试高台");var mesh=visual.GetComponent<MeshFilter>().sharedMesh;
                if(!tile.blocked||tile.rockStyle!=shape||tile.rockTheme!=theme||!mesh.name.Contains("rock"))throw new Exception("Placement geometry or metadata wrong");
                if(shape==2&&Mathf.Abs(mesh.bounds.max.y*visual.lossyScale.y-.55f)>.001f)throw new Exception("Placed low height wrong");
                typeof(MagnetDebugPanel).GetMethod("SelectCell",Flags).Invoke(panel,new object[]{Vector2Int.zero});
                if((int)typeof(MagnetDebugPanel).GetField("rockTheme",Flags).GetValue(panel)!=theme)throw new Exception("Selection lost theme");
            }
            typeof(MagnetDebugPanel).GetField("shape",Flags).SetValue(panel,8);panel.Place();if(tile.blocked)throw new Exception("Erase failed");
            File.WriteAllText("output/rock-placement-result.txt","PASS: Runtime panel placement and immediate replacement of all 15 variants, actual low world height, selection readback and erase.");
        }catch(Exception ex){File.WriteAllText("output/rock-placement-result.txt","FAIL: "+ex);Debug.LogException(ex);}
        finally{UnityEngine.Object.Destroy(root);SceneManager.UnloadSceneAsync(scene);SessionState.SetBool("RockPlacement.Run",false);EditorApplication.isPlaying=false;}
    }
}
}
