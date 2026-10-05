// Execute in Play mode. Isolated runtime boards are destroyed on completion.
if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Ring movement regression (temporary)");root.hideFlags=HideFlags.DontSave;
var boards=new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var rings=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var origins=new System.Collections.Generic.List<Vector2Int>();
var directions=new System.Collections.Generic.List<Vector2Int>();
var initialPoses=new System.Collections.Generic.List<Quaternion>();
var expectedCells=new System.Collections.Generic.List<int>();
var tipping=new System.Collections.Generic.List<bool>();
var layingDown=new System.Collections.Generic.List<bool>();
var cancelled=new System.Collections.Generic.List<bool>();
var names=new System.Collections.Generic.List<string>();
var failures=new System.Collections.Generic.HashSet<string>();
System.Action<bool,string> check=(ok,label)=>{if(!ok)failures.Add(label);};
System.Func<Vector2Int,Vector3> pos=c=>new Vector3(c.x*1.5f,0,c.y*1.5f);
System.Func<Transform,Bounds> bounds=geometry=>{
    var b=new Bounds();bool first=true;
    foreach(var f in geometry.GetComponentsInChildren<MeshFilter>(true))foreach(var v in f.sharedMesh.vertices){
        var p=f.transform.TransformPoint(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);
    }
    return b;
};
for(int rotation=0;rotation<4;rotation++)for(int color=0;color<2;color++)for(int variant=0;variant<17;variant++){
    int i=boards.Count;var home=new Vector2Int(100+i*12,100);
    var yaw=Quaternion.Euler(0,rotation*90,0);var v=yaw*Vector3.right;var push=new Vector2Int(Mathf.RoundToInt(v.x),Mathf.RoundToInt(v.z));
    bool tip=variant==0,down=variant>=9;int obstacle=tip?0:((variant-1)%8)/2;bool immediate=!tip&&variant%2==0;int stopAt=immediate?1:4;
    var host=new GameObject("Ring motion case "+i);host.SetActive(false);host.transform.SetParent(root.transform,false);
    var b=host.AddComponent<BetweenPoles.GridPlayground>();b.enabled=false;
    var player=new GameObject("Player");player.transform.SetParent(host.transform,false);player.transform.position=pos(home-push);b.player=player.transform;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    for(int n=-1;n<=4;n++){
        if(!tip&&obstacle==0&&n>=stopAt)continue;
        var g=new GameObject("Floor");g.transform.SetParent(host.transform,false);g.transform.position=pos(home+push*n);
        var tile=g.AddComponent<BetweenPoles.GridTile>();
        if(!tip&&n==stopAt){if(obstacle==1)tile.blocked=true;if(obstacle==2)tile.surfaceHeight=1;}
        tiles.Add(tile);
    }
    b.tiles=tiles.ToArray();
    var ringObject=new GameObject("Ring");ringObject.transform.SetParent(host.transform,false);ringObject.transform.position=pos(home);
    var ring=ringObject.AddComponent<BetweenPoles.MagnetPiece>();ring.shape=BetweenPoles.MagnetShape.Horseshoe;ring.north=color==0;ring.baseNorth=ring.north;ring.combined=true;ring.product=BetweenPoles.MagnetProduct.Ring;
    ring.geometry=BetweenPoles.MagnetVisuals.Build(ring.transform,ring.shape,ring.north,ring.product,1.5f,ring.north,Vector2Int.right);
    var pose=tip?yaw:down?yaw*Quaternion.Euler(0,0,color==0?90:-90):yaw*Quaternion.Euler(-90,0,0);ring.geometry.rotation=pose;ring.walkable=tip;BetweenPoles.MagnetVisuals.Ground(ring);
    var pieces=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>{ring};
    if(!tip&&obstacle==3){
        var g=new GameObject("Other magnet");g.transform.SetParent(host.transform,false);g.transform.position=pos(home+push*stopAt);
        var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Bar;m.north=color!=0;
        m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,m.shape,m.north,BetweenPoles.MagnetProduct.None,1.5f,m.north,Vector2Int.right);pieces.Add(m);
    }
    b.magnets=pieces.ToArray();host.SetActive(true);
    boards.Add(b);rings.Add(ring);origins.Add(home);directions.Add(push);initialPoses.Add(pose);expectedCells.Add(tip?1:immediate?0:down?1:3);tipping.Add(tip);layingDown.Add(down);cancelled.Add(false);
    names.Add("rotation="+rotation+", color="+color+", variant="+variant);
    check(ring.CanBePushed,names[i]+": assembled ring not pushable");
    check(b.TryStep(push)==(!immediate),names[i]+": acceptance mismatch "+b.LastRule);
    check(b.UndoCount==(immediate?0:1),names[i]+": wrong undo count");
}
int phase=0,frames=0;double started=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
    try{
        frames++;bool busy=false;
        for(int i=0;i<boards.Count;i++){
            var b=boards[i];var ring=rings[i];busy|=b.Busy;
            if(b.Busy){
                if(phase==1&&!cancelled[i]&&(ring.transform.position-pos(origins[i])).magnitude>.25f){
                    check(b.UndoStep()&&!b.Busy&&(ring.transform.position-pos(origins[i])).sqrMagnitude<.0001f
                        &&Quaternion.Angle(ring.Pose,initialPoses[i])<.1f&&ring.walkable==tipping[i],names[i]+": mid-motion undo failed");
                    cancelled[i]=true;check(b.TryStep(directions[i]),names[i]+": replay after mid-motion cancellation failed");
                }
                var body=bounds(ring.geometry);
                check(Mathf.Abs(body.min.y)<.005f,names[i]+": ring not grounded");
                check(Mathf.Abs(body.center.x-ring.transform.position.x)<.005f&&Mathf.Abs(body.center.z-ring.transform.position.z)<.005f,names[i]+": visual/logical center mismatch");
                check(!ring.walkable,names[i]+": moving upright ring remained walkable");
                if(!tipping[i]&&!layingDown[i])check(Mathf.Abs((ring.Pose*Vector3.up).y)<.001f,names[i]+": rolling ring tilted flat");
            }
        }
        if(UnityEditor.EditorApplication.timeSinceStartup-started>45)throw new System.Exception("Timeout");
        if(busy)return;
        for(int i=0;i<boards.Count;i++){
            var b=boards[i];var ring=rings[i];var home=origins[i];var push=directions[i];int distance=expectedCells[i];
            var end=home+push*distance;
            check((ring.transform.position-pos(end)).sqrMagnitude<.0001f,names[i]+": wrong stopping cell");
            check(b.MagnetAt(end)==ring,names[i]+": wrong occupied cell");
            check(b.PlayerCell==(distance>0?home:home-push),names[i]+": player followed beyond first cell");
            bool flat=layingDown[i]&&distance>0;
            check(flat?BetweenPoles.MagnetPiece.FlatU(ring.Pose):Mathf.Abs((ring.Pose*Vector3.up).y)<.001f,names[i]+": incorrect final upright/flat pose");
            check(ring.product==BetweenPoles.MagnetProduct.Ring&&ring.combined&&ring.walkable==flat,names[i]+": ring product/walkability state changed");
            if(phase==0){
                if(distance>0){
                    check(b.UndoStep(),names[i]+": undo failed");
                    check((ring.transform.position-pos(home)).sqrMagnitude<.0001f&&Quaternion.Angle(ring.Pose,initialPoses[i])<.1f&&ring.walkable==tipping[i],names[i]+": undo did not restore ring pose/walkability");
                    check(b.PlayerCell==home-push,names[i]+": undo did not restore player");
                    check(b.TryStep(push),names[i]+": repeat failed");
                }
            }else{
                check(distance==0||cancelled[i],names[i]+": moving cancellation was not exercised");
            }
        }
        if(phase==0){phase=1;return;}
        var result=boards.Count+" ring movement cases: 4 directions x 2 colors; tipping up/down from either axle face, continuous multi-cell rolling, immediate/distant edges, stones, height changes and magnets; floor contact, occupancy, player landing, flat walkability, undo/replay/cancellation. Frames="+frames+"; Failures="+failures.Count;
        foreach(var failure in failures)result+="\n"+failure;
        UnityEditor.SessionState.SetString("RingMovementRegression",result);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
    }catch(System.Exception e){UnityEditor.SessionState.SetString("RingMovementRegression","ERROR: "+e);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);}
};
UnityEditor.SessionState.SetString("RingMovementRegression","Running");UnityEditor.EditorApplication.update+=tick;
return "Started "+boards.Count+" ring movement cases";
