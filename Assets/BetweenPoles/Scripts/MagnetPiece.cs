using UnityEngine;
namespace BetweenPoles {
public enum MagnetShape { Bar, Horseshoe }
public sealed class MagnetPiece:MonoBehaviour {
    public MagnetShape shape;
    public bool north=true;
    public bool combined;
    public Transform geometry;
    [Tooltip("A completed broad bar is walkable; raw pieces reserve their cell.")]
    public bool walkable;
}
}
