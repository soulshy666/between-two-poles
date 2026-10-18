using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetweenPoles {
// The finale stays in the chapter: no scene activation or asset load at impact.
[DefaultExecutionOrder(200), DisallowMultipleComponent]
public sealed class SolarEnding : MonoBehaviour {
    public string sourceRoom = "mvb4dv128hght";
    public int exitRow = -8;
    public Transform sunTemplate;
    [Min(5)] public float flightSeconds = 7.5f;
    public bool Running { get; private set; }
    public bool ShowingThanks => Running && elapsed >= flightSeconds + 2.6f;

    GridPlayground board;
    MagnetPiece cargo;
    CurvedTrialCamera view;
    Camera cameraView;
    PlayerPushPose pose;
    Transform sun;
    Transform sunParent;
    Vector3 sunLocalPosition, sunLocalScale, sunWorldScale, sunViewOffset;
    Quaternion sunLocalRotation, sunViewRotation;
    float sunStartRadius, sunStartClearance;
    Material burst;
    Texture2D title, subtitle, returnLabel, gameTitle;
    Vector3 playerStart, cargoStart, focusStart, centerCorrection;
    Quaternion playerRotation;
    float elapsed, lens, displayScale;
    bool leaving, originalSunActive, islandEnabled, zoomOwned, actorsHidden;
    Vector3 originalEditFocus;
    readonly List<Behaviour> suspended = new List<Behaviour>();
    readonly List<Renderer> hiddenActors = new List<Renderer>();
    AudioSource impactAudio;
    AudioClip impactClip;

    void Awake() {
        title = Resources.Load<Texture2D>("SolarEnding/thanks");
        subtitle = Resources.Load<Texture2D>("SolarEnding/subtitle");
        returnLabel = Resources.Load<Texture2D>("SolarEnding/return");
        gameTitle = Resources.Load<Texture2D>("SolarEnding/title");
        var shader = Resources.Load<Shader>("Shaders/SolarEndingBurst");
        if (shader) burst = new Material(shader) { hideFlags = HideFlags.DontSave };
        // A short, bounded pixel-style impact, allocated before the player launches.
        const int rate = 22050;
        var samples = new float[rate];
        uint seed = 7421;
        for (int i = 0; i < samples.Length; i++) {
            float t = i / (float)rate;
            seed = unchecked(seed * 1664525u + 1013904223u);
            float noise = ((seed >> 16) / 32767.5f - 1);
            samples[i] = (Mathf.Sin(2 * Mathf.PI * (95 * t - 32 * t * t)) * .20f
                + noise * .07f) * Mathf.Exp(-t * 6) * Mathf.Min(1, t * 150);
        }
        impactClip = AudioClip.Create("Solar impact", samples.Length, 1, rate, false);
        impactClip.SetData(samples, 0);
        impactAudio = gameObject.AddComponent<AudioSource>();
        impactAudio.playOnAwake = false; impactAudio.spatialBlend = 0; impactAudio.volume = .65f;
    }

    public bool Matches(GridPlayground source, Vector2Int cell, Vector2Int direction) {
        if (!enabled || Running || direction != Vector2Int.right || cell.y != exitRow
            || source.gameObject.scene != SceneManager.GetActiveScene()
            || ChapterAtlas.IsInspectionScene(source.gameObject.scene) || !sunTemplate) return false;
        var tile = source.TileAt(cell);
        var anchor = tile ? tile.GetComponentInParent<IslandSurfaceAnchor>() : null;
        return anchor && anchor.center && anchor.center.name == sourceRoom;
    }

    public void Begin(GridPlayground source, MagnetPiece magnet) {
        if (Running) return;
        foreach (var candidate in FindObjectsOfType<CurvedTrialCamera>())
            if (candidate.isActiveAndEnabled && candidate.GetComponent<Camera>().enabled
                && candidate.island && candidate.island.board == source) { view = candidate; break; }
        if (!view || !sunTemplate) return;
        board = source; cargo = magnet; cameraView = view.GetComponent<Camera>();
        playerStart = board.player.position; cargoStart = cargo.transform.position;
        playerRotation = board.player.rotation;
        lens = cameraView.orthographicSize;
        focusStart = cameraView.transform.position - view.island.viewingOffset;
        displayScale = Mathf.Max(1, Shader.GetGlobalFloat("_IslandDisplayScale"));
        var actorDisplay = board.RecoilCameraFocus + Vector3.up * (.55f * displayScale);
        var actorRenderer = board.player.GetComponentInChildren<Renderer>();
        if (actorRenderer) { var properties = new MaterialPropertyBlock(); actorRenderer.GetPropertyBlock(properties); actorDisplay += (Vector3)properties.GetVector("_RecoilDisplayOffset"); }
        centerCorrection = Vector3.ProjectOnPlane(actorDisplay - cameraView.transform.position, cameraView.transform.forward);
        islandEnabled = view.island.enabled; zoomOwned = view.navigationOwnsZoom; originalEditFocus = view.editFocus;
        view.island.enabled = false; view.navigationOwnsZoom = true;
        Suspend(board); Suspend(board.GetComponent<ElementGameUI>()); Suspend(board.GetComponent<ChapterMapReturn>());
        Suspend(board.GetComponent<MobileDirectionPad>()); Suspend(board.GetComponent<MagnetDebugPanel>());
        originalSunActive = sunTemplate.gameObject.activeSelf;
        // Take over the visible sun at its exact rendered position and size.
        // Detach it from the camera-following background while the approach owns it.
        sun = sunTemplate; sunParent = sun.parent;
        sunLocalPosition = sun.localPosition; sunLocalRotation = sun.localRotation; sunLocalScale = sun.localScale;
        sunWorldScale = sun.lossyScale;
        sunViewOffset = cameraView.transform.InverseTransformPoint(sun.position);
        sunViewRotation = Quaternion.Inverse(cameraView.transform.rotation) * sun.rotation;
        sunStartRadius = Mathf.Max(.001f, Mathf.Abs(sunWorldScale.x) * .295f);
        sunStartClearance = Mathf.Max(0, sunViewOffset.x - sunStartRadius);
        sun.SetParent(null, true);
        pose = PlayerPushPose.BeginRecoil(board.player);
        elapsed = 0; Running = true;
        var sound = PlayerFootstepAudio.For(board.player); if (sound) sound.StopMovementSounds();
    }

    void Suspend(Behaviour component) {
        if (component && component.enabled) { suspended.Add(component); component.enabled = false; }
    }

    void LateUpdate() {
        if (!Running || !board || !board.player || !view) return;
        float previous = elapsed;
        // A slow frame cannot skip the impact/expansion sequence.
        elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        float flight = Mathf.Min(elapsed, flightSeconds);
        float distance = board.cellSize * (3 * flight + .4f * flight * flight);
        board.player.SetPositionAndRotation(playerStart + Vector3.right * distance, playerRotation);
        cargo.transform.position = cargoStart + Vector3.right * distance;
        if (!actorsHidden && pose) pose.SampleRecoilFlight(Mathf.Floor(flight * 18) / 18, cargo);
        var focus = focusStart + Vector3.right * (distance * displayScale)
            + centerCorrection * Mathf.SmoothStep(0, 1, Mathf.Clamp01(flight / 2));
        view.editFocus = focus; view.Apply(focus); cameraView.orthographicSize = lens;

        float approach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(flight / flightSeconds));
        float radius = Mathf.Lerp(sunStartRadius, Mathf.Max(lens * 1.8f, sunStartRadius), approach);
        float clearance = (1 - approach) * sunStartClearance;
        // SolarWonder's opaque surface is at UV radius .59 (quad half-size .5).
        // The chapter sun starts directly to the right. Approach on that same
        // horizontal line without orbiting or moving it vertically during flight.
        var sunOffset = new Vector3(radius + clearance, sunViewOffset.y, sunViewOffset.z);
        sun.SetPositionAndRotation(cameraView.transform.TransformPoint(sunOffset),
            cameraView.transform.rotation * Quaternion.Slerp(sunViewRotation, Quaternion.identity, approach));
        sun.localScale = sunWorldScale * (radius / sunStartRadius);
        if (previous < flightSeconds && elapsed >= flightSeconds && impactAudio) impactAudio.PlayOneShot(impactClip);
        if (!actorsHidden && elapsed >= flightSeconds + .9f) {
            actorsHidden = true;
            HideActor(board.player); HideActor(cargo.transform);
        }
        if (ShowingThanks && !leaving && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))) ReturnToMenu();
    }

    void HideActor(Transform actor) {
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
            if (renderer.enabled) { hiddenActors.Add(renderer); renderer.enabled = false; }
    }

    public void ReturnToMenu() {
        if (!ShowingThanks || leaving || TelevisionSceneTransition.Busy) return;
        leaving = true;
        GameSaveSlots.SaveNow(); // Retain the last grounded checkpoint, never the airborne pose.
        TelevisionSceneTransition.Load("Cover");
    }

    void OnGUI() {
        if (!Running || elapsed < flightSeconds) return;
        var matrix = GUI.matrix; var color = GUI.color; bool guiEnabled = GUI.enabled;
        GUI.depth = -20000; GUI.matrix = Matrix4x4.identity; GUI.color = Color.white;
        float t = elapsed - flightSeconds, w = Screen.width, h = Screen.height;
        var screen = new Rect(0, 0, w, h);
        if (t < 1.15f) {
            if (Event.current.type == EventType.Repaint && burst) {
                burst.SetFloat("_Progress", Mathf.Clamp01(t / .85f));
                burst.SetVector("_Screen", new Vector4(w, h, Mathf.Max(2, Mathf.Round(h / 180)), 0));
                Graphics.DrawTexture(screen, Texture2D.whiteTexture, burst);
            }
        } else {
            float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 1.15f) / 1.1f));
            Fill(screen, Color.Lerp(new Color(1, .97f, .86f), new Color(.018f, .024f, .043f), fade));
            if (fade >= 1) DrawThanks(w, h, Mathf.Clamp01((t - 2.3f) / .8f));
        }
        GUI.matrix = matrix; GUI.color = color; GUI.enabled = guiEnabled;
    }

    void DrawThanks(float w, float h, float opacity) {
        float k = Mathf.Max(1, Mathf.Floor(Mathf.Min(w / 384, h / 216)));
        for (int i = 0; i < 42; i++) {
            float x = Mathf.Floor(Mathf.Repeat(i * .618034f, 1) * w / k) * k;
            float y = Mathf.Floor(Mathf.Repeat(i * .414214f + .07f, 1) * h / k) * k;
            Fill(new Rect(x, y, k, k), new Color(.36f, .48f, .57f, opacity * .4f));
        }
        GUI.color = new Color(.59f, .68f, .74f, opacity);
        Text(gameTitle, new Vector2(w * .5f, h * .26f), k);
        GUI.color = new Color(1, .91f, .70f, opacity);
        Text(title, new Vector2(w * .5f, h * .41f), k * 3);
        Fill(new Rect(w * .5f - 32 * k, h * .53f, 32 * k, k), new Color(.91f, .28f, .34f, opacity));
        Fill(new Rect(w * .5f, h * .53f, 32 * k, k), new Color(.26f, .68f, .81f, opacity));
        GUI.color = new Color(.65f, .73f, .78f, opacity);
        Text(subtitle, new Vector2(w * .5f, h * .61f), k);
        var button = new Rect(Mathf.Round(w * .5f - 60 * k), Mathf.Round(h * .75f), 120 * k, 24 * k);
        bool hot = button.Contains(Event.current.mousePosition);
        Fill(button, new Color(.28f, .40f, .47f, opacity));
        Fill(new Rect(button.x + k, button.y + k, button.width - 2 * k, button.height - 2 * k),
            hot ? new Color(.14f, .24f, .29f, opacity) : new Color(.035f, .065f, .09f, opacity));
        GUI.color = new Color(1, .91f, .70f, opacity);
        Text(returnLabel, button.center, k);
        GUI.enabled = ShowingThanks && !leaving;
        if (GUI.Button(button, GUIContent.none, GUIStyle.none)) ReturnToMenu();
    }

    static void Text(Texture2D texture, Vector2 center, float scale) {
        if (!texture) return;
        GUI.DrawTexture(new Rect(Mathf.Round(center.x - texture.width * scale / 2),
            Mathf.Round(center.y - texture.height * scale / 2), texture.width * scale, texture.height * scale), texture);
    }
    static void Fill(Rect rect, Color tint) {
        var color = GUI.color; GUI.color = tint; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = color;
    }

    void OnDisable() {
        if (!Running) return;
        Running = false;
        if (board) board.FinishSolarFlight();
        if (sun && sunParent) {
            sun.SetParent(sunParent, false);
            sun.localPosition = sunLocalPosition; sun.localRotation = sunLocalRotation; sun.localScale = sunLocalScale;
            sun.gameObject.SetActive(originalSunActive);
        }
        foreach (var renderer in hiddenActors) if (renderer) renderer.enabled = true;
        hiddenActors.Clear();
        if (view) { view.navigationOwnsZoom = zoomOwned; view.editFocus = originalEditFocus; if (view.island) view.island.enabled = islandEnabled; }
        foreach (var component in suspended) if (component) component.enabled = true;
        suspended.Clear();
        if (pose) pose.End();
    }
    void OnDestroy() {
        if (burst) Destroy(burst);
        if (impactClip) Destroy(impactClip);
    }
}
}
