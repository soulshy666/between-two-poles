using UnityEngine;
namespace BetweenPoles {
public enum MagnetShape { Bar, Horseshoe }
public enum MagnetProduct { None, WideBar, Ring, Cross, Lift, BridgeHalf, Bridge }
public sealed class MagnetPiece:MonoBehaviour {
    public MagnetShape shape;
    public bool north=true;
    public bool combined;
    public Transform geometry;
    public bool walkable;
    public MagnetProduct product;
    public bool baseNorth;
    public Vector2Int bridgeDirection=Vector2Int.right;
    public Quaternion Pose { get { return geometry.rotation; } }
    // Only a single, uncombined material can be translated by a push.
    public bool CanBePushed { get { return !combined&&product==MagnetProduct.None; } }
    public bool Occupies(Vector2Int cell,float size) {
        var home=new Vector2Int(Mathf.RoundToInt(transform.position.x/size),Mathf.RoundToInt(transform.position.z/size));
        return cell==home||((product==MagnetProduct.Bridge||product==MagnetProduct.BridgeHalf)&&cell==home+bridgeDirection);
    }
    public static bool FlatU(Quaternion pose){return Mathf.Abs((pose*Vector3.up).y)>.95f;}
    public static bool VerticalBar(Quaternion pose){return Mathf.Abs((pose*Vector3.right).y)>.95f;}
    public static Quaternion Rolled(MagnetShape shape,Quaternion pose,Vector2Int dir,out float degrees) {
        var axis=new Vector3(dir.y,0,-dir.x);degrees=90;
        // Upright horseshoes roll around their arc axis, retaining their supported pose.
        if(shape==MagnetShape.Horseshoe && Mathf.Abs((pose*Vector3.up).y)<.05f && Mathf.Abs(Vector3.Dot(pose*Vector3.up,axis))>.95f)degrees=360;
        return Quaternion.AngleAxis(degrees,axis)*pose;
    }
}
}
