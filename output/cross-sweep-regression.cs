if(!EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Cross sweep regression");root.SetActive(false);
try{
    var board=root.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
    var avatar=new GameObject("Player");avatar.transform.SetParent(root.transform,false);board.player=avatar.transform;
    var home=new Vector3(150,0,150);avatar.transform.position=home;
    var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Art/AstronautV1/AstronautVisual.prefab"),avatar.transform);visual.name="Visual";
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++){
        var go=new GameObject("Tile");go.transform.SetParent(root.transform,false);go.transform.position=home+new Vector3(x*1.5f,0,z*1.5f);tiles.Add(go.AddComponent<BetweenPoles.GridTile>());
    }
    board.tiles=tiles.ToArray();
    var cross=BetweenPoles.MagnetVisuals.Create(root.transform,home+Vector3.forward*1.5f,BetweenPoles.MagnetShape.Bar,true,Quaternion.identity,1.5f,null);
    BetweenPoles.MagnetVisuals.Product(cross,BetweenPoles.MagnetProduct.Cross,1.5f,Quaternion.identity,true);
    board.magnets=new[]{cross};root.SetActive(true);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var advance=typeof(BetweenPoles.GridPlayground).GetMethod("AdvanceMovement",flags);
    typeof(BetweenPoles.GridPlayground).GetField("recordingPush",flags).SetValue(board,true);
    System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);};
    foreach(var direction in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left}){
        avatar.transform.position=home;avatar.transform.rotation=Quaternion.identity;
        cross.transform.position=home+new Vector3(direction.x,0,direction.y)*1.5f;cross.geometry.localRotation=Quaternion.identity;
        board.CaptureInitialState();check(board.TryStep(direction),"cross push rejected");board.StopAllCoroutines();
        int frames=0;for(;frames<180&&board.Busy;frames++){advance.Invoke(board,null);check(avatar.transform.position==home,"cross moved player");}
        check(!board.Busy&&frames>30,"cross action failed to complete");
        check(Quaternion.Angle(cross.geometry.localRotation,Quaternion.Euler(0,90,0))<.01f,"wrong cross rotation");
        check(Vector3.Dot(avatar.transform.forward,new Vector3(direction.x,0,direction.y))>.999f,"wrong player facing");
        var pose=avatar.GetComponent<BetweenPoles.PlayerPushPose>();var type=pose.GetType();
        var mesh=(Mesh)type.GetField("posed",flags).GetValue(pose);var original=(Vector3[])type.GetField("vertices",flags).GetValue(pose);var arms=(bool[])type.GetField("arms",flags).GetValue(pose);
        var neutral=mesh.vertices;for(int i=0;i<neutral.Length;i++)check(Vector3.Distance(neutral[i],original[i])<.00001f,"pose not restored");
        pose.SampleCrossSweep(.18f);var start=mesh.vertices;pose.SampleCrossSweep(.78f);var end=mesh.vertices;
        float dx=0;int count=0;
        for(int i=0;i<original.Length;i++)if(arms[i]&&original[i].x>0&&original[i].y<.5f){dx+=end[i].x-start[i].x;count++;}
        check(count>0&&dx/count<-.15f,"right hand did not sweep left");pose.End();
        check(board.UndoStep()&&Quaternion.Angle(cross.geometry.localRotation,Quaternion.identity)<.01f,"cross undo failed");
    }
    cross.transform.position=home+Vector3.forward*1.5f;
    var blocker=BetweenPoles.MagnetVisuals.Create(root.transform,cross.transform.position+Vector3.right*1.5f,BetweenPoles.MagnetShape.Horseshoe,false,Quaternion.identity,1.5f,null);
    board.magnets=new[]{cross,blocker};board.CaptureInitialState();
    check(board.TryStep(Vector2Int.up),"blocked cross attempt rejected");board.StopAllCoroutines();
    for(int i=0;i<180&&board.Busy;i++)advance.Invoke(board,null);
    check(!board.Busy&&Quaternion.Angle(cross.geometry.localRotation,Quaternion.identity)<.01f,"blocked cross did not recover");
    return "PASS: all four approach directions, stationary player, right-to-left hand sweep, clockwise quarter-turn, neutral recovery, undo, blocked cross recovery";
}finally{UnityEngine.Object.Destroy(root);}
