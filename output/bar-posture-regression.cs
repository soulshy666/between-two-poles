// Execute this method body in Unity Play mode; the isolated objects are removed afterwards.
if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Bar posture regression (temporary)");root.hideFlags=HideFlags.DontSave;
var boards=new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var inputs=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var receivers=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var inputPoses=new System.Collections.Generic.List<Quaternion>();
var receiverPoses=new System.Collections.Generic.List<Quaternion>();
var inputStanding=new System.Collections.Generic.List<bool>();
var receiverStanding=new System.Collections.Generic.List<bool>();
var homes=new System.Collections.Generic.List<Vector2Int>();
var pushes=new System.Collections.Generic.List<Vector2Int>();
var products=new System.Collections.Generic.List<BetweenPoles.MagnetProduct>();
var names=new System.Collections.Generic.List<string>();
var lastInput=new System.Collections.Generic.List<Vector3>();
var lastReceiver=new System.Collections.Generic.List<Vector3>();
var lastInputAxis=new System.Collections.Generic.List<Vector3>();
var lastReceiverAxis=new System.Collections.Generic.List<Vector3>();
var failures=new System.Collections.Generic.HashSet<string>();
System.Action<bool,string> check=(ok,label)=>{if(!ok)failures.Add(label);};
System.Func<Vector2Int,Vector3> pos=c=>new Vector3(c.x*1.5f,0,c.y*1.5f);
System.Func<Transform,bool?,Bounds> bounds=(geometry,north)=>{
    var b=new Bounds();bool first=true;
    foreach(var f in geometry.GetComponentsInChildren<MeshFilter>(true)){
        var renderer=f.GetComponent<Renderer>();if(BetweenPoles.MagnetVisuals.IsMagneticEffect(renderer))continue;
        var color=renderer.sharedMaterial.color;if(north.HasValue&&(color.r>color.b)!=north.Value)continue;
        foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}
    }return b;
};
System.Func<Transform,Vector2Int,bool,Quaternion,BetweenPoles.MagnetPiece> make=(parent,cell,north,pose)=>{
    var g=new GameObject("Bar");g.transform.SetParent(parent,false);g.transform.position=pos(cell);
    var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Bar;m.north=north;m.baseNorth=north;
    m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,m.shape,north,BetweenPoles.MagnetProduct.None,1.5f,north,Vector2Int.right);
    m.geometry.rotation=pose;BetweenPoles.MagnetVisuals.Ground(m);return m;
};
var variants=new[]{Quaternion.identity,Quaternion.Euler(0,90,0),Quaternion.Euler(0,0,90),Quaternion.Euler(0,0,-90)};
for(int direction=0;direction<4;direction++)for(int color=0;color<2;color++)for(int a=0;a<4;a++)for(int b=0;b<4;b++){
    int i=boards.Count;var home=new Vector2Int(100+i*8,100);
    var v=Quaternion.Euler(0,direction*90,0)*Vector3.right;var push=new Vector2Int(Mathf.RoundToInt(v.x),Mathf.RoundToInt(v.z));
    var host=new GameObject("Case "+i);host.SetActive(false);host.transform.SetParent(root.transform,false);
    var board=host.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
    var player=new GameObject("Player");player.transform.SetParent(host.transform,false);player.transform.position=pos(home-push*2);board.player=player.transform;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++){
        var g=new GameObject("Floor");g.transform.SetParent(host.transform,false);g.transform.position=pos(home+new Vector2Int(x,z));tiles.Add(g.AddComponent<BetweenPoles.GridTile>());
    }board.tiles=tiles.ToArray();
    var m=make(host.transform,home-push,color==0,variants[a]);var r=make(host.transform,home,color!=0,variants[b]);
    board.magnets=new[]{m,r};host.SetActive(true);
    var expected=(a>=2)==(b>=2)?BetweenPoles.MagnetProduct.WideBar:BetweenPoles.MagnetProduct.Cross;
    boards.Add(board);inputs.Add(m);receivers.Add(r);inputPoses.Add(m.Pose);receiverPoses.Add(r.Pose);inputStanding.Add(a>=2);receiverStanding.Add(b>=2);
    homes.Add(home);pushes.Add(push);products.Add(expected);lastInput.Add(Vector3.zero);lastReceiver.Add(Vector3.zero);lastInputAxis.Add(Vector3.zero);lastReceiverAxis.Add(Vector3.zero);
    names.Add("push="+direction+", color="+color+", incoming="+a+", receiver="+b);
    check(BetweenPoles.GridPlayground.Recipe(m,Quaternion.Euler(0,0,90),r,false,push)==expected&&BetweenPoles.GridPlayground.Recipe(m,Quaternion.identity,r,true,push)==expected,names[i]+": recipe depends on rolled arrival/support");
    check(board.TryStep(push),names[i]+": rejected "+board.LastRule);
}
int phase=0,frames=0;double started=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
    try{
        frames++;bool busy=false;
        for(int i=0;i<boards.Count;i++){
            var m=inputs[i];var r=receivers[i];busy|=boards[i].Busy;
            if(phase==0&&boards[i].Busy&&!r.combined){
                var ib=bounds(m.geometry,null);var rb=bounds(r.geometry,null);
                check(ib.min.y>-.005f&&rb.min.y>-.005f,names[i]+": animation penetrated floor");
                if(!inputStanding[i])check(Mathf.Abs((m.Pose*Vector3.right).y)<.001f,names[i]+": flat incoming tipped upright");
                if(!receiverStanding[i])check(Quaternion.Angle(r.Pose,receiverPoses[i])<.1f,names[i]+": flat receiver turned");
                lastInput[i]=ib.center;lastReceiver[i]=rb.center;lastInputAxis[i]=m.Pose*Vector3.right;lastReceiverAxis[i]=r.Pose*Vector3.right;
            }
        }
        if(UnityEditor.EditorApplication.timeSinceStartup-started>40)throw new System.Exception("Timeout");
        if(busy)return;
        for(int i=0;i<boards.Count;i++){
            var m=inputs[i];var r=receivers[i];var board=boards[i];var home=homes[i];var push=pushes[i];bool cross=products[i]==BetweenPoles.MagnetProduct.Cross;
            var ib=bounds(r.geometry,m.north);var rb=bounds(r.geometry,r.north);
            check(r.product==products[i],names[i]+": wrong product "+r.product);
            check((r.transform.position-pos(home)).sqrMagnitude<.0001f&&board.PlayerCell==home-push,names[i]+": receiver/player moved to wrong cell");
            check(!m.gameObject.activeSelf&&r.combined&&!r.CanBePushed,names[i]+": assembly not locked");
            var expectedReceiverAxis=receiverStanding[i]?new Vector3(push.x,0,push.y):receiverPoses[i]*Vector3.right;
            check(Mathf.Abs(Vector3.Dot(r.geometry.right,expectedReceiverAxis))>.99f,names[i]+": receiver axis not preserved");
            float ry=cross&&receiverStanding[i]?.36f:.12f,iy=cross&&inputStanding[i]?.36f:.12f;
            check(Mathf.Abs(rb.center.y-ry)<.005f&&Mathf.Abs(ib.center.y-iy)<.005f,names[i]+": wrong upper/lower material");
            check(rb.size.y<.245f&&ib.size.y<.245f,names[i]+": completed material still upright");
            check(r.walkable==!cross,names[i]+": wrong walkability");
            if(phase==0){
                check((rb.center-lastReceiver[i]).magnitude<.01f&&(ib.center-lastInput[i]).magnitude<.01f,names[i]+": final model swap position jump");
                check(Mathf.Abs(Vector3.Dot(lastReceiverAxis[i],r.geometry.right))>.99f&&Mathf.Abs(Vector3.Dot(lastInputAxis[i],cross?r.geometry.forward:r.geometry.right))>.99f,names[i]+": final model swap orientation jump");
                check(board.UndoStep()&&m.CanBePushed&&r.CanBePushed&&m.gameObject.activeSelf,names[i]+": undo failed");
                check(Quaternion.Angle(m.Pose,inputPoses[i])<.1f&&Quaternion.Angle(r.Pose,receiverPoses[i])<.1f,names[i]+": undo poses mismatch");
                check(board.PlayerCell==home-push*2&&(m.transform.position-pos(home-push)).sqrMagnitude<.0001f,names[i]+": undo positions mismatch");
                check(board.TryStep(push),names[i]+": replay rejected");
            }
        }
        if(phase==0){phase=1;return;}
        var result=boards.Count+" bar combinations: 4 push directions x 2 colors x 4 incoming poses x 4 receiver poses. Equal postures=wide bar; mixed=cross with standing bar on top. Receiver anchor/axis, animation floor contact and flatness, final model continuity, undo/replay. Frames="+frames+"; Failures="+failures.Count;
        foreach(var failure in failures)result+="\n"+failure;
        UnityEditor.SessionState.SetString("BarPostureRegression",result);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
    }catch(System.Exception e){UnityEditor.SessionState.SetString("BarPostureRegression","ERROR: "+e);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);}
};
UnityEditor.SessionState.SetString("BarPostureRegression","Running");UnityEditor.EditorApplication.update+=tick;
return "Started "+boards.Count+" bar posture cases";
