using UnityEditor;
using UnityEngine;
using BetweenPoles.PixelLab;
[CustomEditor(typeof(PixelPlanetPreview))]
public class PixelPlanetPreviewEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector(); var p=(PixelPlanetPreview)target;
        EditorGUILayout.HelpBox("无需进入播放模式。海洋、大陆、云层均为已保存的场景对象。",MessageType.Info);
        for(int i=0;i<PixelPlanetPreview.Names.Length;i++) if(GUILayout.Button(PixelPlanetPreview.Names[i])) {Undo.RecordObject(p,"切换星球配色");p.SelectPalette(i);EditorUtility.SetDirty(p);}
        if(GUILayout.Button("随机地形")){Undo.RecordObject(p,"随机星球");p.Randomize();EditorUtility.SetDirty(p);}
        if(GUILayout.Button("导出透明 PNG")) EditorUtility.RevealInFinder(p.ExportPng());
    }
}
