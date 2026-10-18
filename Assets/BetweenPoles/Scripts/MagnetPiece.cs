using UnityEngine;
using System.Collections.Generic;
namespace BetweenPoles {
public enum MagnetShape { Bar, Horseshoe }
    public enum MagnetProduct { None, WideBar, Ring, Cross, Lift, BridgeHalf, Bridge, ThickU, CrossU }
public struct MagnetRollStep {
    public Vector2Int direction;
    public Quaternion pose;
    public float degrees;
    public MagnetRollStep(Vector2Int direction,Quaternion pose,float degrees){this.direction=direction;this.pose=pose;this.degrees=degrees;}
}
[DefaultExecutionOrder(-1000)]
public sealed class MagnetPiece:MonoBehaviour {
    FiveIslandWindow effectWindow;
    IslandSurfaceAnchor effectAnchor;
    GridPlayground effectGround;
    // Authored magnets often have no anchor.ground. The owning window binds
    // directly, including imported layouts and pieces restored from a save.
    internal void BindEffectWindow(FiveIslandWindow window){effectWindow=window;effectGround=window?window.board:null;}
    public bool NeedsDynamicEffects {
        get {
            if(effectWindow)return effectWindow.NeedsDynamicEffects(transform.position);
            if(!effectAnchor)effectAnchor=GetComponent<IslandSurfaceAnchor>();
            var ground=effectAnchor?effectAnchor.ground:null;
            if(effectGround!=ground||(!effectWindow&&ground)){
                effectGround=ground;
                effectWindow=ground?ground.GetComponent<FiveIslandWindow>():null;
            }
            return !effectWindow||effectWindow.NeedsDynamicEffects(transform.position);
        }
    }
    public int VisualHierarchyRevision { get; private set; }
    void OnTransformChildrenChanged(){unchecked{VisualHierarchyRevision++;}}
    void OnEnable(){
        unchecked{VisualHierarchyRevision++;}
        if(Application.isPlaying){MagnetVisuals.HideLegacyBridgeTrim(geometry);MagnetVisuals.RefreshBridgeSurface(geometry);}
        if(Application.isPlaying&&!GetComponent<MagnetFieldIdentity>())gameObject.AddComponent<MagnetFieldIdentity>();
    }
    void Awake(){
        if(!Application.isPlaying||!geometry||!MagnetVisuals.UseCombinedArcMeshes)return;
        MagnetVisuals.CombineExistingArcs(geometry);
        // Refresh a previously populated anchor cache (e.g. when instantiating a prefab).
        var anchor=GetComponent<IslandSurfaceAnchor>();
        if(anchor&&anchor.enabled){anchor.enabled=false;anchor.enabled=true;}
    }
    public MagnetShape shape;
    public bool north=true;
    public bool combined;
    public Transform geometry;
    public bool walkable;
    public MagnetProduct product;
    public bool baseNorth;
    [System.NonSerialized] public BlackHolePortal storedInPortal;
    [System.NonSerialized] public Vector3 portalEntryPosition;
    [System.NonSerialized] public float portalRollSpeed;
    public Vector2Int bridgeDirection=Vector2Int.right;
    public Quaternion Pose { get { return geometry.rotation; } }
    // Rings move as one assembled body; other products remain fixed.
    public bool CanBePushed { get { return product==MagnetProduct.Ring||(!combined&&product==MagnetProduct.None); } }
    public bool Occupies(Vector2Int cell,float size) {
        var home=new Vector2Int(Mathf.RoundToInt(transform.position.x/size),Mathf.RoundToInt(transform.position.z/size));
        return cell==home||((product==MagnetProduct.Bridge||product==MagnetProduct.BridgeHalf)&&cell==home+bridgeDirection);
    }
    public static bool FlatU(Quaternion pose){return Mathf.Abs((pose*Vector3.up).y)>.95f;}
    // Preserve either flat face. Legacy upright singles are laid flat on load.
    public static Quaternion FlatUPose(Quaternion pose){
        if(FlatU(pose))return pose;
        var opening=pose*Vector3.forward;opening.y=0;
        if(opening.sqrMagnitude<.01f){var right=pose*Vector3.right;right.y=0;opening=Vector3.Cross(right,Vector3.up);}
        if(opening.sqrMagnitude<.01f)opening=Vector3.forward;
        return Quaternion.LookRotation(opening,Vector3.up);
    }
    public static Quaternion UDockPose(Quaternion desired,Quaternion original){
        return Quaternion.LookRotation(desired*Vector3.forward,(original*Vector3.up).y<0?Vector3.down:Vector3.up);
    }
    public static bool VerticalBar(Quaternion pose){return Mathf.Abs((pose*Vector3.right).y)>.95f;}
    public static Quaternion Rolled(MagnetShape shape,Quaternion pose,Vector2Int dir,out float degrees) {
        var axis=new Vector3(dir.y,0,-dir.x);degrees=shape==MagnetShape.Horseshoe?180:90;
        return Quaternion.AngleAxis(degrees,axis)*pose;
    }
    public static List<MagnetRollStep> RollPath(MagnetShape shape,Quaternion pose,Vector2Int push) {
        float degrees;pose=Rolled(shape,pose,push,out degrees);
        // One push flips a U onto its other face in the adjacent cell.
        return new List<MagnetRollStep>{new MagnetRollStep(push,pose,degrees)};
    }
}
}
