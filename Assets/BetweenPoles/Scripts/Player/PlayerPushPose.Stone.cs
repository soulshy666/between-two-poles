using UnityEngine;
namespace BetweenPoles {
public sealed partial class PlayerPushPose {
    const float StonePushSeconds=2.1f;
    bool pushingStone;
    bool fixedMagnetShove;
    float stonePushTime;
    Vector2Int stonePushDirection;
    float stoneContactDistance;
    int stoneSoundsPlayed;
    PlayerFootstepAudio stoneAudio;
    public bool HasBlockedFeedback {get{return pushingStone||balancing;}}
    public static void BeginBlockedMagnetPush(Transform player,MagnetPiece magnet,Vector2Int direction){
        var pose=GetPose(player);
        // Repeated input must not continually restart the effort before the shrug.
        if(pose.pushingStone&&pose.stonePushDirection==direction)return;
        pose.End();pose.pushingStone=true;pose.stonePushTime=0;pose.stonePushDirection=direction;
        pose.fixedMagnetShove=true;
        if(pose.board&&magnet.shape==MagnetShape.Horseshoe&&!magnet.combined){
            var cell=new Vector2Int(Mathf.RoundToInt(magnet.transform.position.x/pose.board.cellSize),Mathf.RoundToInt(magnet.transform.position.z/pose.board.cellSize));
            var target=pose.board.MagnetAt(cell+direction);
            if(target&&target.shape==MagnetShape.Horseshoe&&!target.combined&&target.north!=magnet.north
                &&!GridPlayground.RingOpeningsAligned(magnet.Pose,target.Pose,direction))
                pose.board.BeginBlockedUNudge(magnet,direction);
        }
        player.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        float nearest=float.PositiveInfinity;
        foreach(var filter in magnet.geometry.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh)
            foreach(var vertex in filter.sharedMesh.vertices){
                float distance=Vector3.Dot(filter.transform.TransformPoint(vertex)-player.position,player.forward);
                if(distance>0)nearest=Mathf.Min(nearest,distance);
            }
        pose.stoneContactDistance=float.IsInfinity(nearest)?pose.board.cellSize*.5f:nearest;
        pose.SampleStonePush(0);
    }
    public static void BeginStonePush(Transform player,GridTile stone,Vector2Int direction,bool restart=false){
        var pose=GetPose(player);
        // A fresh press restarts immediately; automatic held repeats let it finish.
        if(pose.pushingStone&&pose.stonePushDirection==direction&&!restart)return;
        pose.End();pose.pushingStone=true;pose.stonePushTime=0;pose.stonePushDirection=direction;
        player.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        pose.stoneContactDistance=pose.MeasureStoneFront(stone);
        pose.SampleStonePush(0);
    }
    float MeasureStoneFront(GridTile stone){
        float nearest=float.PositiveInfinity;
        MeasureStoneMeshes(stone.transform,stone,ref nearest);
        if(stone.transform.parent)foreach(Transform sibling in stone.transform.parent)
            if(sibling.name=="rock"&&sibling.gameObject.activeInHierarchy){
                var delta=sibling.position-stone.transform.position;delta.y=0;
                if(delta.sqrMagnitude<.001f)MeasureStoneMeshes(sibling,stone,ref nearest);
            }
        return float.IsInfinity(nearest)?board.cellSize*.5f:nearest;
    }
    void MeasureStoneMeshes(Transform root,GridTile stone,ref float nearest){
        // Mesh bounds stay in logical space; renderer bounds are expanded for planet bending.
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>()){
            if(!filter.sharedMesh)continue;
            var bounds=filter.sharedMesh.bounds;
            if(filter.transform.TransformPoint(bounds.center).y<=stone.surfaceHeight+.1f)continue;
            for(int i=0;i<8;i++){
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                float distance=Vector3.Dot(filter.transform.TransformPoint(corner)-transform.position,transform.forward);
                if(distance>0)nearest=Mathf.Min(nearest,distance);
            }
        }
    }
    public void InterruptStonePush(Vector2Int direction){if(pushingStone&&direction!=stonePushDirection)End();}
    public void EndStonePush(){if(pushingStone)End();}
    void ResetStonePush(){
        if(board)board.ClearBlockedUNudge();
        pushingStone=false;stonePushTime=0;fixedMagnetShove=false;stoneSoundsPlayed=0;
        if(stoneAudio)stoneAudio.StopStoneBlocked();
    }
    void UpdateStonePush(){
        if(!pushingStone)return;
        if(!board||board.Busy){End();return;}
        stonePushTime+=Time.deltaTime;
        board.SampleBlockedUNudge(stonePushTime);
        if(stonePushTime>=(fixedMagnetShove?1.15f:StonePushSeconds)){End();return;}
        // Three efforts during hand contact, before the recovery/shrug animation.
        if(stoneSoundsPlayed<3&&stonePushTime>=.25f+.4f*stoneSoundsPlayed){
            stoneSoundsPlayed++;stoneAudio=PlayerFootstepAudio.For(transform);
            if(stoneAudio)stoneAudio.PlayStoneBlocked();
        }
        if(fixedMagnetShove){
            // One brief effort against an immovable assembly, then release and shrug.
            float sample=stonePushTime<.35f?Mathf.Lerp(0,1.15f,stonePushTime/.35f)
                :Mathf.Lerp(1.15f,StonePushSeconds,(stonePushTime-.35f)/.8f);
            SampleStonePush(sample,true);
        }else SampleStonePush(stonePushTime);
    }
    public void SampleBlockedChain(float time,float returned){
        // One shove, a braced pause while the force travels, then recoil and shrug.
        float sample=time<.35f?Mathf.Lerp(0,.55f,time/.35f):time<returned?1.1f:Mathf.Lerp(1.15f,StonePushSeconds,Mathf.Clamp01((time-returned)/.55f));
        stoneContactDistance=board?board.cellSize*.5f: .65f;
        SampleStonePush(sample,true);
        if(visual&&time>=returned){float p=Mathf.Clamp01((time-returned)/.24f);visual.position-=transform.forward*(.065f*Mathf.Sin(p*Mathf.PI));}
    }
    void SampleStonePush(float t,bool singleShove=false){
        if(!visual)return;
        float effort=IdlePhase(t,0,.25f)*(1-IdlePhase(t,1.15f,1.45f));
        float shrug=IdlePhase(t,1.45f,1.65f)*(1-IdlePhase(t,1.85f,StonePushSeconds));
        float stride=singleShove?0:Mathf.Sin((t-.25f)*Mathf.PI*2*2.2f)*IdlePhase(t,.25f,.4f)*effort;
        var waist=new Vector3(0,.34f,0);
        var lean=Quaternion.Euler(38*effort-7*shrug,0,0);
        // Tilt the complete visual, including the separately modelled backpack.
        // Rotate about the waist and counter-rotate the legs below it.
        var pivot=Vector3.Scale(waist,visual.localScale);
        visual.localRotation=restRotation*lean;
        visual.localPosition=restPosition+restRotation*(pivot-lean*pivot);
        if(!posed)return;
        var unlean=Quaternion.Inverse(lean);
        for(int i=0;i<vertices.Length;i++){
            Vector3 v=vertices[i],n=i<normals.Length?normals[i]:Vector3.up;
            float side=Mathf.Sign(v.x);
            if(arms[i]){
                var shoulder=new Vector3(side*.235f,.635f,0);
                // Raise the hands in front of the chest with only a small spread.
                // Counter the torso lean so the hands brace horizontally against the stone.
                var q=Quaternion.Euler(-128*effort-75*shrug,-side*12*shrug,side*12*shrug);
                v=shoulder+q*(v-shoulder)+Vector3.up*(.025f*shrug);n=q*n;
            }else if(legs[i]){
                var hip=new Vector3(side*.097f,.34f,0);
                var q=Quaternion.Euler(14*effort+24*stride*side,0,0);
                v=hip+q*(v-hip);n=q*n;
                v+=Vector3.up*(.025f*Mathf.Max(0,stride*side));
            }
            if(legs[i]){v=waist+unlean*(v-waist);n=unlean*n;}
            workVertices[i]=v;if(i<normals.Length)workNormals[i]=n;
        }
        if(effort>0){
            float handReach=float.NegativeInfinity;
            for(int i=0;i<vertices.Length;i++)if(arms[i])
                handReach=Mathf.Max(handReach,Vector3.Dot(body.transform.TransformPoint(workVertices[i])-transform.position,transform.forward));
            if(!float.IsInfinity(handReach)){
                float approach=Mathf.Max(0,stoneContactDistance-handReach-.008f)*effort;
                // Move only the complete visual: grid occupancy and interruption remain unchanged.
                visual.position+=transform.forward*approach;
            }
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
}
}
