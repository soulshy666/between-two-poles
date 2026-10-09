using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetweenPoles.EditorTools {
    // Scene imports, authored prefabs and the test panel must use the same sockets and geometry.
    public static class MagnetSceneConsistency {
        const string MaterialFolder="Assets/BetweenPoles/Materials/StandardMagnets";
        static readonly Dictionary<Material,Material> savedMaterials=new Dictionary<Material,Material>();

        static void PersistMaterials(Transform geometry){
            // Preserve the enlarged culling bounds used by chapter geometry: the planet
            // shader moves vertices far from their logical grid positions.
            PreserveCurvedWorldBounds(geometry);
            if(!AssetDatabase.IsValidFolder(MaterialFolder))AssetDatabase.CreateFolder("Assets/BetweenPoles/Materials","StandardMagnets");
            foreach(var renderer in geometry.GetComponentsInChildren<Renderer>(true)){
                var source=renderer.sharedMaterial;
                if(AssetDatabase.Contains(source))continue;
                if(!savedMaterials.TryGetValue(source,out var saved)){
                    string path=MaterialFolder+"/"+(source.color.r>source.color.b?"North":"South")+".mat";
                    saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!saved){saved=new Material(source);AssetDatabase.CreateAsset(saved,path);}
                    savedMaterials[source]=saved;
                }
                renderer.sharedMaterial=saved;
            }
        }

        public static void PreserveCurvedWorldBounds(Transform geometry){
            var cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/IceWorldTrial/LargeBounds_0.asset");
            if(!cube)throw new InvalidOperationException("Missing curved-world cube mesh");
            foreach(var filter in geometry.GetComponentsInChildren<MeshFilter>(true)){
                if(filter.sharedMesh&&filter.sharedMesh.name=="Cube"){
                    filter.sharedMesh=cube;
                    if(PrefabUtility.IsPartOfPrefabInstance(filter))PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                    EditorUtility.SetDirty(filter);
                }
            }
        }

        public static void ReplaceSingleGeometry(MagnetPiece piece,float cellSize){
            if(!piece||piece.combined||piece.product!=MagnetProduct.None||!piece.geometry)return;
            var old=piece.geometry;
            var pose=piece.Pose;
            if(piece.shape==MagnetShape.Horseshoe)pose=MagnetPiece.FlatUPose(pose);
            Undo.RecordObject(piece,"统一磁铁模型");
            piece.geometry=MagnetVisuals.Build(piece.transform,piece.shape,piece.north,MagnetProduct.None,cellSize,piece.north,Vector2Int.right);
            Undo.RegisterCreatedObjectUndo(piece.geometry.gameObject,"统一磁铁模型");
            piece.geometry.rotation=pose;
            MagnetVisuals.Ground(piece);
            PersistMaterials(piece.geometry);
            Undo.DestroyObjectImmediate(old.gameObject);
            if(PrefabUtility.IsPartOfPrefabInstance(piece))PrefabUtility.RecordPrefabInstancePropertyModifications(piece);
            EditorUtility.SetDirty(piece);
            var binding=piece.GetComponent<IslandSurfaceAnchor>();
            if(binding){bool enabled=binding.enabled;binding.enabled=false;binding.enabled=enabled;binding.Apply();}
        }

        public static void ReusePrefabGeometry(MagnetPiece piece){
            if(!PrefabUtility.IsPartOfPrefabInstance(piece))return;
            var old=piece.geometry;var pose=piece.Pose;
            var property=new SerializedObject(piece).FindProperty("geometry");
            if(property.prefabOverride)PrefabUtility.RevertPropertyOverride(property,InteractionMode.AutomatedAction);
            if(piece.geometry!=old&&old)Undo.DestroyObjectImmediate(old.gameObject);
            piece.geometry.rotation=pose;MagnetVisuals.Ground(piece);
            PrefabUtility.RecordPrefabInstancePropertyModifications(piece.geometry);
            EditorUtility.SetDirty(piece);
        }

        [MenuItem("两极之间/统一章节磁铁模型")]
        public static void Migrate(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出试玩。");
            var original=SceneManager.GetActiveScene();
            var paths=Directory.GetFiles("Assets/BetweenPoles/Scenes","Chapter*.unity").Select(p=>p.Replace('\\','/')).ToArray();
            foreach(var path in paths){var loaded=SceneManager.GetSceneByPath(path);if(loaded.IsValid()&&loaded.isDirty)throw new InvalidOperationException("场景有未保存修改："+path);}
            string backup="output/magnet-scene-consistency-backup/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            foreach(var path in paths.Concat(new[]{"Bar N","Bar S","U N","U S"}.Select(n=>"Assets/BetweenPoles/Prefabs/"+n+".prefab")))File.Copy(path,Path.Combine(backup,Path.GetFileName(path)));
            var report=new List<string>{"Backup: "+backup};
            try{
                // Migrate scene instances before their prefab sources, recording explicit geometry overrides.
                foreach(var path in paths){
                    var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                    if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try{
                        SceneManager.SetActiveScene(scene);
                        int count=0;
                        foreach(var board in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridPlayground>(true))){
                            foreach(var piece in board.magnets){
                                if(!piece||piece.combined||piece.product!=MagnetProduct.None)continue;
                                ReplaceSingleGeometry(piece,board.cellSize);
                                // Old U prefabs had a compensating quarter-unit placement offset.
                                var position=piece.transform.position;
                                position.x=Mathf.Round(position.x/board.cellSize)*board.cellSize;
                                position.z=Mathf.Round(position.z/board.cellSize)*board.cellSize;
                                Undo.RecordObject(piece.transform,"磁铁格点对齐");piece.transform.position=position;
                                if(PrefabUtility.IsPartOfPrefabInstance(piece.transform))PrefabUtility.RecordPrefabInstancePropertyModifications(piece.transform);
                                count++;
                            }
                        }
                        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                        report.Add(path+": "+count+" singles normalized");
                    }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
                }
                foreach(var name in new[]{"Bar N","Bar S","U N","U S"}){
                    string path="Assets/BetweenPoles/Prefabs/"+name+".prefab";
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try{ReplaceSingleGeometry(root.GetComponent<MagnetPiece>(),1.5f);PrefabUtility.SaveAsPrefabAsset(root,path);}
                    finally{PrefabUtility.UnloadPrefabContents(root);}
                    report.Add(path+": normalized");
                }
                // The new source geometry must replace the temporary scene override;
                // otherwise a prefab update introduces a second visible copy.
                foreach(var path in paths){
                    var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                    if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try{
                        foreach(var board in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridPlayground>(true)))
                            foreach(var m in board.magnets)if(m&&!m.combined)ReusePrefabGeometry(m);
                        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    }finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
                }
                AssetDatabase.SaveAssets();
                report.Add("PASS: migration complete");
            }finally{
                if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
                File.WriteAllLines("output/magnet-scene-consistency-migration.txt",report);
            }
        }
    }
}
