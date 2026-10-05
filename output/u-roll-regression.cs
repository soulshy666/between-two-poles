// Execute in Unity Play mode; isolated boards are removed on completion.
if(!Application.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("U flip regression (temporary)");root.hideFlags=HideFlags.DontSave;
var boards=new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var homes=new System.Collections.Generic.List<Vector2Int>();
var pushes=new System.Collections.Generic.List<Vector2Int>();
var poses=new System.Collections.Generic.List<Quaternion>();
var kinds=new System.Collections.Generic.List<int>();
var seenEdge=new System.Collections.Generic.HashSet<int>();
var failures=new System.Collections.Generic.HashSet<string>();
System.Action<bool,string> check=(ok,label)=>{if(!ok)failures.Add(label);};
System.Func<Vector2Int,Vector3> pos=c=>new Vector3(c.x*1.5f,0,c.y*1.5f);
System.Func<Transform,Vector2Int> cell=t=>new Vector2Int(Mathf.RoundToInt(t.position.x/1.5f),Mathf.RoundToInt(t.position.z/1.5f));
System.Func<Transform,Vector2Int,Quaternion,bool,BetweenPoles.MagnetPiece> add=(parent,c,q,north)=>{
    var g=new GameObject("U");g.transform.SetParent(parent,false);g.transform.position=pos(c);
    var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Horseshoe;m.north=north;m.baseNorth=north;
    m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,m.shape,north,BetweenPoles.MagnetProduct.None,1.5f,north,Vector2Int.right);
    m.geometry.rotation=q;BetweenPoles.MagnetVisuals.Ground(m);return m;
};
// 0 normal; 1 stone; 2 height; 3 gap; 4 product; 5 beyond stone; 6 side stone;
// 7 repulsion; 8 blocked repulsion (gap); 9 legacy upright input.
System.Action<Quaternion,Vector2Int,bool,int> make=(q,d,north,kind)=>{
    int i=boards.Count;var home=new Vector2Int(100+i*8,200);
    var g=new GameObject("Flip case "+i);g.SetActive(false);g.transform.SetParent(root.transform,false);
    var b=g.AddComponent<BetweenPoles.GridPlayground>();b.enabled=false;
    var p=new GameObject("Player");p.transform.SetParent(g.transform,false);p.transform.position=pos(home-d);b.player=p.transform;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++){
        var t=new GameObject("Floor");t.transform.SetParent(g.transform,false);t.transform.position=pos(home+new Vector2Int(x,z));tiles.Add(t.AddComponent<BetweenPoles.GridTile>());
    }
    b.tiles=tiles.ToArray();var m=add(g.transform,home,q,north);var magnets=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>{m};
    var target=b.TileAt(home+d);
    if(kind==1)target.blocked=true;
    if(kind==2)target.surfaceHeight=1.5f;
    if(kind==3){tiles.Remove(target);b.tiles=tiles.ToArray();}
    if(kind==4){var other=add(g.transform,home+d,Quaternion.identity,!north);BetweenPoles.MagnetVisuals.Product(other,BetweenPoles.MagnetProduct.Ring,1.5f,Quaternion.identity,!north);magnets.Add(other);}
    if(kind==5)b.TileAt(home+d*2).blocked=true;
    if(kind==6)b.TileAt(home+d+new Vector2Int(-d.y,d.x)).blocked=true;
    if(kind==7||kind==8){magnets.Add(add(g.transform,home+d,q,north));if(kind==8){tiles.Remove(b.TileAt(home+d*2));b.tiles=tiles.ToArray();}}
    b.magnets=magnets.ToArray();g.SetActive(true);
    check(BetweenPoles.MagnetPiece.FlatU(m.Pose),"case "+i+": initial/legacy U not flat");
    boards.Add(b);homes.Add(home);pushes.Add(d);poses.Add(m.Pose);kinds.Add(kind);
    bool reject=(kind>=1&&kind<=4)||kind==8;
    check(b.TryStep(d)!=reject,"case "+i+": acceptance "+b.LastRule);
    if(reject)check(!b.Busy&&b.UndoCount==0&&cell(m.transform)==home,"case "+i+": rejection changed state");
    else check(!b.TryStep(d),"case "+i+": accepted while busy");
};
Vector2Int[] dirs={Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
for(int yaw=0;yaw<4;yaw++)for(int face=0;face<2;face++)for(int d=0;d<4;d++)for(int color=0;color<2;color++)
    make(Quaternion.Euler(0,yaw*90,0)*Quaternion.Euler(0,0,face*180),dirs[d],color==0,0);
for(int d=0;d<4;d++)for(int kind=1;kind<=6;kind++)make(Quaternion.identity,dirs[d],false,kind);
for(int d=0;d<4;d++)for(int face=0;face<2;face++)for(int kind=7;kind<=8;kind++)make(Quaternion.Euler(0,0,face*180),dirs[d],false,kind);
for(int d=0;d<4;d++)for(int tilt=0;tilt<2;tilt++)make(Quaternion.Euler(tilt==0?-90:0,0,tilt==1?90:0),dirs[d],true,9);
int phase=0,frames=0;double started=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
    try{
        frames++;bool busy=false;
        for(int i=0;i<boards.Count;i++){
            var b=boards[i];busy|=b.Busy;var m=b.magnets[0];var d=pushes[i];
            if(b.Busy&&phase==0){
                var delta=m.transform.position-pos(homes[i]);var forward=new Vector3(d.x,0,d.y);
                check((delta-forward*Vector3.Dot(delta,forward)).magnitude<.005f,"case "+i+": movement turned a corner");
                check(Vector3.Dot(delta,forward)<1.501f,"case "+i+": moved beyond one cell");
                if(Mathf.Abs((m.Pose*Vector3.up).y)<.5f)seenEdge.Add(i);
                float bottom=float.PositiveInfinity;
                foreach(var f in m.geometry.GetComponentsInChildren<MeshFilter>()){
                    if(BetweenPoles.MagnetVisuals.IsMagneticEffect(f.GetComponent<Renderer>()))continue;
                    foreach(var v in f.sharedMesh.vertices)bottom=Mathf.Min(bottom,f.transform.TransformPoint(v).y);
                }
                check(Mathf.Abs(bottom)<.005f,"case "+i+": flip lost floor contact");
            }
        }
        if(UnityEditor.EditorApplication.timeSinceStartup-started>45)throw new System.Exception("Timeout");
        if(busy)return;
        for(int i=0;i<boards.Count;i++){
            var b=boards[i];var m=b.magnets[0];var home=homes[i];var d=pushes[i];int kind=kinds[i];
            bool reject=(kind>=1&&kind<=4)||kind==8;
            if(reject){check(cell(m.transform)==home&&b.PlayerCell==home-d&&b.UndoCount==0,"case "+i+": blocked state changed");continue;}
            if(phase==1&&kind!=0)continue;
            int steps=phase==1?2:1;
            check(cell(m.transform)==home+d*steps&&b.PlayerCell==home+d*(steps-1),"case "+i+": wrong landing");
            check(BetweenPoles.MagnetPiece.FlatU(m.Pose),"case "+i+": final U standing");
            var oldOpening=poses[i]*Vector3.forward;var forward=new Vector3(d.x,0,d.y);
            var opening=steps==2?oldOpening:oldOpening-2*Vector3.Dot(oldOpening,forward)*forward;
            check(Vector3.Dot(m.Pose*Vector3.forward,opening)>.999f,"case "+i+": opening reflection wrong");
            check(Vector3.Dot(m.Pose*Vector3.up,poses[i]*Vector3.up)*(steps==2?1:-1)>.999f,"case "+i+": face did not flip");
            check(b.UndoCount==steps,"case "+i+": wrong undo count");
            if(kind==7)check(cell(b.magnets[1].transform)==home+d*2&&BetweenPoles.MagnetPiece.FlatU(b.magnets[1].Pose),"case "+i+": repelled U wrong");
            if(phase==0&&kind==0){check(b.TryStep(d),"case "+i+": second push rejected");continue;}
            check(b.UndoStep(),"case "+i+": undo failed");
            if(steps==2){check(cell(m.transform)==home+d&&BetweenPoles.MagnetPiece.FlatU(m.Pose),"case "+i+": second undo landing");check(b.UndoStep(),"case "+i+": first undo failed");}
            check(cell(m.transform)==home&&Quaternion.Angle(m.Pose,poses[i])<.1f&&b.PlayerCell==home-d,"case "+i+": undo did not restore");
            b.ResetPuzzle();check(Quaternion.Angle(m.Pose,poses[i])<.1f,"case "+i+": reset pose wrong");
        }
        if(phase==0){phase=1;return;}
        check(seenEdge.Count>0,"No intermediate flip frames observed");
        var summary=boards.Count+" runtime U flip cases: 64 facing/face/direction/color combinations, 24 target/side/beyond blockers, 16 same-pole cases, 8 legacy upright layouts; one-cell paths, opening reflection, face swap, floor contact, second push, undo/reset; "+frames+" frames; Failures="+failures.Count;
        UnityEditor.SessionState.SetString("URollRegression",summary+"\n"+string.Join("\n",new System.Collections.Generic.List<string>(failures).ToArray()));
        UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
    }catch(System.Exception e){UnityEditor.SessionState.SetString("URollRegression","ERROR: "+e);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);}
};
UnityEditor.SessionState.SetString("URollRegression","Running");UnityEditor.EditorApplication.update+=tick;
return "Started "+boards.Count+" U flip cases";
