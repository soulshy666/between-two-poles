using System.Collections;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    MagnetPiece CrossMaterial(MagnetPiece cross){
        return magnets.FirstOrDefault(m=>m&&m!=cross&&!m.gameObject.activeSelf
            &&m.transform.parent==cross.transform&&m.shape==MagnetShape.Bar);
    }
    // Authored crosses need the same second material that runtime assemblies retain.
    // Allocate before the action snapshot, so undo/save/reset include both pieces.
    void EnsureCrossMaterials(){
        foreach(var cross in magnets){
            if(!cross||cross.product!=MagnetProduct.Cross||CrossMaterial(cross))continue;
            var child=new GameObject("十字 · 可拆上层材料");child.SetActive(false);
            child.transform.SetParent(cross.transform,false);
            var material=child.AddComponent<MagnetPiece>();material.shape=MagnetShape.Bar;
            material.north=!cross.north;material.geometry=MagnetVisuals.Build(child.transform,MagnetShape.Bar,material.north,MagnetProduct.None,cellSize,material.north,Vector2Int.right);
            material.enabled=false;material.combined=true;
            var anchor=child.AddComponent<IslandSurfaceAnchor>();var sourceAnchor=cross.GetComponent<IslandSurfaceAnchor>();
            anchor.center=sourceAnchor?sourceAnchor.center:Owner(cross);anchor.ground=this;
            magnets=magnets.Concat(new[]{material}).ToArray();
            var saved=SavePiece(material);
            if(initialState!=null)initialState.pieces=initialState.pieces.Concat(new[]{saved}).ToArray();
            foreach(var state in history)state.pieces=state.pieces.Concat(new[]{saved}).ToArray();
        }
    }
    bool CrossLayers(MagnetPiece cross,out Transform top,out Transform bottom,out Quaternion topPose,out Quaternion bottomPose,out bool topNorth){
        top=bottom=null;topPose=bottomPose=Quaternion.identity;topNorth=false;
        if(!cross||cross.product!=MagnetProduct.Cross||!cross.geometry||cross.geometry.childCount<2)return false;
        var a=cross.geometry.GetChild(0);var b=cross.geometry.GetChild(1);
        var ab=AssemblyBounds(AssemblyBodies(a));var bb=AssemblyBounds(AssemblyBodies(b));
        top=ab.center.y>bb.center.y?a:b;bottom=top==a?b:a;
        topPose=Yaw(top.TransformDirection(top.localScale.x>top.localScale.z?Vector3.right:Vector3.forward));
        bottomPose=Yaw(bottom.TransformDirection(bottom.localScale.x>bottom.localScale.z?Vector3.right:Vector3.forward));
        var color=top.GetComponent<Renderer>().sharedMaterial.color;topNorth=color.r>color.b;
        return true;
    }
    bool CrossImpactPossible(MagnetPiece ring,MagnetPiece cross,Vector2Int direction){
        Transform top,bottom;Quaternion pose,lower;bool north;
        return ring&&ring.product==MagnetProduct.Ring&&!MagnetPiece.FlatU(ring.Pose)
            &&Mathf.Abs(Vector3.Dot(ring.Pose*Vector3.up,new Vector3(direction.x,0,direction.y)))<.05f
            &&cross&&cross.enabled&&cross.gameObject.activeInHierarchy&&!MagnetStillAnimating(cross)
            &&SameHeight(ring.transform.position.y,cross.transform.position.y)
            &&CrossLayers(cross,out top,out bottom,out pose,out lower,out north)
            &&Mathf.Abs(Vector3.Dot(pose*Vector3.right,new Vector3(direction.x,0,direction.y)))>.95f;
    }
    void SetImpactBar(MagnetPiece piece,bool north,Quaternion pose){
        recordedGeometry.Add(piece.geometry);piece.geometry.gameObject.SetActive(false);
        piece.shape=MagnetShape.Bar;piece.north=piece.baseNorth=north;
        piece.product=MagnetProduct.None;piece.combined=false;piece.walkable=false;piece.enabled=true;
        piece.geometry=MagnetVisuals.Build(piece.transform,piece.shape,north,MagnetProduct.None,cellSize,north,Vector2Int.right);
        piece.geometry.rotation=pose;MagnetVisuals.Ground(piece);
        var flow=piece.GetComponent<MagnetFlow>();if(flow)flow.active=false;
        var anchor=piece.GetComponent<IslandSurfaceAnchor>();if(anchor){anchor.enabled=false;anchor.enabled=true;}
    }
    IEnumerator KnockCrossTop(MagnetPiece ring,MagnetPiece cross,Vector2Int direction,bool portalClock){
        if(!CrossImpactPossible(ring,cross,direction))yield break;
        Transform top,bottom;Quaternion pose,lower;bool north;
        CrossLayers(cross,out top,out bottom,out pose,out lower,out north);
        var material=CrossMaterial(cross);if(!material)yield break;
        var origin=Cell(cross.transform);var next=origin+direction;
        var destination=InspectMagnetDestination(origin,cross.transform.position.y,next,cross);
        if(next==PlayerCell||destination.kind==MagnetLandingKind.Blocked)yield break;
        // Probe with a flat bar: impact translates the rail, it does not roll it.
        material.north=north;material.geometry.rotation=pose;
        var receiver=destination.obstacle;var recipe=MagnetProduct.None;
        MagnetLanding farDestination=default(MagnetLanding);bool repel=false;
        Vector2Int bridge=Vector2Int.right;
        if(receiver){
            if(MagnetStillAnimating(receiver)||!SameHeight(receiver.transform.position.y,destination.position.y))yield break;
            if(receiver.product==MagnetProduct.None&&!receiver.combined&&receiver.north==north){
                if(!Supported(receiver)&&receiver.shape!=MagnetShape.Bar&&!CanRepelBackToLand(receiver,direction))yield break;
                farDestination=InspectMagnetDestination(next,receiver.transform.position.y,next+direction,receiver);
                if(next+direction==PlayerCell||farDestination.kind==MagnetLandingKind.Blocked||farDestination.kind==MagnetLandingKind.Magnet)yield break;
                repel=true;
            }else{
                recipe=Recipe(material,pose,receiver,Supported(receiver),direction);
                if(recipe==MagnetProduct.None)yield break;
                bridge=recipe==MagnetProduct.BridgeHalf?Direction(receiver.Pose*Vector3.forward):receiver.bridgeDirection;
                if(recipe==MagnetProduct.BridgeHalf||recipe==MagnetProduct.Bridge){
                    var second=Cell(receiver.transform)+bridge;var tile=Tile(second);
                    if(second==PlayerCell||second==origin||TransportObstacle(second,receiver)
                        ||(tile&&(tile.blocked||!SameHeight(tile.surfaceHeight,receiver.transform.position.y))))yield break;
                }
            }
        }
        var startBounds=AssemblyBounds(AssemblyBodies(top));
        material.transform.SetParent(cross.transform.parent,true);material.transform.position=cross.transform.position;
        SetImpactBar(material,north,pose);material.gameObject.SetActive(true);
        material.geometry.position+=startBounds.center-AssemblyBounds(AssemblyBodies(material.geometry)).center;
        SetImpactBar(cross,!north,lower);
        pushHoverParticipants.Add(material);pushHoverParticipants.Add(cross);
        AnimatePortalMagnet(material,true);if(receiver)AnimatePortalMagnet(receiver,true);
        try{
            if(recipe!=MagnetProduct.None){
                yield return AssembleCore(material,receiver,direction,pose,recipe,PlayerCell,bridge,true);
            }else{
                var start=material.transform.position;var geoStart=material.geometry.position;
                var farStart=repel?receiver.transform.position:Vector3.zero;
                var farPose=repel?receiver.Pose:Quaternion.identity;float degrees=0;
                var farEndPose=repel?MagnetPiece.Rolled(receiver.shape,farPose,direction,out degrees):Quaternion.identity;
                if(repel)pushHoverParticipants.Add(receiver);
                const float seconds=.36f;
                for(float elapsed=0;elapsed<seconds;){
                    elapsed=Mathf.Min(seconds,elapsed+(portalClock?BlackHoleTravel.AnimationDeltaTime:MovementDeltaTime));
                    float p=elapsed/seconds,travel=1-Mathf.Pow(1-p,3);
                    material.transform.position=Vector3.Lerp(start,destination.position,travel);
                    material.geometry.rotation=pose;MagnetVisuals.Ground(material);
                    material.geometry.position+=Vector3.up*((geoStart.y-start.y)*(1-travel));
                    if(repel){
                        float r=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,1,p));
                        receiver.transform.position=Vector3.Lerp(farStart,farDestination.position,r);
                        receiver.geometry.rotation=Quaternion.AngleAxis(degrees*r,new Vector3(direction.y,0,-direction.x))*farPose;
                        MagnetVisuals.Ground(receiver);
                    }
                    yield return null;
                }
                material.transform.position=destination.position;MagnetVisuals.Ground(material);
                if(repel){receiver.geometry.rotation=farEndPose;yield return SettleMagnetArrival(receiver,farStart,portalClock);}
                yield return SettleMagnetArrival(material,start,portalClock);
            }
            LastRule=repel?"圆环撞落十字上层，同极磁铁向前排斥":recipe!=MagnetProduct.None?"圆环撞落十字上层并合成":"圆环撞落十字上层，保留下层";
        }finally{
            AnimatePortalMagnet(material,false);if(receiver)AnimatePortalMagnet(receiver,false);
            pushHoverParticipants.Remove(material);pushHoverParticipants.Remove(cross);if(receiver)pushHoverParticipants.Remove(receiver);
        }
    }
}
}
