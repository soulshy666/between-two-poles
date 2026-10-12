using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    readonly HashSet<Transform> collectedStars=new HashSet<Transform>();
    public void ResetTestStars(){
        if(collectingStar)return;
        foreach(var tile in tiles)if(tile)foreach(var mesh in tile.GetComponentsInChildren<MeshFilter>(true)){
            if(mesh.name!="金色星星"||!mesh.transform.parent||mesh.transform.parent.name!="测试星星")continue;
            collectedStars.Remove(mesh.transform);mesh.gameObject.SetActive(true);
        }
    }
    void TryCollectStar(Vector2Int cell){
        if(collectingStar||recordingPush||!Application.isPlaying||ChapterAtlas.IsInspectionScene(gameObject.scene))return;
        foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var mesh in root.GetComponentsInChildren<MeshFilter>()){
            if(!mesh.sharedMesh||(mesh.name!="金色星星"&&mesh.sharedMesh.name!="Faceted collectible star")||collectedStars.Contains(mesh.transform))continue;
            if(Cell(mesh.transform)!=cell)continue;
            collectingStar=true;bufferedSteps.Clear();StartCoroutine(CollectStar(mesh.transform));return;
        }
    }
    IEnumerator CollectStar(Transform star){
        var start=star.position;var rotation=star.rotation;var scale=star.localScale;
        var renderer=star.GetComponent<Renderer>();var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);
        var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
        PlayerPushPose pose=null;var sparks=new List<GameObject>();bool complete=false;
        try{
            while(movementBusy)yield return null;
            if(!player||!star)yield break;
            pose=PlayerPushPose.BeginStar(player);
            Vector3 release=Vector3.zero;const float releaseTime=1.2f;
            for(float time=0;time<2.95f;time+=Mathf.Min(Time.deltaTime,.05f)){
                var hands=pose.SampleStar(time);
                if(time<releaseTime){
                    float take=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/.4f));
                    star.position=Vector3.Lerp(start,hands,take);release=hands;
                    star.localScale=scale*Mathf.Lerp(1,.6f,take);
                }else{
                    float rise=Mathf.Clamp01((time-releaseTime)/1.35f);
                    star.position=release+Vector3.up*(1.65f*(1-Mathf.Pow(1-rise,2)));
                    star.rotation=rotation*Quaternion.Euler(0,(time-releaseTime)*100,0);
                    float flash=Mathf.Sin(Mathf.Clamp01((time-2.4f)/.3f)*Mathf.PI);
                    properties.SetColor("_Color",Color.Lerp(new Color(1,.72f,.12f),new Color(2.5f,2.2f,1.2f),flash));renderer.SetPropertyBlock(properties);
                    if(time>=2.7f&&sparks.Count==0){
                        renderer.enabled=false;
                        for(int i=0;i<12;i++){
                            var spark=GameObject.CreatePrimitive(PrimitiveType.Cube);spark.name="星星金色碎光";Destroy(spark.GetComponent<Collider>());
                            spark.transform.SetParent(star.parent,true);spark.transform.position=star.position;spark.GetComponent<Renderer>().sharedMaterial=renderer.sharedMaterial;
                            spark.GetComponent<Renderer>().SetPropertyBlock(properties);sparks.Add(spark);
                        }
                    }
                    for(int i=0;i<sparks.Count;i++){
                        float fade=Mathf.Clamp01((time-2.7f)/.25f);float angle=i*Mathf.PI*2/12;
                        sparks[i].transform.position=star.position+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),Mathf.Sin(angle*3)*.35f)*fade*.55f;
                        sparks[i].transform.localScale=Vector3.one*(.07f*(1-fade));
                    }
                }
                yield return null;
            }
            complete=true;collectedStars.Add(star);star.gameObject.SetActive(false);
        }finally{
            foreach(var spark in sparks)if(spark)Destroy(spark);
            if(star){star.SetPositionAndRotation(start,rotation);star.localScale=scale;if(renderer){renderer.SetPropertyBlock(original);renderer.enabled=true;}if(!complete)star.gameObject.SetActive(true);}
            if(pose)pose.EndStar();collectingStar=false;bufferedSteps.Clear();nextHeldStep=Time.unscaledTime+.15f;
        }
    }
}
}
