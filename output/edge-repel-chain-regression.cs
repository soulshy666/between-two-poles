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
    check(!board.TryStep(Vector2Int.up)&&board.Busy,"chain did not trigger blocked feedback");board.StopAllCoroutines();
    for(int frame=0;frame<150&&board.Busy;frame++){
        advance.Invoke(board,null);
        for(int i=0;i<3;i++){
            float offset=chain[i].geometry.position.z-starts[i].z;
            if(offset>.002f&&onset[i]<0)onset[i]=frame;
            if(peaks[i]>.05f&&offset<peaks[i]-.002f&&rebound[i]<0)rebound[i]=frame;
            peaks[i]=Mathf.Max(peaks[i],offset);
            check(chain[i].transform.position==home+Vector3.forward*(i+1)*1.5f,"magnet changed cell");
        }
        check(avatar.transform.position==home&&board.UndoCount==0,"player/history changed");
    }
    check(!board.Busy&&onset[0]>=0&&onset[0]<onset[1]&&onset[1]<onset[2],"outgoing propagation order wrong");
    check(rebound[2]>=0&&rebound[2]<rebound[1]&&rebound[1]<rebound[0],"return propagation order wrong");
    for(int i=0;i<3;i++)check(Vector3.Distance(chain[i].geometry.position,starts[i])<.00001f,"magnet failed to return");
    board.TryStep(Vector2Int.up);board.StopAllCoroutines();for(int i=0;i<18;i++)advance.Invoke(board,null);board.ResetPuzzle();
    for(int i=0;i<3;i++)check(Vector3.Distance(chain[i].geometry.position,starts[i])<.00001f,"reset left offsets");
    chain[2].north=false;check(!board.TryStep(Vector2Int.up)&&!board.Busy,"opposite pole incorrectly passed repulsion");
    chain[2].north=true;chain[2].transform.position+=Vector3.forward*1.5f;
    var finder=typeof(BetweenPoles.GridPlayground).GetMethod("EdgeRepelChain",flags);
    check(finder.Invoke(board,new object[]{chain[0],chain[1],Vector2Int.up})==null,"chain crossed empty cell");
    return "PASS: three magnets with mixed orientations, near-to-far motion, far-to-near rebound, fixed cells/history, exact recovery/reset, opposite-pole and empty-cell boundaries";
}finally{UnityEngine.Object.Destroy(root);}
