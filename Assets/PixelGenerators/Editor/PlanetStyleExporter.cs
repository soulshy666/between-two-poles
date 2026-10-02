using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BetweenPoles.Generators {
 [InitializeOnLoad] public static class PlanetStyleExporter {
  static PlanetStyleExporter(){PixelGeneratorLab.ExportStyleRequested=lab=>Export(lab);}
  public static PlanetStyle Export(PixelGeneratorLab lab){
   if(lab.spaceMode)throw new InvalidOperationException("请在像素星球实验室中导出。");lab.EnsureData();var data=lab.catalog.models[lab.model];
   var shaders=new Shader[data.layers.Length];for(int i=0;i<shaders.Length;i++){shaders[i]=Shader.Find("PixelGenerators/Surface/"+data.layers[i].shader);if(!shaders[i])throw new InvalidOperationException("主星球支持类地、海岛、无大气、气态、冰雪和熔岩类型。星环、恒星、黑洞及小行星请使用独立预制体导出。");}
   var style=ScriptableObject.CreateInstance<PlanetStyle>();style.model=lab.model;style.modelName=data.name;style.seed=lab.seed;style.pixels=lab.pixels;style.rotation=lab.rotation;style.speed=lab.speed;style.light=lab.light;style.dither=lab.dither;
   style.data=JsonUtility.FromJson<ModelData>(JsonUtility.ToJson(data));style.surfaceShaders=shaders;
   string folder="Assets/PixelGenerators/Exports/PlanetStyles";Directory.CreateDirectory(folder);AssetDatabase.Refresh();string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+data.name+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".asset");AssetDatabase.CreateAsset(style,path);AssetDatabase.SaveAssets();
   Selection.activeObject=style;EditorGUIUtility.PingObject(style);lab.Status="已导出主星球样式。打开游玩场景，选中此资源，点击菜单：两极之间 → 生成器 → 应用选中的主星球样式。";return style;
  }
  [MenuItem("两极之间/生成器/应用选中的主星球样式")]
  public static void ApplySelected(){var style=Selection.activeObject as PlanetStyle;if(!style){Debug.LogWarning("请在 Project 中选中导出的主星球样式 .asset。");return;}Apply(style);}
  public static MainPlanetStyle Apply(PlanetStyle style){
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();CurvedTrialCamera view=null;foreach(var c in UnityEngine.Object.FindObjectsOfType<CurvedTrialCamera>())if(c.gameObject.scene==scene){view=c;break;}
   if(!view||!view.planet)throw new InvalidOperationException("请先打开游玩场景。");
   var component=view.planet.GetComponent<MainPlanetStyle>();if(!component)component=Undo.AddComponent<MainPlanetStyle>(view.planet.gameObject);Undo.RecordObject(component,"应用主星球样式");component.style=style;component.targetCamera=view.GetComponent<Camera>();component.Rebuild();EditorUtility.SetDirty(component);EditorSceneManager.MarkSceneDirty(scene);return component;
  }
 }
 [CustomEditor(typeof(PlanetStyle))] public sealed class PlanetStyleEditor:Editor {
  public override void OnInspectorGUI(){DrawDefaultInspector();EditorGUILayout.HelpBox("保存了实验室的参数和逐层颜色。应用后仍使用立体球体；球面投影与实验室的二维预览会略有区别。",MessageType.Info);if(GUILayout.Button("应用到当前游玩场景主星球")){try{PlanetStyleExporter.Apply((PlanetStyle)target);}catch(Exception e){Debug.LogWarning(e.Message);}}if(GUILayout.Button("导出样式资源包 .unitypackage")){string path=EditorUtility.SaveFilePanel("导出主星球样式包","",target.name,"unitypackage");if(path!="")AssetDatabase.ExportPackage(new[]{AssetDatabase.GetAssetPath(target),"Assets/PixelGenerators/Editor/PlanetStyleExporter.cs","Assets/IceWorldTrial/MainPlanetStyle.cs","Assets/PixelGenerators/PixelPlanets-LICENSE.txt"},path,ExportPackageOptions.IncludeDependencies);}}
 }
 [CustomEditor(typeof(MainPlanetStyle))] public sealed class MainPlanetStyleEditor:Editor {
  public override void OnInspectorGUI(){DrawDefaultInspector();if(GUILayout.Button("恢复原来的星球样式")){var planet=(MainPlanetStyle)target;Undo.RecordObject(planet,"恢复原星球样式");planet.style=null;planet.Rebuild();EditorUtility.SetDirty(planet);EditorSceneManager.MarkSceneDirty(planet.gameObject.scene);} }
 }
}
