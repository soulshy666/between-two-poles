// Execute as a method body through Unity execute_code in Play mode.
if(!Application.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Edge regression temporary");root.hideFlags=HideFlags.DontSave;
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
for(int d=0;d<4;d++)for(int level=0;level<2;level++)for(int color=0;color<2;color++)for(int variant=0;variant<4;variant++){
    var dir=directions[d];float h=level*2.25f;var home=new Vector2Int(200+boards.Count*6,200);
    var b=board(home,dir,h,false);bool u=variant>=2;
    var pose=variant==1?Quaternion.Euler(0,0,90):variant==3?Quaternion.Euler(180,0,0):Quaternion.identity;
    var m=magnet(b,home-dir,u?BetweenPoles.MagnetShape.Horseshoe:BetweenPoles.MagnetShape.Bar,color==0,pose);
    b.magnets=new[]{m};b.gameObject.SetActive(true);string label="single "+boards.Count;
    stages.Add(phase=>{
        if(phase==0)check(b.TryStep(dir),label+" first push rejected "+b.LastRule);
        if(phase==1){check((m.transform.position-pos(home,h)).sqrMagnitude<.001f&&b.PlayerCell==home-dir,label+" hovering height or player");check(!u||BetweenPoles.MagnetPiece.FlatU(m.Pose),label+" U not flat");check(!b.TryStep(dir),label+" reached hovering magnet");}
        if(phase==2){check((m.transform.position-pos(home,h)).sqrMagnitude<.001f&&b.PlayerCell==home-dir&&Mathf.Abs(b.player.position.y-h)<.001f,label+" rejected push moved magnet/player");check(!b.Busy&&b.UndoCount==1,label+" rejected push changed history/busy");check(!b.TryStep(dir),label+" repeated push reached hovering magnet");check(b.UndoStep(),label+" undo");check((m.transform.position-pos(home-dir,h)).sqrMagnitude<.001f,label+" undo location");b.ResetPuzzle();}
        if(phase==3)check((m.transform.position-pos(home-dir,h)).sqrMagnitude<.001f&&Quaternion.Angle(m.Pose,pose)<.1f&&b.PlayerCell==home-dir*2,label+" reset");
    });
}
for(int d=0;d<4;d++)for(int level=0;level<2;level++)for(int color=0;color<2;color++)for(int mode=0;mode<4;mode++){
    var dir=directions[d];float h=level*2.25f;bool supported=(mode&1)!=0,perpendicular=(mode&2)!=0;
    var home=new Vector2Int(200+boards.Count*6,200);var b=board(home,dir,h,supported);
    var incoming=magnet(b,home-dir,BetweenPoles.MagnetShape.Bar,color==0,perpendicular?Quaternion.Euler(0,90,0):Quaternion.identity);
    var receiver=magnet(b,home,BetweenPoles.MagnetShape.Bar,color!=0,Quaternion.identity);
    b.magnets=new[]{incoming,receiver};b.gameObject.SetActive(true);bool cross=perpendicular&&!supported;string label="combine "+boards.Count;
    stages.Add(phase=>{
        if(phase==0)check(b.TryStep(dir),label+" join rejected "+b.LastRule);
        if(phase==1){check(receiver.product==(cross?BetweenPoles.MagnetProduct.Cross:BetweenPoles.MagnetProduct.WideBar),label+" recipe");check((receiver.transform.position-pos(home,h)).sqrMagnitude<.001f&&b.PlayerCell==home-dir,label+" anchor/player");check(receiver.walkable==!cross,label+" walkability");if(cross)check(Mathf.Abs(receiver.geometry.localPosition.y+.24f)<.001f,label+" cross did not sink");check(b.TryStep(dir)==!supported,label+" bridge entry/cross rotation/land obstacle");}
        if(phase==2){if(cross){check(b.PlayerCell==home-dir&&Quaternion.Angle(receiver.Pose,Quaternion.Euler(0,90,0))<.1f,label+" cross rotation moved player");}else if(!supported){check(b.PlayerCell==home&&Mathf.Abs(b.player.position.y-h-.24f)<.001f,label+" deck entry");}check(b.UndoStep(),label+" undo");if(!supported)check(b.UndoStep(),label+" undo assembly");check(incoming.gameObject.activeSelf&&receiver.product==BetweenPoles.MagnetProduct.None,label+" restore materials");}
    });
}
// End-to-end: roll a standing bar off the shore, bring a second flat bar
// from an adjacent shore lane, combine into a one-cell bridge, then cross.
{
    var home=new Vector2Int(200+boards.Count*6,200);var dir=Vector2Int.right;var b=board(home,dir,0,false);
    var first=magnet(b,home-dir,BetweenPoles.MagnetShape.Bar,true,Quaternion.Euler(0,0,90));
    var second=magnet(b,home-dir+Vector2Int.up,BetweenPoles.MagnetShape.Bar,false,Quaternion.identity);
    var extra=new System.Collections.Generic.List<BetweenPoles.GridTile>(b.tiles);
    foreach(var c in new[]{home-dir+Vector2Int.up,home-dir*2+Vector2Int.up,home-dir+Vector2Int.up*2,home-dir*2+Vector2Int.up*2,home+dir}){var g=new GameObject("Route floor");g.transform.SetParent(b.transform,false);g.transform.position=pos(c,0);extra.Add(g.AddComponent<BetweenPoles.GridTile>());}
    b.tiles=extra.ToArray();b.magnets=new[]{first,second};b.gameObject.SetActive(true);
    var moves=new[]{dir,-dir,Vector2Int.up,Vector2Int.up,dir,Vector2Int.down,-dir,Vector2Int.down,dir,dir,dir};
    stages.Add(phase=>{trace.AppendLine("route "+phase+" player="+(b.PlayerCell-home)+" y="+b.player.position.y+" first="+first.transform.position+" second="+second.transform.position+" product="+first.product);if(phase<moves.Length)check(b.TryStep(moves[phase]),"route move "+phase+": "+b.LastRule);else if(phase==moves.Length)check(b.PlayerCell==home+dir&&first.product==BetweenPoles.MagnetProduct.WideBar,"route did not cross bridge");});
}
// Two-cell bridge completion, top-to-cross blocking, repulsion and obstacles.
for(int d=0;d<4;d++)for(int mode=0;mode<7;mode++){
    var dir=directions[d];var home=new Vector2Int(200+boards.Count*6,200);float h=2.25f;
    var b=board(home,dir,h,mode==3||mode==4||mode==6);
    var incoming=magnet(b,home-dir,BetweenPoles.MagnetShape.Bar,true,Quaternion.identity);
    var receiver=magnet(b,home,mode==0?BetweenPoles.MagnetShape.Horseshoe:BetweenPoles.MagnetShape.Bar,mode==2||mode==6,Quaternion.LookRotation(new Vector3(dir.x,0,dir.y)));
    var third=magnet(b,home-dir*3,BetweenPoles.MagnetShape.Bar,true,Quaternion.identity);
    third.gameObject.SetActive(false);string label="extra "+d+"/"+mode;int testMode=mode;
    if(mode==1){BetweenPoles.MagnetVisuals.Product(incoming,BetweenPoles.MagnetProduct.WideBar,1.5f,Quaternion.identity,true);BetweenPoles.MagnetVisuals.Product(receiver,BetweenPoles.MagnetProduct.Cross,1.5f,Quaternion.identity,false);b.player.position=pos(home-dir,h+.24f);}
    if(mode==3)b.TileAt(home).blocked=true;
    if(mode==4)b.TileAt(home).surfaceHeight=h+1;
    if(mode==3||mode==4||mode==5)receiver.gameObject.SetActive(false);
    if(mode==5){receiver.gameObject.SetActive(true);BetweenPoles.MagnetVisuals.Product(receiver,BetweenPoles.MagnetProduct.BridgeHalf,1.5f,Quaternion.identity,false);incoming.gameObject.SetActive(false);b.player.position=pos(home-dir,h);}
    b.magnets=new[]{incoming,receiver,third};b.gameObject.SetActive(true);
    stages.Add(phase=>{
        if(phase==0)check(b.TryStep(dir)==(testMode==0||testMode==6),label+" initial action "+b.LastRule);
        if(testMode==0){
            if(phase==1){check(receiver.product==BetweenPoles.MagnetProduct.BridgeHalf&&!receiver.walkable,label+" half bridge");third.transform.position=pos(home-dir,h);third.gameObject.SetActive(true);b.player.position=pos(home-dir*2,h);check(b.TryStep(dir),label+" complete bridge "+b.LastRule);}
            if(phase==2){check(receiver.product==BetweenPoles.MagnetProduct.Bridge&&receiver.walkable,label+" completion");check(b.TryStep(dir),label+" enter bridge");}
            if(phase==3){check(b.PlayerCell==home,label+" first bridge cell");check(b.TryStep(dir),label+" traverse bridge");}
            if(phase==4){check(b.PlayerCell==home+dir,label+" second bridge cell");check(b.TryStep(dir),label+" exit bridge");}
            if(phase==5)check(b.PlayerCell==home+dir*2&&Mathf.Abs(b.player.position.y-h)<.01f,label+" opposite shore");
        }
        if(testMode==6&&phase==1){check((receiver.transform.position-pos(home+dir,h)).sqrMagnitude<.001f&&(incoming.transform.position-pos(home,h)).sqrMagnitude<.001f,label+" repel hover");check(b.PlayerCell==home-dir,label+" repel player");check(b.UndoStep(),label+" repel undo");}
        if(testMode!=0&&testMode!=6&&phase==1)check(b.UndoCount==0,label+" rejection modified history");
    });
}
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
int phaseIndex=0;double started=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
    if(!Application.isPlaying)throw new System.Exception("Play mode ended");
    if(UnityEditor.EditorApplication.timeSinceStartup-started>70)throw new System.Exception("Timeout");
    foreach(var b in boards)if(b.Busy)return;
    if(phaseIndex<=11){foreach(var stage in stages)stage(phaseIndex);phaseIndex++;return;}
    string result=boards.Count+" edge cases; Failures="+failures.Count;foreach(var f in failures)result+="\n"+f;if(failures.Count>0)result+="\n"+trace;
    UnityEditor.SessionState.SetString("EdgeMagnetRegression",result);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
}catch(System.Exception e){UnityEditor.SessionState.SetString("EdgeMagnetRegression","ERROR: "+e);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);}};
UnityEditor.SessionState.SetString("EdgeMagnetRegression","Running");UnityEditor.EditorApplication.update+=tick;
return "Started "+boards.Count+" edge cases";
