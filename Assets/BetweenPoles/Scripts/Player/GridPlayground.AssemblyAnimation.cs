using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    // Bounds are measured in logical world space, before the planet shader bends them.
    static Bounds AssemblyBounds(IEnumerable<Renderer> renderers) {
        var bounds=new Bounds();bool first=true;
        foreach(var renderer in renderers){
            var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
            foreach(var vertex in filter.sharedMesh.vertices){var point=filter.transform.TransformPoint(vertex);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
        }
        return bounds;
    }
    static float AssemblyPhase(float progress,float begin,float end){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(begin,end,progress));}
    static void PlaceAssemblyBody(MagnetPiece piece,Quaternion rotation,Vector3 scale,Vector3 center) {
        piece.geometry.rotation=rotation;piece.geometry.localScale=scale;
        var bounds=AssemblyBounds(piece.geometry.GetComponentsInChildren<Renderer>());
        piece.geometry.position+=center-bounds.center;
    }
    // Two supported quarter-turns with a readable upright pause.
    public static float ForwardWideAngle(float progress) {
        return 90*AssemblyPhase(progress,0,.42f)+90*AssemblyPhase(progress,.50f,.92f);
    }
    IEnumerator AnimateForwardWide(MagnetPiece incoming,MagnetPiece receiver,Vector3 forward,Vector3 axis,
        Quaternion incomingPose,Quaternion receiverPose,Vector3 incomingScale,Vector3 receiverScale,
        Bounds startIncoming,Bounds startReceiver,Bounds endIncoming,Bounds endReceiver) {
        float length=Mathf.Abs(forward.x)*startIncoming.size.x+Mathf.Abs(forward.z)*startIncoming.size.z;
        float thickness=startIncoming.size.y;
        Vector3 pivot=startIncoming.center+forward*(length*.5f)-Vector3.up*(thickness*.5f);
        Vector3 relative=startIncoming.center-pivot;
        Vector3 uprightCenter=pivot+Quaternion.AngleAxis(90,axis)*relative;
        Vector3 nextPivot=pivot+forward*thickness;
        Vector3 rollEnd=nextPivot+Quaternion.AngleAxis(90,axis)*(uprightCenter-nextPivot);
        float duration=Mathf.Max(1.55f,joinSeconds);
        for(float elapsed=0;elapsed<duration;elapsed+=Time.deltaTime){
            float p=Mathf.Clamp01(elapsed/duration),angle=ForwardWideAngle(p);
            Vector3 center=angle<=90?pivot+Quaternion.AngleAxis(angle,axis)*relative
                :nextPivot+Quaternion.AngleAxis(angle-90,axis)*(uprightCenter-nextPivot);
            // Open parallel lanes before the second half-turn, then settle at the
            // exact recipe sockets; the logical receiver cell stays fixed.
            Vector3 correction=endIncoming.center-rollEnd;
            Vector3 along=forward*Vector3.Dot(correction,forward);
            Vector3 across=correction-along;across.y=0;
            center+=across*AssemblyPhase(p,.18f,.48f)+along*AssemblyPhase(p,.50f,.96f);
            center.y+=correction.y*AssemblyPhase(p,.50f,.96f);
            PlaceAssemblyBody(incoming,Quaternion.AngleAxis(angle,axis)*incomingPose,incomingScale,center);
            float giveWay=AssemblyPhase(p,.12f,.42f)*(1-AssemblyPhase(p,.86f,1));
            Vector3 receiverCenter=Vector3.Lerp(startReceiver.center,endReceiver.center,AssemblyPhase(p,.12f,.48f))
                +forward*(cellSize*.22f*giveWay);
            PlaceAssemblyBody(receiver,receiverPose,receiverScale,receiverCenter);
            yield return null;
        }
        PlaceAssemblyBody(incoming,Quaternion.AngleAxis(180,axis)*incomingPose,incomingScale,endIncoming.center);
        PlaceAssemblyBody(receiver,receiverPose,receiverScale,endReceiver.center);
        yield return null;
    }
    // A standing bar falls once onto the flat receiver. Keep its signed
    // quarter-turn all the way to contact instead of aligning to an equivalent yaw.
    IEnumerator AnimateStandingFall(MagnetPiece incoming,MagnetPiece receiver,Vector3 forward,Vector3 axis,
        Quaternion incomingPose,Quaternion receiverPose,Vector3 incomingScale,Vector3 receiverScale,
        Bounds startIncoming,Bounds startReceiver,Bounds endIncoming,Bounds endReceiver) {
        float halfDepth=(Mathf.Abs(forward.x)*startIncoming.size.x+Mathf.Abs(forward.z)*startIncoming.size.z)*.5f;
        Vector3 pivot=startIncoming.center+forward*halfDepth-Vector3.up*(startIncoming.size.y*.5f);
        Vector3 relative=startIncoming.center-pivot;
        Vector3 landing=pivot+Quaternion.AngleAxis(90,axis)*relative;
        float duration=Mathf.Max(1.1f,joinSeconds);
        for(float elapsed=0;elapsed<duration;elapsed+=Time.deltaTime){
            float p=Mathf.Clamp01(elapsed/duration),fall=AssemblyPhase(p,0,.94f);
            Quaternion turn=Quaternion.AngleAxis(90*fall,axis);
            Vector3 center=pivot+turn*relative+(endIncoming.center-landing)*fall;
            PlaceAssemblyBody(incoming,turn*incomingPose,incomingScale,center);
            PlaceAssemblyBody(receiver,receiverPose,receiverScale,Vector3.Lerp(startReceiver.center,endReceiver.center,fall));
            yield return null;
        }
        PlaceAssemblyBody(incoming,Quaternion.AngleAxis(90,axis)*incomingPose,incomingScale,endIncoming.center);
        PlaceAssemblyBody(receiver,receiverPose,receiverScale,endReceiver.center);
        yield return null;
    }
    // Raise the incoming bar beside the standing receiver, then tip the pair
    // as one rigid arrangement. The receiver waits until the bars are parallel.
    IEnumerator AnimatePairedFall(MagnetPiece incoming,MagnetPiece receiver,Vector3 forward,Vector3 axis,
        Quaternion incomingPose,Quaternion receiverPose,Vector3 incomingScale,Vector3 receiverScale,
        Bounds startIncoming,Bounds startReceiver,Bounds endIncoming,Bounds endReceiver) {
        Quaternion quarter=Quaternion.AngleAxis(90,axis);
        float length=Mathf.Abs(forward.x)*startIncoming.size.x+Mathf.Abs(forward.z)*startIncoming.size.z;
        Vector3 liftPivot=startIncoming.center+forward*(length*.5f)-Vector3.up*(startIncoming.size.y*.5f);
        Vector3 liftRelative=startIncoming.center-liftPivot;
        Vector3 receiverUpright=startReceiver.center;
        Vector3 incomingUpright=receiverUpright+Quaternion.Inverse(quarter)*(endIncoming.center-endReceiver.center);
        Vector3 liftEnd=liftPivot+quarter*liftRelative;
        float depth=Mathf.Abs(forward.x)*startReceiver.size.x+Mathf.Abs(forward.z)*startReceiver.size.z;
        Vector3 pairPivot=receiverUpright+forward*(depth*.5f)-Vector3.up*(startReceiver.size.y*.5f);
        Vector3 receiverLanding=pairPivot+quarter*(receiverUpright-pairPivot);
        Vector3 correction=endReceiver.center-receiverLanding;
        float duration=Mathf.Max(1.65f,joinSeconds);
        for(float elapsed=0;elapsed<duration;elapsed+=Time.deltaTime){
            float p=Mathf.Clamp01(elapsed/duration),rise=AssemblyPhase(p,0,.40f),fall=AssemblyPhase(p,.50f,.96f);
            if(p<.50f){
                Quaternion turn=Quaternion.AngleAxis(90*rise,axis);
                Vector3 center=liftPivot+turn*liftRelative+(incomingUpright-liftEnd)*rise;
                PlaceAssemblyBody(incoming,turn*incomingPose,incomingScale,center);
                PlaceAssemblyBody(receiver,receiverPose,receiverScale,receiverUpright);
            }else{
                Quaternion turn=Quaternion.AngleAxis(90*fall,axis);
                PlaceAssemblyBody(incoming,turn*quarter*incomingPose,incomingScale,
                    pairPivot+turn*(incomingUpright-pairPivot)+correction*fall);
                PlaceAssemblyBody(receiver,turn*receiverPose,receiverScale,
                    pairPivot+turn*(receiverUpright-pairPivot)+correction*fall);
            }
            yield return null;
        }
        PlaceAssemblyBody(incoming,quarter*quarter*incomingPose,incomingScale,endIncoming.center);
        PlaceAssemblyBody(receiver,quarter*receiverPose,receiverScale,endReceiver.center);
        yield return null;
    }
    // The transverse flat bar slides into contact, then the standing bar
    // tips forward onto it. Neither body changes length or spins to match yaw.
    IEnumerator AnimateSlideAndTopple(MagnetPiece incoming,MagnetPiece receiver,Vector3 forward,Vector3 axis,
        Quaternion incomingPose,Quaternion receiverPose,Vector3 incomingScale,Vector3 receiverScale,
        Bounds startIncoming,Bounds startReceiver,Bounds endIncoming,Bounds endReceiver) {
        float inDepth=Mathf.Abs(forward.x)*startIncoming.size.x+Mathf.Abs(forward.z)*startIncoming.size.z;
        float recvDepth=Mathf.Abs(forward.x)*startReceiver.size.x+Mathf.Abs(forward.z)*startReceiver.size.z;
        float distance=Vector3.Dot(endIncoming.center-startIncoming.center,forward);
        float gap=Vector3.Dot(startReceiver.center-startIncoming.center,forward)-(inDepth+recvDepth)*.5f;
        float contact=distance>0?Mathf.Clamp01(gap/distance):0;
        Vector3 pivot=startReceiver.center+forward*(recvDepth*.5f)-Vector3.up*(startReceiver.size.y*.5f);
        Vector3 relative=startReceiver.center-pivot;
        Vector3 landing=pivot+Quaternion.AngleAxis(90,axis)*relative;
        float duration=Mathf.Max(1.4f,joinSeconds);
        for(float elapsed=0;elapsed<duration;elapsed+=Time.deltaTime){
            float p=Mathf.Clamp01(elapsed/duration),approach=AssemblyPhase(p,0,.40f),fall=AssemblyPhase(p,.40f,.96f);
            float slide=contact*approach+(1-contact)*fall;
            PlaceAssemblyBody(incoming,incomingPose,incomingScale,Vector3.Lerp(startIncoming.center,endIncoming.center,slide));
            Quaternion turn=Quaternion.AngleAxis(90*fall,axis);
            PlaceAssemblyBody(receiver,turn*receiverPose,receiverScale,
                pivot+turn*relative+(endReceiver.center-landing)*fall);
            yield return null;
        }
        PlaceAssemblyBody(incoming,incomingPose,incomingScale,endIncoming.center);
        PlaceAssemblyBody(receiver,Quaternion.AngleAxis(90,axis)*receiverPose,receiverScale,endReceiver.center);
        yield return null;
    }
    IEnumerator AnimateAssembly(MagnetPiece incoming,MagnetPiece receiver,Vector2Int direction,MagnetProduct product,Quaternion yaw,Vector2Int bridgeDirection,bool uNorth,bool receiverOnTop,bool incomingOnTop,Quaternion receiverLanding) {
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
            bool belongs;
            if(product==MagnetProduct.Bridge)belongs=r.transform.IsChildOf(final.GetChild(2));
            else {var c=r.sharedMaterial.color;belongs=(c.r>c.b)==incoming.north;}
            (belongs?incomingParts:receiverParts).Add(r);
        }
        var endIncoming=AssemblyBounds(incomingParts);var endReceiver=AssemblyBounds(receiverParts);
        // Socket measurements are values; no preview objects are needed during motion.
        Destroy(preview);
        var startIncoming=AssemblyBounds(incoming.geometry.GetComponentsInChildren<Renderer>());
        var startReceiver=AssemblyBounds(receiver.geometry.GetComponentsInChildren<Renderer>());
        Quaternion poseIncoming=incoming.Pose,poseReceiver=receiver.Pose;
        Quaternion endPoseIncoming=yaw,endPoseReceiver=receiverLanding;
        if(product==MagnetProduct.Cross){endPoseIncoming=yaw*Quaternion.Euler(0,90,0);endPoseReceiver=yaw;}
        if(product==MagnetProduct.Ring){endPoseIncoming=yaw;endPoseReceiver=yaw*Quaternion.Euler(0,180,0);}
        if(product==MagnetProduct.Lift){endPoseIncoming=incoming.shape==MagnetShape.Bar?yaw*Quaternion.Euler(0,0,90):yaw;endPoseReceiver=receiver.shape==MagnetShape.Bar?yaw*Quaternion.Euler(0,0,90):yaw;}
        if(product==MagnetProduct.BridgeHalf){endPoseIncoming=incoming.shape==MagnetShape.Horseshoe?yaw*Quaternion.Euler(0,90,0):yaw;endPoseReceiver=receiver.shape==MagnetShape.Horseshoe?yaw*Quaternion.Euler(0,90,0):yaw;}
        if(product==MagnetProduct.Bridge)endPoseReceiver=yaw;
        if(product==MagnetProduct.WideBar)endPoseReceiver=yaw;
        var scaleIncoming=incoming.geometry.localScale;var scaleReceiver=receiver.geometry.localScale;
        // Bridge recipes extend a rail; make that existing stylized extension visible over time.
        var finalScaleIncoming=scaleIncoming;var finalScaleReceiver=scaleReceiver;
        if(product==MagnetProduct.BridgeHalf||product==MagnetProduct.Bridge){
            float length=cellSize*2-.48f;
            if(incoming.shape==MagnetShape.Bar)finalScaleIncoming.x*=length/cellSize;
            if(receiver.product==MagnetProduct.None&&receiver.shape==MagnetShape.Bar)finalScaleReceiver.x*=length/cellSize;
        }
        float rollDegrees;var rolled=MagnetPiece.Rolled(incoming.shape,poseIncoming,direction,out rollDegrees);
        var rollAxis=new Vector3(direction.y,0,-direction.x);
        var forward=new Vector3(direction.x,0,direction.y);var lateral=Vector3.Cross(Vector3.up,forward);
        if(incoming.shape==MagnetShape.Bar&&receiver.shape==MagnetShape.Bar
            &&MagnetPiece.VerticalBar(poseIncoming)&&!MagnetPiece.VerticalBar(poseReceiver)
            &&(product==MagnetProduct.Cross||product==MagnetProduct.WideBar)){
            yield return AnimateStandingFall(incoming,receiver,forward,rollAxis,poseIncoming,poseReceiver,
                scaleIncoming,scaleReceiver,startIncoming,startReceiver,endIncoming,endReceiver);
            yield break;
        }
        if(product==MagnetProduct.WideBar&&incoming.shape==MagnetShape.Bar&&receiver.shape==MagnetShape.Bar
            &&MagnetPiece.VerticalBar(poseReceiver)
            &&Mathf.Abs(Vector3.Dot(poseIncoming*Vector3.right,forward))>.95f){
            yield return AnimatePairedFall(incoming,receiver,forward,rollAxis,poseIncoming,poseReceiver,
                scaleIncoming,scaleReceiver,startIncoming,startReceiver,endIncoming,endReceiver);
            yield break;
        }
        if(product==MagnetProduct.Cross&&incoming.shape==MagnetShape.Bar&&receiver.shape==MagnetShape.Bar
            &&!MagnetPiece.VerticalBar(poseIncoming)&&MagnetPiece.VerticalBar(poseReceiver)){
            yield return AnimateSlideAndTopple(incoming,receiver,forward,rollAxis,poseIncoming,poseReceiver,
                scaleIncoming,scaleReceiver,startIncoming,startReceiver,endIncoming,endReceiver);
            yield break;
        }
        // A flat bar pushed along its long axis rolls through upright and keeps
        // turning forward. Do not Slerp back to the receiver's zero-degree yaw.
        bool forwardWide=product==MagnetProduct.WideBar&&incoming.shape==MagnetShape.Bar
            &&receiver.shape==MagnetShape.Bar&&!MagnetPiece.VerticalBar(poseReceiver)
            &&Mathf.Abs(Vector3.Dot(poseIncoming*Vector3.right,forward))>.95f
            &&Mathf.Abs(Vector3.Dot(poseReceiver*Vector3.right,forward))>.95f;
        if(forwardWide){
            yield return AnimateForwardWide(incoming,receiver,forward,rollAxis,poseIncoming,poseReceiver,
                scaleIncoming,scaleReceiver,startIncoming,startReceiver,endIncoming,endReceiver);
            yield break;
        }
        float seconds=Mathf.Max(1.1f,joinSeconds);
        for(float elapsed=0;elapsed<seconds;elapsed+=Time.deltaTime){
            float p=Mathf.Clamp01(elapsed/seconds),roll=AssemblyPhase(p,0,.48f),align=AssemblyPhase(p,.38f,.70f),travel=AssemblyPhase(p,.48f,.94f);
            Quaternion inPose=Quaternion.Slerp(Quaternion.AngleAxis(rollDegrees*roll,rollAxis)*poseIncoming,endPoseIncoming,align);
            // A standing receiver falls in the push direction, with explicit signed rotation.
            Quaternion recvPose;
            if(receiverOnTop||MagnetPiece.VerticalBar(poseReceiver)&&receiver.shape==MagnetShape.Bar){float sign=(poseReceiver*Vector3.right).y>=0?1:-1;recvPose=Quaternion.AngleAxis(90*sign*AssemblyPhase(p,.30f,.82f),rollAxis)*poseReceiver;}
            else recvPose=Quaternion.Slerp(poseReceiver,endPoseReceiver,AssemblyPhase(p,0,.48f));
            Vector3 lane=startIncoming.center+lateral*Vector3.Dot(endIncoming.center-startIncoming.center,lateral)*AssemblyPhase(p,.12f,.45f);
            Vector3 inCenter=Vector3.Lerp(lane,endIncoming.center,travel);
            Vector3 recvCenter=Vector3.Lerp(startReceiver.center,endReceiver.center,AssemblyPhase(p,.05f,.45f));
            var inScale=Vector3.Lerp(scaleIncoming,finalScaleIncoming,align);var recvScale=Vector3.Lerp(scaleReceiver,finalScaleReceiver,align);
            PlaceAssemblyBody(incoming,inPose,inScale,inCenter);PlaceAssemblyBody(receiver,recvPose,recvScale,recvCenter);
            // Keep rotating bodies on their support plane, rather than rotating through the ice.
            // Upper/lower cross layers reach their support height before horizontal docking.
            var ib=AssemblyBounds(incoming.geometry.GetComponentsInChildren<Renderer>());var rb=AssemblyBounds(receiver.geometry.GetComponentsInChildren<Renderer>());
            float inBottom=Mathf.Lerp(startIncoming.min.y,endIncoming.min.y,AssemblyPhase(p,.12f,.45f));
            float recvBottom=Mathf.Lerp(startReceiver.min.y,endReceiver.min.y,AssemblyPhase(p,.05f,.45f));
            incoming.geometry.position+=Vector3.up*(inBottom-ib.min.y);receiver.geometry.position+=Vector3.up*(recvBottom-rb.min.y);
            yield return null;
        }
        PlaceAssemblyBody(incoming,endPoseIncoming,finalScaleIncoming,endIncoming.center);
        PlaceAssemblyBody(receiver,endPoseReceiver,finalScaleReceiver,endReceiver.center);
        // Show the contact pose for one frame before committing the logical recipe.
        yield return null;
    }
}
}
