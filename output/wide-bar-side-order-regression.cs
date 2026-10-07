// Execute in Play mode; tests actual docking, visible color order and undo.
if(!Application.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Wide bar side order regression");root.hideFlags=HideFlags.DontSave;
var boards=new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var directions=new System.Collections.Generic.List<Vector3>();var homes=new System.Collections.Generic.List<Vector2Int>();
var heights=new System.Collections.Generic.List<float>();var poses=new System.Collections.Generic.List<Quaternion>();
var lastI=new System.Collections.Generic.List<Vector3>();var lastR=new System.Collections.Generic.List<Vector3>();
var failures=new System.Collections.Generic.HashSet<string>();int samples=0;
System.Action<bool,string> check=(ok,label)=>{if(!ok)failures.Add(label);};
System.Func<Vector2Int,float,Vector3> pos=(c,h)=>new Vector3(c.x*1.5f,h,c.y*1.5f);
System.Func<Transform,bool?,Bounds> bounds=(g,north)=>{
    Bounds b=new Bounds();bool first=true;
    foreach(var f in g.GetComponentsInChildren<MeshFilter>()){
        var r=f.GetComponent<Renderer>();if(BetweenPoles.MagnetVisuals.IsMagneticEffect(r))continue;
        if(north.HasValue&&(r.sharedMaterial.color.r>r.sharedMaterial.color.b)!=north.Value)continue;
        foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}
    }return b;
};
for(int d=0;d<4;d++)for(int color=0;color<2;color++)for(int reverse=0;reverse<2;reverse++)for(int incomingReverse=0;incomingReverse<2;incomingReverse++)for(int supported=0;supported<2;supported++){
    int i=boards.Count;var forward=Quaternion.Euler(0,d*90,0)*Vector3.forward;
    var dir=new Vector2Int(Mathf.RoundToInt(forward.x),Mathf.RoundToInt(forward.z));var home=new Vector2Int(100+i*6,400);float h=color*2.25f;
    var yaw=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(0,reverse*180,0);
    var host=new GameObject("Case "+i);host.transform.SetParent(root.transform,false);host.SetActive(false);
    var b=host.AddComponent<BetweenPoles.GridPlayground>();b.enabled=false;
    var player=new GameObject("Player");player.transform.SetParent(host.transform,false);player.transform.position=pos(home-dir*2,h);b.player=player.transform;
    var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
    foreach(int n in new[]{-2,-1,0,1}){
        if(n==0&&supported==0)continue;
        var t=new GameObject("Floor");t.transform.SetParent(host.transform,false);t.transform.position=pos(home+dir*n,h);var tile=t.AddComponent<BetweenPoles.GridTile>();tile.surfaceHeight=h;tiles.Add(tile);
    }b.tiles=tiles.ToArray();var pieces=new BetweenPoles.MagnetPiece[2];
    for(int k=0;k<2;k++){
        var g=new GameObject(k==0?"Incoming":"Receiver");g.transform.SetParent(host.transform,false);g.transform.position=pos(home-dir*(1-k),h);
        var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Bar;m.north=(k==0)==(color==0);m.baseNorth=m.north;
        m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,m.shape,m.north,BetweenPoles.MagnetProduct.None,1.5f,m.north,Vector2Int.right);
        m.geometry.rotation=yaw*Quaternion.Euler(0,k==0?incomingReverse*180:0,0);BetweenPoles.MagnetVisuals.Ground(m);pieces[k]=m;
    }
    b.magnets=pieces;host.SetActive(true);boards.Add(b);directions.Add(forward);homes.Add(home);heights.Add(h);poses.Add(pieces[0].Pose);lastI.Add(Vector3.zero);lastR.Add(Vector3.zero);
    check(b.TryStep(dir),i+": join rejected "+b.LastRule);
}
double start=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
    if(!Application.isPlaying)throw new System.Exception("Play mode ended");bool busy=false;
    for(int i=0;i<boards.Count;i++){
        var b=boards[i];var m=b.magnets[0];var r=b.magnets[1];busy|=b.Busy;
        if(b.Busy&&!r.combined){
            var ib=bounds(m.geometry,null);var rb=bounds(r.geometry,null);samples++;
            check(Vector3.Dot(ib.center-rb.center,directions[i])<-.23f,i+": halves crossed or swapped during docking");
            check(Mathf.Abs(ib.min.y-heights[i])<.002f&&Mathf.Abs(rb.min.y-heights[i])<.002f,i+": rails left support height");
            check(Mathf.Abs((m.Pose*Vector3.right).y)<.001f&&Mathf.Abs((r.Pose*Vector3.right).y)<.001f,i+": flat rail tipped up");
            check(Quaternion.Angle(m.Pose,poses[i])<.1f,i+": parallel bar unnecessarily rotated");
            lastI[i]=ib.center;lastR[i]=rb.center;
        }
    }
    if(UnityEditor.EditorApplication.timeSinceStartup-start>35)throw new System.Exception("Timeout");if(busy)return;
    for(int i=0;i<boards.Count;i++){
        var b=boards[i];var m=b.magnets[0];var r=b.magnets[1];var forward=directions[i];var dir=new Vector2Int(Mathf.RoundToInt(forward.x),Mathf.RoundToInt(forward.z));
        var ib=bounds(r.geometry,m.north);var rb=bounds(r.geometry,r.north);
        check(r.product==BetweenPoles.MagnetProduct.WideBar,i+": wrong product");
        check(Mathf.Abs(Vector3.Dot(ib.center-rb.center,forward)+.24f)<.002f,i+": incoming color is not on approach side");
        check(Mathf.Abs(ib.center.y-rb.center.y)<.001f,i+": colors vertically stacked");
        check((r.transform.position-pos(homes[i],heights[i])).sqrMagnitude<.0001f,i+": receiver anchor moved");
        check((ib.center-lastI[i]).magnitude<.01f&&(rb.center-lastR[i]).magnitude<.01f,i+": final color swap jumped");
        check(b.PlayerCell==homes[i]-dir,i+": player landing");
        check(b.UndoStep()&&m.gameObject.activeSelf&&r.product==BetweenPoles.MagnetProduct.None,i+": undo failed");
        check((m.transform.position-pos(homes[i]-dir,heights[i])).sqrMagnitude<.0001f&&Quaternion.Angle(m.Pose,poses[i])<.1f,i+": undo position/pose");
    }
    string result=boards.Count+" wide bar side-order cases; animation samples="+samples+"; Failures="+failures.Count;foreach(var f in failures)result+="\n"+f;
    UnityEditor.SessionState.SetString("WideBarSideOrderRegression",result);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
}catch(System.Exception e){UnityEditor.SessionState.SetString("WideBarSideOrderRegression","ERROR: "+e);UnityEditor.EditorApplication.update-=tick;if(root){if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);}}};
UnityEditor.SessionState.SetString("WideBarSideOrderRegression","Running");UnityEditor.EditorApplication.update+=tick;
return "Started "+boards.Count+" wide bar side-order cases";
