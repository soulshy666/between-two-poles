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
                Check(!b.TryStep(dir),"An exact one-layer drop must be rejected");
                rock.transform.localPosition=Vector3.up*.275f;rock.transform.localScale=new Vector3(1,.55f,1);
                Check(BlackHolePortal.TrySurface(b,Vector2Int.zero,out found,out top),"Low shore top missing");b.player.position=top;
                Check(b.TryStep(dir),"Rock-to-bridge input rejected: "+b.LastRule);Call(b,"FinishMovement");
                Check(b.PlayerCell==dir&&Mathf.Abs(b.player.position.y-height)<.001f,"Jump missed bridge height");
                rock.name="调试高台";
                for(int theme=0;theme<5;theme++)for(int variant=0;variant<3;variant++){
                    tile.rockTheme=theme;tile.rockStyle=2;tile.lowRockVariant=variant;IceRockVisuals.Apply(b);
                    Check(BlackHolePortal.TrySurface(b,Vector2Int.zero,out found,out top),"Authored rock top missing");
                    b.player.position=top;
                    Check(b.TryStep(dir),"Authored rock-to-bridge rejected theme="+theme+" variant="+variant+": "+b.LastRule);
                    Call(b,"FinishMovement");
                    Check(b.PlayerCell==dir&&Mathf.Abs(b.player.position.y-height)<.001f,"Authored rock jump missed bridge");
                }
                // Restore simple geometry for the height-limit checks below.
                UnityEngine.Object.DestroyImmediate(rock);rock=GameObject.CreatePrimitive(PrimitiveType.Cube);rock.transform.SetParent(shore.transform);
                Check((bool)Call(b,"BridgePassage",dir,dir*2,null),"Bridge-to-island path blocked");
                var side=new Vector2Int(-dir.y,dir.x);Check(!(bool)Call(b,"BridgePassage",dir,dir+side,null),"Side entry allowed");
                b.tiles=new[]{tile};b.InvalidateTileLookup();Check(!(bool)Call(b,"WideBarConnected",bridge)&&(bool)Call(b,"ShouldHover",bridge),"Missing far shore still connected");
                Check((bool)Call(b,"BridgePassage",Vector2Int.zero,dir,null),"Floating wide bar rejected along its length");
                Check(!(bool)Call(b,"BridgePassage",dir,dir+side,null),"Floating wide bar allowed sideways exit");
                var nextPlatform=MagnetVisuals.Create(root.transform,axis*3,MagnetShape.Bar,false,yaw,1.5f,root.transform);
                MagnetVisuals.Product(nextPlatform,MagnetProduct.WideBar,1.5f,yaw,false);
                var cargo=MagnetVisuals.Create(root.transform,axis*1.5f,MagnetShape.Horseshoe,true,Quaternion.identity,1.5f,root.transform);
                b.magnets=new[]{bridge,nextPlatform,cargo};
                Check((bool)Call(b,"Supported",cargo),"Floating wide bar failed to support cargo");
                Check((bool)Call(b,"BridgePassage",dir,dir*2,cargo),"Consecutive floating wide bars blocked cargo");
                var destination=b.InspectMagnetDestination(dir,0,dir*2,cargo);
                Check(destination.kind==GridPlayground.MagnetLandingKind.Deck,"Next floating wide bar is not a cargo deck");
                nextPlatform.product=MagnetProduct.None;nextPlatform.combined=false;nextPlatform.walkable=false;
                Check(b.InspectMagnetDestination(dir,0,dir*2,cargo).kind==GridPlayground.MagnetLandingKind.Magnet,"Single floating bar became a platform");
                nextPlatform.gameObject.SetActive(false);cargo.gameObject.SetActive(false);b.magnets=new[]{bridge};
                // Stand on a flat single on land: use its top, not the land below,
                // as the takeoff height for an adjacent rock.
                bridge.gameObject.SetActive(false);
                var floorObject=new GameObject("Magnet floor");floorObject.transform.SetParent(root.transform);floorObject.transform.position=axis*1.5f;
                var floorTile=floorObject.AddComponent<GridTile>();b.tiles=new[]{tile,floorTile};b.InvalidateTileLookup();
                var single=MagnetVisuals.Create(root.transform,axis*1.5f,MagnetShape.Bar,false,yaw,1.5f,root.transform);b.magnets=new[]{single};
                Check(BlackHolePortal.TrySurface(b,dir,out found,out top),"Single top missing");
                rock.name="调试高台";
                for(int theme=0;theme<5;theme++){
                    tile.rockTheme=theme;tile.rockStyle=0;tile.lowRockVariant=0;IceRockVisuals.Apply(b);b.player.position=top;
                    Check(b.TryStep(-dir),"Normal authored stone rejected from single theme="+theme+": "+b.LastRule);
                    Call(b,"FinishMovement");
                }
                UnityEngine.Object.DestroyImmediate(rock);rock=GameObject.CreatePrimitive(PrimitiveType.Cube);rock.transform.SetParent(shore.transform);
                b.player.position=top;
                float reachable=top.y+b.LayerHeight-.04f;
                rock.transform.localScale=new Vector3(1,reachable,1);rock.transform.localPosition=Vector3.up*(reachable*.5f);
                Check((bool)Call(b,"TryStepCore",-dir),"Magnet height ignored when jumping to rock: "+b.LastRule);
                Call(b,"FinishMovement");
                Check(b.PlayerCell==Vector2Int.zero&&Mathf.Abs(b.player.position.y-reachable)<.001f,"Jump did not land on stone top");
                b.player.position=top;
                float tooHigh=top.y+b.LayerHeight+.1f;
                rock.transform.localScale=new Vector3(1,tooHigh,1);rock.transform.localPosition=Vector3.up*(tooHigh*.5f);
                Check(!(bool)Call(b,"TryStepCore",-dir),"Jump exceeded takeoff height limit");
                b.player.position=floorObject.transform.position;
                Check(!(bool)Call(b,"TryStepCore",-dir),"Ground-level player climbed stone");
                UnityEngine.Object.DestroyImmediate(root);
            }
            Directory.CreateDirectory("output");File.WriteAllText("output/rock-shore-bridge.txt","PASS: four orientations; rock anchors wide bar, bridge access and hover; magnet-top jump to reachable stone; excessive height and ground-level ascent rejected.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/rock-shore-bridge.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
