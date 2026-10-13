using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
public static class PortalFloatRegression {
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            var root=new GameObject("Portal float test");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var b=root.AddComponent<GridPlayground>();b.enabled=false;
            b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
            var tileObject=new GameObject("Shore");tileObject.transform.SetParent(root.transform);var tile=tileObject.AddComponent<GridTile>();b.tiles=new[]{tile};
            var portal=tileObject.AddComponent<BlackHolePortal>();portal.board=b;portal.tile=tile;
            var magnet=MagnetVisuals.Create(root.transform,Vector3.right*1.5f,MagnetShape.Bar,true,Quaternion.identity,1.5f,root.transform);b.magnets=new[]{magnet};root.SetActive(true);
            GridTile support;Vector3 standing;
            Check(BlackHolePortal.TrySurface(b,Vector2Int.right,out support,out standing),"Missing magnet surface");
            Check(Mathf.Abs(standing.x-1.5f)<.001f&&Mathf.Abs(standing.z)<.001f,"Standing point is not at the cell centre");
            var closest=typeof(BlackHolePortal).GetMethod("ClosestTopPoint",BindingFlags.NonPublic|BindingFlags.Static);
            var edge=(Vector3)closest.Invoke(null,new object[]{Vector3.zero,new Vector3(1,2,-1),new Vector3(1,2,1),new Vector3(2,2,0)});
            Check((edge-new Vector3(1,2,0)).sqrMagnitude<.000001f,"Hole fallback must remain on solid geometry");
            var ages=(Dictionary<MagnetPiece,float>)typeof(GridPlayground).GetField("hoveringMagnets",F).GetValue(b);
            var levels=(Dictionary<MagnetPiece,float>)typeof(GridPlayground).GetField("hoverLevels",F).GetValue(b);
            ages[magnet]=1;levels[magnet]=-.24f;b.BeginPortalMagnetPresentation();
            typeof(GridPlayground).GetMethod("OnDisable",F).Invoke(b,null);
            Check(ages.ContainsKey(magnet)&&Mathf.Abs(levels[magnet]+.24f)<.001f,"Portal disable lost hover state");
            Check(Time.unscaledDeltaTime>0,"Missing editor clock");
            typeof(GridPlayground).GetMethod("UpdateMagnetHover",F).Invoke(b,null);
            Check(ages[magnet]>1,"Disabled portal hover clock stopped");
            Check(b.PortalMagnetOffset(magnet)<-.1f,"Floating top not lowered to shore");
            var saved=magnet.geometry.position;var rotation=magnet.geometry.rotation;
            // A prone silhouette extends forward from the feet, as the astronaut does.
            b.player.rotation=Quaternion.LookRotation(Vector3.right);
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.transform.SetParent(b.player,false);
            body.transform.localPosition=new Vector3(0,.2f,.55f);body.transform.localScale=new Vector3(.3f,.25f,1.1f);
            var travel=root.AddComponent<BlackHoleTravel>();
            var animation=(IEnumerator)typeof(BlackHoleTravel).GetMethod("UnstablePortalLanding",F).Invoke(travel,new object[]{b.player,portal,Vector2Int.right,Vector3.zero,new Vector3(1.5f,.24f,0),Vector3.one});
            int guard=20000;bool compressed=false,returnedAirborne=false;float maxX=0;
            while(animation.MoveNext()){
                Check(--guard>0,"Animation stuck");compressed|=magnet.geometry.position.y<saved.y-.5f;
                maxX=Mathf.Max(maxX,b.player.position.x);
                if(magnet.geometry.position.y<saved.y-.5f){
                    var bounds=body.GetComponent<Renderer>().bounds;
                    Check(bounds.min.x>.75f&&bounds.max.x<2.25f,"Compressed prone body overlaps an island edge");
                }
                returnedAirborne|=compressed&&b.player.position.x<1&&b.player.position.y>.6f;
            }
            Check(compressed&&returnedAirborne&&maxX>.9f&&maxX<1.2f,"Missing spring return or feet still landing in span centre");
            Check(b.player.position==Vector3.zero&&b.player.localScale==Vector3.zero,"Did not reenter hole hidden");
            Check(magnet.geometry.position==saved&&Quaternion.Angle(rotation,magnet.geometry.rotation)<.01f,"Magnet not restored");
            b.EndPortalMagnetPresentation();
            Directory.CreateDirectory("output");File.WriteAllText("output/portal-float.txt","PASS: hover survives disable and continues ticking; shore offset; spring compression and high airborne return; hidden entry and magnet restoration.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText("output/portal-float.txt","FAIL: "+ex);throw;}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
