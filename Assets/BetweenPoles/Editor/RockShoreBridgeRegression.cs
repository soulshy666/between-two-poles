using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
public static class RockShoreBridgeRegression {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(GridPlayground b,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Flags).Invoke(b,args);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Drain(IEnumerator action){var stack=new Stack<IEnumerator>();stack.Push(action);int guard=1000;while(stack.Count>0){Check(--guard>0,"Movement timeout");var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}if(top.Current is IEnumerator child)stack.Push(child);}}
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
                var root=new GameObject("Rock shore bridge test");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.magnets=new MagnetPiece[0];
                b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
                var shore=new GameObject("Rock shore");shore.transform.SetParent(root.transform);var tile=shore.AddComponent<GridTile>();tile.blocked=true;
                var rock=GameObject.CreatePrimitive(PrimitiveType.Cube);rock.transform.SetParent(shore.transform);rock.transform.localPosition=Vector3.up*.375f;rock.transform.localScale=new Vector3(1,.75f,1);
                var far=new GameObject("Far shore");far.transform.SetParent(root.transform);far.transform.position=new Vector3(dir.x*3,0,dir.y*3);var farTile=far.AddComponent<GridTile>();b.tiles=new[]{tile,farTile};
                var axis=new Vector3(dir.x,0,dir.y);var yaw=Quaternion.FromToRotation(Vector3.right,axis);
                var bridge=MagnetVisuals.Create(root.transform,axis*1.5f,MagnetShape.Bar,true,yaw,1.5f,root.transform);
                MagnetVisuals.Product(bridge,MagnetProduct.WideBar,1.5f,yaw,true);b.magnets=new[]{bridge};root.SetActive(true);
                typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(b,true);
                GridTile found;Vector3 top;
                Check(BlackHolePortal.TrySurface(b,Vector2Int.zero,out found,out top)&&top.y>.7f,"Rock top missing");b.player.position=top;
                Check((bool)Call(b,"WideBarConnected",bridge),"Rock rejected as anchor");
                Check((bool)Call(b,"BridgePassage",Vector2Int.zero,dir,null),"Rock-to-bridge path blocked");
                Check(!(bool)Call(b,"ShouldHover",bridge),"Connected bridge still hovers");
                float height=(float)Call(b,"DeckHeight",bridge);
                Drain((IEnumerator)Call(b,"WalkOffObject",dir,axis*1.5f+Vector3.up*height));
                Check(b.PlayerCell==dir&&Mathf.Abs(b.player.position.y-height)<.001f,"Jump missed bridge height");
                Check((bool)Call(b,"BridgePassage",dir,dir*2,null),"Bridge-to-island path blocked");
                var side=new Vector2Int(-dir.y,dir.x);Check(!(bool)Call(b,"BridgePassage",dir,dir+side,null),"Side entry allowed");
                b.tiles=new[]{tile};b.InvalidateTileLookup();Check(!(bool)Call(b,"WideBarConnected",bridge)&&(bool)Call(b,"ShouldHover",bridge),"Missing far shore still connected");
                UnityEngine.Object.DestroyImmediate(root);
            }
            Directory.CreateDirectory("output");File.WriteAllText("output/rock-shore-bridge.txt","PASS: four orientations; rock anchors wide bar, no hover, rock-top jump lands at deck height, onward shore access, side/missing-shore rejection.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/rock-shore-bridge.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
