using UnityEngine;
namespace BetweenPoles {
public sealed partial class PlayerPushPose {
    const float StonePushSeconds=2.1f;
    bool pushingStone;
    float stonePushTime;
    Vector2Int stonePushDirection;
    float stoneContactDistance;
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
    void ResetStonePush(){pushingStone=false;stonePushTime=0;}
    void UpdateStonePush(){
        if(!pushingStone)return;
        if(!board||board.Busy){End();return;}
        stonePushTime+=Time.deltaTime;
        if(stonePushTime>=StonePushSeconds){End();return;}
        SampleStonePush(stonePushTime);
    }
    void SampleStonePush(float t){
        if(!visual)return;
        float effort=IdlePhase(t,0,.25f)*(1-IdlePhase(t,1.15f,1.45f));
        float shrug=IdlePhase(t,1.45f,1.65f)*(1-IdlePhase(t,1.85f,StonePushSeconds));
        float stride=Mathf.Sin((t-.25f)*Mathf.PI*2*2.2f)*IdlePhase(t,.25f,.4f)*effort;
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
