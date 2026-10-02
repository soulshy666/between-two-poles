using UnityEditor;
using UnityEngine;
using BetweenPoles.Generators;
[CustomEditor(typeof(PixelGeneratorLab))]
public class PixelGeneratorLabEditor:Editor {
 public override void OnInspectorGUI(){DrawDefaultInspector();var lab=(PixelGeneratorLab)target;lab.EnsureData();
  EditorGUILayout.Space();EditorGUILayout.LabelField("逐色编辑（实时预览）",EditorStyles.boldLabel);var colors=lab.GetColors();bool changed=false;
  for(int i=0;i<colors.Count;i++){Color c=EditorGUILayout.ColorField("颜色 "+(i+1),colors[i]);if(c!=colors[i]){Undo.RecordObject(lab,"修改星球颜色");colors[i]=c;changed=true;}}
  if(changed)lab.SetColors(colors);
  if(GUILayout.Button("随机配色"))lab.RandomColors();if(GUILayout.Button("恢复原色"))lab.ResetColors();
  if(GUILayout.Button("导出 Unity 预制体"))lab.RequestUnityExport(false);if(GUILayout.Button("导出 Unity 资源包"))lab.RequestUnityExport(true);
  if(!lab.spaceMode&&GUILayout.Button("导出主星球样式（游玩场景使用）"))lab.RequestStyleExport();
  if(!lab.standaloneAsset && GUILayout.Button("导出 PNG"))lab.BeginExport("png");if(GUILayout.Button("保存方案"))lab.SaveSettings();lab.Apply();
 }
}
