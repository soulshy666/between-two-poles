using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class RockVisualRegression {
    static RockVisualRegression(){EditorApplication.delayCall+=()=>{if(File.Exists("output/rocks.run")){File.Delete("output/rocks.run");Run();}};}
    [MenuItem("两极之间/测试/石头外观与高度检查")]
    static void Run(){
        var root=new GameObject("Rock preview validation"){hideFlags=HideFlags.HideAndDontSave};
        try{
            var board=root.AddComponent<GridPlayground>();board.cellSize=1.5f;
            var obj=new GameObject("tile");obj.transform.SetParent(root.transform);var tile=obj.AddComponent<GridTile>();tile.blocked=true;board.tiles=new[]{tile};
            var stone=new GameObject("调试高台");stone.transform.SetParent(obj.transform);var mf=stone.AddComponent<MeshFilter>();mf.sharedMesh=new Mesh();var mr=stone.AddComponent<MeshRenderer>();
            for(int theme=0;theme<5;theme++)for(int style=0;style<3;style++)for(int variant=0;variant<(style==2?3:1);variant++){
                tile.rockTheme=theme;tile.rockStyle=style;tile.lowRockVariant=variant;IceRockVisuals.Apply(board);
                var mesh=mf.sharedMesh;if(!mesh||mesh.vertexCount==0)throw new Exception("Missing mesh");
                float h=mesh.bounds.max.y*stone.transform.localScale.y;
                if(style==2&&Mathf.Abs(h-.55f)>.001f)throw new Exception("Low stone height incorrect: "+h);
                if(variant>0){
                    var top=mesh.vertices.Where(v=>v.y*1.12f>.4f).ToArray();
                    if(top.Max(v=>v.y)-top.Min(v=>v.y)<.07f)throw new Exception("Rock crown has insufficient relief");
                    if(mesh.vertices.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)))throw new Exception("Invalid rock geometry");
                }
                if(theme==0&&mesh.GetTriangles(3).Length!=(variant==0?15:0))throw new Exception("Bare rock contains coating or drifts");
                var preview=new PreviewRenderUtility();Material[] mats=null;
                try{
                    preview.BeginStaticPreview(new Rect(0,0,600,460));preview.camera.orthographic=true;preview.camera.orthographicSize=.86f;
                    preview.camera.transform.position=new Vector3(2,1.9f,-4);preview.camera.transform.LookAt(new Vector3(0,.27f,0));
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.13f,.15f,.18f);
                    preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-35,0);preview.lights[1].intensity=.5f;
                    mats=mr.sharedMaterials.Select(m=>new Material(Shader.Find("Standard")){color=m.color}).ToArray();
                    for(int sub=0;sub<mesh.subMeshCount;sub++)preview.DrawMesh(mesh,Matrix4x4.Scale(stone.transform.localScale),mats[sub],sub);
                    preview.camera.Render();var image=preview.EndStaticPreview();File.WriteAllBytes("output/rock-"+theme+"-"+style+"-"+variant+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                    if(variant>0){
                        preview.BeginStaticPreview(new Rect(0,0,300,300));preview.camera.orthographicSize=.8f;
                        preview.camera.transform.position=new Vector3(0,5,-3);preview.camera.transform.LookAt(new Vector3(0,.27f,0));
                        for(int sub=0;sub<mesh.subMeshCount;sub++)preview.DrawMesh(mesh,Matrix4x4.Scale(stone.transform.localScale),mats[sub],sub);
                        preview.camera.Render();image=preview.EndStaticPreview();File.WriteAllBytes("output/rock-top-"+theme+"-"+variant+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                    }
                }finally{preview.Cleanup();if(mats!=null)foreach(var mat in mats)UnityEngine.Object.DestroyImmediate(mat);}
            }
            // Selecting a different theme must replace both mesh and palette; repeated Apply is stable.
            tile.rockTheme=1;tile.rockStyle=0;IceRockVisuals.Apply(board);var ice=mf.sharedMesh;
            tile.rockTheme=0;IceRockVisuals.Apply(board);if(ice==mf.sharedMesh)throw new Exception("Theme did not change");
            var bare=mf.sharedMesh;IceRockVisuals.Apply(board);if(bare!=mf.sharedMesh)throw new Exception("Mesh cache unstable");
            File.WriteAllText("output/rocks-result.txt","PASS: All 25 shape/variant/theme combinations, low height 0.55, bare geometry without coating/drifts, theme replacement and cached reapply. Preview images rendered.");
        }catch(Exception ex){File.WriteAllText("output/rocks-result.txt","FAIL: "+ex);Debug.LogException(ex);}
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
}
