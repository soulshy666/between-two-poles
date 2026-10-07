if(!EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Edge repel feedback regression");root.SetActive(false);
try{
    var board=root.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
    var avatar=new GameObject("Player");avatar.transform.SetParent(root.transform,false);board.player=avatar.transform;
    var home=new Vector3(150,0,150);avatar.transform.position=home;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    foreach(var offset in new[]{Vector3.zero,Vector3.forward}){
        var go=new GameObject("Tile");go.transform.SetParent(root.transform,false);go.transform.position=home+offset*1.5f;tiles.Add(go.AddComponent<BetweenPoles.GridTile>());
    }
    board.tiles=tiles.ToArray();
    var near=BetweenPoles.MagnetVisuals.Create(root.transform,home+Vector3.forward*1.5f,BetweenPoles.MagnetShape.Bar,true,Quaternion.identity,1.5f,null);
    var far=BetweenPoles.MagnetVisuals.Create(root.transform,home+Vector3.forward*3,BetweenPoles.MagnetShape.Bar,true,Quaternion.identity,1.5f,null);
    board.magnets=new[]{near,far};root.SetActive(true);
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var advance=typeof(BetweenPoles.GridPlayground).GetMethod("AdvanceMovement",flags);
    typeof(BetweenPoles.GridPlayground).GetField("recordingPush",flags).SetValue(board,true);
    System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);};
    var np=near.transform.position;var fp=far.transform.position;var ng=near.geometry.position;var fg=far.geometry.position;
    int landings=0;board.Landed+=tile=>landings++;
    for(int attempt=0;attempt<2;attempt++){
        check(!board.TryStep(Vector2Int.up)&&board.Busy,"rejected push must start feedback");board.StopAllCoroutines();
        float maxNear=0,maxFar=0;int nearFrame=-1,farFrame=-1;bool returning=false;
        for(int i=0;i<100&&board.Busy;i++){
            advance.Invoke(board,null);
            float n=near.geometry.position.z-ng.z,f=far.geometry.position.z-fg.z;
            if(n>.001f&&nearFrame<0)nearFrame=i;if(f>.001f&&farFrame<0)farFrame=i;
            if(maxNear>.1f&&n<maxNear-.01f)returning=true;
            maxNear=Mathf.Max(maxNear,n);maxFar=Mathf.Max(maxFar,f);
            check(near.transform.position==np&&far.transform.position==fp&&avatar.transform.position==home,"logical position moved");
            check(board.UndoCount==0&&landings==0,"feedback changed history or landing");
        }
        check(!board.Busy&&nearFrame>=0&&farFrame>nearFrame,"delayed far response missing");
        check(maxNear>.2f&&maxNear<.3f&&maxFar>.1f&&maxFar<.15f&&returning,"wrong approach/rebound amplitude");
        check(Vector3.Distance(near.geometry.position,ng)<.00001f&&Vector3.Distance(far.geometry.position,fg)<.00001f,"visual offset not restored");
        check(Quaternion.Angle(near.Pose,Quaternion.identity)<.01f&&Quaternion.Angle(far.Pose,Quaternion.identity)<.01f,"posture changed");
    }
    board.TryStep(Vector2Int.up);board.StopAllCoroutines();for(int i=0;i<12;i++)advance.Invoke(board,null);
    board.ResetPuzzle();check(!board.Busy&&Vector3.Distance(near.geometry.position,ng)<.00001f&&Vector3.Distance(far.geometry.position,fg)<.00001f,"reset during feedback failed");
    return "PASS: near approach, delayed far response, rebound, unchanged cells/postures/history, repeat playback and reset during feedback";
}finally{UnityEngine.Object.Destroy(root);}
