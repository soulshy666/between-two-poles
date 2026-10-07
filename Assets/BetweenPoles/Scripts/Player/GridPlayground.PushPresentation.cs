using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    const float PushSampleSeconds=1f/60f;
    bool recordingPush;
    sealed class PushFrame {
        public Vector3 position,scale; public Quaternion rotation;
        public PushFrame(Transform t){position=t.position;rotation=t.rotation;scale=t.lossyScale;}
    }
    sealed class PushTrack {
        public MagnetPiece piece; public Renderer source,display;
        public readonly List<PushFrame> frames=new List<PushFrame>();
    }
    sealed class PushPresentation {
        public GameObject root; public Coroutine playback;
        public readonly List<PushTrack> tracks=new List<PushTrack>();
        public readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
    }
    readonly List<PushPresentation> pushPresentations=new List<PushPresentation>();
    void HideCommittedGeometry(PushPresentation presentation){
        foreach(var track in presentation.tracks){
            if(!track.piece||!track.piece.geometry)continue;
            foreach(var renderer in track.piece.geometry.GetComponentsInChildren<Renderer>(true)){
                if(!presentation.hidden.ContainsKey(renderer))presentation.hidden.Add(renderer,renderer.forceRenderingOff);
                renderer.forceRenderingOff=true;
            }
        }
    }
    void RemovePushPresentation(PushPresentation presentation){
        if(presentation.playback!=null)StopCoroutine(presentation.playback);
        foreach(var entry in presentation.hidden)if(entry.Key)entry.Key.forceRenderingOff=entry.Value;
        if(presentation.root){presentation.root.SetActive(false);Destroy(presentation.root);}
        pushPresentations.Remove(presentation);
    }
    void ClearPushPresentations(){
        foreach(var presentation in pushPresentations.ToArray())RemovePushPresentation(presentation);
    }
    bool MagnetStillAnimating(MagnetPiece piece){
        foreach(var presentation in pushPresentations)
            foreach(var track in presentation.tracks)if(track.piece==piece)return true;
        return false;
    }
    void FinishPushWithPresentation(){
        // Evaluate the remaining existing animation at its normal time scale.
        // Commit its logical result now; replay only renderer poses independently.
        var presentation=new PushPresentation();
        foreach(var piece in magnets){
            if(!piece||!piece.gameObject.activeInHierarchy||!piece.geometry||MagnetStillAnimating(piece))continue;
            foreach(var renderer in piece.geometry.GetComponentsInChildren<MeshRenderer>()){
                if(!renderer.enabled)continue;
                var track=new PushTrack{piece=piece,source=renderer};
                track.frames.Add(new PushFrame(renderer.transform));presentation.tracks.Add(track);
            }
        }
        if(movementPlayback!=null)StopCoroutine(movementPlayback);
        movementPlayback=null;recordingPush=true;
        try{
            while(AdvanceMovement()){
                foreach(var track in presentation.tracks)
                    track.frames.Add(new PushFrame(track.source.transform));
            }
        }finally{recordingPush=false;}
        var changed=new HashSet<MagnetPiece>();
        foreach(var track in presentation.tracks){
            var first=track.frames[0];
            foreach(var frame in track.frames)
                if((frame.position-first.position).sqrMagnitude>.000001f||Quaternion.Angle(frame.rotation,first.rotation)>.01f
                    ||(frame.scale-first.scale).sqrMagnitude>.000001f){changed.Add(track.piece);break;}
            if(!track.source.gameObject.activeInHierarchy)changed.Add(track.piece);
        }
        presentation.tracks.RemoveAll(track=>!changed.Contains(track.piece));
        if(presentation.tracks.Count==0)return;
        presentation.root=new GameObject("Remaining push animation");
        foreach(var track in presentation.tracks){
            var filter=track.source.GetComponent<MeshFilter>();if(!filter)continue;
            var go=new GameObject(track.source.name);go.layer=track.source.gameObject.layer;
            go.transform.SetParent(presentation.root.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=track.source.sharedMaterials;
            renderer.shadowCastingMode=track.source.shadowCastingMode;renderer.receiveShadows=track.source.receiveShadows;
            var block=new MaterialPropertyBlock();track.source.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
            renderer.localBounds=track.source.localBounds;track.display=renderer;
            ApplyPushFrame(track,0,0,0);
        }
        pushPresentations.Add(presentation);HideCommittedGeometry(presentation);
        presentation.playback=StartCoroutine(PlayPushPresentation(presentation));
    }
    static void ApplyPushFrame(PushTrack track,int a,int b,float t){
        if(!track.display)return;
        var first=track.frames[a];var second=track.frames[b];var transform=track.display.transform;
        transform.SetPositionAndRotation(Vector3.Lerp(first.position,second.position,t),Quaternion.Slerp(first.rotation,second.rotation,t));
        transform.localScale=Vector3.Lerp(first.scale,second.scale,t);
    }
    IEnumerator PlayPushPresentation(PushPresentation presentation){
        float elapsed=0;int count=presentation.tracks[0].frames.Count;
        float duration=Mathf.Max(PushSampleSeconds,(count-1)*PushSampleSeconds);
        while(elapsed<duration){
            float sample=elapsed/PushSampleSeconds;int a=Mathf.Min((int)sample,count-1),b=Mathf.Min(a+1,count-1);
            foreach(var track in presentation.tracks)ApplyPushFrame(track,a,b,sample-a);
            yield return null;elapsed+=Time.deltaTime;
        }
        presentation.playback=null;RemovePushPresentation(presentation);
    }
}
}
