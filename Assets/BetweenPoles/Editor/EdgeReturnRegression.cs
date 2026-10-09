using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
    [InitializeOnLoad] static class EdgeReturnRegression {
        const string Report="output/edge-return-regression.txt";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static EdgeReturnRegression(){
            EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("EdgeReturn.OwnPlay",false))EditorApplication.delayCall+=Run;
            };
        }
        static object Call(GridPlayground b,string method,params object[] args){return typeof(GridPlayground).GetMethod(method,Flags).Invoke(b,args);}
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void CheckRender(GridPlayground b,MagnetPiece m){
            b.enabled=true;
            Call(b,"UpdateMagnetHover");
            var levels=(Dictionary<MagnetPiece,float>)typeof(GridPlayground).GetField("hoverLevels",Flags).GetValue(b);
            var timers=(Dictionary<MagnetPiece,float>)typeof(GridPlayground).GetField("hoveringMagnets",Flags).GetValue(b);
            float level=(float)Call(b,"EdgeHoverLevel",m);levels[m]=level;timers[m]=0;
            var root=m.transform.position;var geometry=m.geometry.position;
            Call(b,"RenderMagnetHover",new object[]{null});
            Check(Vector3.Distance(m.geometry.position,geometry+Vector3.up*level)<.001f,"Rendered hover height incorrect");
            Check(m.transform.position==root,"Rendering changed logical position");
            Call(b,"RestoreMagnetHover",new object[]{null});
            Check(Vector3.Distance(m.geometry.position,geometry)<.001f,"Render offset leaked into movement/undo");
            b.enabled=false;
        }
        static void CheckChapterHover(GridPlayground b,MagnetPiece m,float expected){
            // Use the chapter's actual expanded-bounds mesh, not fresh primitives.
            MagnetSceneConsistency.PreserveCurvedWorldBounds(m.geometry);
            Check(Mathf.Abs((float)Call(b,"EdgeHoverLevel",m)-expected)<.001f,"Chapter culling bounds changed hover height");
            var root=m.transform.position;var geometry=m.geometry.position;
            b.enabled=true;
            var timers=(Dictionary<MagnetPiece,float>)typeof(GridPlayground).GetField("hoveringMagnets",Flags).GetValue(b);
            for(int frame=0;frame<600;frame++){
                Call(b,"UpdateMagnetHover");timers[m]=frame/60f;
                // Two camera passes each frame must not accumulate displacement.
                for(int camera=0;camera<2;camera++){
                    Call(b,"RenderMagnetHover",new object[]{null});
                    float offset=m.geometry.position.y-geometry.y;
                    Check(offset>=expected-.091f&&offset<=.091f,"Hover drifted below the shore band");
                    Call(b,"RestoreMagnetHover",new object[]{null});
                }
                Check(Vector3.Distance(m.geometry.position,geometry)<.001f&&m.transform.position==root,"Hover accumulated into transforms");
            }
            b.enabled=false;
        }
        [MenuItem("两极之间/验证边缘磁铁回推与高度")]
        static void Run(){
            Directory.CreateDirectory("output");
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Run;return;}
            if(!EditorApplication.isPlaying){SessionState.SetBool("EdgeReturn.OwnPlay",true);EditorApplication.isPlaying=true;return;}
            var root=new GameObject("Edge return regression");root.SetActive(false);int cases=0;
            try{
                for(int rotation=0;rotation<4;rotation++)for(int color=0;color<2;color++)for(int shape=0;shape<2;shape++)for(int scenario=0;scenario<5;scenario++){
                    var host=new GameObject("Case");host.transform.SetParent(root.transform,false);
                    var b=host.AddComponent<GridPlayground>();b.enabled=false;
                    var axis=Quaternion.Euler(0,rotation*90,0)*Vector3.right;
                    var dir=new Vector2Int(Mathf.RoundToInt(axis.x),Mathf.RoundToInt(axis.z));
                    var p=new GameObject("Player");p.transform.SetParent(host.transform,false);b.player=p.transform;
                    new GameObject("Visual").transform.SetParent(p.transform,false);
                    var tiles=new List<GridTile>();
                    foreach(int n in new[]{0,1,3}){
                        if(n==3&&scenario==1)continue;
                        var obj=new GameObject("Floor");obj.transform.SetParent(host.transform,false);obj.transform.position=axis*(n*1.5f);
                        var tile=obj.AddComponent<GridTile>();if(n==3){tile.blocked=scenario==2;tile.surfaceHeight=scenario==3?1.5f:0;}tiles.Add(tile);
                    }
                    b.tiles=tiles.ToArray();var magnets=new List<MagnetPiece>();
                    for(int n=1;n<=(scenario==4?3:2);n++){
                        var obj=new GameObject("Magnet");obj.transform.SetParent(host.transform,false);obj.transform.position=axis*(n*1.5f);
                        var m=obj.AddComponent<MagnetPiece>();m.shape=(MagnetShape)shape;m.north=color==0;
                        m.geometry=MagnetVisuals.Build(obj.transform,m.shape,m.north,MagnetProduct.None,1.5f,m.north,dir);MagnetVisuals.Ground(m);magnets.Add(m);
                    }
                    b.magnets=magnets.ToArray();root.SetActive(true);b.CaptureInitialState();
                    var near=magnets[0];var far=magnets[1];
                    Check(Mathf.Abs((float)Call(b,"EdgeHoverLevel",far))<.001f,"Same-pole hover not aligned");
                    foreach(var other in magnets)if(other!=far)other.north=!far.north;
                    float top=0;foreach(var renderer in far.geometry.GetComponentsInChildren<Renderer>())top=Mathf.Max(top,renderer.bounds.max.y);
                    Check(Mathf.Abs((float)Call(b,"EdgeHoverLevel",far)+top)<.001f,"Ordinary hover not shore-flush");
                    CheckRender(b,far);
                    if(rotation==0&&scenario==0)CheckChapterHover(b,far,-top);
                    if(shape==0){
                        var pose=far.Pose;far.geometry.rotation=Quaternion.Euler(0,0,90);MagnetVisuals.Ground(far);
                        top=b.cellSize;
                        Check(Mathf.Abs((float)Call(b,"EdgeHoverLevel",far)+top)<.001f,"Upright hover not shore-flush");
                        CheckRender(b,far);far.geometry.rotation=pose;MagnetVisuals.Ground(far);
                    }
                    foreach(var other in magnets)other.north=far.north;
                    CheckRender(b,far);
                    bool accepted=b.TryStep(dir);
                    Check(accepted==(scenario==0),"Acceptance mismatch scenario="+scenario+" "+b.LastRule);
                    b.StopAllCoroutines();
                    typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(b,true);
                    int samples=0;
                    while((bool)Call(b,"AdvanceMovement")){
                        Check(++samples<1000,"Movement did not finish");
                        Check(Vector3.Dot(b.player.position,axis)<=1.501f,"Player walked into gap during animation");
                        Check(Mathf.Abs(near.transform.position.y)<.001f&&Mathf.Abs(far.transform.position.y)<.001f,"Visual height changed logical layer");
                    }
                    typeof(GridPlayground).GetField("recordingPush",Flags).SetValue(b,false);
                    Call(b,"FinishMovement");
                    if(scenario==0){
                        Check(Vector3.Distance(near.transform.position,axis*3)<.001f&&Vector3.Distance(far.transform.position,axis*4.5f)<.001f,"Magnet landing mismatch");
                        Check(Vector3.Distance(b.player.position,axis*1.5f)<.001f,"Player entered gap");
                        Check(Mathf.Abs((float)Call(b,"EdgeHoverLevel",near))<.001f,"New hovering magnet not aligned with far shore");
                        Check(b.UndoStep(),"Undo rejected");
                        Check(Vector3.Distance(near.transform.position,axis*1.5f)<.001f&&Vector3.Distance(far.transform.position,axis*3)<.001f&&b.player.position.sqrMagnitude<.001f,"Undo positions incorrect");
                        CheckRender(b,far);
                    }else{
                        Check(Vector3.Distance(near.transform.position,axis*1.5f)<.001f&&Vector3.Distance(far.transform.position,axis*3)<.001f&&!b.RecoilFlying,"Blocked push changed cells or recoiled");
                    }
                    UnityEngine.Object.DestroyImmediate(host);root.SetActive(false);cases++;
                }
                File.WriteAllText(Report,"PASS: "+cases+" cases; four directions, both poles, bar/U, return to land, open gap/stone/height/occupied blockers, player landing, undo, ordinary and same-pole hover heights. Chapter meshes: 600 frames x 2 camera passes for both poles and shapes, no downward drift.");
            }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);}
            finally{UnityEngine.Object.DestroyImmediate(root);if(SessionState.GetBool("EdgeReturn.OwnPlay",false)){SessionState.SetBool("EdgeReturn.OwnPlay",false);EditorApplication.isPlaying=false;}}
        }
    }
}
