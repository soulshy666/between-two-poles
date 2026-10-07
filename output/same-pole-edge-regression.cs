// Synchronous rejection checks through actual TryStep; works in Edit or Play mode.

var root=new GameObject("Same pole edge regression temporary");root.hideFlags=HideFlags.DontSave;
var boards=new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var stages=new System.Collections.Generic.List<System.Action<int>>();
var failures=new System.Collections.Generic.HashSet<string>();
var trace=new System.Text.StringBuilder();
System.Action<bool,string> check=(ok,label)=>{if(!ok)failures.Add(label);};
System.Func<Vector2Int,float,Vector3> pos=(c,h)=>new Vector3(c.x*1.5f,h,c.y*1.5f);
System.Func<BetweenPoles.GridPlayground,Vector2Int,BetweenPoles.MagnetShape,bool,Quaternion,BetweenPoles.MagnetPiece> magnet=(b,c,shape,north,pose)=>{
    var g=new GameObject("Material");g.transform.SetParent(b.transform,false);g.transform.position=pos(c,b.player.position.y);
    var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=shape;m.north=north;m.baseNorth=north;
    m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,shape,north,BetweenPoles.MagnetProduct.None,1.5f,north,Vector2Int.right);
    m.geometry.rotation=pose;BetweenPoles.MagnetVisuals.Ground(m);return m;
};
System.Func<Vector2Int,Vector2Int,float,bool,BetweenPoles.GridPlayground> board=(home,dir,h,supported)=>{
    var g=new GameObject("Case "+boards.Count);g.transform.SetParent(root.transform,false);g.SetActive(false);
    var b=g.AddComponent<BetweenPoles.GridPlayground>();b.enabled=false;b.stepSeconds=.04f;b.uRollSeconds=.12f;
    var p=new GameObject("Player");p.transform.SetParent(g.transform,false);p.transform.position=pos(home-dir*2,h);b.player=p.transform;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    foreach(int n in new[]{-3,-2,-1,2,3}){var t=new GameObject("Floor");t.transform.SetParent(g.transform,false);t.transform.position=pos(home+dir*n,h);var tile=t.AddComponent<BetweenPoles.GridTile>();tile.surfaceHeight=h;tiles.Add(tile);}
    if(supported){var t=new GameObject("Receiver floor");t.transform.SetParent(g.transform,false);t.transform.position=pos(home,h);var tile=t.AddComponent<BetweenPoles.GridTile>();tile.surfaceHeight=h;tiles.Add(tile);}
    b.tiles=tiles.ToArray();boards.Add(b);return b;
};
var directions=new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
try{
// Hovering same-pole materials block both bar/U inputs, even with a stone
// beyond them: rejection must not accidentally start the recoil animation.
for(int d=0;d<4;d++)for(int color=0;color<2;color++)for(int shapes=0;shapes<4;shapes++){
    var dir=directions[d];var home=new Vector2Int(200+boards.Count*6,200);float h=color*2.25f;
    var b=board(home,dir,h,false);
    var near=magnet(b,home-dir,(shapes&1)==0?BetweenPoles.MagnetShape.Bar:BetweenPoles.MagnetShape.Horseshoe,color==0,Quaternion.identity);
    var far=magnet(b,home,(shapes&2)==0?BetweenPoles.MagnetShape.Bar:BetweenPoles.MagnetShape.Horseshoe,color==0,Quaternion.identity);
    if(color==0){var g=new GameObject("Far stone");g.transform.SetParent(b.transform,false);g.transform.position=pos(home+dir,h);var t=g.AddComponent<BetweenPoles.GridTile>();t.surfaceHeight=h;t.blocked=true;var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>(b.tiles);tiles.Add(t);b.tiles=tiles.ToArray();}
    b.magnets=new[]{near,far};b.gameObject.SetActive(true);string label="same-pole edge "+boards.Count;
    stages.Add(phase=>{
        if(phase==0||phase==1){
            check(!b.TryStep(dir)&&!b.Busy&&b.UndoCount==0,label+" push/repulsion/recoil was accepted");
            check((near.transform.position-pos(home-dir,h)).sqrMagnitude<.0001f&&(far.transform.position-pos(home,h)).sqrMagnitude<.0001f,label+" material moved");
            check(b.PlayerCell==home-dir*2&&Mathf.Abs(b.player.position.y-h)<.001f,label+" player moved");
            check(Quaternion.Angle(near.Pose,Quaternion.identity)<.1f&&Quaternion.Angle(far.Pose,Quaternion.identity)<.1f,label+" material rolled");
        }
    });
}

foreach(var stage in stages){stage(0);stage(1);}
string result=boards.Count+" same-pole edge rejection cases; Failures="+failures.Count;foreach(var f in failures)result+="\n"+f;return result;
}finally{if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);}
