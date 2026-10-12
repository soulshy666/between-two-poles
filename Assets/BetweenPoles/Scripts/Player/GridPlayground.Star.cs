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
        var star=FindCollectibleStar(cell);if(!star)return;
        collectingStar=true;bufferedSteps.Clear();StartCoroutine(CollectStar(star));
    }
    Transform FindCollectibleStar(Vector2Int cell){
        foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var mesh in root.GetComponentsInChildren<MeshFilter>()){
            if(!mesh.sharedMesh||(mesh.name!="金色星星"&&mesh.sharedMesh.name!="Faceted collectible star")||collectedStars.Contains(mesh.transform))continue;
            if(Cell(mesh.transform)!=cell)continue;
            return mesh.transform;
        }
        return null;
    }
    IEnumerator CollectStar(Transform star,Vector3? destination=null){
        var start=star.position;var rotation=star.rotation;var scale=star.localScale;
        var renderer=star.GetComponent<Renderer>();var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);
        var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
        PlayerPushPose pose=null;var sparks=new List<GameObject>();bool complete=false;
        var origin=player.position;var rays=new List<LineRenderer>();Material glow=null;
        Material haloMaterial=null;Texture2D haloTexture=null;GameObject burstHalo=null,bodyHalo=null;LineRenderer shockwave=null;
        Camera effectCamera=null;foreach(var view in FindObjectsOfType<IslandCamera>())if(view.board==this){effectCamera=view.GetComponent<Camera>();break;}
        var actorRenderers=player.GetComponentsInChildren<Renderer>();
        var actorGlow=new MaterialPropertyBlock();
        var previousGlow=new float[actorRenderers.Length];
        for(int i=0;i<actorRenderers.Length;i++){actorRenderers[i].GetPropertyBlock(actorGlow);previousGlow[i]=actorGlow.GetFloat("_StarAbsorbGlow");}
        try{
            while(!destination.HasValue&&movementBusy)yield return null;
            if(!player||!star)yield break;
            pose=PlayerPushPose.BeginStar(player);
            glow=new Material(Shader.Find("Sprites/Default"));
            haloTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){
                float radius=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;
                pixels[y*64+x]=new Color(1,Mathf.Lerp(.55f,1,Mathf.Clamp01(1-radius*2)),.2f+.8f*Mathf.Clamp01(1-radius*3),Mathf.Pow(Mathf.Clamp01(1-radius),2));
            }
            haloTexture.SetPixels(pixels);haloTexture.Apply();
            haloMaterial=new Material(glow);haloMaterial.mainTexture=haloTexture;
            burstHalo=CreateStarHalo("星星爆闪光晕",haloMaterial);bodyHalo=CreateStarHalo("吸收金色光晕",haloMaterial);
            var ringObject=new GameObject("星星冲击光环");ringObject.transform.SetParent(star.parent,false);
            shockwave=ringObject.AddComponent<LineRenderer>();shockwave.sharedMaterial=glow;shockwave.positionCount=65;shockwave.useWorldSpace=true;shockwave.enabled=false;
            for(int i=0;i<28;i++){
                var rayObject=new GameObject("星星金色射线");rayObject.transform.SetParent(star.parent,false);
                var ray=rayObject.AddComponent<LineRenderer>();ray.sharedMaterial=glow;ray.positionCount=2;ray.useWorldSpace=true;ray.enabled=false;ray.numCapVertices=2;rays.Add(ray);
            }
            // Walk the hands to the stationary star before attaching it. Once gripped,
            // preserve the contact offset instead of pulling the star toward the actor.
            const float contactTime=.55f,releaseTime=1.45f,chargeSeconds=.65f,fastSpinSeconds=.35f;
            const float burstTime=releaseTime+chargeSeconds+fastSpinSeconds;
            // The mesh lies in XY with its top point along +Y. Discard the display
            // prefab's 55-degree tilt before spinning around the world vertical axis.
            var upright=Quaternion.Euler(0,rotation.eulerAngles.y,0);
            var readyHands=pose.SampleStar(.3f);
            var contact=origin;
            if(destination.HasValue){
                var handOffset=readyHands-player.position;
                contact=start-handOffset;contact.y=origin.y;
            }
            var gripOffset=start-(readyHands+(contact-origin));
            Vector3 release=Vector3.zero;
            float spin=0;
            for(float time=0;time<burstTime+2.3f;time+=Mathf.Min(Time.deltaTime,.05f)){
                if(destination.HasValue)player.position=time<contactTime
                    ?Vector3.Lerp(origin,contact,Mathf.SmoothStep(0,1,time/contactTime))
                    :Vector3.Lerp(contact,destination.Value,Mathf.SmoothStep(0,1,Mathf.InverseLerp(contactTime,.95f,time)));
                float poseTime=time<contactTime?.3f*time/contactTime:.3f+time-contactTime;
                var hands=pose.SampleStar(poseTime,time-burstTime)+gripOffset;
                if(time<releaseTime){
                    star.position=time<contactTime?start:hands;release=hands;
                    star.rotation=Quaternion.Slerp(rotation,upright,Mathf.SmoothStep(0,1,Mathf.InverseLerp(contactTime,releaseTime,time)));
                    star.localScale=scale;
                }else{
                    float rise=Mathf.Clamp01((time-releaseTime)/.8f);
                    star.position=release+Vector3.up*(1.65f*(1-Mathf.Pow(1-rise,2)));
                    float charge=Mathf.Clamp01((time-releaseTime)/chargeSeconds);
                    spin+=Mathf.Lerp(180,540,1-Mathf.Pow(1-charge,2))*Mathf.Min(Time.deltaTime,.05f);
                    star.rotation=Quaternion.AngleAxis(spin,Vector3.up)*upright;
                    properties.SetColor("_Color",Color.Lerp(new Color(1,.72f,.12f),new Color(8,7,3),charge*charge));renderer.SetPropertyBlock(properties);
                    float burst=Mathf.Clamp01((time-burstTime)/.6f);
                    var center=CrashColorReveal.DisplayPoint(star.position);
                    for(int i=0;i<rays.Count;i++){
                        var ray=rays[i];float angle=i*Mathf.PI*2/rays.Count+spin*.0015f;
                        var direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),Mathf.Sin(i*2.4f)*.4f).normalized;
                        float length=(.2f+charge*charge*(i%3==0?2.5f:1.5f))*(1+burst*2.8f);
                        ray.enabled=true;ray.startWidth=(.03f+.2f*charge*charge)*(1-burst);ray.endWidth=0;
                        ray.startColor=new Color(1,Mathf.Lerp(.6f,1,charge),.25f,Mathf.Clamp01(charge*2)*(1-burst));ray.endColor=new Color(1,.55f,0,0);
                        ray.SetPosition(0,center);ray.SetPosition(1,center+direction*length);
                    }
                    star.localScale=scale*(1+.12f*charge);
                    if(time>=burstTime&&sparks.Count==0){
                        renderer.enabled=false;
                        for(int i=0;i<72;i++){
                            var spark=new GameObject("星星金色星点",typeof(MeshFilter),typeof(MeshRenderer));
                            spark.GetComponent<MeshFilter>().sharedMesh=star.GetComponent<MeshFilter>().sharedMesh;
                            spark.transform.SetParent(star.parent,true);spark.transform.position=star.position;spark.GetComponent<Renderer>().sharedMaterial=renderer.sharedMaterial;
                            spark.GetComponent<Renderer>().SetPropertyBlock(properties);sparks.Add(spark);
                        }
                    }
                    for(int i=0;i<sparks.Count;i++){
                        float age=time-burstTime;
                        float angle=i*2.399963f;
                        float y=1-2*(i+.5f)/sparks.Count;float radius=Mathf.Sqrt(1-y*y);
                        var direction=new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
                        var spread=star.position+direction*(1.1f+(i%5)*.23f);
                        float gather=Mathf.Clamp01((age-.4f-(i%8)*.045f)/1.1f);
                        var target=player.TransformPoint(new Vector3(0,.58f,0));
                        var point=Vector3.Lerp(star.position,spread,1-Mathf.Pow(1-Mathf.Clamp01(age/.4f),3));
                        if(gather>0){float t=gather*gather;var control=spread+Vector3.up*.45f+Vector3.Cross(direction,Vector3.up)*.4f;point=(1-t)*(1-t)*spread+2*(1-t)*t*control+t*t*target;}
                        sparks[i].transform.position=point;
                        sparks[i].transform.rotation=upright*Quaternion.Euler(0,age*130+i*31,0);
                        sparks[i].transform.localScale=Vector3.one*((.11f+(i%3)*.025f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,gather))));
                        if(gather>=1)sparks[i].SetActive(false);
                    }
                    float absorbAge=time-burstTime;
                    float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1.4f,absorbAge))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.85f,2.3f,absorbAge)));
                    for(int i=0;i<actorRenderers.Length;i++)if(actorRenderers[i]){actorRenderers[i].GetPropertyBlock(actorGlow);actorGlow.SetFloat("_StarAbsorbGlow",previousGlow[i]+rim*3.5f*(1+.12f*Mathf.Sin(absorbAge*24)));actorRenderers[i].SetPropertyBlock(actorGlow);}
                    var cameraRotation=effectCamera?effectCamera.transform.rotation:Quaternion.identity;
                    float displayScale=Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"));
                    float flash=absorbAge<0?charge*.3f:Mathf.Pow(1-Mathf.Clamp01(absorbAge/.5f),2);
                    SetStarHalo(burstHalo,center,cameraRotation,(absorbAge<0?1.4f:3.2f+burst*3)*displayScale,flash);
                    SetStarHalo(bodyHalo,CrashColorReveal.DisplayPoint(player.TransformPoint(new Vector3(0,.55f,0))),cameraRotation,(2.1f+.3f*Mathf.Sin(absorbAge*12))*displayScale,rim*.8f);
                    shockwave.enabled=absorbAge>=0&&absorbAge<.65f;
                    if(shockwave.enabled){
                        float progress=Mathf.Clamp01(absorbAge/.65f);shockwave.widthMultiplier=.14f*(1-progress)*displayScale;
                        shockwave.startColor=shockwave.endColor=new Color(1,.85f,.25f,1-progress);
                        for(int i=0;i<65;i++){float angle=i*Mathf.PI*2/64;shockwave.SetPosition(i,center+cameraRotation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*(.25f+progress*2.6f)*displayScale);}
                    }
                }
                yield return null;
            }
            complete=true;collectedStars.Add(star);star.gameObject.SetActive(false);
        }finally{
            foreach(var spark in sparks)if(spark)Destroy(spark);
            foreach(var ray in rays)if(ray)Destroy(ray.gameObject);if(glow)Destroy(glow);
            if(burstHalo)Destroy(burstHalo);if(bodyHalo)Destroy(bodyHalo);if(shockwave)Destroy(shockwave.gameObject);if(haloMaterial)Destroy(haloMaterial);if(haloTexture)Destroy(haloTexture);
            for(int i=0;i<actorRenderers.Length;i++)if(actorRenderers[i]){actorRenderers[i].GetPropertyBlock(actorGlow);actorGlow.SetFloat("_StarAbsorbGlow",previousGlow[i]);actorRenderers[i].SetPropertyBlock(actorGlow);}
            if(star){star.SetPositionAndRotation(start,rotation);star.localScale=scale;if(renderer){renderer.SetPropertyBlock(original);renderer.enabled=true;}if(!complete)star.gameObject.SetActive(true);}
            if(pose)pose.EndStar();collectingStar=false;bufferedSteps.Clear();nextHeldStep=Time.unscaledTime+.15f;
        }
    }
    static GameObject CreateStarHalo(string name,Material material){
        var halo=GameObject.CreatePrimitive(PrimitiveType.Quad);halo.name=name;Destroy(halo.GetComponent<Collider>());
        halo.GetComponent<Renderer>().sharedMaterial=material;halo.SetActive(false);return halo;
    }
    static void SetStarHalo(GameObject halo,Vector3 position,Quaternion rotation,float size,float opacity){
        halo.SetActive(opacity>.001f);halo.transform.SetPositionAndRotation(position,rotation);halo.transform.localScale=Vector3.one*size;
        var block=new MaterialPropertyBlock();block.SetColor("_Color",new Color(1,1,1,opacity));halo.GetComponent<Renderer>().SetPropertyBlock(block);
    }
}
}
