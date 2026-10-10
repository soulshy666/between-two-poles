using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class BridgeJunctionRegression {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static BridgeJunctionRegression(){
        EditorApplication.delayCall+=()=>{if(!File.Exists("output/bridge-junction.run"))return;File.Delete("output/bridge-junction.run");SessionState.SetBool("Junction.Run",true);if(!Application.isPlaying)EditorApplication.isPlaying=true;else Run();};
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("Junction.Run",false)){
            double ready=EditorApplication.timeSinceStartup+1;EditorApplication.CallbackFunction tick=null;
            tick=()=>{if(EditorApplication.timeSinceStartup<ready)return;EditorApplication.update-=tick;Run();};EditorApplication.update+=tick;
        }};
    }
    static object Call(GridPlayground b,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Flags).Invoke(b,args);}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Run(){
        try{
            var lab=UnityEngine.Object.FindObjectOfType<MagnetTestLayouts>();
            for(int rotation=0;rotation<4;rotation++){
                lab.Load(0);var b=lab.board;var yaw=Quaternion.Euler(0,rotation*90,0);
                Func<Vector2Int,Vector2Int> rotate=p=>{var v=yaw*new Vector3(p.x,0,p.y);return new Vector2Int(Mathf.RoundToInt(v.x),Mathf.RoundToInt(v.z));};
                var origin=Vector2Int.zero;var branchCell=rotate(Vector2Int.down);var dir=branchCell;
                foreach(var m in b.magnets)m.gameObject.SetActive(false);b.magnets=new MagnetPiece[0];
                b.player.position=new Vector3(4*b.cellSize,0,3*b.cellSize);b.CaptureInitialState();string message;
                Check(lab.EditTile(origin,false,out message),message);Check(lab.EditTile(branchCell,false,out message),message);
                Func<Vector2Int,Quaternion,MagnetPiece> wide=(cell,pose)=>{
                    var m=MagnetVisuals.Create(lab.transform,new Vector3(cell.x*b.cellSize,0,cell.y*b.cellSize),MagnetShape.Bar,true,pose,b.cellSize,lab.center);
                    MagnetVisuals.Product(m,MagnetProduct.WideBar,b.cellSize,pose,true);return m;
                };
                var main=wide(origin,yaw);var branch=wide(branchCell,yaw*Quaternion.Euler(0,90,0));
                var red=MagnetVisuals.Create(lab.transform,new Vector3(dir.x*2*b.cellSize,0,dir.y*2*b.cellSize),MagnetShape.Bar,true,yaw*Quaternion.Euler(0,90,0),b.cellSize,lab.center);
                b.magnets=new[]{main,branch,red};b.player.position=Vector3.zero;b.CaptureInitialState();
                Check((bool)Call(b,"WideBarConnected",branch),"Branch not anchored");
                Call(b,"UpdateBridgeJunctionLinks");
                var link=main.transform.Find("宽条桥接缝电磁波");
                Check(link&&link.GetComponentsInChildren<MagneticLinkPulse>().Length==2,"Missing paired animated junction links");
                Check(link.childCount==2&&link.GetChild(0).childCount==17&&link.GetChild(1).childCount==17,"Incomplete wave and pulse geometry");
                var endpoint=link.TransformPoint(new Vector3(.24f,.16f,0));
                Check(Vector3.Distance(endpoint,new Vector3(dir.x*.24f,-.12f,dir.y*.24f))<.001f,"Wave starts away from transverse bar");
                Call(b,"UpdateBridgeJunctionLinks");Check(main.GetComponentsInChildren<MagneticLinkPulse>().Length==2,"Duplicate wave created");
                if(rotation==0){
                    foreach(var pulse in link.GetComponentsInChildren<MagneticLinkPulse>())typeof(MagneticLinkPulse).GetMethod("Update",Flags).Invoke(pulse,null);
                    var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;
                    var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                    var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                    File.WriteAllBytes("output/bridge-junction-wave.png",image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;
                    UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
                }
                Check(b.TryStep(dir),"Cannot enter branch: "+b.LastRule);Call(b,"FinishMovement");
                Check(b.PlayerCell==branchCell,"Wrong branch landing");
                Check(b.TryStep(-dir),"Cannot return across junction");Call(b,"FinishMovement");
                Check(b.TryStep(dir),"Cannot reenter branch");Call(b,"FinishMovement");
                Check(b.TryStep(dir),"Cannot push red from branch: "+b.LastRule);Call(b,"FinishMovement");
                Check(Vector3.Distance(red.transform.position,new Vector3(dir.x*3*b.cellSize,0,dir.y*3*b.cellSize))<.01f,"Red did not move");
                // A sideways shore exit is still blocked, even from the valid main span.
                Check(!(bool)Call(b,"BridgePassage",origin,-dir,null),"Unconnected sideways exit allowed");
                var shore=b.TileAt(dir*2);b.tiles=b.tiles.Where(t=>t!=shore).ToArray();shore.gameObject.SetActive(false);
                Check(!(bool)Call(b,"WideBarConnected",branch),"Open branch accepted");
                Check(!(bool)Call(b,"BridgePassage",origin,branchCell,null),"Open branch walkable");
                Call(b,"UpdateBridgeJunctionLinks");Check(!link.gameObject.activeSelf,"Broken route still displays wave");
                UnityEngine.Object.DestroyImmediate(main.gameObject);UnityEngine.Object.DestroyImmediate(branch.gameObject);UnityEngine.Object.DestroyImmediate(red.gameObject);
            }
            File.WriteAllText("output/bridge-junction-result.txt","PASS: four orientations; anchored perpendicular branch, forward/reverse walking, pushing red onward, sideways shore blocked, open branch blocked; animated links created at correct sockets, deduplicated, removed on disconnection.");
        }catch(Exception ex){File.WriteAllText("output/bridge-junction-result.txt","FAIL: "+ex);}
        finally{SessionState.SetBool("Junction.Run",false);EditorApplication.isPlaying=false;}
    }
}
}
