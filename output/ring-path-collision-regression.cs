if(!Application.isPlaying)throw new System.Exception("Play mode required for mesh sampling");
// Deterministic sampling of the production ring path, using actual mesh footprints.
var root=new GameObject("Ring path collision sampling");root.hideFlags=HideFlags.DontSave;
try{
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var sampler=typeof(BetweenPoles.GridPlayground).GetMethod("SampleRingDock",flags);
    var axialSampler=typeof(BetweenPoles.GridPlayground).GetMethod("SampleAxialRingDock",flags);
    var sideSampler=typeof(BetweenPoles.GridPlayground).GetMethod("SampleSideBySideRingDock",flags);
    var grounder=typeof(BetweenPoles.GridPlayground).GetMethod("GroundAssemblyBody",flags);
    var placer=typeof(BetweenPoles.GridPlayground).GetMethod("PlaceAssemblyBody",flags);
    var failures=new System.Collections.Generic.HashSet<string>();
    var preview=new System.Text.StringBuilder("[");bool firstPreview=true;
    System.Func<bool,BetweenPoles.MagnetPiece> make=north=>{
        var g=new GameObject("U");g.transform.SetParent(root.transform,false);
        var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Horseshoe;m.north=north;
        m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,m.shape,north,BetweenPoles.MagnetProduct.None,1.5f,north,Vector2Int.right);return m;
    };
    var incoming=make(false);var receiver=make(true);
    var inFilters=incoming.geometry.GetComponentsInChildren<MeshFilter>();var recvFilters=receiver.geometry.GetComponentsInChildren<MeshFilter>();
    System.Func<MeshFilter[],Vector2[][]> boxes=filters=>{
        var footprints=new Vector2[filters.Length][];
        for(int i=0;i<filters.Length;i++){
            var f=filters[i];var b=f.sharedMesh.bounds;footprints[i]=new Vector2[4];
            for(int j=0;j<4;j++){var v=f.transform.TransformPoint(b.center+new Vector3((j==0||j==3?-1:1)*b.extents.x,0,(j<2?-1:1)*b.extents.z));footprints[i][j]=new Vector2(v.x,v.z);}
        }return footprints;
    };
    System.Func<Vector2[],Vector2[],float> penetration=(a,b)=>{
        float best=float.PositiveInfinity;
        for(int set=0;set<2;set++)for(int side=0;side<2;side++){
            var points=set==0?a:b;var edge=points[side+1]-points[side];var axis=new Vector2(-edge.y,edge.x).normalized;
            float amin=float.PositiveInfinity,amax=float.NegativeInfinity,bmin=amin,bmax=amax;
            for(int k=0;k<4;k++){float av=Vector2.Dot(a[k],axis),bv=Vector2.Dot(b[k],axis);amin=Mathf.Min(amin,av);amax=Mathf.Max(amax,av);bmin=Mathf.Min(bmin,bv);bmax=Mathf.Max(bmax,bv);}
            float overlap=Mathf.Min(amax,bmax)-Mathf.Max(amin,bmin);if(overlap<=.00001f)return 0;best=Mathf.Min(best,overlap);
        }return best;
    };
    System.Func<Transform,Bounds> bounds=geometry=>{
        Bounds b=new Bounds();bool first=true;
        foreach(var f in geometry.GetComponentsInChildren<MeshFilter>())foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}return b;
    };
    System.Func<Vector3,Vector3,float> angle=(a,b)=>{float v=Vector3.SignedAngle(a,b,Vector3.up);return Mathf.Abs(v)>179.9f?180:Mathf.Abs(v)<.1f?0:v;};
    int cases=0,samples=0;float maxExtra=0,maxSeam=0;
    for(int facing=0;facing<4;facing++)for(int inFacing=0;inFacing<4;inFacing++)for(int dir=0;dir<4;dir++)for(int faces=0;faces<4;faces++){
        var yaw=Quaternion.Euler(0,facing*90,0);var opening=yaw*Vector3.forward;
        var inPose=Quaternion.Euler(0,inFacing*90,0)*Quaternion.Euler(0,0,faces%2*180);
        var recvPose=yaw*Quaternion.Euler(0,0,faces/2*180);
        var push=Quaternion.Euler(0,dir*90,0)*Vector3.forward;
        bool axial=Mathf.Abs(Vector3.Dot(opening,push))>.95f&&Mathf.Abs(Vector3.Dot(inPose*Vector3.forward,push))>.95f;
        bool side=Vector3.Dot(inPose*Vector3.forward,opening)>.95f&&Mathf.Abs(Vector3.Dot(opening,push))<.05f;
        bool flipI=axial&&Vector3.Dot(inPose*Vector3.forward,push)<0,flipR=axial&&Vector3.Dot(opening,push)>0;
        bool flip=flipI||flipR;var axis=Vector3.Cross(Vector3.up,push);
        if(axial||side){opening=-push;yaw=Quaternion.LookRotation(opening,Vector3.up);}
        float flipSpan=flipI&&flipR?1.25f:1,flipEnd=.7f/(.7f*flipSpan+1.35f*.7f);
        var startR=new Vector3(0,.12f,0);var startI=startR-push*1.5f;
        var model=BetweenPoles.MagnetVisuals.Build(root.transform,BetweenPoles.MagnetShape.Horseshoe,true,BetweenPoles.MagnetProduct.Ring,1.5f,true,Vector2Int.right);
        model.rotation=yaw;
        var filters=model.GetComponentsInChildren<MeshFilter>();
        Bounds endR=new Bounds(),endI=new Bounds();bool firstR=true,firstI=true;
        foreach(var f in filters){bool red=f.GetComponent<Renderer>().sharedMaterial.color.r>f.GetComponent<Renderer>().sharedMaterial.color.b;foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v);if(red){if(firstR){endR=new Bounds(p,Vector3.zero);firstR=false;}else endR.Encapsulate(p);}else{if(firstI){endI=new Bounds(p,Vector3.zero);firstI=false;}else endI.Encapsulate(p);}}}
        UnityEngine.Object.DestroyImmediate(model.gameObject);
        float turn=angle(inPose*Vector3.forward,-opening),orbit=angle(startI-startR,opening);
        float alignSeconds=Mathf.Max(Mathf.Abs(turn)/90*.65f,Mathf.Abs(orbit)/90*.85f),alignEnd=alignSeconds/(alignSeconds+1.35f);
        if(side)alignEnd=.7f/(.7f+1.35f);
        placer.Invoke(null,new object[]{incoming,flip?Quaternion.AngleAxis(flipI?180:0,axis)*inPose:BetweenPoles.MagnetPiece.UDockPose(yaw*Quaternion.Euler(0,180,0),inPose),Vector3.one,endI.center});
        placer.Invoke(null,new object[]{receiver,side?BetweenPoles.MagnetPiece.UDockPose(yaw,recvPose):Quaternion.AngleAxis(flipR?-180:0,axis)*recvPose,Vector3.one,endR.center});
        var finalI=boxes(inFilters);var finalR=boxes(recvFilters);var seam=new float[20,20];
        for(int a=0;a<20;a++)for(int b=0;b<20;b++){seam[a,b]=penetration(finalI[a],finalR[b]);maxSeam=Mathf.Max(maxSeam,seam[a,b]);}
        string label="receiver="+facing+", incoming="+inFacing+", push="+dir+", faces="+faces;
        for(int step=0;step<=120;step++){
            float p=step/120f;
            object[] args={p,alignEnd,inPose,turn,startI,startR,endI.center,endR.center,opening,orbit,null,null,null};
            sampler.Invoke(null,args);var q=(Quaternion)args[10];var ip=(Vector3)args[11];var rp=(Vector3)args[12];
            Quaternion qr=recvPose;
            if(side){object[] sideArgs={p,alignEnd,inPose,recvPose,push,startI,startR,endI.center,endR.center,null,null,null,null};
                sideSampler.Invoke(null,sideArgs);q=(Quaternion)sideArgs[9];qr=(Quaternion)sideArgs[10];ip=(Vector3)sideArgs[11];rp=(Vector3)sideArgs[12];}
            if(flip){object[] axialArgs={p,flipEnd,axis,flipI,flipR,inPose,recvPose,startI,startR,endI.center,endR.center,null,null,null,null};
                axialSampler.Invoke(null,axialArgs);q=(Quaternion)axialArgs[11];qr=(Quaternion)axialArgs[12];ip=(Vector3)axialArgs[13];rp=(Vector3)axialArgs[14];}
            placer.Invoke(null,new object[]{incoming,q,Vector3.one,ip});placer.Invoke(null,new object[]{receiver,qr,Vector3.one,rp});
            if(flip){grounder.Invoke(null,new object[]{incoming,0f});grounder.Invoke(null,new object[]{receiver,0f});}
            var inBounds=bounds(incoming.geometry);var recvBounds=bounds(receiver.geometry);
            if(flip&&p<flipEnd*flipSpan&&inBounds.Intersects(recvBounds))failures.Add(label+": bounding volumes crossed during flip");
            if(axial||side){var delta=ip-rp;delta.y=0;if(Vector3.Cross(delta,push).magnitude>.001f||Vector3.Dot(delta,push)>=0)failures.Add(label+": pair detoured or reversed");}
            if(flip&&p<flipEnd*flipSpan){var di=ip-startI;di.y=0;var dr=rp-startR;dr.y=0;if(di.magnitude>.001f||dr.magnitude>.001f)failures.Add(label+": approached before flip completed");}
            var ib=boxes(inFilters);var rb=boxes(recvFilters);
            // Existing segmented models have a tiny shared seam where the tips join.
            // No frame may introduce an intersection deeper than that final seam.
            for(int a=0;a<20;a++)for(int b=0;b<20;b++){
                float depth=penetration(ib[a],rb[b]);float excess=depth-seam[a,b];maxExtra=Mathf.Max(maxExtra,excess);
                if(excess>.001f)failures.Add(label+": material intersection at t="+p.ToString("F3")+", depth="+depth.ToString("F4"));
            }
            if(!flip&&(!BetweenPoles.MagnetPiece.FlatU(q)||Vector3.Dot(q*Vector3.up,inPose*Vector3.up)<.999f))failures.Add(label+": face flipped");
            if(Mathf.Abs(bounds(incoming.geometry).min.y)>.001f||Mathf.Abs(bounds(receiver.geometry).min.y)>.001f)failures.Add(label+": left floor");
            if(!flipI&&Mathf.Abs(turn)<.1f&&Quaternion.Angle(q,inPose)>.1f)failures.Add(label+": aligned U rotated");
            if(Mathf.Abs(turn)<.1f&&Mathf.Abs(orbit)<.1f&&(Vector3.Cross(ip-startI,endI.center-startI)).magnitude>.001f)failures.Add(label+": direct docking detoured");
            if(step==120){
                if(Vector3.Dot(q*Vector3.up,inPose*Vector3.up)*(flipI?-1:1)<.999f||Vector3.Dot(qr*Vector3.up,recvPose*Vector3.up)*(flipR?-1:1)<.999f)failures.Add(label+": wrong face at end");
                if(Vector3.Dot(q*Vector3.forward,-opening)<.999f||Vector3.Dot(qr*Vector3.forward,opening)<.999f)failures.Add(label+": wrong final opening");
            }
            if(facing==0&&faces==0&&((inFacing==2&&dir==2)||(inFacing==0&&dir==3)||(inFacing==0&&dir==0))&&step%24==0){
                if(!firstPreview)preview.Append(",");firstPreview=false;
                preview.Append("{\"case\":\"").Append(label).Append("\",\"t\":").Append(p.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)).Append(",\"parts\":[");
                for(int k=0;k<40;k++){if(k>0)preview.Append(",");preview.Append("[");var verts=k<20?ib[k]:rb[k-20];for(int v=0;v<4;v++){if(v>0)preview.Append(",");preview.Append("[").Append(verts[v].x.ToString("F5",System.Globalization.CultureInfo.InvariantCulture)).Append(",").Append(verts[v].y.ToString("F5",System.Globalization.CultureInfo.InvariantCulture)).Append("]");}preview.Append("]");}preview.Append("]}");
            }
            samples++;
        }cases++;
    }
    preview.Append("]");System.IO.File.WriteAllText("D:/unity/between-two-poles/output/ring-docking-path-frames.json",preview.ToString());
    var result=cases+" paths sampled at 121 fixed times each; "+samples+" samples; mesh rectangle SAT checks, no extra rotation in aligned cases, straight direct docking, only misaligned axial halves flip; unchanged halves retain pose; flip-stage bounds separated; continuous floor contact; final mesh seam depth="+maxSeam.ToString("F5")+", maximum extra penetration="+maxExtra.ToString("F5")+"; Failures="+failures.Count;
    foreach(var f in failures)result+="\n"+f;
    UnityEditor.SessionState.SetString("RingPathCollisionRegression",result);return result;
}finally{UnityEngine.Object.DestroyImmediate(root);}


