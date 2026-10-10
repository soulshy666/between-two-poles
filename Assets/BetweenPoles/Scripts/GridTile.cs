using UnityEngine;
namespace BetweenPoles {
public sealed class GridTile:MonoBehaviour {
    public bool blocked;
    [Range(0,2)] public int rockStyle;
    // Low-rock artwork: original, approved concept 04, approved concept 05.
    [Range(0,2)] public int lowRockVariant;
    // 0 bare, 1 ice (legacy default), 2 verdant, 3 sand/ring, 4 lava.
    [Range(0,4)] public int rockTheme=1;
    // Authored world-space yaw, persisted in the scene; never randomized at runtime.
    [Range(0,360)] public float rockYaw;
    public bool goal;
    public float surfaceHeight;
}
}
