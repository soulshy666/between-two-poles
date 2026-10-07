// Run as execute_code method body in Unity Play mode.
if(!Application.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Flat-to-standing edge regression");root.hideFlags=HideFlags.DontSave;
var boards=new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var homes=new System.Collections.Generic.List<Vector2Int>();var pushes=new System.Collections.Generic.List<Vector2Int>();
var levels=new System.Collections.Generic.List<float>();var originals=new System.Collections.Generic.List<Quaternion>();
var lastI=new System.Collections.Generic.List<Vector3>();var lastR=new System.Collections.Generic.List<Vector3>();
var failures=new System.Collections.Generic.HashSet<string>();int samples=0;
System.Action<bool,string> check=(ok,label)=>{if(!ok)failures.Add(label);};
System.Func<Vector2Int,float,Vector3> pos=(c,h)=>new Vector3(c.x*1.5f,h,c.y*1.5f);
System.Func<Transform,bool?,Bounds> bounds=(geometry,north)=>{
    var b=new Bounds();bool first=true;
    foreach(var f in geometry.GetComponentsInChildren<MeshFilter>()){
        var r=f.GetComponent<Renderer>();if(BetweenPoles.MagnetVisuals.IsMagneticEffect(r))continue;
        if(north.HasValue&&(r.sharedMaterial.color.r>r.sharedMaterial.color.b)!=north.Value)continue;
        foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}
    }return b;
};
for(int direction=0;direction<4;direction++)for(int color=0;color<2;color++)for(int sign=0;sign<2;sign++)for(int level=0;level<2;level++){
    int i=boards.Count;var yaw=Quaternion.Euler(0,direction*90,0);var forward=yaw*Vector3.right;
    var push=new Vector2Int(Mathf.RoundToInt(forward.x),Mathf.RoundToInt(forward.z));var home=new Vector2Int(100+i*8,300);float height=level*2.25f;
    var host=new GameObject("Case "+i);host.transform.SetParent(root.transform,false);host.SetActive(false);
    var board=host.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;board.stepSeconds=.08f;
    var player=new GameObject("Player");player.transform.SetParent(host.transform,false);player.transform.position=pos(home-push*2,height);board.player=player.transform;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    foreach(int n in new[]{-2,-1,1,2}){var g=new GameObject("Floor");g.transform.SetParent(host.transform,false);g.transform.position=pos(home+push*n,height);var t=g.AddComponent<BetweenPoles.GridTile>();t.surfaceHeight=height;tiles.Add(t);}board.tiles=tiles.ToArray();
    var pieces=new BetweenPoles.MagnetPiece[2];
    for(int j=0;j<2;j++){
        var g=new GameObject(j==0?"Incoming":"Receiver");g.transform.SetParent(host.transform,false);g.transform.position=pos(home-push*(1-j),height);
        var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Bar;m.north=(j==0)==(color==0);m.baseNorth=m.north;
        m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,m.shape,m.north,BetweenPoles.MagnetProduct.None,1.5f,m.north,Vector2Int.right);
        m.geometry.rotation=j==1?yaw*Quaternion.Euler(0,0,sign==0?90:-90):yaw*Quaternion.Euler(0,color*180,0);BetweenPoles.MagnetVisuals.Ground(m);pieces[j]=m;
    }
    board.magnets=pieces;host.SetActive(true);boards.Add(board);homes.Add(home);pushes.Add(push);levels.Add(height);originals.Add(pieces[0].Pose);lastI.Add(Vector3.zero);lastR.Add(Vector3.zero);
    check(BetweenPoles.GridPlayground.Recipe(pieces[0],Quaternion.identity,pieces[1],true,push)==BetweenPoles.MagnetProduct.Cross,i+": land recipe should remain cross");
    check(BetweenPoles.GridPlayground.Recipe(pieces[0],Quaternion.identity,pieces[1],false,new Vector2Int(-push.y,push.x))==BetweenPoles.MagnetProduct.Cross,i+": perpendicular edge rule changed");
    check(board.TryStep(push),i+": assembly rejected "+board.LastRule);
}
int phase=0;double started=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
    if(!Application.isPlaying)throw new System.Exception("Play mode ended");
    bool busy=false;
    for(int i=0;i<boards.Count;i++){
        var b=boards[i];busy|=b.Busy;var m=b.magnets[0];var r=b.magnets[1];
        if(phase==0&&b.Busy&&!r.combined){
            var ib=bounds(m.geometry,null);var rb=bounds(r.geometry,null);samples++;
            check(rb.min.y>=levels[i]-.005f&&Mathf.Abs(ib.min.y-levels[i])<.005f,i+": floor penetration");
            check(!BetweenPoles.MagnetPiece.VerticalBar(m.Pose),i+": incoming flat rail tipped");
            float ox=Mathf.Min(ib.max.x,rb.max.x)-Mathf.Max(ib.min.x,rb.min.x),oy=Mathf.Min(ib.max.y,rb.max.y)-Mathf.Max(ib.min.y,rb.min.y),oz=Mathf.Min(ib.max.z,rb.max.z)-Mathf.Max(ib.min.z,rb.min.z);
            check(ox<.002f||oy<.002f||oz<.002f,i+": bars intersected while falling");
            lastI[i]=ib.center;lastR[i]=rb.center;
        }
    }
    if(UnityEditor.EditorApplication.timeSinceStartup-started>35)throw new System.Exception("Timeout");
    if(busy)return;
    for(int i=0;i<boards.Count;i++){
        var b=boards[i];var r=b.magnets[1];var m=b.magnets[0];var home=homes[i];var push=pushes[i];float h=levels[i];
        if(phase==0){
            var ib=bounds(r.geometry,m.north);var rb=bounds(r.geometry,r.north);
            check(r.product==BetweenPoles.MagnetProduct.WideBar&&r.walkable&&b.PlayerCell==home-push,i+": not a walkable wide bar");
            check((r.transform.position-pos(home,h)).sqrMagnitude<.0001f,i+": receiver anchor changed");
            check(Mathf.Abs(ib.center.y-rb.center.y)<.001f&&Mathf.Abs(ib.min.y-h)<.001f,i+": material stacked instead of parallel");
            check((ib.center-lastI[i]).magnitude<.01f&&(rb.center-lastR[i]).magnitude<.01f,i+": model swap jumped");
            check(b.TryStep(push),i+": bridge entry failed");
        }else if(phase==1){check(b.PlayerCell==home,i+": not on bridge");check(b.TryStep(push),i+": bridge exit failed");}
        else {check(b.PlayerCell==home+push&&Mathf.Abs(b.player.position.y-h)<.001f,i+": did not reach opposite shore");check(b.UndoStep()&&b.UndoStep()&&b.UndoStep(),i+": undo failed");check(m.gameObject.activeSelf&&Quaternion.Angle(m.Pose,originals[i])<.1f&&r.product==BetweenPoles.MagnetProduct.None&&BetweenPoles.MagnetPiece.VerticalBar(r.Pose),i+": undo did not restore materials");}
    }
    if(phase++<2)return;
    string result=boards.Count+" flat-to-standing edge bridge cases; animation samples="+samples+"; Failures="+failures.Count;foreach(var f in failures)result+="\n"+f;
    System.IO.File.WriteAllText("D:/unity/between-two-poles/output/flat-to-standing-edge-result.md",result);UnityEditor.SessionState.SetString("FlatToStandingEdgeRegression",result);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
}catch(System.Exception e){UnityEditor.SessionState.SetString("FlatToStandingEdgeRegression","ERROR: "+e);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);}};
UnityEditor.SessionState.SetString("FlatToStandingEdgeRegression","Running");UnityEditor.EditorApplication.update+=tick;
return "Started "+boards.Count+" standing edge bar cases";
