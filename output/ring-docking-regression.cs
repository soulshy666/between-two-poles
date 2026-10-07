// Run this method body through Unity execute_code in Play mode.
if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Ring docking regression (temporary)");root.hideFlags=HideFlags.DontSave;
var boards=new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var incoming=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var receivers=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var poses=new System.Collections.Generic.List<Quaternion>();
var inputPoses=new System.Collections.Generic.List<Quaternion>();
var homes=new System.Collections.Generic.List<Vector2Int>();
var pushes=new System.Collections.Generic.List<Vector2Int>();
var names=new System.Collections.Generic.List<string>();
var starts=new System.Collections.Generic.List<Vector3>();
var lastIncoming=new System.Collections.Generic.List<Vector3>();
var lastReceiver=new System.Collections.Generic.List<Vector3>();
var previousOpening=new System.Collections.Generic.List<Vector3>();
var turnDegrees=new System.Collections.Generic.List<float>();
var lastInPose=new System.Collections.Generic.List<Quaternion>();
var lastRecvPose=new System.Collections.Generic.List<Quaternion>();
var halfDuration=new System.Collections.Generic.List<float>();
var failures=new System.Collections.Generic.HashSet<string>();
System.Action<bool,string> check=(ok,label)=>{if(!ok)failures.Add(label);};
System.Func<Vector2Int,Vector3> position=c=>new Vector3(c.x*1.5f,0,c.y*1.5f);
System.Func<Transform,bool?,Bounds> bounds=(geometry,north)=>{
    Bounds b=new Bounds();bool first=true;
    foreach(var f in geometry.GetComponentsInChildren<MeshFilter>(true)){
        var renderer=f.GetComponent<Renderer>();
        if(BetweenPoles.MagnetVisuals.IsMagneticEffect(renderer))continue;
        if(north.HasValue){var color=renderer.sharedMaterial.color;if((color.r>color.b)!=north.Value)continue;}
        foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}
    }
    return b;
};
System.Func<Transform,Vector2Int,bool,Quaternion,BetweenPoles.MagnetPiece> make=(parent,cell,north,pose)=>{
    var g=new GameObject("Test U");g.transform.SetParent(parent,false);g.transform.position=position(cell);
    var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Horseshoe;m.north=north;m.baseNorth=north;
    m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,m.shape,north,BetweenPoles.MagnetProduct.None,1.5f,north,Vector2Int.right);
    m.geometry.rotation=pose;BetweenPoles.MagnetVisuals.Ground(m);return m;
};
for(int facing=0;facing<4;facing++)for(int inFacing=0;inFacing<4;inFacing++)for(int direction=0;direction<4;direction++)for(int color=0;color<2;color++)for(int faces=0;faces<4;faces++){
    int i=boards.Count;var home=new Vector2Int(100+i*8,100);
    var vector=Quaternion.Euler(0,direction*90,0)*Vector3.right;
    var push=new Vector2Int(Mathf.RoundToInt(vector.x),Mathf.RoundToInt(vector.z));
    var host=new GameObject("Ring case "+i);host.SetActive(false);host.transform.SetParent(root.transform,false);
    var board=host.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
    var player=new GameObject("Test player");player.transform.SetParent(host.transform,false);player.transform.position=position(home-push*2);board.player=player.transform;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++){
        var t=new GameObject("Floor");t.transform.SetParent(host.transform,false);t.transform.position=position(home+new Vector2Int(x,z));tiles.Add(t.AddComponent<BetweenPoles.GridTile>());
    }
    board.tiles=tiles.ToArray();var pose=Quaternion.Euler(0,facing*90,0)*Quaternion.Euler(0,0,(faces/2)*180);var inPose=Quaternion.Euler(0,inFacing*90,0)*Quaternion.Euler(0,0,(faces%2)*180);
    var m=make(host.transform,home-push,color==0,inPose);var r=make(host.transform,home,color!=0,pose);
    board.magnets=new[]{m,r};host.SetActive(true);
    boards.Add(board);incoming.Add(m);receivers.Add(r);poses.Add(pose);inputPoses.Add(inPose);homes.Add(home);pushes.Add(push);
    starts.Add(bounds(r.geometry,null).center);lastIncoming.Add(Vector3.zero);lastReceiver.Add(Vector3.zero);previousOpening.Add(inPose*Vector3.forward);turnDegrees.Add(0);halfDuration.Add(0);lastInPose.Add(inPose);lastRecvPose.Add(pose);
    names.Add("receiver="+facing+", incoming="+inFacing+", push="+direction+", color="+color+", faces="+faces);
    check(board.TryStep(push),names[i]+": push rejected "+board.LastRule);
}
int phase=0,frames=0;float startTime=Time.time;double start=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
    try{
        frames++;bool busy=false;
        for(int i=0;i<boards.Count;i++){
            var b=boards[i];var m=incoming[i];var r=receivers[i];busy|=b.Busy;
            if(phase==0&&b.Busy&&!r.combined){
                var rb=bounds(r.geometry,null);var ib=bounds(m.geometry,null);var opening=poses[i]*Vector3.forward;
                var pushVector=new Vector3(pushes[i].x,0,pushes[i].y);
                bool axial=Mathf.Abs(Vector3.Dot(opening,pushVector))>.95f&&Mathf.Abs(Vector3.Dot(inputPoses[i]*Vector3.forward,pushVector))>.95f;
                bool side=Vector3.Dot(inputPoses[i]*Vector3.forward,opening)>.95f&&Mathf.Abs(Vector3.Dot(opening,pushVector))<.05f;
                bool flipI=axial&&Vector3.Dot(inputPoses[i]*Vector3.forward,pushVector)<0,flipR=axial&&Vector3.Dot(opening,pushVector)>0;
                if(flipR)opening=-opening;
                if(side)opening=-pushVector;
                check(flipR||side||Quaternion.Angle(r.Pose,poses[i])<.1f,names[i]+": receiver rotated during docking");
                var delta=rb.center-starts[i];delta.y=0;var along=Vector3.Dot(delta,opening);
                check(side?delta.magnitude<.5f:along<.005f&&along>-.36f&&(delta-opening*along).magnitude<.01f,names[i]+": receiver moved beyond small settle");
                if(side){
                    float rt=Vector3.SignedAngle(poses[i]*Vector3.forward,r.Pose*Vector3.forward,Vector3.up);
                    float it=Vector3.SignedAngle(inputPoses[i]*Vector3.forward,m.Pose*Vector3.forward,Vector3.up);
                    float expected=Vector3.SignedAngle(poses[i]*Vector3.forward,-pushVector,Vector3.up);
                    check(Mathf.Abs(rt+it)<.1f&&rt*Mathf.Sign(expected)>=-.1f&&Mathf.Abs(rt)<=90.1f,names[i]+": halves did not counter-rotate by quarter turns");
                    check(Vector3.Dot(r.Pose*Vector3.up,poses[i]*Vector3.up)>.999f,names[i]+": receiver flipped face");
                }
                check(ib.min.y>-.005f&&rb.min.y>-.005f,names[i]+": material passed through floor");
                check(flipI||(Vector3.Dot(m.Pose*Vector3.up,inputPoses[i]*Vector3.up)>.999f&&Mathf.Abs(ib.min.y)<.005f&&ib.size.y<.245f),names[i]+": incoming left the ground plane");
                var currentOpening=m.Pose*Vector3.forward;
                float clockwise=Vector3.SignedAngle(previousOpening[i],currentOpening,Vector3.up);
                float needed=Vector3.SignedAngle(inputPoses[i]*Vector3.forward,-opening,Vector3.up);
                if(Mathf.Abs(needed)>179.9f)needed=180;
                check(flipI||(Mathf.Abs(needed)<.1f?Mathf.Abs(clockwise)<.1f:clockwise*Mathf.Sign(needed)>=-.1f),names[i]+": unnecessary/reversed rotation");
                turnDegrees[i]+=clockwise;previousOpening[i]=currentOpening;
                lastInPose[i]=m.Pose;lastRecvPose[i]=r.Pose;
                if(axial||side){var relative=ib.center-rb.center;relative.y=0;check(Vector3.Cross(relative,pushVector).magnitude<.005f&&Vector3.Dot(relative,pushVector)<0,names[i]+": pair detoured or swapped sides");}
                lastIncoming[i]=ib.center;lastReceiver[i]=rb.center;halfDuration[i]=Time.time-startTime;
            }
        }
        if(UnityEditor.EditorApplication.timeSinceStartup-start>45)throw new System.Exception("Timeout");
        if(busy)return;
        for(int i=0;i<boards.Count;i++){
            var b=boards[i];var m=incoming[i];var r=receivers[i];var home=homes[i];var push=pushes[i];var opening=poses[i]*Vector3.forward;
            var pushVector=new Vector3(push.x,0,push.y);
            bool axial=Mathf.Abs(Vector3.Dot(opening,pushVector))>.95f&&Mathf.Abs(Vector3.Dot(inputPoses[i]*Vector3.forward,pushVector))>.95f;
            bool side=Vector3.Dot(inputPoses[i]*Vector3.forward,opening)>.95f&&Mathf.Abs(Vector3.Dot(opening,pushVector))<.05f;
            bool flipI=axial&&Vector3.Dot(inputPoses[i]*Vector3.forward,pushVector)<0,flipR=axial&&Vector3.Dot(opening,pushVector)>0;
            if(flipR)opening=-opening;
            if(side)opening=-pushVector;
            var rb=bounds(r.geometry,r.north);var ib=bounds(r.geometry,m.north);
            check(r.product==BetweenPoles.MagnetProduct.Ring&&r.walkable,names[i]+": product is not walkable ring");
            check((r.transform.position-position(home)).sqrMagnitude<.0001f,names[i]+": receiver anchor moved");
            check(b.PlayerCell==home-push&&!m.gameObject.activeSelf,names[i]+": player/material state mismatch");
            check(Vector3.Dot(rb.center-position(home),opening)<-.2f&&Vector3.Dot(ib.center-position(home),opening)>.2f,names[i]+": final color halves reversed");
            check(rb.size.y<.25f&&ib.size.y<.25f&&Mathf.Abs(rb.center.y-ib.center.y)<.001f,names[i]+": ring is vertically stacked");
            if(phase==0){
                check((rb.center-lastReceiver[i]).magnitude<.01f&&(ib.center-lastIncoming[i]).magnitude<.01f,names[i]+": model swap jumped");
                float expectedTurn=Vector3.SignedAngle(inputPoses[i]*Vector3.forward,-opening,Vector3.up);
                if(Mathf.Abs(expectedTurn)>179.9f)expectedTurn=180;
                check(flipI||Mathf.Abs(turnDegrees[i]-expectedTurn)<.2f,names[i]+": clockwise rotation did not reach closing pose");
                check(Vector3.Dot(lastInPose[i]*Vector3.up,inputPoses[i]*Vector3.up)*(flipI?-1:1)>.999f,names[i]+": incoming face wrong after docking");
                check(Vector3.Dot(lastRecvPose[i]*Vector3.up,poses[i]*Vector3.up)*(flipR?-1:1)>.999f,names[i]+": receiver face wrong after docking");
                check(Vector3.Dot(lastRecvPose[i]*Vector3.forward,opening)>.999f,names[i]+": receiver final opening wrong");
                check(halfDuration[i]>=1.1f,names[i]+": docking animation too fast");
                check(b.UndoStep()&&m.CanBePushed&&r.CanBePushed&&m.gameObject.activeSelf,names[i]+": undo failed");
                check(Quaternion.Angle(m.Pose,inputPoses[i])<.1f&&Quaternion.Angle(r.Pose,poses[i])<.1f,names[i]+": undo changed original poses");
                check((m.transform.position-position(home-push)).sqrMagnitude<.0001f&&b.PlayerCell==home-push*2,names[i]+": undo changed positions");
                check(b.TryStep(push),names[i]+": replay rejected");
            }
        }
        if(phase==0){phase=1;return;}
        var result=boards.Count+" ring cases (4 receiver orientations x 4 incoming orientations x 4 push directions x 2 colors x 4 face pairs); axial pairs flip only misaligned halves and retain sides; other pairs use minimum planar turn; continuous floor contact, color halves, model-swap continuity, player landing, undo/replay; "+frames+" frames; Failures="+failures.Count;
        foreach(var failure in failures)result+="\n"+failure;
        UnityEditor.SessionState.SetString("RingDockRegression",result);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
    }catch(System.Exception e){UnityEditor.SessionState.SetString("RingDockRegression","ERROR: "+e);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);}
};
UnityEditor.SessionState.SetString("RingDockRegression","Running");UnityEditor.EditorApplication.update+=tick;
return "Started "+boards.Count+" ring docking cases";

