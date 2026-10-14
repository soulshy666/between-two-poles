using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] public static class HeightRulesRegression {
    const string Request="output/height-rules.run",Report="output/height-rules-result.txt";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static HeightRulesRegression(){EditorApplication.update+=Poll;}
    static void Poll(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        File.Delete(Request);Run();
    }
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static object Call(GridPlayground b,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Private).Invoke(b,args);}
    [MenuItem("两极之间/验证统一高度规则")]
    public static void Run(){
        checks=0;var scene=EditorSceneManager.NewPreviewScene();
        try{
            foreach(float scale in new[]{.5f,1f,2f}){
                float layer=.75f*scale;
                Check(GridPlayground.EvaluateHeight(.24f*scale,.55f*scale,true,layer)==GridPlayground.HeightMove.Up,"Flat magnet to low rock");
                Check(GridPlayground.EvaluateHeight(.24f*scale,.75f*scale,true,layer)==GridPlayground.HeightMove.Up,"Flat magnet to platform");
                Check(GridPlayground.EvaluateHeight(layer,layer*2,true,layer)==GridPlayground.HeightMove.Up,"Inclusive upward boundary");
                Check(GridPlayground.EvaluateHeight(layer,0,true,layer)==GridPlayground.HeightMove.Blocked,"Exclusive downward boundary");
                Check(GridPlayground.EvaluateHeight(layer-.001f,0,true,layer)==GridPlayground.HeightMove.Down,"Just below downward boundary");
                Check(GridPlayground.EvaluateHeight(0,.24f*scale,false,layer)==GridPlayground.HeightMove.Blocked,"Ground must not climb");
                Check(GridPlayground.EvaluateHeight(layer,layer*2+.001f,true,layer)==GridPlayground.HeightMove.Blocked,"Excess upward height");
            }
            foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
                var root=new GameObject("Height regression");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var b=root.AddComponent<GridPlayground>();b.enabled=false;b.magnets=new MagnetPiece[0];
                b.player=new GameObject("Player").transform;b.player.SetParent(root.transform);
                var a=new GameObject("Source");a.transform.SetParent(root.transform);var source=a.AddComponent<GridTile>();
                var z=new GameObject("Target");z.transform.SetParent(root.transform);z.transform.position=new Vector3(dir.x*1.5f,0,dir.y*1.5f);var target=z.AddComponent<GridTile>();
                b.tiles=new[]{source,target};root.SetActive(true);
                typeof(GridPlayground).GetField("recordingPush",Private).SetValue(b,true);
                Action<float,float,bool> move=(from,to,allowed)=>{
                    source.surfaceHeight=from;target.surfaceHeight=to;b.player.position=Vector3.up*from;
                    var previousPose=b.player.GetComponent<PlayerPushPose>();if(previousPose)previousPose.EndEdgeBalance();
                    int history=b.UndoCount;var start=b.player.position;
                    bool accepted=b.TryStep(dir);Check(accepted==allowed,"Surface transition "+from+" -> "+to+": "+b.LastRule);
                    if(!accepted){
                        var pose=b.player.GetComponent<PlayerPushPose>();
                        bool balancing=pose&&(bool)typeof(PlayerPushPose).GetField("balancing",Private).GetValue(pose);
                        Check(balancing==(to<from),"Only rejected downward moves should trigger edge balance");
                        Check(b.player.position==start&&!b.Busy&&b.UndoCount==history,"Rejected jump changed logical state");
                        if(balancing){
                            Check(float.IsPositiveInfinity((float)typeof(GridPlayground).GetField("nextHeldStep",Private).GetValue(b)),"Held input not suppressed");
                            typeof(PlayerPushPose).GetField("balanceTime",Private).SetValue(pose,.4f);
                            Check(!b.TryStep(dir)&&(float)typeof(PlayerPushPose).GetField("balanceTime",Private).GetValue(pose)==.4f,"Repeated direction restarted balance");
                            target.surfaceHeight=from;
                            Check(b.TryStep(dir),"Safe movement could not interrupt balance");Call(b,"FinishMovement");
                            Check(!(bool)typeof(PlayerPushPose).GetField("balancing",Private).GetValue(pose),"Balance survived safe movement");
                            Check(b.UndoStep()&&b.player.position==start,"Undo after balance failed");target.surfaceHeight=to;
                        }
                    }
                    if(accepted){Call(b,"FinishMovement");Check(Mathf.Abs(b.player.position.y-to)<.0001f,"Wrong landing");Check(b.PlayerSupport.valid&&Mathf.Abs(b.PlayerSupport.point.y-to)<.0001f,"Support not committed");
                        Check(b.UndoStep()&&Mathf.Abs(b.player.position.y-from)<.0001f,"Undo height");Check(b.PlayerSupport.valid,"Undo support");}
                };
                move(.24f,.55f,true);move(.24f,.75f,true);move(.75f,1.5f,true);
                move(.55f,0,true);move(.24f,0,true);move(.75f,0,false);move(1.5f,0,false);move(0,.24f,false);move(.75f,1.501f,false);
                // Verify model-backed tops use the same rules as authored platforms.
                source.surfaceHeight=0;target.surfaceHeight=.75f;
                var bar=MagnetVisuals.Create(root.transform,Vector3.zero,MagnetShape.Bar,true,Quaternion.identity,1.5f,root.transform);b.magnets=new[]{bar};
                // Equal-height adjacent supports must have airtime, even with
                // the test scene's very short interaction step duration.
                target.surfaceHeight=0;b.stepSeconds=.045f;
                var adjacent=MagnetVisuals.Create(root.transform,z.transform.position,MagnetShape.Bar,false,Quaternion.identity,1.5f,root.transform);
                b.magnets=new[]{bar,adjacent};b.player.position=Vector3.up*.24f;
                int jumpHistory=b.UndoCount;
                Check(b.TryStep(dir),"Equal-height magnet jump rejected: "+b.LastRule);
                float peak=b.player.position.y;int frames=0;
                while((bool)Call(b,"AdvanceMovement")){
                    peak=Mathf.Max(peak,b.player.position.y);
                    if(++frames>240)throw new Exception("Equal-height jump did not finish");
                }
                Check(peak>.5f&&frames>10,"Equal-height move slid or teleported instead of jumping");
                Check(Vector3.Distance(b.player.position,z.transform.position+Vector3.up*.24f)<.001f,"Equal-height jump landing");
                Check(b.PlayerSupport.valid&&b.PlayerSupport.owner==adjacent,"Equal-height jump support");
                Check(b.UndoCount==jumpHistory+1&&b.UndoStep()&&Vector3.Distance(b.player.position,Vector3.up*.24f)<.001f,"Equal-height jump undo");
                // Removing the ground below the same single magnet makes it an
                // unsafe landing: feedback only, without a jump or undo entry.
                b.tiles=new[]{source};b.InvalidateTileLookup();
                var unsafeStart=b.player.position;int unsafeHistory=b.UndoCount;
                var magnetStart=adjacent.transform.position;
                Check(!b.TryStep(dir),"Unsupported single magnet accepted a jump");
                var unsafePose=b.player.GetComponent<PlayerPushPose>();
                Check(unsafePose&&(bool)typeof(PlayerPushPose).GetField("balancing",Private).GetValue(unsafePose),"Unsupported single magnet did not play edge balance");
                Check(b.player.position==unsafeStart&&!b.Busy&&b.UndoCount==unsafeHistory&&adjacent.transform.position==magnetStart,"Unsafe landing changed world state");
                Check(b.PlayerSupport.valid&&b.PlayerSupport.owner==bar,"Unsafe landing changed support");
                typeof(PlayerPushPose).GetField("balanceTime",Private).SetValue(unsafePose,.4f);
                Check(!b.TryStep(dir)&&(float)typeof(PlayerPushPose).GetField("balanceTime",Private).GetValue(unsafePose)==.4f,"Unsafe landing restarted edge feedback");
                Check(float.IsPositiveInfinity((float)typeof(GridPlayground).GetField("nextHeldStep",Private).GetValue(b)),"Unsafe landing repeated held input");
                b.tiles=new[]{source,target};b.InvalidateTileLookup();
                UnityEngine.Object.DestroyImmediate(adjacent.gameObject);b.magnets=new[]{bar};target.surfaceHeight=.75f;
                b.player.position=Vector3.up*.24f;
                Check(b.TryStep(dir),"Actual flat magnet to platform: "+b.LastRule);Call(b,"FinishMovement");Check(Mathf.Abs(b.player.position.y-.75f)<.0001f,"Magnet jump landing");
                b.player.position=Vector3.zero;target.surfaceHeight=0;bar.transform.position=z.transform.position;
                // Ground contact may push a bar, but must not land on its top.
                if(b.TryStep(dir)){Call(b,"FinishMovement");Check(Mathf.Abs(b.player.position.y)<.0001f,"Ground climbed magnet instead of pushing");}
                bar.gameObject.SetActive(false);b.magnets=new MagnetPiece[0];
                target.blocked=true;
                var rock=GameObject.CreatePrimitive(PrimitiveType.Cube);rock.transform.SetParent(z.transform,false);
                rock.transform.localScale=new Vector3(1,.55f,1);rock.transform.localPosition=Vector3.up*.275f;
                source.surfaceHeight=.24f;b.player.position=Vector3.up*.24f;
                Check(b.TryStep(dir),"Low rock rejected");Call(b,"FinishMovement");Check(Mathf.Abs(b.player.position.y-.55f)<.0001f,"Low rock top height");
                Check(b.TryStep(-dir),"Low rock descent rejected");Call(b,"FinishMovement");
                source.surfaceHeight=0;b.player.position=Vector3.zero;
                Check(!b.TryStep(dir),"Ground climbed rock");
                UnityEngine.Object.DestroyImmediate(rock);target.blocked=false;
                // An upright magnet is a two-layer solid support. Height alone
                // decides access from an elevated platform, including equality.
                var upright=MagnetVisuals.Create(root.transform,z.transform.position,MagnetShape.Bar,true,Quaternion.Euler(0,0,90),1.5f,root.transform);b.magnets=new[]{upright};
                source.surfaceHeight=.75f;b.player.position=Vector3.up*.75f;
                Check(b.TryStep(dir),"Upright top unreachable from one layer: "+b.LastRule);Call(b,"FinishMovement");
                Check(Mathf.Abs(b.player.position.y-1.5f)<.0001f,"Upright top wrong height");
                Check(!b.TryStep(-dir),"Exact one-layer drop from upright permitted");
                upright.gameObject.SetActive(false);
                var lift=MagnetVisuals.Create(root.transform,z.transform.position,MagnetShape.Bar,true,Quaternion.identity,1.5f,root.transform);
                MagnetVisuals.Product(lift,MagnetProduct.Lift,1.5f,Quaternion.identity,true);b.magnets=new[]{lift};
                source.surfaceHeight=0;b.player.position=Vector3.zero;
                Check(b.TryStep(dir),"Lift entry rejected");Call(b,"FinishMovement");
                Check(b.PlayerSupport.valid&&Mathf.Abs(b.PlayerSupport.point.y-.75f)<.0001f,"Lift endpoint support missing");
                Check(!b.TryStep(-dir),"Lift endpoint bypassed downward limit");
                Check(b.UndoStep()&&Mathf.Abs(b.player.position.y)<.0001f,"Lift undo failed");
                // Elevated island edge: no destination surface at all, rather
                // than a lower floor rejected by the height limit.
                lift.gameObject.SetActive(false);b.magnets=new MagnetPiece[0];b.tiles=new[]{source};b.InvalidateTileLookup();
                source.surfaceHeight=.75f;b.player.position=Vector3.up*.75f;
                var edgePose=b.player.GetComponent<PlayerPushPose>();edgePose.EndEdgeBalance();
                var edgeStart=b.player.position;int edgeHistory=b.UndoCount;
                Check(!b.TryStep(dir),"Elevated edge allowed movement into space");
                Check((bool)typeof(PlayerPushPose).GetField("balancing",Private).GetValue(edgePose),"Elevated edge did not start balance");
                Check(b.player.position==edgeStart&&!b.Busy&&b.UndoCount==edgeHistory&&b.PlayerSupport.valid,"Elevated edge changed support or history");
                Check(float.IsPositiveInfinity((float)typeof(GridPlayground).GetField("nextHeldStep",Private).GetValue(b)),"Elevated edge repeated held input");
                typeof(PlayerPushPose).GetField("balanceTime",Private).SetValue(edgePose,.4f);
                Check(!b.TryStep(dir)&&(float)typeof(PlayerPushPose).GetField("balanceTime",Private).GetValue(edgePose)==.4f,"Elevated edge restarted balance");
                b.tiles=new[]{source,target};target.surfaceHeight=.75f;b.InvalidateTileLookup();
                Check(b.TryStep(dir),"Safe exit after edge feedback failed");Call(b,"FinishMovement");
                Check(!(bool)typeof(PlayerPushPose).GetField("balancing",Private).GetValue(edgePose),"Safe movement left edge animation active");
                UnityEngine.Object.DestroyImmediate(root);
            }
            Directory.CreateDirectory("output");File.WriteAllText(Report,"PASS: "+checks+" checks; height boundaries, scaled layers, four directions, actual magnet top, support commits and undo.");
        }catch(Exception ex){Directory.CreateDirectory("output");File.WriteAllText(Report,"FAIL: "+ex);Debug.LogException(ex);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
