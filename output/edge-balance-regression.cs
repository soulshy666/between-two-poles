if(!EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Edge balance regression");root.SetActive(false);
try{
    var board=root.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
    var avatar=new GameObject("Player");avatar.transform.SetParent(root.transform,false);board.player=avatar.transform;
    avatar.transform.position=new Vector3(150,0,150);var home=avatar.transform.position;
    var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Art/AstronautV1/AstronautVisual.prefab"),avatar.transform);
    visual.name="Visual";var rest=visual.transform.localRotation;var restPosition=visual.transform.localPosition;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    foreach(var offset in new[]{Vector3.zero,Vector3.left,Vector3.back}){
        var tile=new GameObject("Tile");tile.transform.SetParent(root.transform,false);tile.transform.position=home+offset*1.5f;
        tiles.Add(tile.AddComponent<BetweenPoles.GridTile>());
    }
    board.tiles=tiles.ToArray();board.magnets=new BetweenPoles.MagnetPiece[0];root.SetActive(true);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var type=typeof(BetweenPoles.PlayerPushPose);
    System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);};
    check(!board.TryStep(Vector2Int.right),"edge should reject movement");
    var pose=avatar.GetComponent<BetweenPoles.PlayerPushPose>();
    check(pose!=null&&(bool)type.GetField("balancing",flags).GetValue(pose),"edge pose not active");
    check(!board.Busy&&board.UndoCount==0&&avatar.transform.position==home,"edge changed logical state");
    type.GetMethod("SampleBalance",flags).Invoke(pose,new object[]{.3f});
    check(Quaternion.Angle(rest,visual.transform.localRotation)>20,"missing outward lean");
    var mesh=(Mesh)type.GetField("posed",flags).GetValue(pose);check(mesh!=null,"astronaut mesh not found");
    var a=mesh.vertices;type.GetMethod("SampleBalance",flags).Invoke(pose,new object[]{.35f});var b=mesh.vertices;
    var arms=(bool[])type.GetField("arms",flags).GetValue(pose);bool left=false,right=false;
    for(int i=0;i<a.Length;i++)if(arms[i]&&Vector3.Distance(a[i],b[i])>.02f){if(a[i].x<0)left=true;else right=true;}
    check(left&&right,"both arms must flail");
    type.GetField("balanceTime",flags).SetValue(pose,.4f);board.TryStep(Vector2Int.right);
    check((float)type.GetField("balanceTime",flags).GetValue(pose)==.4f,"same direction restarted pose");
    check(float.IsPositiveInfinity((float)typeof(BetweenPoles.GridPlayground).GetField("nextHeldStep",flags).GetValue(board)),"held repeat not suppressed");
    check(board.TryStep(Vector2Int.left),"safe direction could not interrupt");
    check(!(bool)type.GetField("balancing",flags).GetValue(pose),"balance survived walking");
    typeof(BetweenPoles.GridPlayground).GetMethod("FinishMovement",flags).Invoke(board,null);
    check(avatar.transform.position==home+Vector3.left*1.5f&&board.UndoCount==1,"redirected walk or history incorrect");
    board.UndoStep();check(avatar.transform.position==home,"undo failed");
    board.TryStep(Vector2Int.right);tiles[2].blocked=true;board.TryStep(Vector2Int.down);
    check(!(bool)type.GetField("balancing",flags).GetValue(pose),"blocked other direction did not cancel");
    check(Quaternion.Angle(rest,visual.transform.localRotation)<.01f&&visual.transform.localPosition==restPosition,"interruption left residual pose");
    board.TryStep(Vector2Int.right);type.GetField("balanceTime",flags).SetValue(pose,2f);
    type.GetMethod("LateUpdate",flags).Invoke(pose,null);
    check(!(bool)type.GetField("balancing",flags).GetValue(pose)&&Quaternion.Angle(rest,visual.transform.localRotation)<.01f,"natural completion failed");
    return "PASS: edge rejection, fixed player cell, no undo entry, outward lean, both arms flail, same-direction stability, held-repeat suppression, immediate redirect, walking/undo, blocked-direction cancel, natural recovery";
}finally{UnityEngine.Object.Destroy(root);}
