using System;
using System.Reflection;
using BetweenPoles.Generators;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetweenPoles.EditorTools {
public static class ChapterBackgroundRegression {
    static PixelGeneratorLab Background(Scene scene,bool visible){
        var root=new GameObject("动态太空背景");root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root,scene);
        var lab=root.AddComponent<PixelGeneratorLab>();lab.spaceMode=true;
        root.SetActive(visible);return lab;
    }
    static PixelGeneratorLab Select(Scene scene){
        return (PixelGeneratorLab)typeof(ChapterRecoilTravel).GetMethod("VisibleBackground",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{scene});
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    [MenuItem("两极之间/测试/连续切章背景选择")]
    public static void Run(){
        int count=0;
        foreach(bool hiddenFirst in new[]{true,false}){
            var source=EditorSceneManager.NewPreviewScene();
            var destination=EditorSceneManager.NewPreviewScene();
            try{
                PixelGeneratorLab hidden=null;
                if(hiddenFirst)hidden=Background(source,false);
                var visible=Background(source,true);
                if(!hiddenFirst)hidden=Background(source,false);
                Check(Select(source)==visible,"Selected dormant chapter background");count++;
                var target=Background(destination,false);
                SceneManager.MoveGameObjectToScene(visible.gameObject,destination);
                Check(Select(destination)==visible,"Next chapter lost the carried background");count++;
                Check(!hidden.gameObject.activeSelf&&!target.gameObject.activeSelf,"Template became visible");count++;
                // The in-flight undo moves the same root back; selection must remain stable.
                SceneManager.MoveGameObjectToScene(visible.gameObject,source);
                Check(Select(source)==visible,"Undo selected the hidden template");count++;
                visible.enabled=false;
                Check(Select(source)==null,"Disabled generator selected for animation");count++;
            }finally{
                EditorSceneManager.ClosePreviewScene(source);
                EditorSceneManager.ClosePreviewScene(destination);
            }
        }
        Debug.Log("ChapterBackgroundRegression PASS "+count);
    }
}
}
