using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace BetweenPoles.Generators {
 [InitializeOnLoad] public static class UnityGeneratorAssetExporter {
  static UnityGeneratorAssetExporter(){PixelGeneratorLab.ExportUnityRequested=(lab,package)=>Export(lab,package);}
  public static string Export(PixelGeneratorLab source,bool package){
   source.Apply();
   string folder="Assets/PixelGenerators/Exports/"+(source.spaceMode?"Space_":"Planet_")+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
   Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var scene=EditorSceneManager.NewPreviewScene();string path=folder+"/"+(source.spaceMode?"动态太空背景":"像素星球")+".prefab";
   try {
    var root=new GameObject(source.spaceMode?"动态太空背景":"像素星球");root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
    var lab=root.AddComponent<PixelGeneratorLab>();EditorUtility.CopySerialized(source,lab);
    lab.previewCamera=null;lab.standaloneAsset=true;lab.showPanel=false;lab.visuals=new List<VisualBinding>();
    var meshes=new Dictionary<Mesh,Mesh>();var materials=new Dictionary<Material,Material>();int index=0;
    foreach(var binding in source.visuals){
     if(!binding.renderer||(!source.spaceMode&&binding.model!=source.model))continue;
     var src=binding.renderer;var go=new GameObject(src.name);go.transform.SetParent(root.transform,false);
     go.transform.localPosition=source.transform.InverseTransformPoint(src.transform.position);go.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*src.transform.rotation;go.transform.localScale=src.transform.localScale;
     Mesh original=src.GetComponent<MeshFilter>().sharedMesh;Mesh mesh;
     if(!meshes.TryGetValue(original,out mesh)){mesh=UnityEngine.Object.Instantiate(original);AssetDatabase.CreateAsset(mesh,folder+"/Mesh_"+index+".asset");meshes.Add(original,mesh);}
     go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();Material material;
     if(!materials.TryGetValue(src.sharedMaterial,out material)){material=UnityEngine.Object.Instantiate(src.sharedMaterial);if(source.spaceMode)material.renderQueue=binding.layer==-1?900:binding.layer==100?1100:1000+binding.layer;AssetDatabase.CreateAsset(material,folder+"/Material_"+index+".mat");materials.Add(src.sharedMaterial,material);}
     renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
     lab.visuals.Add(new VisualBinding{renderer=renderer,model=binding.model,layer=binding.layer,origin=binding.origin,wrap=binding.wrap});index++;
    }
    if(source.spaceMode){var follower=root.AddComponent<GeneratedBackgroundFollower>();follower.environment=lab;}else root.AddComponent<GeneratedPlanetBillboard>();
    lab.Apply();root.SetActive(true);PrefabUtility.SaveAsPrefabAsset(root,path);
   } finally {EditorSceneManager.ClosePreviewScene(scene);}
   AssetDatabase.SaveAssets();
   if(package){string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../PixelGeneratorExports/UnityPackages"));Directory.CreateDirectory(dir);string output=Path.Combine(dir,Path.GetFileName(folder)+".unitypackage");var items=new List<string>{path,"Assets/PixelGenerators/Scripts","Assets/PixelGenerators/Editor"};foreach(string license in Directory.GetFiles("Assets/PixelGenerators","*LICENSE*"))if(!license.EndsWith(".meta"))items.Add(license.Replace('\\','/'));AssetDatabase.ExportPackage(items.ToArray(),output,ExportPackageOptions.IncludeDependencies|ExportPackageOptions.Recurse);source.Status="资源包已导出："+output;}else source.Status="预制体已导出："+path;
   Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(path);EditorGUIUtility.PingObject(Selection.activeObject);return path;
  }
  [MenuItem("两极之间/生成器/将选中的背景预制体应用到当前场景")]
  static void ApplySelected(){var prefab=Selection.activeObject as GameObject;if(!prefab||!PrefabUtility.IsPartOfPrefabAsset(prefab)||!prefab.GetComponent<PixelGeneratorLab>()||!prefab.GetComponent<PixelGeneratorLab>().spaceMode){Debug.LogWarning("请先选中导出的太空背景预制体。");return;}Install(prefab);}
  public static GameObject Install(GameObject prefab){
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   foreach(var lab in UnityEngine.Object.FindObjectsOfType<PixelGeneratorLab>())if(lab.gameObject.scene==scene&&!lab.standaloneAsset)throw new InvalidOperationException("请先打开游玩场景，再应用背景。");
   foreach(var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>()){if(renderer.gameObject.scene!=scene||!renderer.sharedMaterial)continue;string shader=renderer.sharedMaterial.shader.name;if(shader=="BetweenPoles/LivingSpace"||shader=="BetweenPoles/SolarWonder"){Undo.RecordObject(renderer.gameObject,"停用旧背景");renderer.gameObject.SetActive(false);}}
   foreach(var old in UnityEngine.Object.FindObjectsOfType<GeneratedBackgroundFollower>())if(old.gameObject.scene==scene){Undo.RecordObject(old.gameObject,"停用旧背景");old.gameObject.SetActive(false);}
   var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);Undo.RegisterCreatedObjectUndo(instance,"导入动态背景");var follow=instance.GetComponent<GeneratedBackgroundFollower>();
   foreach(var camera in UnityEngine.Object.FindObjectsOfType<Camera>())if(camera.gameObject.scene==scene){if(camera.GetComponent("PixelWorldCamera")){follow.targetCamera=camera;break;}if(camera.CompareTag("MainCamera"))follow.targetCamera=camera;}
   PrefabUtility.RecordPrefabInstancePropertyModifications(follow);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=instance;return instance;
  }
 }
}
