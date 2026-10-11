using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class MapTabletAuthoring {
    const string Request="output/map-tablet-bake.run";
    static MapTabletAuthoring(){EditorApplication.update+=Tick;}
    [MenuItem("两极之间/更新背包地图板")]
    static void Queue(){File.WriteAllText(Request,"");}
    static void Tick(){
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(Request);
        try{Bake();File.WriteAllText("output/map-tablet-bake.txt","PASS: tablet flush against backpack; no mounting rail in shared astronaut prefab.");}
        catch(Exception ex){File.WriteAllText("output/map-tablet-bake.txt",ex.ToString());}
    }
    static Material MaterialAsset(string name,Color color,Material source){
        string path="Assets/BetweenPoles/Art/AstronautV1/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
        material.color=color;EditorUtility.SetDirty(material);return material;
    }
    static void Bake(){
        const string path="Assets/BetweenPoles/Art/AstronautV1/AstronautVisual.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try{
            var body=root.GetComponentsInChildren<MeshFilter>().First(f=>f.sharedMesh.name=="AstronautBody");
            var old=body.transform.Find("电子地图板");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var rail=body.transform.Find("地图板侧挂支架");if(rail)UnityEngine.Object.DestroyImmediate(rail.gameObject);
            var source=body.GetComponent<Renderer>().sharedMaterial;
            var casing=MaterialAsset("MapTabletCase",new Color(.045f,.075f,.11f),source);
            var screen=MaterialAsset("MapTabletScreen",new Color(.12f,.85f,1f),source);
            PlayerPushPose.CreateMountedMapTablet(body.transform,casing,screen);
            PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
}
