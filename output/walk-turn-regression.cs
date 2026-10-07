if(!EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Walk turn regression");root.SetActive(false);
try{
    var board=root.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
    var avatar=new GameObject("Player");avatar.transform.SetParent(root.transform,false);board.player=avatar.transform;
    var home=new Vector3(150,0,150);avatar.transform.position=home;avatar.transform.rotation=Quaternion.LookRotation(Vector3.left);
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++){
        var go=new GameObject("Tile");go.transform.SetParent(root.transform,false);go.transform.position=home+new Vector3(x*1.5f,0,z*1.5f);tiles.Add(go.AddComponent<BetweenPoles.GridTile>());
    }
    board.tiles=tiles.ToArray();board.magnets=new BetweenPoles.MagnetPiece[0];root.SetActive(true);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var advance=typeof(BetweenPoles.GridPlayground).GetMethod("AdvanceMovement",flags);
    typeof(BetweenPoles.GridPlayground).GetField("recordingPush",flags).SetValue(board,true);
    System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);};
    check(board.TryStep(Vector2Int.up),"turn/walk rejected");board.StopAllCoroutines();
    check(avatar.transform.position==home&&Quaternion.Angle(avatar.transform.rotation,Quaternion.LookRotation(Vector3.left))<.01f,"initial frame snapped or moved");
    advance.Invoke(board,null);
    float angle=Quaternion.Angle(avatar.transform.rotation,Quaternion.identity);
    check(angle>1&&angle<89&&avatar.transform.position==home,"no planted intermediate turning pose");
    for(int i=0;i<60&&board.Busy;i++){
        advance.Invoke(board,null);
        if(avatar.transform.position!=home)check(Quaternion.Angle(avatar.transform.rotation,Quaternion.identity)<.01f,"walk began before facing forward");
    }
    check(!board.Busy&&avatar.transform.position==home+Vector3.forward*1.5f,"wrong turn landing");
    board.UndoStep();check(avatar.transform.position==home&&Quaternion.Angle(avatar.transform.rotation,Quaternion.LookRotation(Vector3.left))<.01f,"undo lost facing");
    check(board.TryStep(Vector2Int.left),"straight walk rejected");board.StopAllCoroutines();
    check(avatar.transform.position!=home,"straight walking gained turn delay");
    var beforeRedirect=avatar.transform.position;
    check(board.TryStep(Vector2Int.up),"mid-walk direction change rejected");board.StopAllCoroutines();
    var corner=home+Vector3.left*1.5f;
    check(avatar.transform.position==beforeRedirect,"redirect teleported player");
    int approachFrames=0;
    for(int i=0;i<60&&board.Busy;i++){
        var previous=avatar.transform.position;advance.Invoke(board,null);approachFrames++;
        check(avatar.transform.position.z==home.z,"turned before reaching cell centre");
        check(Vector3.Distance(previous,avatar.transform.position)<=1.5f/.18f*1.6f/60f+.001f,"approach exceeded continuous speed");
    }
    check(approachFrames>1&&avatar.transform.position==corner,"missing continuous approach to corner");
    typeof(BetweenPoles.GridPlayground).GetMethod("PumpMovementInput",flags).Invoke(board,new object[]{Vector2Int.zero});board.StopAllCoroutines();
    check(avatar.transform.position==corner&&Quaternion.Angle(avatar.transform.rotation,Quaternion.LookRotation(Vector3.left))<.01f,"redirect snapped heading");
    advance.Invoke(board,null);angle=Quaternion.Angle(avatar.transform.rotation,Quaternion.identity);
    check(angle>1&&angle<89&&avatar.transform.position==corner,"redirect lacks turn transition");
    for(int i=0;i<60&&board.Busy;i++)advance.Invoke(board,null);
    check(!board.Busy&&avatar.transform.position==corner+Vector3.forward*1.5f,"redirect landing failed");
    foreach(int frames in new[]{5,8}){
        avatar.transform.position=home;avatar.transform.rotation=Quaternion.LookRotation(Vector3.left);board.CaptureInitialState();
        check(board.TryStep(Vector2Int.left),"repeat walk rejected");board.StopAllCoroutines();
        for(int i=0;i<frames;i++)advance.Invoke(board,null);
        var midway=avatar.transform.position;
        check(midway!=corner,"test must interrupt before centre");
        board.TryStep(Vector2Int.up);board.TryStep(Vector2Int.down);
        check(avatar.transform.position==midway,"late redirect teleported");
        for(int i=0;i<60&&board.Busy;i++)advance.Invoke(board,null);
        check(avatar.transform.position==corner,"late redirect skipped centre");
        var obstacle=board.TileAt(new Vector2Int(99,99));obstacle.blocked=frames==8;
        typeof(BetweenPoles.GridPlayground).GetMethod("PumpMovementInput",flags).Invoke(board,new object[]{Vector2Int.zero});board.StopAllCoroutines();
        for(int i=0;i<60&&board.Busy;i++)advance.Invoke(board,null);
        check(avatar.transform.position==(frames==8?corner:corner+Vector3.back*1.5f),"latest direction or blocked destination incorrect");
        check(board.UndoCount==(frames==8?1:2),"redirect history incorrect");
        obstacle.blocked=false;
    }
    return "PASS: planted turn, face before walking, straight-line speed, undo facing, early/mid/late redirects without teleport, centre before turn, latest input wins, blocked destination and history";
}finally{UnityEngine.Object.Destroy(root);}
