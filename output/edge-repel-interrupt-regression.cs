if(!EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Edge repel chain regression");root.SetActive(false);
try{
    var board=root.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
    var avatar=new GameObject("Player");avatar.transform.SetParent(root.transform,false);board.player=avatar.transform;
    var home=new Vector3(150,0,150);avatar.transform.position=home;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    for(int z=0;z<3;z++){
        var go=new GameObject("Tile");go.transform.SetParent(root.transform,false);go.transform.position=home+Vector3.forward*z*1.5f;tiles.Add(go.AddComponent<BetweenPoles.GridTile>());
    }
    board.tiles=tiles.ToArray();var chain=new BetweenPoles.MagnetPiece[3];
    for(int i=0;i<3;i++)chain[i]=BetweenPoles.MagnetVisuals.Create(root.transform,home+Vector3.forward*(i+1)*1.5f,BetweenPoles.MagnetShape.Bar,true,i==0?Quaternion.Euler(0,90,0):Quaternion.identity,1.5f,null);
    board.magnets=chain;root.SetActive(true);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var advance=typeof(BetweenPoles.GridPlayground).GetMethod("AdvanceMovement",flags);
    typeof(BetweenPoles.GridPlayground).GetField("recordingPush",flags).SetValue(board,true);
    System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);};
    var starts=new Vector3[3];var peaks=new float[3];var onset=new[]{-1,-1,-1};var rebound=new[]{-1,-1,-1};
    for(int i=0;i<3;i++)starts[i]=chain[i].geometry.position;
    var exitTile=new GameObject("Side exit");exitTile.transform.SetParent(root.transform,false);exitTile.transform.position=home+Vector3.right*1.5f;
    tiles.Add(exitTile.AddComponent<BetweenPoles.GridTile>());board.tiles=tiles.ToArray();
    board.TryStep(Vector2Int.up);board.StopAllCoroutines();
    for(int i=0;i<12;i++)advance.Invoke(board,null);
    var shown=chain[0].geometry.GetComponentInChildren<Renderer>().transform.position;
    check(board.TryStep(Vector2Int.right),"redirect rejected");board.StopAllCoroutines();
    var presentations=(System.Collections.IList)typeof(BetweenPoles.GridPlayground).GetField("pushPresentations",flags).GetValue(board);
    check(presentations.Count==1,"magnet continuation missing");
    var presentation=presentations[0];var tracks=(System.Collections.IList)presentation.GetType().GetField("tracks").GetValue(presentation);
    check(tracks.Count>=3,"chain continuation incomplete");
    var track=tracks[0];var display=(Renderer)track.GetType().GetField("display").GetValue(track);
    check(Vector3.Distance(display.transform.position,shown)<.00001f,"magnet jumped at interruption");
    var frames=(System.Collections.IList)track.GetType().GetField("frames").GetValue(track);
    check(frames.Count>10,"remaining motion skipped");
    var pose=avatar.GetComponent<BetweenPoles.PlayerPushPose>();
    check(!(bool)pose.GetType().GetField("pushing",flags).GetValue(pose),"player still pushing");
    typeof(BetweenPoles.GridPlayground).GetField("recordingPush",flags).SetValue(board,true);
    for(int i=0;i<60&&board.Busy;i++)advance.Invoke(board,null);
    check(avatar.transform.position==home+Vector3.right*1.5f&&board.UndoCount==1,"player redirect or history incorrect");
    var replay=(System.Collections.IEnumerator)typeof(BetweenPoles.GridPlayground).GetMethod("PlayPushPresentation",flags).Invoke(board,new object[]{presentation});
    var first=display.transform.position;bool moved=false;
    for(int i=0;i<10000&&replay.MoveNext();i++)if(display&&Vector3.Distance(first,display.transform.position)>.001f)moved=true;
    check(moved&&presentations.Count==0,"independent playback failed to finish");
    for(int i=0;i<3;i++){
        check(Vector3.Distance(chain[i].geometry.position,starts[i])<.00001f,"magnet not restored");
        foreach(var renderer in chain[i].geometry.GetComponentsInChildren<Renderer>())check(!renderer.forceRenderingOff,"magnet remained hidden");
    }
    check(board.UndoStep()&&avatar.transform.position==home,"redirect undo failed");
    return "PASS: immediate player redirect, push pose cancelled, uninterrupted chain visuals, independent playback completion, geometry restoration, walk-only history and undo";
}finally{UnityEngine.Object.Destroy(root);}
