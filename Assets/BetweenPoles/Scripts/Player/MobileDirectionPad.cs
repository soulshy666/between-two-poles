using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetweenPoles {
// Sample before GridPlayground.Update; IMGUI only draws and never advances input.
[DefaultExecutionOrder(-200), DisallowMultipleComponent]
public sealed class MobileDirectionPad : MonoBehaviour {
    public bool previewInEditor;
    GridPlayground board;
    ChapterMapReturn map;
    int finger = -1;
    bool mouseOwned;
    Rect area;
    static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    public Vector2Int Held { get; private set; }
    public Vector2Int Pressed { get; private set; }
    bool Visible => (Application.isMobilePlatform || (Application.isEditor && previewInEditor))
        && Application.isFocused && board && board.isActiveAndEnabled
        && gameObject.scene == SceneManager.GetActiveScene() && !board.OpeningCinematic
        && !ElementGameUI.Paused && Time.timeScale > 0
        && !ChapterRecoilTravel.Active && !BlackHoleTravel.InTransit
        && !BlackHoleTravel.Selecting && !BlackHoleTravel.SkipTransitionActive
        && (!map || !map.MapSessionActive)
        && (!GetComponent<MagnetDebugPanel>() || !GetComponent<MagnetDebugPanel>().IsOpen);

    void Awake() { board = GetComponent<GridPlayground>(); }
    void Update() {
        if (!map) map = GetComponent<ChapterMapReturn>();
        Pressed = Vector2Int.zero;
        if (!Visible) { Release(); return; }
        var safe = Screen.safeArea;
        float size = Mathf.Min(safe.width * .28f, safe.height * .38f);
        float margin = Mathf.Max(12, safe.height * .035f);
        area = new Rect(safe.xMin + margin, safe.yMin + margin, size, size);
        Vector2Int next = Vector2Int.zero;
        if (Input.touchCount > 0) {
            mouseOwned = false;
            if (finger < 0) {
                for (int i = 0; i < Input.touchCount; i++) {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began && area.Contains(touch.position)) { finger = touch.fingerId; break; }
                }
            }
            bool found = false;
            for (int i = 0; i < Input.touchCount; i++) {
                var touch = Input.GetTouch(i);
                if (touch.fingerId != finger) continue;
                found = true;
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) finger = -1;
                else next = DirectionAt(touch.position);
                break;
            }
            if (!found) finger = -1;
        } else {
            finger = -1;
            // Mouse simulation is editor-only; do not reuse Unity's emulated touch mouse.
            if (Application.isEditor && previewInEditor) {
                if (Input.GetMouseButtonDown(0) && area.Contains(Input.mousePosition)) mouseOwned = true;
                if (!Input.GetMouseButton(0)) mouseOwned = false;
                if (mouseOwned) next = DirectionAt(Input.mousePosition);
            }
        }
        if (next != Held) Pressed = next;
        Held = next;
    }
    Vector2Int DirectionAt(Vector2 point) {
        if (!area.Contains(point)) return Vector2Int.zero;
        var offset = (point - area.center) / area.width;
        if (offset.magnitude < .14f) return Vector2Int.zero;
        if (Mathf.Abs(offset.x) > Mathf.Abs(offset.y)) return offset.x > 0 ? Vector2Int.right : Vector2Int.left;
        return offset.y > 0 ? Vector2Int.up : Vector2Int.down;
    }
    void Release() { Held = Pressed = Vector2Int.zero; finger = -1; mouseOwned = false; }
    void OnDisable() { Release(); }
    void OnApplicationFocus(bool focused) { if (!focused) Release(); }
    void OnApplicationPause(bool paused) { if (paused) Release(); }
    // Same immediate-mode GUI and integer pixel sizing as ElementGameUI.
    // Geometry is drawn directly; no generated bitmap or external artwork is needed.
    static void Fill(Rect rect, Color color) {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }
    static void Arrow(Vector2 center, Vector2Int direction, float pixel, Color color) {
        // Five stepped rows form a cardinal arrow on the shared pixel grid.
        for (int row = 0; row < 5; row++) {
            int halfWidth = row < 3 ? row : 1;
            for (int column = -halfWidth; column <= halfWidth; column++) {
                Vector2 offset = new Vector2(column, row - 2);
                Vector2 rotated = direction == Vector2Int.up ? offset : direction == Vector2Int.down ? -offset
                    : direction == Vector2Int.right ? new Vector2(-offset.y, offset.x) : new Vector2(offset.y, -offset.x);
                Fill(new Rect(center.x + rotated.x * pixel, center.y + rotated.y * pixel, pixel, pixel), color);
            }
        }
    }
    void OnGUI() {
        if (!Visible || area.width <= 0) return;
        var oldColor = GUI.color; int oldDepth = GUI.depth;
        GUI.depth = -30;
        try {
            var rect = new Rect(area.x, Screen.height - area.yMax, area.width, area.height);
            float pixel = Mathf.Max(2, Mathf.Round(rect.width / 56));
            float button = pixel * 16;
            var ink = ElementGameUI.Ink;
            foreach (var direction in Directions) {
                var center = rect.center + new Vector2(direction.x, -direction.y) * rect.width * .31f;
                center = new Vector2(Mathf.Round(center.x / pixel) * pixel, Mathf.Round(center.y / pixel) * pixel);
                bool pressed = Held == direction;
                var face = new Rect(center.x-button/2, center.y-button/2+(pressed?pixel:0), button, button);
                // Thin themed edge, white translucent face and a short pixel shadow.
                Fill(new Rect(face.x,face.y+pixel,face.width,face.height+pixel),new Color(ink.r,ink.g,ink.b,.55f));
                Fill(face,new Color(1,1,1,pressed?.90f:.40f));
                Fill(new Rect(face.x+pixel,face.y+pixel,face.width-2*pixel,face.height-2*pixel),new Color(ink.r,ink.g,ink.b,pressed?.45f:.35f));
                var arrowCenter = new Vector2(center.x-pixel/2,face.center.y-pixel/2);
                Arrow(arrowCenter+Vector2.up*pixel,direction,pixel,new Color(ink.r,ink.g,ink.b,.8f));
                Arrow(arrowCenter,direction,pixel,Color.white);
            }
            Fill(new Rect(rect.center.x-pixel,rect.center.y-pixel,pixel*2,pixel*2),new Color(1,1,1,.55f));
        } finally { GUI.color = oldColor; GUI.depth = oldDepth; }
    }
}
}