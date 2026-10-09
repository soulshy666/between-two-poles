using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    float AttractionDuration(float seconds){return Mathf.Clamp(seconds*.55f,.5f,1.15f)/Mathf.Clamp(attractionAnimationSpeed,1f,2f);}
    // Positive acceleration up to contact, followed by an immediate stop.
    static float AttractionProgress(float p){p=Mathf.Clamp01(p);return .12f*p+.88f*p*p*p;}
    // Bar attraction starts at rest and accelerates all the way to a hard contact stop.
    static float BarAttractionProgress(float p){p=Mathf.Clamp01(p);return p*p*(.35f+.65f*p);}
    static float BarAssemblyPhase(float progress,float begin,float end){return BarAttractionProgress(Mathf.InverseLerp(begin,end,progress));}
    // Bounds are measured in logical world space, before the planet shader bends them.
    static Bounds AssemblyBounds(IEnumerable<Renderer> renderers) {
        var bounds=new Bounds();bool first=true;
        foreach(var renderer in renderers){
            var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
            foreach(var vertex in filter.sharedMesh.vertices){var point=filter.transform.TransformPoint(vertex);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
        }
        return bounds;
    }
    static IEnumerable<Renderer> AssemblyBodies(Transform geometry) {
        return geometry.GetComponentsInChildren<Renderer>(true).Where(r=>!MagnetVisuals.IsMagneticEffect(r));
    }
    static float AssemblyPhase(float progress,float begin,float end){return AttractionProgress(Mathf.InverseLerp(begin,end,progress));}
    static void PlaceAssemblyBody(MagnetPiece piece,Quaternion rotation,Vector3 scale,Vector3 center) {
        piece.geometry.rotation=rotation;piece.geometry.localScale=scale;
        var bounds=AssemblyBounds(AssemblyBodies(piece.geometry));
        piece.geometry.position+=center-bounds.center;
    }
    static void PlaceDockingBar(MagnetPiece piece,Quaternion original,Quaternion desired,Vector3 scale,
        Bounds start,Bounds end,Vector3 push,float progress){
        if(!MagnetPiece.VerticalBar(original)){
            // Equivalent ends need no extra half-turn; flat bars stay on the floor.
            if(Vector3.Dot(original*Vector3.right,desired*Vector3.right)<0)
                desired=Quaternion.AngleAxis(180,Vector3.up)*desired;
            PlaceAssemblyBody(piece,Quaternion.Slerp(original,desired,progress),scale,Vector3.Lerp(start.center,end.center,progress));
            return;
        }
        Vector3 fall=desired*Vector3.right;fall.y=0;fall.Normalize();
        if(Vector3.Dot(fall,push)<-.01f)fall=-fall;
        Vector3 axis=Vector3.Cross(Vector3.up,fall);
        float depth=Mathf.Abs(fall.x)*start.size.x+Mathf.Abs(fall.z)*start.size.z;
        Vector3 pivot=start.center+fall*(depth*.5f)-Vector3.up*(start.size.y*.5f);
        Vector3 relative=start.center-pivot;
        Quaternion turn=Quaternion.AngleAxis(90*progress,axis);
        Vector3 landing=pivot+Quaternion.AngleAxis(90,axis)*relative;
        PlaceAssemblyBody(piece,turn*original,scale,pivot+turn*relative+(end.center-landing)*progress);
    }
    IEnumerator AnimateBarDock(MagnetPiece incoming,MagnetPiece receiver,Vector3 push,MagnetProduct product,Quaternion yaw,
        Quaternion incomingPose,Quaternion receiverPose,Vector3 incomingScale,Vector3 receiverScale,
        Bounds startIncoming,Bounds startReceiver,Bounds endIncoming,Bounds endReceiver,System.Action<float> follow=null){
        bool incomingStanding=MagnetPiece.VerticalBar(incomingPose),receiverStanding=MagnetPiece.VerticalBar(receiverPose);
        Quaternion incomingEnd=product==MagnetProduct.Cross?yaw*Quaternion.Euler(0,90,0):yaw;
        Quaternion receiverEnd=receiverStanding?yaw:receiverPose;
        bool bothStanding=incomingStanding&&receiverStanding;
        bool parallelRail=product==MagnetProduct.WideBar&&incomingStanding&&!receiverStanding;
        bool fallingReceiverRail=product==MagnetProduct.WideBar&&receiverStanding&&!incomingStanding;
        bool axialFlatRails=product==MagnetProduct.WideBar&&!incomingStanding&&!receiverStanding
            &&Mathf.Abs(Vector3.Dot(incomingPose*Vector3.right,push))>.95f
            &&Mathf.Abs(Vector3.Dot(receiverPose*Vector3.right,push))>.95f;
        Bounds railStart=startIncoming;
        var railSide=Vector3.Cross(Vector3.up,push);
        railStart.center+=railSide*Vector3.Dot(endIncoming.center-startIncoming.center,railSide);
        Bounds receiverRailStart=startReceiver;
        receiverRailStart.center+=railSide*Vector3.Dot(endReceiver.center-startReceiver.center,railSide);
        Bounds stagedIncoming=startIncoming;
        stagedIncoming.center=startReceiver.center+(endIncoming.center-endReceiver.center);
        Vector3 sideFirst=startIncoming.center+(endIncoming.center-endReceiver.center);
        // Bar pairs snap together quickly; retain the same accelerating path and player timing.
        float seconds=AttractionDuration(Mathf.Max(joinSeconds,(incomingStanding||receiverStanding)?1.6f:1.35f))*.6f;
        for(float elapsed=0;elapsed<seconds;elapsed+=MovementDeltaTime){
            float p=Mathf.Clamp01(elapsed/seconds);
            follow?.Invoke(axialFlatRails?BarAssemblyPhase(p,.30f,1):BarAttractionProgress(p));
            if(axialFlatRails){
                // End-to-end bars initially touch. Establish the full side-by-side
                // clearance before their lengths overlap, then slide along the lanes.
                float lane=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.30f,p));
                float advance=BarAssemblyPhase(p,.30f,1);
                Vector3 inLane=startIncoming.center+railSide*Vector3.Dot(endIncoming.center-startIncoming.center,railSide)*lane;
                Vector3 recvLane=startReceiver.center+railSide*Vector3.Dot(endReceiver.center-startReceiver.center,railSide)*lane;
                PlaceAssemblyBody(incoming,incomingPose,incomingScale,Vector3.Lerp(inLane,endIncoming.center,advance));
                PlaceAssemblyBody(receiver,receiverPose,receiverScale,Vector3.Lerp(recvLane,endReceiver.center,advance));
                yield return null;continue;
            }
            if(fallingReceiverRail){
                // Separate the rail lanes before approaching or tipping the
                // receiver; neither body sweeps through the other rail.
                if(p<.20f){
                    float lane=BarAssemblyPhase(p,0,.20f);
                    PlaceAssemblyBody(incoming,incomingPose,incomingScale,Vector3.Lerp(startIncoming.center,railStart.center,lane));
                    PlaceAssemblyBody(receiver,receiverPose,receiverScale,Vector3.Lerp(startReceiver.center,receiverRailStart.center,lane));
                }else{
                    float dock=BarAssemblyPhase(p,.20f,1f);
                    PlaceDockingBar(incoming,incomingPose,incomingEnd,incomingScale,railStart,endIncoming,push,dock);
                    PlaceDockingBar(receiver,receiverPose,receiverEnd,receiverScale,receiverRailStart,endReceiver,push,dock);
                }
                yield return null;continue;
            }
            if(parallelRail){
                // Establish parallel lanes before lowering the upright rail, so
                // it falls beside the receiver without intersecting its body.
                float lane=BarAssemblyPhase(p,0,.20f);
                if(p<.20f)PlaceAssemblyBody(incoming,incomingPose,incomingScale,Vector3.Lerp(startIncoming.center,railStart.center,lane));
                else PlaceDockingBar(incoming,incomingPose,incomingEnd,incomingScale,railStart,endIncoming,push,BarAssemblyPhase(p,.20f,1f));
                PlaceAssemblyBody(receiver,receiverPose,receiverScale,Vector3.Lerp(startReceiver.center,endReceiver.center,lane));
                yield return null;continue;
            }
            if(bothStanding){
                // Move into parallel lanes while upright, then fall together.
                // Sideways clearance comes before approaching the receiver.
                if(p<.35f){
                    Vector3 center=Vector3.Lerp(startIncoming.center,sideFirst,BarAssemblyPhase(p,0,.12f));
                    center=Vector3.Lerp(center,stagedIncoming.center,BarAssemblyPhase(p,.12f,.35f));
                    PlaceAssemblyBody(incoming,incomingPose,incomingScale,center);
                    PlaceAssemblyBody(receiver,receiverPose,receiverScale,startReceiver.center);
                }else{
                    float fall=BarAssemblyPhase(p,.35f,1f);
                    PlaceDockingBar(incoming,incomingPose,incomingEnd,incomingScale,stagedIncoming,endIncoming,push,fall);
                    PlaceDockingBar(receiver,receiverPose,receiverEnd,receiverScale,startReceiver,endReceiver,push,fall);
                }
                yield return null;continue;
            }
            // For a standing receiver, slide the lower bar in before it falls on top.
            float inProgress=BarAssemblyPhase(p,0,receiverStanding&&!incomingStanding ? .45f : 1f);
            float recvProgress=BarAssemblyPhase(p,receiverStanding&&!incomingStanding ? .30f : 0,1f);
            PlaceDockingBar(incoming,incomingPose,incomingEnd,incomingScale,startIncoming,endIncoming,push,inProgress);
            PlaceDockingBar(receiver,receiverPose,receiverEnd,receiverScale,startReceiver,endReceiver,push,recvProgress);
            yield return null;
        }
        PlaceDockingBar(incoming,incomingPose,incomingEnd,incomingScale,bothStanding?stagedIncoming:(parallelRail||fallingReceiverRail)?railStart:startIncoming,endIncoming,push,1);
        PlaceDockingBar(receiver,receiverPose,receiverEnd,receiverScale,fallingReceiverRail?receiverRailStart:startReceiver,endReceiver,push,1);
        follow?.Invoke(1);
        AssemblyContact(incoming,receiver);
        yield return null;
    }
    static bool AxialRingDock(Quaternion incoming,Quaternion receiver,Vector2Int direction){
        var push=new Vector3(direction.x,0,direction.y);
        return Mathf.Abs(Vector3.Dot(incoming*Vector3.forward,push))>.95f&&Mathf.Abs(Vector3.Dot(receiver*Vector3.forward,push))>.95f;
    }
    static bool SideBySideRingDock(Quaternion incoming,Quaternion receiver,Vector2Int direction){
        var push=new Vector3(direction.x,0,direction.y);
        return Vector3.Dot(incoming*Vector3.forward,receiver*Vector3.forward)>.95f
            &&Mathf.Abs(Vector3.Dot(receiver*Vector3.forward,push))<.05f;
    }
    static void SampleSideBySideRingDock(float progress,float alignEnd,Quaternion incomingPose,Quaternion receiverPose,Vector3 push,
        Vector3 startIncoming,Vector3 startReceiver,Vector3 endIncoming,Vector3 endReceiver,
        out Quaternion inPose,out Quaternion recvPose,out Vector3 inCenter,out Vector3 recvCenter){
        // Adjacent, equally oriented halves meet by opposite quarter-turns.
        // Keep clearance during the turn, then close without orbiting or swapping sides.
        float turn=AssemblyPhase(progress,0,alignEnd),close=AssemblyPhase(progress,alignEnd,1);
        inPose=Quaternion.AngleAxis(DockTurn(incomingPose*Vector3.forward,push)*turn,Vector3.up)*incomingPose;
        recvPose=Quaternion.AngleAxis(DockTurn(receiverPose*Vector3.forward,-push)*turn,Vector3.up)*receiverPose;
        inCenter=Vector3.Lerp(startIncoming,endIncoming,close);
        recvCenter=Vector3.Lerp(startReceiver,endReceiver,close);
    }
    IEnumerator AnimateSideBySideRingDock(MagnetPiece incoming,MagnetPiece receiver,Vector2Int direction,
        Quaternion incomingPose,Quaternion receiverPose,Vector3 incomingScale,Vector3 receiverScale,
        Bounds startIncoming,Bounds startReceiver,Bounds endIncoming,Bounds endReceiver,System.Action<float> follow=null){
        var push=new Vector3(direction.x,0,direction.y);
        float alignSeconds=Mathf.Max(.65f,uRollSeconds),seconds=alignSeconds+Mathf.Max(1.1f,joinSeconds);
        float alignEnd=alignSeconds/seconds;
        seconds=AttractionDuration(seconds);
        for(float elapsed=0;elapsed<seconds;elapsed+=MovementDeltaTime){
            follow?.Invoke(AttractionProgress(Mathf.Clamp01(elapsed/seconds)));
            Quaternion inPose,recvPose;Vector3 inCenter,recvCenter;
            SampleSideBySideRingDock(elapsed/seconds,alignEnd,incomingPose,receiverPose,push,
                startIncoming.center,startReceiver.center,endIncoming.center,endReceiver.center,
                out inPose,out recvPose,out inCenter,out recvCenter);
            PlaceAssemblyBody(incoming,inPose,incomingScale,inCenter);
            PlaceAssemblyBody(receiver,recvPose,receiverScale,recvCenter);
            yield return null;
        }
        var yaw=Quaternion.LookRotation(-push,Vector3.up);
        PlaceAssemblyBody(incoming,MagnetPiece.UDockPose(yaw*Quaternion.Euler(0,180,0),incomingPose),incomingScale,endIncoming.center);
        PlaceAssemblyBody(receiver,MagnetPiece.UDockPose(yaw,receiverPose),receiverScale,endReceiver.center);
        follow?.Invoke(1);
        AssemblyContact(incoming,receiver);
        yield return null;
    }
    static void SampleAxialRingDock(float progress,float flipEnd,Vector3 axis,bool flipIncoming,bool flipReceiver,Quaternion incomingPose,Quaternion receiverPose,
        Vector3 startIncoming,Vector3 startReceiver,Vector3 endIncoming,Vector3 endReceiver,
        out Quaternion inPose,out Quaternion recvPose,out Vector3 inCenter,out Vector3 recvCenter){
        // Only halves facing away from their partner flip. Both retain their side.
        float delay=flipIncoming&&flipReceiver?flipEnd*.25f:0;
        inPose=Quaternion.AngleAxis(flipIncoming?180*AssemblyPhase(progress,0,flipEnd):0,axis)*incomingPose;
        recvPose=Quaternion.AngleAxis(flipReceiver?-180*AssemblyPhase(progress,delay,flipEnd+delay):0,axis)*receiverPose;
        float close=AssemblyPhase(progress,flipEnd+delay,1);
        inCenter=Vector3.Lerp(startIncoming,endIncoming,close);
        recvCenter=Vector3.Lerp(startReceiver,endReceiver,close);
    }
    static void GroundAssemblyBody(MagnetPiece piece,float bottom){
        var bounds=AssemblyBounds(AssemblyBodies(piece.geometry));
        piece.geometry.position+=Vector3.up*(bottom-bounds.min.y);
    }
    IEnumerator AnimateAxialRingDock(MagnetPiece incoming,MagnetPiece receiver,Vector2Int direction,
        Quaternion incomingPose,Quaternion receiverPose,Vector3 incomingScale,Vector3 receiverScale,
        Bounds startIncoming,Bounds startReceiver,Bounds endIncoming,Bounds endReceiver,System.Action<float> follow=null){
        var axis=new Vector3(direction.y,0,-direction.x);
        var push=new Vector3(direction.x,0,direction.y);
        bool flipIncoming=Vector3.Dot(incomingPose*Vector3.forward,push)<0;
        bool flipReceiver=Vector3.Dot(receiverPose*Vector3.forward,push)>0;
        float flipSeconds=Mathf.Max(.65f,uRollSeconds),flipSpan=flipIncoming&&flipReceiver?1.25f:1;
        float seconds=flipSeconds*flipSpan+Mathf.Max(.8f,joinSeconds*.7f),flipEnd=flipSeconds/seconds;
        seconds=AttractionDuration(seconds);
        for(float elapsed=0;elapsed<seconds;elapsed+=MovementDeltaTime){
            follow?.Invoke(AttractionProgress(Mathf.Clamp01(elapsed/seconds)));
            float p=Mathf.Clamp01(elapsed/seconds);Quaternion inPose,recvPose;Vector3 inCenter,recvCenter;
            SampleAxialRingDock(p,flipEnd,axis,flipIncoming,flipReceiver,incomingPose,receiverPose,
                startIncoming.center,startReceiver.center,endIncoming.center,endReceiver.center,
                out inPose,out recvPose,out inCenter,out recvCenter);
            PlaceAssemblyBody(incoming,inPose,incomingScale,inCenter);
            PlaceAssemblyBody(receiver,recvPose,receiverScale,recvCenter);
            float close=AssemblyPhase(p,flipEnd*flipSpan,1);
            GroundAssemblyBody(incoming,Mathf.Lerp(startIncoming.min.y,endIncoming.min.y,close));
            GroundAssemblyBody(receiver,Mathf.Lerp(startReceiver.min.y,endReceiver.min.y,close));
            yield return null;
        }
        PlaceAssemblyBody(incoming,Quaternion.AngleAxis(flipIncoming?180:0,axis)*incomingPose,incomingScale,endIncoming.center);
        PlaceAssemblyBody(receiver,Quaternion.AngleAxis(flipReceiver?-180:0,axis)*receiverPose,receiverScale,endReceiver.center);
        follow?.Invoke(1);
        AssemblyContact(incoming,receiver);
        yield return null;
    }
    static float DockTurn(Vector3 from,Vector3 to){
        float angle=Vector3.SignedAngle(from,to,Vector3.up);
        // Either half-turn is equally short; use clockwise consistently.
        if(Mathf.Abs(angle)>179.9f)return 180;
        return Mathf.Abs(angle)<.1f?0:angle;
    }
    // Rotate only as far as needed, outside the receiver. Then close along its
    // opening, where the two aligned halves cannot cross through one another.
    static void SampleRingDock(float progress,float alignEnd,Quaternion incomingPose,float turn,
        Vector3 startIncoming,Vector3 startReceiver,Vector3 endIncoming,Vector3 endReceiver,
        Vector3 opening,float approachTurn,out Quaternion pose,out Vector3 incoming,out Vector3 receiver){
        Vector3 radial=startIncoming-startReceiver;radial.y=0;
        Vector3 approach=startReceiver+opening*radial.magnitude;approach.y=startIncoming.y;
        if(alignEnd>0&&progress<alignEnd){
            float align=Mathf.SmoothStep(0,1,progress/alignEnd);
            pose=Quaternion.AngleAxis(turn*align,Vector3.up)*incomingPose;
            incoming=startReceiver+Quaternion.AngleAxis(approachTurn*align,Vector3.up)*radial;
            incoming.y=startIncoming.y;receiver=startReceiver;
        }else{
            float close=AssemblyPhase(progress,alignEnd,1);
            pose=Quaternion.AngleAxis(turn,Vector3.up)*incomingPose;
            incoming=Vector3.Lerp(approach,endIncoming,close);
            receiver=Vector3.Lerp(startReceiver,endReceiver,close);
        }
    }
    IEnumerator AnimateRingDock(MagnetPiece incoming,MagnetPiece receiver,Quaternion yaw,
        Quaternion incomingPose,Quaternion receiverPose,Vector3 incomingScale,Vector3 receiverScale,
        Bounds startIncoming,Bounds startReceiver,Bounds endIncoming,Bounds endReceiver,System.Action<float> follow=null) {
        Vector3 opening=yaw*Vector3.forward;
        float turn=DockTurn(incomingPose*Vector3.forward,-opening);
        Vector3 radial=startIncoming.center-startReceiver.center;radial.y=0;
        float approachTurn=DockTurn(radial,opening);
        // Already facing the socket from its open side: slide straight in.
        // Side/back approaches stay outside on a circular arc before closing.
        float alignSeconds=Mathf.Max(Mathf.Abs(turn)/90*.65f,Mathf.Abs(approachTurn)/90*.85f);
        float seconds=alignSeconds+Mathf.Max(1.1f,joinSeconds);
        float alignEnd=alignSeconds/seconds;
        seconds=AttractionDuration(seconds);
        for(float elapsed=0;elapsed<seconds;elapsed+=MovementDeltaTime){
            follow?.Invoke(AttractionProgress(Mathf.Clamp01(elapsed/seconds)));
            Quaternion pose;Vector3 inCenter,recvCenter;
            SampleRingDock(Mathf.Clamp01(elapsed/seconds),alignEnd,incomingPose,turn,
                startIncoming.center,startReceiver.center,endIncoming.center,endReceiver.center,
                opening,approachTurn,out pose,out inCenter,out recvCenter);
            PlaceAssemblyBody(incoming,pose,incomingScale,inCenter);
            PlaceAssemblyBody(receiver,receiverPose,receiverScale,recvCenter);
            yield return null;
        }
        PlaceAssemblyBody(incoming,MagnetPiece.UDockPose(yaw*Quaternion.Euler(0,180,0),incomingPose),incomingScale,endIncoming.center);
        PlaceAssemblyBody(receiver,receiverPose,receiverScale,endReceiver.center);
        follow?.Invoke(1);
        AssemblyContact(incoming,receiver);
        yield return null;
    }
    IEnumerator AnimateAssembly(MagnetPiece incoming,MagnetPiece receiver,Vector2Int direction,MagnetProduct product,Quaternion yaw,Vector2Int bridgeDirection,bool uNorth,bool receiverOnTop,bool incomingOnTop,Quaternion receiverLanding,System.Action<float> onSlideProgress=null) {
        // Plan the actual final sockets first. Each material approaches its own socket,
        // rather than both bodies occupying the same center before a model swap.
        var preview=new GameObject("吸合姿态预览");
        // FiveIslandWindow restores renderer.enabled each frame. An inactive root
        // cannot leak into the game even when the island refreshes visibility.
        preview.SetActive(false);
        preview.transform.SetParent(receiver.transform,false);
        preview.transform.SetPositionAndRotation(receiver.transform.position,yaw);
        var final=MagnetVisuals.Build(preview.transform,receiver.shape,receiver.north,product,cellSize,uNorth,bridgeDirection);
        if(product==MagnetProduct.Cross&&receiverOnTop){final.GetChild(0).localPosition=new Vector3(0,.36f,0);final.GetChild(1).localPosition=new Vector3(0,.12f,0);}
        if(product==MagnetProduct.Cross&&!receiverOnTop&&!incomingOnTop)final.localPosition=Vector3.down*.24f;
        var incomingParts=new List<Renderer>();var receiverParts=new List<Renderer>();
        foreach(var r in final.GetComponentsInChildren<Renderer>(true)) {
            if(MagnetVisuals.IsMagneticEffect(r))continue;
            bool belongs;
            if(product==MagnetProduct.Bridge)belongs=r.transform.IsChildOf(final.GetChild(2));
            else {var c=r.sharedMaterial.color;belongs=(c.r>c.b)==incoming.north;}
            (belongs?incomingParts:receiverParts).Add(r);
        }
        var endIncoming=AssemblyBounds(incomingParts);var endReceiver=AssemblyBounds(receiverParts);
        // Socket measurements are values; no preview objects are needed during motion.
        Destroy(preview);
        var startIncoming=AssemblyBounds(AssemblyBodies(incoming.geometry));
        var startReceiver=AssemblyBounds(AssemblyBodies(receiver.geometry));
        Quaternion poseIncoming=incoming.Pose,poseReceiver=receiver.Pose;
        Quaternion endPoseIncoming=yaw,endPoseReceiver=receiverLanding;
        if(product==MagnetProduct.Cross){endPoseIncoming=yaw*Quaternion.Euler(0,90,0);endPoseReceiver=yaw;}
        if(product==MagnetProduct.Ring){endPoseIncoming=yaw*Quaternion.Euler(0,180,0);endPoseReceiver=yaw;}
        if(product==MagnetProduct.Lift){endPoseIncoming=incoming.shape==MagnetShape.Bar?yaw*Quaternion.Euler(0,0,90):yaw;endPoseReceiver=receiver.shape==MagnetShape.Bar?yaw*Quaternion.Euler(0,0,90):yaw;}
        if(product==MagnetProduct.BridgeHalf){endPoseIncoming=incoming.shape==MagnetShape.Horseshoe?yaw*Quaternion.Euler(0,90,0):yaw;endPoseReceiver=receiver.shape==MagnetShape.Horseshoe?yaw*Quaternion.Euler(0,90,0):yaw;}
        if(product==MagnetProduct.Bridge)endPoseReceiver=yaw;
        if(product==MagnetProduct.WideBar)endPoseReceiver=yaw;
        bool slideDocking=product==MagnetProduct.BridgeHalf||product==MagnetProduct.Bridge||product==MagnetProduct.Lift;
        // A flipped U docks on its current face, without tipping upright to align.
        if(slideDocking&&incoming.shape==MagnetShape.Horseshoe)
            endPoseIncoming=MagnetPiece.UDockPose(endPoseIncoming,poseIncoming);
        if(slideDocking&&receiver.product==MagnetProduct.None&&receiver.shape==MagnetShape.Horseshoe)
            endPoseReceiver=MagnetPiece.UDockPose(endPoseReceiver,poseReceiver);
        // A bar has equivalent forward/backward orientations. Keep an already aligned
        // bar's exact pose instead of turning it to the recipe's canonical orientation.
        if(slideDocking&&incoming.shape==MagnetShape.Bar&&Mathf.Abs(Vector3.Dot(poseIncoming*Vector3.right,endPoseIncoming*Vector3.right))>.95f)
            endPoseIncoming=poseIncoming;
        if(slideDocking&&receiver.product==MagnetProduct.None&&receiver.shape==MagnetShape.Bar
            &&Mathf.Abs(Vector3.Dot(poseReceiver*Vector3.right,endPoseReceiver*Vector3.right))>.95f)
            endPoseReceiver=poseReceiver;
        var scaleIncoming=incoming.geometry.localScale;var scaleReceiver=receiver.geometry.localScale;
        if((product==MagnetProduct.BridgeHalf||product==MagnetProduct.Lift)&&incoming.shape==MagnetShape.Bar){
            // Lift vertically before crossing the U, align above it, and descend
            // only once over the final rail or lift-column socket. The vertical
            // extents keep the whole upright bar above the U during flight.
            float clearance=Mathf.Max(startReceiver.max.y,endReceiver.max.y)
                +Mathf.Max(startIncoming.extents.y,endIncoming.extents.y)+.35f;
            float height=Mathf.Max(clearance,startIncoming.center.y+.85f);
            float duration=.85f/Mathf.Clamp(attractionAnimationSpeed,1f,2f);
            for(float elapsed=0;elapsed<duration;elapsed+=MovementDeltaTime){
                float p=Mathf.Clamp01(elapsed/duration);
                float lift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.28f,p));
                float flight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,.78f,p));
                float land=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,p));
                var center=Vector3.Lerp(startIncoming.center,endIncoming.center,flight);
                center.y=p<.28f?Mathf.Lerp(startIncoming.center.y,height,lift)
                    :Mathf.Lerp(height,endIncoming.center.y,land)+Mathf.Sin(flight*Mathf.PI)*cellSize*.22f;
                PlaceAssemblyBody(incoming,Quaternion.Slerp(poseIncoming,endPoseIncoming,flight),scaleIncoming,center);
                PlaceAssemblyBody(receiver,Quaternion.Slerp(poseReceiver,endPoseReceiver,flight),scaleReceiver,
                    Vector3.Lerp(startReceiver.center,endReceiver.center,flight));
                onSlideProgress?.Invoke(p);yield return null;
            }
            PlaceAssemblyBody(incoming,endPoseIncoming,scaleIncoming,endIncoming.center);
            PlaceAssemblyBody(receiver,endPoseReceiver,scaleReceiver,endReceiver.center);
            onSlideProgress?.Invoke(1);AssemblyContact(incoming,receiver);
            yield return null;yield break;
        }
        float rollDegrees;var rolled=MagnetPiece.Rolled(incoming.shape,poseIncoming,direction,out rollDegrees);
        var rollAxis=new Vector3(direction.y,0,-direction.x);
        var forward=new Vector3(direction.x,0,direction.y);var lateral=Vector3.Cross(Vector3.up,forward);
        if(product==MagnetProduct.Ring){
            var push=new Vector3(direction.x,0,direction.y);
            if(SideBySideRingDock(poseIncoming,poseReceiver,direction)){
                yield return AnimateSideBySideRingDock(incoming,receiver,direction,poseIncoming,poseReceiver,
                    scaleIncoming,scaleReceiver,startIncoming,startReceiver,endIncoming,endReceiver,onSlideProgress);
                yield break;
            }
            if(AxialRingDock(poseIncoming,poseReceiver,direction)
                &&(Vector3.Dot(poseIncoming*Vector3.forward,push)<0||Vector3.Dot(poseReceiver*Vector3.forward,push)>0)){
                yield return AnimateAxialRingDock(incoming,receiver,direction,poseIncoming,poseReceiver,
                    scaleIncoming,scaleReceiver,startIncoming,startReceiver,endIncoming,endReceiver,onSlideProgress);
                yield break;
            }
            yield return AnimateRingDock(incoming,receiver,yaw,poseIncoming,poseReceiver,
                scaleIncoming,scaleReceiver,startIncoming,startReceiver,endIncoming,endReceiver,onSlideProgress);
            yield break;
        }
        if(incoming.shape==MagnetShape.Bar&&receiver.shape==MagnetShape.Bar
            &&(product==MagnetProduct.WideBar||product==MagnetProduct.Cross)){
            yield return AnimateBarDock(incoming,receiver,forward,product,yaw,poseIncoming,poseReceiver,
                scaleIncoming,scaleReceiver,startIncoming,startReceiver,endIncoming,endReceiver,onSlideProgress);
            yield break;
        }
        float seconds=Mathf.Max(1.1f,joinSeconds);
        // Mixed recipes slide without a preparatory roll or standing-bar fall.
        if(slideDocking)seconds*=.70f;
        seconds=AttractionDuration(seconds);
        for(float elapsed=0;elapsed<seconds;elapsed+=MovementDeltaTime){
            float p=Mathf.Clamp01(elapsed/seconds),roll=AssemblyPhase(p,0,.48f),align=AssemblyPhase(p,.38f,.70f),travel=AssemblyPhase(p,.48f,1f);
            Quaternion inPose=Quaternion.Slerp(Quaternion.AngleAxis(rollDegrees*roll,rollAxis)*poseIncoming,endPoseIncoming,align);
            if(slideDocking)inPose=Quaternion.Slerp(poseIncoming,endPoseIncoming,AssemblyPhase(p,0,1f));
            // A standing receiver falls in the push direction, with explicit signed rotation.
            Quaternion recvPose;
            if(!slideDocking&&(receiverOnTop||MagnetPiece.VerticalBar(poseReceiver)&&receiver.shape==MagnetShape.Bar)){float sign=(poseReceiver*Vector3.right).y>=0?1:-1;recvPose=Quaternion.AngleAxis(90*sign*AssemblyPhase(p,.30f,.82f),rollAxis)*poseReceiver;}
            else recvPose=Quaternion.Slerp(poseReceiver,endPoseReceiver,AssemblyPhase(p,0,.48f));
            Vector3 lane=startIncoming.center+lateral*Vector3.Dot(endIncoming.center-startIncoming.center,lateral)*AssemblyPhase(p,.12f,.45f);
            Vector3 inCenter=Vector3.Lerp(lane,endIncoming.center,travel);
            if(slideDocking){
                float slideProgress=AssemblyPhase(p,0,1f);
                inCenter=Vector3.Lerp(startIncoming.center,endIncoming.center,slideProgress);
                onSlideProgress?.Invoke(slideProgress);
            }
            Vector3 recvCenter=Vector3.Lerp(startReceiver.center,endReceiver.center,AssemblyPhase(p,.05f,.45f));
            PlaceAssemblyBody(incoming,inPose,scaleIncoming,inCenter);PlaceAssemblyBody(receiver,recvPose,scaleReceiver,recvCenter);
            // Keep rotating bodies on their support plane, rather than rotating through the ice.
            // Upper/lower cross layers reach their support height before horizontal docking.
            var ib=AssemblyBounds(AssemblyBodies(incoming.geometry));var rb=AssemblyBounds(AssemblyBodies(receiver.geometry));
            float inBottom=Mathf.Lerp(startIncoming.min.y,endIncoming.min.y,AssemblyPhase(p,.12f,.45f));
            float recvBottom=Mathf.Lerp(startReceiver.min.y,endReceiver.min.y,AssemblyPhase(p,.05f,.45f));
            incoming.geometry.position+=Vector3.up*(inBottom-ib.min.y);receiver.geometry.position+=Vector3.up*(recvBottom-rb.min.y);
            yield return null;
        }
        PlaceAssemblyBody(incoming,endPoseIncoming,scaleIncoming,endIncoming.center);
        PlaceAssemblyBody(receiver,endPoseReceiver,scaleReceiver,endReceiver.center);
        if(slideDocking)onSlideProgress?.Invoke(1f);
        AssemblyContact(incoming,receiver);
        // Show the contact pose for one frame before committing the logical recipe.
        yield return null;
    }
}
}
