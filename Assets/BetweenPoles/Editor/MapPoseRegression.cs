using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
    [InitializeOnLoad] static class MapPoseRegression {
        const string Request="output/map-pose.run",Report="output/map-pose-regression.txt";
        static int stage;static double deadline;static ChapterMapReturn map;static GridPlayground board;static Vector3 position;static Quaternion rotation;
        static bool background;
        static MapPoseRegression(){EditorApplication.update+=Tick;}
        [MenuItem("两极之间/验证电子地图动作")]
        static void Run(){File.WriteAllText(Request,"");}
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Tick(){
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(stage==0){
                if(!File.Exists(Request))return;
                if(!EditorApplication.isPlaying){SessionState.SetBool("MapPoseTest.OwnPlay",true);EditorApplication.isPlaying=true;return;}
                File.Delete(Request);background=Application.runInBackground;Application.runInBackground=true;stage=1;deadline=EditorApplication.timeSinceStartup+35;
            }
            try{
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Stage "+stage+" timeout");
                if(stage==1){
                    map=UnityEngine.Object.FindObjectOfType<ChapterMapReturn>();if(!map)return;
                    board=map.GetComponent<GridPlayground>();if(!board||board.Busy||board.OpeningCinematic)return;
                    position=board.player.position;rotation=board.player.rotation;map.ToggleMap();stage=2;return;
                }
                var pose=board.player.GetComponent<PlayerPushPose>();
                if(stage==2){
                    if(!map.IsLocalOverview)return;
                    Check(pose.ReadingMap&&!board.enabled&&board.player.gameObject.activeInHierarchy,"Map did not keep visible reading pose/input lock");
                    Check(board.player.GetComponentInChildren<MeshFilter>()!=null,"Missing player mesh");
                    CapturePose(board.player);
                    var sample=typeof(PlayerPushPose).GetMethod("SampleMap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    foreach(float progress in new[]{0f,.15f,.3f,.5f,.7f,1f}){
                        sample.Invoke(pose,new object[]{progress});CapturePose(board.player);
                        File.Copy("output/map-pose-preview.png","output/map-tablet-"+Mathf.RoundToInt(progress*100)+".png",true);
                    }
                    stage=3;deadline=EditorApplication.timeSinceStartup+5;return;
                }
                if(stage==3){
                    if(EditorApplication.timeSinceStartup<deadline-1)return;
                    Check(pose.ReadingMap&&board.player.position==position&&board.player.rotation==rotation,"Inspection changed player or lost pose");
                    map.ToggleMap();stage=4;deadline=EditorApplication.timeSinceStartup+8;return;
                }
                if(stage==4){
                    if(!board.enabled)return;
                    Check(!pose.ReadingMap&&!map.IsLocalOverview,"Stow did not finish");
                    Check(board.player.position==position&&board.player.rotation==rotation,"Return changed logical player pose");
                    map.OpenLocalOverview();map.ReturnDirectlyToGame();stage=5;deadline=EditorApplication.timeSinceStartup+8;return;
                }
                if(stage==5){
                    if(!board.enabled)return;
                    Check(!pose.ReadingMap&&!map.IsLocalOverview,"Cancel during draw left player stuck");
                    map.OpenLocalOverview();stage=6;deadline=EditorApplication.timeSinceStartup+10;return;
                }
                if(stage==6){
                    if(!map.IsLocalOverview)return;
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    var navigation=(BlackHoleTravel)typeof(ChapterMapReturn).GetField("localNavigation",flags).GetValue(map);
                    var scroll=typeof(BlackHoleTravel).GetMethod("ApplyNavigationScroll",flags);
                    Check(!navigation.ChartZoomRequested,"Opening zoom triggered star chart");
                    scroll.Invoke(navigation,new object[]{1f});Check(!navigation.ChartZoomRequested,"Zooming in triggered star chart");
                    scroll.Invoke(navigation,new object[]{-100f});Check(navigation.ChartZoomRequested,"Zooming out past threshold did not request star chart");
                    // Let the normal Update consume the request and run the outward transition.
                    stage=7;deadline=EditorApplication.timeSinceStartup+25;return;
                }
                if(stage==7){
                    var atlas=UnityEngine.Object.FindObjectOfType<ChapterAtlas>();if(!atlas||map.gameObject.activeInHierarchy)return;
                    Check(pose.ReadingMap,"Star chart discarded reading pose");ChapterMapReturn.ResumeGame();stage=8;deadline=EditorApplication.timeSinceStartup+10;return;
                }
                if(stage==8){
                    if(!board.enabled)return;
                    Check(!pose.ReadingMap&&board.player.gameObject.activeInHierarchy&&board.player.position==position,"Star chart return did not restore actor/input");
                    File.WriteAllText(Report,"PASS: map toggle/hold/close, visible player while inspecting, input lock, unchanged grid position/facing, cancellation during draw, no chart on opening/inward zoom, outward zoom threshold and automatic transition, star chart suspension and animated return.");Finish();
                }
            }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);Finish();}
        }
        static void Finish(){Application.runInBackground=background;stage=0;if(SessionState.GetBool("MapPoseTest.OwnPlay",false)){SessionState.SetBool("MapPoseTest.OwnPlay",false);EditorApplication.isPlaying=false;}}
        static void CapturePose(Transform player){
            // Isolated geometry preview; never move or change the live actor.
            var clone=UnityEngine.Object.Instantiate(player.Find("Visual").gameObject);clone.transform.SetPositionAndRotation(new Vector3(10000,0,10000),Quaternion.identity);
            foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>())script.enabled=false;
            var materials=new System.Collections.Generic.List<Material>();
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>()){
                var source=renderer.sharedMaterials;var copies=new Material[source.Length];
                for(int i=0;i<source.Length;i++){var material=new Material(Shader.Find("Standard"));material.color=source[i].color;materials.Add(material);copies[i]=material;}
                renderer.sharedMaterials=copies;renderer.SetPropertyBlock(null);
            }
            var cameraObject=new GameObject("Map pose preview camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.85f;
            camera.transform.position=clone.transform.position+new Vector3(1.4f,1.25f,2.2f);camera.transform.LookAt(clone.transform.position+Vector3.up*.6f);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.2f);
            var light=cameraObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;
            var rt=new RenderTexture(640,640,24);camera.targetTexture=rt;var previous=RenderTexture.active;
            camera.Render();RenderTexture.active=rt;var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes("output/map-pose-preview.png",image.EncodeToPNG());RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(clone);UnityEngine.Object.DestroyImmediate(rt);foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
