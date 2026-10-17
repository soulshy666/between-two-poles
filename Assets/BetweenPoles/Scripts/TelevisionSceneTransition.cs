using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetweenPoles {
// A persistent screen shutter keeps both halves of the CRT transition continuous across loading.
public sealed class TelevisionSceneTransition : MonoBehaviour {
    static TelevisionSceneTransition current;
    public static bool Busy => current;
    float closed;

    public static void Load(string scene) {
        if (Busy) return;
        var host = new GameObject("电视息屏转场");
        DontDestroyOnLoad(host);
        current = host.AddComponent<TelevisionSceneTransition>();
        current.StartCoroutine(current.ChangeScene(scene));
    }

    IEnumerator ChangeScene(string scene) {
        // Finish and present the shutter before loading can stall the main thread.
        yield return Animate(0, 1, .55f);
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(.12f);
        // Release a dormant chapter before a direct load can replace its scene handle.
        yield return ChapterRecoilTravel.CancelPreloadForMenu();
        var operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        if (operation == null) { Destroy(gameObject); yield break; }
        operation.allowSceneActivation = false;
        while (operation.progress < .9f) yield return null;
        operation.allowSceneActivation = true;
        yield return operation;
        // Give the new cameras and UI a full frame to initialize behind the black shutter.
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return Animate(1, 0, .6f);
        Destroy(gameObject);
    }

    IEnumerator Animate(float from, float to, float seconds) {
        for (float t = 0; t < seconds;) {
            closed = Mathf.Lerp(from, to, t / seconds);
            yield return null;
            // A loading hitch must not skip the visible line-to-point phase.
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        }
        closed = to;
    }

    void OnGUI() {
        var matrix = GUI.matrix;
        var color = GUI.color;
        GUI.matrix = Matrix4x4.identity;
        // Keep this behaviour at the front across GUI events. Restoring depth to zero
        // lets the cover's full-screen IMGUI repaint over the shutter.
        GUI.depth = -32760;
        float w = Screen.width, h = Screen.height;
        float collapse = Mathf.SmoothStep(0, 1, Mathf.Clamp01(closed / .72f));
        float pinch = Mathf.SmoothStep(0, 1, Mathf.Clamp01((closed - .72f) / .28f));
        // Reveal the unchanged scene through a vertical shutter. Only the light
        // contracts horizontally after the scene has been completely covered.
        float apertureHeight = h * (1 - collapse);
        float apertureWidth = w * (1 - pinch);
        float left = (w - apertureWidth) * .5f, top = (h - apertureHeight) * .5f;
        Draw(new Rect(0, 0, w, top), Color.black);
        Draw(new Rect(0, h - top, w, top + 1), Color.black);
        // A restrained white phosphor line contracts to a point, then disappears.
        float glow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, .72f, closed)) * (1 - pinch);
        if (glow > 0 && closed < 1) {
            Draw(new Rect(left, h * .5f - 3, apertureWidth, 6), new Color(.65f, .83f, 1, glow * .16f));
            Draw(new Rect(left, h * .5f - 1, apertureWidth, 2), new Color(.92f, .97f, 1, glow));
        }
        if (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel)
            Event.current.Use();
        GUI.matrix = matrix; GUI.color = color;
    }

    static void Draw(Rect rect, Color color) {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }

    void OnDestroy() {
        if (current == this) current = null;
    }
}
}
