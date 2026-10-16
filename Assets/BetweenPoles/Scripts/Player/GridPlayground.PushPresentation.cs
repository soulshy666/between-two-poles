using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    const float PushSampleSeconds=1f/60f;
    bool recordingPush;
    PushPresentation recordingPresentation;
    sealed class ContactEvent {
        public float seconds;public Vector3 point,direction;public MaterialPropertyBlock block;
    }
    readonly List<MagnetContactSpark> contactSparks=new List<MagnetContactSpark>();
    void AssemblyContact(MagnetPiece incoming,MagnetPiece receiver){
        var a=AssemblyBounds(AssemblyBodies(incoming.geometry));
        var b=AssemblyBounds(AssemblyBodies(receiver.geometry));
        var point=(a.ClosestPoint(b.center)+b.ClosestPoint(a.center))*.5f;
        point.y=Mathf.Max(a.max.y,b.max.y)+.025f;
        var block=new MaterialPropertyBlock();
        var reference=receiver.geometry.GetComponentInChildren<Renderer>();
        if(reference)reference.GetPropertyBlock(block);
        var contact=new ContactEvent{point=point,block=block,direction=receiver.transform.position-incoming.transform.position};
        if(recordingPush){
            if(recordingPresentation!=null){
                contact.seconds=recordingPresentation.tracks.Count>0?recordingPresentation.tracks[0].frames.Count*PushSampleSeconds:0;
                recordingPresentation.contacts.Add(contact);
            }
        }else PlayContact(contact);
    }
    void PlayMagnetPushSound(){
        if(!Application.isPlaying||recordingPush||finishingMovement)return;
        var audio=PlayerFootstepAudio.For(player);if(audio)audio.PlayPush();
    }
    void PlayContact(ContactEvent contact){
        if(Application.isPlaying&&!finishingMovement){
            if(!BlackHoleTravel.SkipTransitionActive){var audio=PlayerFootstepAudio.For(player);if(audio)audio.PlayMerge();}
            foreach(var camera in FindObjectsOfType<IslandCamera>())
                if(camera.board==this)camera.PlayMergeShake(contact.direction,cellSize);
        }
        contactSparks.RemoveAll(effect=>!effect);
        contactSparks.Add(MagnetContactSpark.Spawn(contact.point,contact.block));
    }
    sealed class PushFrame {
        public Vector3 position,scale; public Quaternion rotation;
        public PushFrame(Transform t){position=t.position;rotation=t.rotation;scale=t.lossyScale;}
    }
    sealed class PushTrack {
        public MagnetPiece piece; public Renderer source,display;
        public readonly List<PushFrame> frames=new List<PushFrame>();
    }
    sealed class PushPresentation {
        public readonly HashSet<Vector2Int> reservedCells=new HashSet<Vector2Int>();
        public GameObject root; public Coroutine playback;
        public float playbackSeconds;
        public readonly List<ContactEvent> contacts=new List<ContactEvent>();
        public readonly List<PushTrack> tracks=new List<PushTrack>();
        public readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
    }
    readonly List<PushPresentation> pushPresentations=new List<PushPresentation>();
    sealed class LiveRing {
        public MagnetPiece piece;
        public Snapshot destination;
        public Coroutine playback;
        public Vector2Int direction;
        public readonly HashSet<Vector2Int> route=new HashSet<Vector2Int>();
    }
    readonly List<LiveRing> liveRings=new List<LiveRing>();
    LiveRing pendingRingRelease;
    LiveRing PrepareLiveRing(MagnetPiece ring,Vector2Int origin,Vector2Int direction,int cells,Vector3 end,Quaternion pose,bool space){
        var position=ring.transform.position;var localPosition=ring.geometry.localPosition;var rotation=ring.geometry.rotation;bool walkable=ring.walkable;
        Snapshot destination;
        // Evaluate only the final pose once, without simulating or copying the remaining frames.
        try{
            ring.transform.position=end;ring.geometry.rotation=pose;MagnetVisuals.Ground(ring);
            ring.walkable=MagnetPiece.FlatU(pose);
            if(space)ring.geometry.position+=Vector3.up*(end.y-MagnetGeometryTop(ring));
            destination=SavePiece(ring);
        }finally{
            ring.transform.position=position;ring.geometry.localPosition=localPosition;ring.geometry.rotation=rotation;ring.walkable=walkable;
        }
        var flight=new LiveRing{piece=ring,destination=destination,direction=direction};
        for(int i=1;i<=cells;i++)flight.route.Add(origin+direction*i);
        liveRings.Add(flight);return flight;
    }
    IEnumerator ContinueLiveRing(LiveRing flight,Stack<IEnumerator> actions){
        try{
            // The handoff frame already advanced the ring; resume on the next frame.
            yield return null;
            while(actions.Count>0){
                var action=actions.Peek();
                if(!action.MoveNext()){actions.Pop();(action as System.IDisposable)?.Dispose();continue;}
                if(action.Current is IEnumerator child){actions.Push(child);continue;}
                yield return null;
            }
        }finally{
            CommitLiveRing(flight);liveRings.Remove(flight);
        }
    }
    void CommitLiveRing(LiveRing flight){
        if(!flight.piece||!flight.piece.geometry)return;
        var ring=flight.piece;var final=flight.destination;
        ring.transform.SetPositionAndRotation(final.position,final.rotation);
        ring.geometry.localPosition=final.geoPosition;ring.geometry.localRotation=final.pose;
        ring.walkable=final.walkable;
        if(final.storedInPortal){
            ring.storedInPortal=final.storedInPortal;ring.portalEntryPosition=final.portalEntryPosition;
            ring.portalRollSpeed=final.portalRollSpeed;ring.gameObject.SetActive(false);
        }
    }
    bool RingRouteReserved(Vector2Int cell){
        foreach(var flight in liveRings){
            if(!flight.piece||!flight.route.Contains(cell))continue;
            var center=flight.piece.transform.position;
            float ahead=(cell.x*cellSize-center.x)*flight.direction.x
                +(cell.y*cellSize-center.z)*flight.direction.y;
            // Release passed cells as soon as the ring clears their centre footprint.
            // Keep the current and future cells protected against crossing traffic.
            if(ahead>=-cellSize*.5f)return true;
        }
        foreach(var p in pushPresentations)if(p.reservedCells.Contains(cell))return true;return false;
    }
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
    internal void ClearPushPresentations(){
        pendingRingRelease=null;
        foreach(var flight in liveRings.ToArray()){
            if(flight.playback!=null)StopCoroutine(flight.playback);
            CommitLiveRing(flight);
        }
        liveRings.Clear();
        RestoreMagnetHover(null);barRocks.Clear();
        foreach(var presentation in pushPresentations.ToArray())RemovePushPresentation(presentation);
        foreach(var effect in contactSparks)if(effect){effect.gameObject.SetActive(false);Destroy(effect.gameObject);}
        contactSparks.Clear();
        foreach(var camera in FindObjectsOfType<IslandCamera>())
            if(camera.board==this)camera.CancelMergeShake();
    }
    bool MagnetStillAnimating(MagnetPiece piece){
        foreach(var flight in liveRings)if(flight.piece==piece)return true;
        foreach(var presentation in pushPresentations)
            foreach(var track in presentation.tracks)if(track.piece==piece)return true;
        return false;
    }
    void FinishPushWithPresentation(float catchupSeconds,Vector2Int[] reserved=null){
        // Sample the original path, including every turn and contact. Commit its
        // logical result now, then compress the render playback into the catch-up window.
        var presentation=new PushPresentation{playbackSeconds=catchupSeconds};
        if(reserved!=null)foreach(var cell in reserved)presentation.reservedCells.Add(cell);
        foreach(var piece in magnets){
            if(!piece||!piece.gameObject.activeInHierarchy||!piece.geometry||MagnetStillAnimating(piece))continue;
            foreach(var renderer in piece.geometry.GetComponentsInChildren<MeshRenderer>()){
                if(!renderer.enabled)continue;
                var track=new PushTrack{piece=piece,source=renderer};
                track.frames.Add(new PushFrame(renderer.transform));presentation.tracks.Add(track);
            }
        }
        if(movementPlayback!=null)StopCoroutine(movementPlayback);
        movementPlayback=null;recordingPush=true;recordingPresentation=presentation;
        try{
            while(AdvanceMovement()){
                foreach(var track in presentation.tracks)
                    track.frames.Add(new PushFrame(track.source.transform));
            }
        }finally{recordingPush=false;recordingPresentation=null;}
        var changed=new HashSet<MagnetPiece>();
        foreach(var track in presentation.tracks){
            var first=track.frames[0];
            foreach(var frame in track.frames)
                if((frame.position-first.position).sqrMagnitude>.000001f||Quaternion.Angle(frame.rotation,first.rotation)>.01f
                    ||(frame.scale-first.scale).sqrMagnitude>.000001f){changed.Add(track.piece);break;}
            if(!track.source.gameObject.activeInHierarchy)changed.Add(track.piece);
        }
        presentation.tracks.RemoveAll(track=>!changed.Contains(track.piece));
        if(presentation.tracks.Count==0){foreach(var contact in presentation.contacts)PlayContact(contact);return;}
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
        float speed=Mathf.Clamp(duration/Mathf.Max(.01f,presentation.playbackSeconds),1f,Mathf.Clamp(maxPushSpeedMultiplier,1f,1.5f));
        int nextContact=0;
        while(elapsed<duration){
            float sample=elapsed/PushSampleSeconds;int a=Mathf.Min((int)sample,count-1),b=Mathf.Min(a+1,count-1);
            foreach(var track in presentation.tracks)ApplyPushFrame(track,a,b,sample-a);
            while(nextContact<presentation.contacts.Count&&presentation.contacts[nextContact].seconds<=elapsed)
                PlayContact(presentation.contacts[nextContact++]);
            yield return null;elapsed+=Time.deltaTime*speed;
        }
        foreach(var track in presentation.tracks)ApplyPushFrame(track,count-1,count-1,0);
        while(nextContact<presentation.contacts.Count)PlayContact(presentation.contacts[nextContact++]);
        presentation.playback=null;RemovePushPresentation(presentation);
    }
}
}
