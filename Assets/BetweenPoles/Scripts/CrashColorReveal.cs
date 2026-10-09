using UnityEngine;
namespace BetweenPoles {
// Project the logical collision point through the same sphere frame as the ice surface.
public static class CrashColorReveal {
    public static Vector3 DisplayPoint(Vector3 position){
        Vector3 focus=Shader.GetGlobalVector("_IceFocus"),center=Shader.GetGlobalVector("_IceCurveFocus");
        float scale=Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"));
        if(Shader.GetGlobalFloat("_IslandRigidLayout")<.5f)return focus+(position-focus)*scale;
        if(Shader.GetGlobalFloat("_IslandFlatEnabled")>.5f){var bounds=Shader.GetGlobalVector("_IslandFlatBounds");center=new Vector3(Mathf.Clamp(position.x,bounds.x,bounds.z),center.y,Mathf.Clamp(position.z,bounds.y,bounds.w));}
        Vector3 q=(position-center)*scale;float distance=new Vector2(q.x,q.z).magnitude;
        float radius=Mathf.Max(12,Shader.GetGlobalFloat("_IceRadius")*1.65f),angle=Mathf.Min(distance/radius,1.15f),extra=Mathf.Max(0,distance-radius*1.15f);
        float radial=radius*Mathf.Sin(angle)+extra*Mathf.Cos(angle),drop=radius*(Mathf.Cos(angle)-1)-extra*Mathf.Sin(angle);
        var up=distance>.0001f?new Vector3(q.x/distance*Mathf.Sin(angle),Mathf.Cos(angle),q.z/distance*Mathf.Sin(angle)):Vector3.up;
        return focus+(center-focus)*scale+new Vector3(distance>.0001f?q.x/distance*radial:0,drop,distance>.0001f?q.z/distance*radial:0)+up*q.y;
    }
}
}
