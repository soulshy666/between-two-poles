// Execute this method body with Unity MCP execute_code while in Play mode.
// Tests create isolated runtime objects and destroy them on completion.
if (!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Play mode required");
var root = new GameObject("Mixed magnet regression (temporary)");
root.hideFlags = HideFlags.DontSave;
var boards = new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var inputs = new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var receivers = new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var names = new System.Collections.Generic.List<string>();
var results = new System.Collections.Generic.List<string>();
var failures = new System.Collections.Generic.HashSet<string>();
var directions = new System.Collections.Generic.List<Vector2Int>();
var receiverCells = new System.Collections.Generic.List<Vector2Int>();
var initialInputPoses = new System.Collections.Generic.List<Quaternion>();
var initialReceiverPoses = new System.Collections.Generic.List<Quaternion>();
var expected = new System.Collections.Generic.List<BetweenPoles.MagnetProduct>();
System.Func<Vector2Int, Vector3> position = c => new Vector3(c.x * 1.5f, 0, c.y * 1.5f);
System.Func<Transform, Vector2Int, BetweenPoles.MagnetShape, bool, Quaternion, BetweenPoles.MagnetPiece> material = (parent, cell, shape, north, pose) => {
    var go = new GameObject("Test material"); go.transform.SetParent(parent, false); go.transform.position = position(cell);
    var m = go.AddComponent<BetweenPoles.MagnetPiece>(); m.shape = shape; m.north = north; m.baseNorth = north;
    m.geometry = BetweenPoles.MagnetVisuals.Build(go.transform, shape, north, BetweenPoles.MagnetProduct.None, 1.5f, north, Vector2Int.right);
    m.geometry.rotation = pose; BetweenPoles.MagnetVisuals.Ground(m); return m;
};
System.Action<bool, string> check = (ok, label) => { if (!ok) failures.Add(label); };
for (int rotation = 0; rotation < 4; rotation++) for (int approach = 0; approach < 4; approach++) for (int color = 0; color < 2; color++) for (int variant = 0; variant < 4; variant++)
for (int face = 0; face < 2; face++) for (int alignment = 0; alignment < (variant < 2 ? 2 : 1); alignment++) {
    int i = boards.Count;
    var yaw = Quaternion.Euler(0, rotation * 90, 0);
    var right = yaw * Vector3.right;
    var axis = new Vector2Int(Mathf.RoundToInt(right.x), Mathf.RoundToInt(right.z));
    bool reverse = variant % 2 == 1, standing = variant >= 2;
    var pushVector = Quaternion.Euler(0, approach * 90, 0) * new Vector3(axis.x,0,axis.y);
    var push = new Vector2Int(Mathf.RoundToInt(pushVector.x),Mathf.RoundToInt(pushVector.z));
    var home = new Vector2Int(100 + i * 8, 100);
    var host = new GameObject("Case " + i); host.SetActive(false); host.transform.SetParent(root.transform, false);
    var board = host.AddComponent<BetweenPoles.GridPlayground>();
    var player = new GameObject("Test player"); player.transform.SetParent(host.transform, false);
    player.transform.position = position(home - push * 2); board.player = player.transform;
    var tiles = new System.Collections.Generic.List<BetweenPoles.GridTile>();
    for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) {
        var tile = new GameObject("Test floor"); tile.transform.SetParent(host.transform, false); tile.transform.position = position(home + new Vector2Int(x,z));
        tiles.Add(tile.AddComponent<BetweenPoles.GridTile>());
    }
    board.tiles = tiles.ToArray();
    var uPoseStart = yaw * Quaternion.Euler(0,0,face*180);
    var barPose = yaw * (standing ? Quaternion.Euler(0,0,90) : Quaternion.Euler(0,alignment*90,0));
    var input = material(host.transform, home - push, reverse ? BetweenPoles.MagnetShape.Horseshoe : BetweenPoles.MagnetShape.Bar, reverse ? color == 0 : color != 0, reverse ? uPoseStart : barPose);
    var receiver = material(host.transform, home, reverse ? BetweenPoles.MagnetShape.Bar : BetweenPoles.MagnetShape.Horseshoe, reverse ? color != 0 : color == 0, reverse ? barPose : uPoseStart);
    board.magnets = new[] { input, receiver }; board.enabled = false; host.SetActive(true);
    boards.Add(board); inputs.Add(input); receivers.Add(receiver); directions.Add(push); receiverCells.Add(home);
    initialInputPoses.Add(input.Pose); initialReceiverPoses.Add(receiver.Pose);
    expected.Add(standing ? BetweenPoles.MagnetProduct.Lift : BetweenPoles.MagnetProduct.BridgeHalf);
    names.Add("rotation=" + rotation + ", approach=" + approach + ", color=" + color + ", example=" + (variant + 1) + ", face=" + face + ", alignment=" + alignment);
    check(board.TryStep(push), names[i] + ": rejected " + board.LastRule);
}
int phase = 0, frames = 0;
double start = UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () => {
    try {
        frames++;
        bool busy = false;
        for (int i = 0; i < boards.Count; i++) {
            var b = boards[i]; busy |= b.Busy;
            if (phase == 0 && b.Busy && !receivers[i].combined) {
                foreach (var m in new[] { inputs[i], receivers[i] }) {
                    bool valid = m.shape == BetweenPoles.MagnetShape.Horseshoe ? BetweenPoles.MagnetPiece.FlatU(m.Pose)
                        : expected[i] == BetweenPoles.MagnetProduct.Lift ? BetweenPoles.MagnetPiece.VerticalBar(m.Pose) : Mathf.Abs((m.Pose * Vector3.right).y) < .05f;
                    check(valid, names[i] + ": posture changed during animation");
                    var uPose = inputs[i].shape == BetweenPoles.MagnetShape.Horseshoe ? initialInputPoses[i] : initialReceiverPoses[i];
                    if (m.shape == BetweenPoles.MagnetShape.Horseshoe)
                        check(Vector3.Dot(m.Pose * Vector3.forward, uPose * Vector3.forward) > .95f, names[i] + ": U opening rotated during docking");
                }
            }
        }
        if (UnityEditor.EditorApplication.timeSinceStartup - start > 30) { failures.Add("Timeout"); busy = false; phase = 2; }
        if (busy) return;
        if (phase == 0) {
            for (int i = 0; i < boards.Count; i++) {
                var b = boards[i]; var r = receivers[i]; var m = inputs[i]; var dir = directions[i]; var home = receiverCells[i];
                check(r.product == expected[i], names[i] + ": incorrect product " + r.product);
                check((r.transform.position - position(home)).sqrMagnitude < .0001f, names[i] + ": receiver cell moved");
                var landing = expected[i] == BetweenPoles.MagnetProduct.BridgeHalf && home + r.bridgeDirection == home - dir ? home - dir * 2 : home - dir;
                check(b.PlayerCell == landing, names[i] + ": incorrect player landing");
                check(r.baseNorth == (r.shape == BetweenPoles.MagnetShape.Horseshoe ? r.north : m.north), names[i] + ": U polarity lost");
                check(r.combined && !r.CanBePushed && !m.gameObject.activeSelf, names[i] + ": materials not locked");
                check(!r.walkable, names[i] + ": half bridge/lift unexpectedly walkable");
                if (expected[i] == BetweenPoles.MagnetProduct.BridgeHalf) {
                    check(b.MagnetAt(home + r.bridgeDirection) == r, names[i] + ": missing second occupied cell");
                    check(home + r.bridgeDirection != b.PlayerCell, names[i] + ": bridge overlaps player");
                    var uOpening = (m.shape == BetweenPoles.MagnetShape.Horseshoe ? initialInputPoses[i] : initialReceiverPoses[i]) * Vector3.forward;
                    check(Vector3.Dot(new Vector3(r.bridgeDirection.x,0,r.bridgeDirection.y), uOpening) > .95f, names[i] + ": bridge direction differs from U opening");
                    check(Vector3.Dot(r.geometry.GetChild(0).forward, uOpening) > .95f, names[i] + ": final U opening changed");
                    var railCenter = r.geometry.GetChild(1).position;
                    check(Vector3.Dot(railCenter - position(home), uOpening) > 1.4f, names[i] + ": bar failed to slide toward U opening");
                }
                check(b.UndoStep(), names[i] + ": undo failed");
                check(m.CanBePushed && r.CanBePushed && m.gameObject.activeSelf, names[i] + ": undo did not restore materials");
                check(Quaternion.Angle(m.Pose, initialInputPoses[i]) < .1f && Quaternion.Angle(r.Pose, initialReceiverPoses[i]) < .1f, names[i] + ": undo did not restore poses");
                check(b.PlayerCell == home - dir * 2, names[i] + ": undo player mismatch");
                if (expected[i] == BetweenPoles.MagnetProduct.BridgeHalf) {
                    var opening = (m.shape == BetweenPoles.MagnetShape.Horseshoe ? initialInputPoses[i] : initialReceiverPoses[i]) * Vector3.forward;
                    var secondCell = home + new Vector2Int(Mathf.RoundToInt(opening.x),Mathf.RoundToInt(opening.z));
                    var ground = b.TileAt(secondCell);
                    ground.blocked = true;
                    check(!b.TryStep(dir) && m.CanBePushed && r.CanBePushed, names[i] + ": stone failed to block assembly");
                    ground.blocked = false; ground.surfaceHeight = 1;
                    check(!b.TryStep(dir) && m.CanBePushed && r.CanBePushed, names[i] + ": height failed to block assembly");
                    ground.surfaceHeight = 0;
                    if(secondCell != home - dir) {
                        var obstacle = material(b.transform, secondCell, BetweenPoles.MagnetShape.Bar, true, Quaternion.identity);
                        b.magnets = new[] { m, r, obstacle };
                        check(!b.TryStep(dir) && m.CanBePushed && r.CanBePushed, names[i] + ": unrelated magnet failed to block assembly");
                        obstacle.gameObject.SetActive(false); UnityEngine.Object.Destroy(obstacle.gameObject);
                        b.magnets = new[] { m, r };
                    }
                }
                check(b.TryStep(dir), names[i] + ": repeat rejected");
            }
            phase = 1; return;
        }
        if (phase == 1) {
            for (int i = 0; i < boards.Count; i++) {
                if (expected[i] != BetweenPoles.MagnetProduct.BridgeHalf) continue;
                var b = boards[i]; var r = receivers[i]; var home = receiverCells[i];
                var dir = new Vector2Int(-r.bridgeDirection.y, r.bridgeDirection.x);
                var pose = Quaternion.FromToRotation(Vector3.right, new Vector3(dir.x,0,dir.y));
                var third = material(b.transform, home - dir, BetweenPoles.MagnetShape.Bar, !r.baseNorth, pose);
                b.magnets = new[] { inputs[i], r, third }; b.player.position = position(home - dir * 2); b.CaptureInitialState();
                var second = b.TileAt(home + r.bridgeDirection); second.blocked = true;
                check(!b.TryStep(dir) && r.product == BetweenPoles.MagnetProduct.BridgeHalf && third.CanBePushed, names[i] + ": blocked completion consumed material");
                second.blocked = false;
                third.geometry.rotation = Quaternion.Euler(0,0,90); BetweenPoles.MagnetVisuals.Ground(third);
                check(!b.TryStep(dir), names[i] + ": standing third bar incorrectly accepted");
                third.geometry.rotation = pose; BetweenPoles.MagnetVisuals.Ground(third);
                check(b.TryStep(dir), names[i] + ": flat third bar rejected " + b.LastRule);
            }
            phase = 2; return;
        }
        for (int i = 0; i < boards.Count; i++) {
            if (expected[i] == BetweenPoles.MagnetProduct.BridgeHalf) {
                var r = receivers[i]; var b = boards[i]; var home = receiverCells[i];
                var opening = (inputs[i].shape == BetweenPoles.MagnetShape.Horseshoe ? initialInputPoses[i] : initialReceiverPoses[i]) * Vector3.forward;
                var bridgeDir = new Vector2Int(Mathf.RoundToInt(opening.x),Mathf.RoundToInt(opening.z));
                check(r.product == BetweenPoles.MagnetProduct.Bridge && r.walkable, names[i] + ": completion did not create walkable bridge");
                check(r.bridgeDirection == bridgeDir && (r.transform.position - position(home)).sqrMagnitude < .0001f, names[i] + ": completion changed bridge direction/anchor");
                check(b.MagnetAt(home + bridgeDir) == r && Vector3.Dot(r.geometry.GetChild(0).forward, opening) > .95f, names[i] + ": complete bridge footprint/opening changed");
                check(b.UndoStep() && r.product == BetweenPoles.MagnetProduct.BridgeHalf && r.bridgeDirection == bridgeDir, names[i] + ": completion undo failed to retain half-bridge direction");
            }
            else check(receivers[i].product == BetweenPoles.MagnetProduct.Lift, names[i] + ": lift changed after replay");
        }
        var summary = boards.Count + " mixed combinations; 4 orientations x 4 independent push directions x 2 polarity assignments; both U faces and flat-bar axes; animation posture and U opening sampled over " + frames + " editor frames; undo/replay; stone/height/unrelated-material rejection; 256 bridge completions with blocked-cell and upright-third-bar rejection. Failures=" + failures.Count;
        foreach (var failure in failures) results.Add(failure);
        UnityEditor.SessionState.SetString("MixedMagnetRegression", summary + "\n" + string.Join("\n", results.ToArray()));
        UnityEditor.EditorApplication.update -= tick;
        UnityEngine.Object.Destroy(root);
    } catch (System.Exception e) {
        UnityEditor.SessionState.SetString("MixedMagnetRegression", "ERROR: " + e);
        UnityEditor.EditorApplication.update -= tick;
        UnityEngine.Object.Destroy(root);
    }
};
UnityEditor.SessionState.SetString("MixedMagnetRegression", "Running");
UnityEditor.EditorApplication.update += tick;
return "Started " + boards.Count + " isolated runtime regression cases";

