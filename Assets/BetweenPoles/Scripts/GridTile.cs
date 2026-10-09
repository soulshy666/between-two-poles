using UnityEngine;
namespace BetweenPoles {
public sealed class GridTile:MonoBehaviour {
    public bool blocked;
    [Range(0,1)] public int rockStyle;
    public bool goal;
    public float surfaceHeight;
}
}
