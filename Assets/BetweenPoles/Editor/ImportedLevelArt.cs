using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
namespace BetweenPoles.Authoring {
public static class ImportedLevelArt {
    public static void Apply(Scene scene,string folder) {
        var shader=Shader.Find("BetweenPoles/PaintedIceTrial");
        var finish=Shader.Find("BetweenPoles/IcePaintFinish");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Prefabs/OriginalIslandRock.prefab");
        if(!shader||!finish||!prefab)throw new InvalidOperationException("缺少小岛美术材质或原版石头资源。");
        var window=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FiveIslandWindow>(true)).First();
        var rockRenderers=new HashSet<Renderer>();
        foreach(var room in window.rooms) {
            foreach(var binding in room.center.parent.GetComponentsInChildren<IslandSurfaceAnchor>(true).Where(a=>a.name=="rock").ToArray()) {
                var old=binding.transform.Find("rock");
                var geometry=binding.transform.Find("原版石头模型");
                if(!geometry) {
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    Undo.RegisterCreatedObjectUndo(instance,"恢复原版石头");
                    instance.name="原版石头模型";instance.transform.SetParent(binding.transform,false);
                    instance.transform.localPosition=new Vector3(0,.48f,0);
                    geometry=instance.transform;
                }
                // Only replace the known importer-generated cube, preserving its logical owner.
                if(old && old.GetComponent<MeshFilter>())Undo.DestroyObjectImmediate(old.gameObject);
                foreach(var r in geometry.GetComponentsInChildren<Renderer>(true))rockRenderers.Add(r);
            }
            Undo.RecordObject(window,"更新小岛显示引用");
            room.surfaces=room.center.parent.GetComponentsInChildren<Renderer>(true);
        }
        var clones=new Dictionary<string,Material>();
        foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) {
            var mats=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<mats.Length;i++) {
                var old=mats[i];if(!old)continue;
                bool rock=rockRenderers.Contains(renderer);
                if(!rock && old.shader.name!="BetweenPoles/ImportedIslandSurface" && old.shader!=shader)continue;
                bool ice=renderer.name=="冰面及外侧壁";
                if(old.shader==shader && Mathf.Approximately(old.GetFloat("_Painted"),ice?1:0))continue;
                string key=old.GetInstanceID()+":"+ice;
                Material mat;if(!clones.TryGetValue(key,out mat)) {
                    mat=new Material(old);mat.shader=shader;mat.SetFloat("_Painted",ice?1:0);mat.SetFloat("_Cracks",.22f);
                    if(ice){mat.color=new Color(.76f,.88f,.94f);mat.SetFloat("_Grid",1);mat.SetFloat("_Snow",1);}
                    else {mat.SetFloat("_Grid",0);mat.SetFloat("_Snow",0);}
                    AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+(ice?"PaintedIce":rock?"OriginalRock":"ProjectedObject")+".mat"));clones.Add(key,mat);
                }
                mats[i]=mat;changed=true;
            }
            if(changed){Undo.RecordObject(renderer,"更新小岛材质");renderer.sharedMaterials=mats;}
        }
        foreach(var root in scene.GetRootGameObjects())foreach(var anchor in root.GetComponentsInChildren<IslandSurfaceAnchor>(true)) {
            bool enabled=anchor.enabled;anchor.enabled=false;anchor.enabled=true;anchor.enabled=enabled;
        }
        var pixel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PixelWorldCamera>(true)).FirstOrDefault();
        if(pixel) {
            Undo.RecordObject(pixel,"更新冰面后处理");
            if(!pixel.outlineMaterial||pixel.outlineMaterial.shader!=finish){var mat=pixel.outlineMaterial?new Material(pixel.outlineMaterial):new Material(finish);mat.shader=finish;AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(folder+"/IceFinish.mat"));pixel.outlineMaterial=mat;}
            pixel.depthEdge=.12f;pixel.normalEdge=.08f;
        }
        window.Show(window.initialRoom);
    }
}
}
