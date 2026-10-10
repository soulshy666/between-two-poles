using UnityEngine;
namespace BetweenPoles {
public sealed class GridTile:MonoBehaviour {
    public bool blocked;
    [Range(0,2)] public int rockStyle;
    // 0 bare, 1 ice (legacy default), 2 verdant, 3 sand/ring, 4 lava.
    [Range(0,4)] public int rockTheme=1;
    public bool goal;
    public float surfaceHeight;
}
}
