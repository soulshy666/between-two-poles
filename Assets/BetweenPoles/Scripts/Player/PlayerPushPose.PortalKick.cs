using UnityEngine;
namespace BetweenPoles {
public sealed partial class PlayerPushPose {
    public void SamplePortalKick(float progress){
        if(!visual)return;
        float weight=Mathf.Sin(Mathf.Clamp01(progress)*Mathf.PI);
        visual.localRotation=restRotation*Quaternion.Euler(-6*weight,0,0);
        if(!posed)return;
        var pivot=new Vector3(.13f,.34f,0);var turn=Quaternion.Euler(-55*weight,0,0);
        for(int i=0;i<vertices.Length;i++){
            bool foot=legs[i]&&vertices[i].x>0;
            workVertices[i]=foot?pivot+turn*(vertices[i]-pivot):vertices[i];
            if(i<normals.Length)workNormals[i]=foot?turn*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
}
}
