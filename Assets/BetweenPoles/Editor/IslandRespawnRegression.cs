using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] public static class IslandRespawnRegression {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static IslandRespawnRegression(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("output/island-respawn.run"))return;
        File.Delete("output/island-respawn.run");Run();
    }
    static object Call(GridPlayground b,string method,params object[] args){return typeof(GridPlayground).GetMethod(method,Flags).Invoke(b,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static GridTile Tile(Transform root,Transform island,int x){
        var obj=new GameObject("Tile "+x);obj.transform.SetParent(root);obj.transform.position=Vector3.right*x*1.5f;
        obj.AddComponent<IslandSurfaceAnchor>().center=island;return obj.AddComponent<GridTile>();
    }
    static void Land(GridPlayground b,GridTile tile,float height=0){b.player.position=tile.transform.position+Vector3.up*height;Call(b,"TrackIsland",tile);}
    [MenuItem("两极之间/验证小岛首次登陆与重置")]
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            var root=new GameObject("Island respawn regression");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var b=root.AddComponent<GridPlayground>();b.enabled=false;
            b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
            var a=new GameObject("Island A").transform;a.SetParent(root.transform);
            var c=new GameObject("Island B").transform;c.SetParent(root.transform);
            var d=new GameObject("Island C").transform;d.SetParent(root.transform);
            var a0=Tile(root.transform,a,0);var a1=Tile(root.transform,a,1);
            var b0=Tile(root.transform,c,10);var b1=Tile(root.transform,c,11);var b2=Tile(root.transform,c,12);
            var c0=Tile(root.transform,d,20);var c1=Tile(root.transform,d,21);c0.blocked=true;
            var rock=GameObject.CreatePrimitive(PrimitiveType.Cube);rock.transform.SetParent(c0.transform,false);rock.transform.localPosition=Vector3.up*.375f;rock.transform.localScale=new Vector3(1,.75f,1);
            b.tiles=new[]{a0,a1,b0,b1,b2,c0,c1};
            var local=MagnetVisuals.Create(root.transform,b0.transform.position,MagnetShape.Bar,true,Quaternion.identity,1.5f,c);
            var foreign=MagnetVisuals.Create(root.transform,a1.transform.position,MagnetShape.Bar,true,Quaternion.identity,1.5f,a);
            b.magnets=new[]{local,foreign};root.SetActive(true);b.CaptureInitialState();
            // Recoil landing knocks the local magnet aside and records a fallback.
            // A foreign arrival must not reserve the player's entry against that magnet.
            local.transform.position=b1.transform.position;Call(b,"RecordKnockCheckpoint",local);Land(b,b0);b.player.rotation=Quaternion.Euler(0,90,0);
            foreign.transform.position=b2.transform.position;Call(b,"RecordRecoilArrival",foreign,c);foreign.transform.position=b1.transform.position;
            b.player.position=b2.transform.position;var before=b.player.position;
            Check(b.ResetCurrentIsland(),"Reset rejected");
            Check(local.transform.position==b0.transform.position,"Local magnet did not return to initial layout");
            Check(foreign.transform.position==b2.transform.position,"Foreign magnet lost its arrival checkpoint");
            Check(b.PlayerCell==new Vector2Int(10,0)&&b.player.position.y>.05f,"Player was moved away from occupied first entry");
            Check(b.UndoStep()&&b.player.position==before&&local.transform.position==b1.transform.position,"Z did not undo reset");
            Land(b,a0);Land(b,b1);Check(b.ResetCurrentIsland()&&b.PlayerCell==new Vector2Int(10,0),"Revisit overwrote first entry");
            // If the displaced piece has no landing, R must also recall it from space.
            Call(b,"ClearKnockFlights");
            ((System.Collections.IList)typeof(GridPlayground).GetField("knockCheckpoints",Flags).GetValue(b)).Clear();
            Call(b,"LaunchLandingMagnets",foreign,new Vector2Int(10,0),Vector2Int.up);
            Check(b.IsKnockedFlying(local),"Missing drifting impact setup");
            Check(b.ResetCurrentIsland()&&!b.IsKnockedFlying(local)&&local.transform.position==b0.transform.position&&b.player.position.y>.05f,"Drifting magnet not recalled below player");
            // Undo across the first visit, then enter elsewhere: retain the original first visit.
            Land(b,a0);var prior=Call(b,"SaveWorld");Land(b,c0,.75f);Call(b,"RestoreWorld",prior);Land(b,c1);
            b.player.position=b1.transform.position;Check(b.ResetCurrentIsland(),"Rock reset rejected");
            Check(b.PlayerCell==new Vector2Int(20,0)&&Mathf.Abs(b.player.position.y-.75f)<.001f,"Rock entry did not respawn on its top");
            Land(b,a1);Check(b.ResetCurrentIsland()&&b.PlayerCell==Vector2Int.zero,"Starting island entry was overwritten");
            Check(foreign.transform.position==a1.transform.position,"Home island did not recall its magnet");
            Directory.CreateDirectory("output");File.WriteAllText("output/island-respawn.txt","PASS: knocked magnet returns from displaced checkpoint and space below player; foreign arrival checkpoint and home recall; first entry survives revisit and undo; rock-top respawn; starting island; Z undo.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/island-respawn.txt","FAIL: "+ex);Debug.LogException(ex);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
