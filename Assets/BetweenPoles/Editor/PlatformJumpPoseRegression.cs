using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.EditorTools {
public static class PlatformJumpPoseRegression {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run(){
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            var root=new GameObject("Jump pose preview");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Art/AstronautV1/AstronautVisual.prefab");
            var visual=UnityEngine.Object.Instantiate(prefab,root.transform);visual.name="Visual";
            var pose=PlayerPushPose.Begin(root.transform);var type=typeof(PlayerPushPose);
            var original=(Vector3[])type.GetField("vertices",Flags).GetValue(pose);
            var legs=(bool[])type.GetField("legs",Flags).GetValue(pose);
            var arms=(bool[])type.GetField("arms",Flags).GetValue(pose);
            var mesh=(Mesh)type.GetField("posed",Flags).GetValue(pose);
            if(!mesh)throw new Exception("Actual astronaut mesh missing");
            pose.SamplePlatformJump(.5f);var apex=mesh.vertices;
            var sums=new float[4];var counts=new int[4];
            for(int i=0;i<original.Length;i++){
                int group=arms[i]?(original[i].x<0?0:1):legs[i]?(original[i].x<0?2:3):-1;
                if(group<0)continue;sums[group]+=apex[i].z-original[i].z;counts[group]++;
            }
            for(int i=0;i<4;i++){if(counts[i]==0)throw new Exception("Limb missing");sums[i]/=counts[i];}
            if(!(sums[1]>sums[0]&&sums[2]>sums[3]))throw new Exception("Opposite arm and knee must lead");
            foreach(float p in new[]{0f,1f}){pose.SamplePlatformJump(p);var v=mesh.vertices;
                for(int i=0;i<v.Length;i++)if(Vector3.Distance(v[i],original[i])>.00001f)throw new Exception("Pose endpoint does not restore");}
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>()){
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++){var old=mats[i];mats[i]=new Material(Shader.Find("Standard"));mats[i].color=old&&old.HasProperty("_Color")?old.color:Color.white;}
                renderer.sharedMaterials=mats;
            }
            var cameraObject=new GameObject("Camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=.78f;
            camera.transform.position=new Vector3(2,.95f,2.8f);camera.transform.LookAt(new Vector3(0,.57f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.2f);
            var lightObject=new GameObject("Light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject,scene);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(35,-35,0);
            var rt=new RenderTexture(400,480,24);camera.targetTexture=rt;var sheet=new Texture2D(1600,480,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            for(int i=0;i<4;i++){pose.SamplePlatformJump(new[]{.1f,.38f,.6f,.91f}[i]);camera.Render();RenderTexture.active=rt;sheet.ReadPixels(new Rect(0,0,400,480),400*i,0);}
            sheet.Apply();RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            Directory.CreateDirectory("output");File.WriteAllBytes("output/platform-jump-poses.png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
            pose.End();File.WriteAllText("output/platform-jump-pose-result.txt","PASS: actual astronaut mesh, opposite arm/leg lead, exact neutral endpoints; four poses rendered.");
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
}
