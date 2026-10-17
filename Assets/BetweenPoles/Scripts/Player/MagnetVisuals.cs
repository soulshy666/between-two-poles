using UnityEngine;
namespace BetweenPoles {
// Small runtime meshes use the same bent-world material as the imported islands.
public static class MagnetVisuals {
    // Weak keys avoid retaining meshes discarded by undo or procedural products.
    // Magnet meshes are immutable after creation; transforms still update every sample.
    static System.Runtime.CompilerServices.ConditionalWeakTable<Mesh,Vector3[]> vertexCache=new System.Runtime.CompilerServices.ConditionalWeakTable<Mesh,Vector3[]>();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Mesh,Vector3[]>.CreateValueCallback readVertices=ReadMeshVertices;
    public static Vector3[] CachedVertices(Mesh mesh){
        if(!Application.isPlaying)return mesh.vertices;
        return vertexCache.GetValue(mesh,readVertices);
    }
    static Vector3[] ReadMeshVertices(Mesh mesh){return mesh.vertices;}
    public static void InvalidateVertices(Mesh mesh){if(mesh)vertexCache.Remove(mesh);}
    // Set false before entering Play mode to compare with the original geometry.
    public static bool UseCombinedArcMeshes=true;
    static readonly System.Collections.Generic.Dictionary<string,Mesh> arcMeshes=new System.Collections.Generic.Dictionary<string,Mesh>();
    static readonly System.Collections.Generic.Dictionary<Vector2,Mesh> generatedArcs=new System.Collections.Generic.Dictionary<Vector2,Mesh>();
    static Mesh cubeMesh,cylinderMesh;
    static readonly Unity.Profiling.ProfilerMarker buildMarker=new Unity.Profiling.ProfilerMarker("Magnets.BuildVisuals");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetArcMeshes(){
        vertexCache=new System.Runtime.CompilerServices.ConditionalWeakTable<Mesh,Vector3[]>();
        foreach(var mesh in arcMeshes.Values)if(mesh)Object.DestroyImmediate(mesh);
        arcMeshes.Clear();
        foreach(var mesh in generatedArcs.Values)if(mesh)Object.DestroyImmediate(mesh);
        generatedArcs.Clear();cubeMesh=null;cylinderMesh=null;
    }
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void RegisterArcCleanup(){
        UnityEditor.EditorApplication.playModeStateChanged-=OnArcPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged+=OnArcPlayModeChanged;
    }
    static void OnArcPlayModeChanged(UnityEditor.PlayModeStateChange state){
        if(state==UnityEditor.PlayModeStateChange.EnteredEditMode)ResetArcMeshes();
    }
#endif
    const string LinkName="磁流连接",BridgeSurfaceName="通路电屏障",RingSurfaceName="圆环电屏障";
    static Material red,blue,link,bridgeSurface,bridgeTrim;
    static Material Color(bool north) {
        var m=north?red:blue;if(m)return m;
        m=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));m.color=north?new Color(.88f,.22f,.29f):new Color(.18f,.55f,.78f);
        m.SetFloat("_Painted",0);m.SetFloat("_Snow",0);m.SetFloat("_Grid",0);
        if(north)red=m;else blue=m;return m;
    }
    static Transform Group(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static Mesh PrimitiveMesh(PrimitiveType type){
        var mesh=type==PrimitiveType.Cube?cubeMesh:cylinderMesh;if(mesh)return mesh;
        // Obtain Unity's exact built-in mesh once, without keeping a physics object.
        var sample=GameObject.CreatePrimitive(type);sample.SetActive(false);
        mesh=sample.GetComponent<MeshFilter>().sharedMesh;
        if(Application.isPlaying)Object.Destroy(sample);else Object.DestroyImmediate(sample);
        if(type==PrimitiveType.Cube)cubeMesh=mesh;else cylinderMesh=mesh;
        return mesh;
    }
    static Transform MeshPart(Transform parent,string name,Mesh mesh,Material material){
        var part=Group(parent,name);part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        part.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return part;
    }
    static void Box(Transform parent,Vector3 p,Vector3 size,Material material,string name){var part=MeshPart(parent,name,PrimitiveMesh(PrimitiveType.Cube),material);part.localPosition=p;part.localScale=size;}
    static void Box(Transform parent,Vector3 p,Vector3 size,bool north){Box(parent,p,size,Color(north),north?"N 极实体":"S 极实体");}
    static Material LinkMaterial() {
        if(link)return link;
        link=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));link.color=new Color(1,.78f,.16f);
        link.SetFloat("_Painted",0);link.SetFloat("_Snow",0);link.SetFloat("_Grid",0);return link;
    }
    static Material BridgeSurfaceMaterial() {
        if(bridgeSurface)return bridgeSurface;
        bridgeSurface=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));
        bridgeSurface.color=new Color(.48f,.73f,.78f,1);
        bridgeSurface.SetFloat("_Painted",0);bridgeSurface.SetFloat("_Snow",0);bridgeSurface.SetFloat("_Grid",0);
        return bridgeSurface;
    }
    static Material BridgeTrimMaterial(){
        if(bridgeTrim)return bridgeTrim;
        bridgeTrim=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));
        bridgeTrim.color=new Color(.76f,.95f,.96f,1);
        bridgeTrim.SetFloat("_Painted",0);bridgeTrim.SetFloat("_Snow",0);bridgeTrim.SetFloat("_Grid",0);
        return bridgeTrim;
    }
    public static Transform MagneticLink(Transform parent,float z,float begin,float end) {
        var root=Group(parent,LinkName);
        const int waveSegments=12,pulseCount=3;
        for(int i=0;i<waveSegments;i++)Box(root,new Vector3(begin,.16f,z),new Vector3(.1f,.045f,.065f),LinkMaterial(),"波动磁流");
        Box(root,new Vector3(begin,.16f,z),new Vector3(.13f,.08f,.14f),LinkMaterial(),"U 型接口");
        Box(root,new Vector3(end,.16f,z),new Vector3(.13f,.08f,.14f),LinkMaterial(),"长条接口");
        for(int i=0;i<pulseCount;i++)Box(root,new Vector3(begin,.18f,z),new Vector3(.17f,.085f,.12f),LinkMaterial(),"传输脉冲");
        root.gameObject.AddComponent<MagneticLinkPulse>().Configure(begin,end,z);
        return root;
    }
    static void BridgeSurface(Transform parent,float begin,float end) {
        var root=Group(parent,BridgeSurfaceName);
        // A solid continuous walkway reads at the low render resolution. Keep
        // its top at the gameplay deck height and leave the coloured rails exposed.
        // Match the U's hollow with a rounded end. A rectangular panel's rear
        // corners (including its trim) otherwise poke through the curved shell.
        float arcCenter=begin+.48f,radius=.35f;
        RoundedBridgeDeck(root,arcCenter,end,radius);
        float length=end-arcCenter,center=(arcCenter+end)*.5f;
        foreach(float side in new[]{-1f,1f})
            Box(root,new Vector3(center,.23f,side*(radius-.025f)),new Vector3(length,.02f,.04f),BridgeTrimMaterial(),"通路边线");
        Box(root,new Vector3(end-.045f,.23f,0),new Vector3(.09f,.02f,radius*2),BridgeTrimMaterial(),"桥头踏边");
    }
    static void RoundedBridgeDeck(Transform parent,float arcCenter,float end,float radius){
        const int segments=20;
        var outline=new Vector3[segments+3];
        outline[0]=new Vector3(end,0,-radius);outline[1]=new Vector3(end,0,radius);
        for(int i=0;i<=segments;i++){
            float angle=Mathf.PI*(.5f+(float)i/segments);
            outline[i+2]=new Vector3(arcCenter+radius*Mathf.Cos(angle),0,radius*Mathf.Sin(angle));
        }
        int count=outline.Length;
        var vertices=new Vector3[count*6];var triangles=new int[(count-2)*6+count*6];int t=0;
        for(int i=0;i<count;i++){
            vertices[i]=outline[i]+Vector3.up*.24f;
            vertices[count+i]=outline[i]+Vector3.up*.15f;
            int next=(i+1)%count,s=count*2+i*4;
            vertices[s]=outline[i]+Vector3.up*.15f;vertices[s+1]=outline[next]+Vector3.up*.15f;
            vertices[s+2]=outline[next]+Vector3.up*.24f;vertices[s+3]=outline[i]+Vector3.up*.24f;
            triangles[t++]=s;triangles[t++]=s+2;triangles[t++]=s+1;
            triangles[t++]=s;triangles[t++]=s+3;triangles[t++]=s+2;
        }
        for(int i=1;i<count-1;i++){
            triangles[t++]=0;triangles[t++]=i+1;triangles[t++]=i;
            triangles[t++]=count;triangles[t++]=count+i;triangles[t++]=count+i+1;
        }
        var mesh=new Mesh{name="圆头连续桥面"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        var deck=Group(parent,"连续桥面");deck.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        deck.gameObject.AddComponent<RuntimeMagnetMesh>().ownedMesh=mesh;
        deck.gameObject.AddComponent<MeshRenderer>().sharedMaterial=BridgeSurfaceMaterial();
    }
    static void RingSurface(Transform parent){
        var root=Group(parent,RingSurfaceName);
        // Fill the full shell thickness (0..24 cm), so both faces stay flush
        // after flipping. The pale rim is inset 2 mm on each face to avoid
        // coplanar caps fighting with the blue centre.
        RingPanel(root,"双面白蓝圆形踏边",.365f,.12f,.236f,BridgeTrimMaterial());
        RingPanel(root,"双面蓝色圆形板",.34f,.12f,.24f,BridgeSurfaceMaterial());
    }
    static void RingPanel(Transform parent,string name,float radius,float height,float thickness,Material material){
        var panel=MeshPart(parent,name,PrimitiveMesh(PrimitiveType.Cylinder),material);
        panel.localPosition=Vector3.up*height;
        // Unity's cylinder has radius .5 and height 2.
        panel.localScale=new Vector3(radius*2,thickness*.5f,radius*2);
    }
    public static bool IsMagneticEffect(Renderer renderer) {
        for(var current=renderer?renderer.transform:null;current;current=current.parent)
            if(current.name=="磁极微粒"||current.name==LinkName||current.name==BridgeSurfaceName||current.name==RingSurfaceName)return true;
        return false;
    }
    static void Arc(Transform parent,bool north,float begin,float end) {
        if(Application.isPlaying&&UseCombinedArcMeshes){
            var key=new Vector2(begin,end);
            if(!generatedArcs.TryGetValue(key,out var mesh)){
                var cube=PrimitiveMesh(PrimitiveType.Cube);var parts=new CombineInstance[20];
                for(int i=0;i<20;i++){
                    float a=Mathf.Lerp(begin,end,(i+.5f)/20)*Mathf.Deg2Rad;
                    // Same transform and source vertices as the original twenty boxes.
                    parts[i]=new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(
                        new Vector3(Mathf.Cos(a)*.48f,.12f,Mathf.Sin(a)*.48f),Quaternion.Euler(0,-a*Mathf.Rad2Deg,0),new Vector3(.22f,.24f,.09f))};
                }
                mesh=new Mesh{name="U 型磁弧 · 原样合并",hideFlags=HideFlags.DontSave};
                mesh.CombineMeshes(parts,true,true);generatedArcs.Add(key,mesh);
            }
            MeshPart(parent,"U 型磁弧 · 合并",mesh,Color(north));return;
        }
        for(int i=0;i<20;i++){float a=Mathf.Lerp(begin,end,(i+.5f)/20)*Mathf.Deg2Rad;
            var part=Group(parent,"磁弧");part.localPosition=new Vector3(Mathf.Cos(a)*.48f,.12f,Mathf.Sin(a)*.48f);
            part.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);Box(part,Vector3.zero,new Vector3(.22f,.24f,.09f),north);
        }
    }
    // Convert only the authored arc segments, never rails, panels or animated effects.
    // This runs at load/build time; scene assets remain unchanged for easy rollback.
    public static void CombineExistingArcs(Transform root) {
        if(!root||!UseCombinedArcMeshes)return;
        for(int i=0;i<root.childCount;i++)CombineExistingArcs(root.GetChild(i));
        for(int start=0;start+20<=root.childCount;start++){
            var sources=new MeshRenderer[20];var parts=new Transform[20];
            var combines=new CombineInstance[20];bool valid=true;
            var key=new System.Text.StringBuilder();
            for(int j=0;j<20;j++){
                var part=root.GetChild(start+j);parts[j]=part;
                if(part.name!="磁弧"||part.childCount!=1){valid=false;break;}
                var child=part.GetChild(0);var filter=child.GetComponent<MeshFilter>();var renderer=child.GetComponent<MeshRenderer>();
                if(!filter||!filter.sharedMesh||!renderer||renderer.sharedMaterials.Length!=1||
                    (j>0&&renderer.sharedMaterial!=sources[0].sharedMaterial)){valid=false;break;}
                sources[j]=renderer;
                // Use local matrices: independent of the magnet's pose, including zero-scale portal previews.
                var matrix=Matrix4x4.TRS(part.localPosition,part.localRotation,part.localScale)*Matrix4x4.TRS(child.localPosition,child.localRotation,child.localScale);
                combines[j]=new CombineInstance{mesh=filter.sharedMesh,transform=matrix};
                key.Append(filter.sharedMesh.GetInstanceID()).Append(':');
                for(int k=0;k<16;k++)key.Append(matrix[k].ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            }
            if(!valid)continue;
            string meshKey=key.ToString();
            if(!arcMeshes.TryGetValue(meshKey,out var mesh)){
                mesh=new Mesh{name="U 型磁弧 · 原样合并",hideFlags=HideFlags.DontSave};
                // Keep all original faces, UVs and hard normals; do not weld or smooth.
                mesh.CombineMeshes(combines,true,true);arcMeshes.Add(meshKey,mesh);
            }
            var combined=Group(root,"U 型磁弧 · 合并");combined.SetSiblingIndex(start);
            combined.gameObject.layer=sources[0].gameObject.layer;
            combined.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var target=combined.gameObject.AddComponent<MeshRenderer>();var source=sources[0];
            target.sharedMaterial=source.sharedMaterial;target.enabled=source.enabled;
            target.shadowCastingMode=source.shadowCastingMode;target.receiveShadows=source.receiveShadows;
            target.lightProbeUsage=source.lightProbeUsage;target.reflectionProbeUsage=source.reflectionProbeUsage;
            target.probeAnchor=source.probeAnchor;target.allowOcclusionWhenDynamic=source.allowOcclusionWhenDynamic;
            var properties=new MaterialPropertyBlock();source.GetPropertyBlock(properties);target.SetPropertyBlock(properties);
            foreach(var part in parts){
                // Detach immediately: hierarchy scans must not re-enable the deferred-destroyed renderers.
                part.gameObject.SetActive(false);part.SetParent(null,false);
                if(Application.isPlaying)Object.Destroy(part.gameObject);else Object.DestroyImmediate(part.gameObject);
            }
        }
    }
    public static Transform Build(Transform owner,MagnetShape shape,bool north,MagnetProduct product,float cell,bool baseNorth,Vector2Int direction,bool? secondRailNorth=null,float upperUYaw=90) {
        using(var sample=buildMarker.Auto())return BuildCore(owner,shape,north,product,cell,baseNorth,direction,secondRailNorth,upperUYaw);
    }
    static Transform BuildCore(Transform owner,MagnetShape shape,bool north,MagnetProduct product,float cell,bool baseNorth,Vector2Int direction,bool? secondRailNorth,float upperUYaw) {
        var g=Group(owner,"磁铁实体 · "+product);
        if(product==MagnetProduct.None){if(shape==MagnetShape.Bar)Box(g,new Vector3(0,.12f,0),new Vector3(cell,.24f,.24f),north);else Arc(g,north,180,360);}
        if(product==MagnetProduct.WideBar){Box(g,new Vector3(0,.12f,-.12f),new Vector3(cell,.24f,.24f),north);Box(g,new Vector3(0,.12f,.12f),new Vector3(cell,.24f,.24f),!north);}
        // The receiver retains its U opening; the incoming U closes that opening.
        if(product==MagnetProduct.Ring){Arc(g,north,180,360);Arc(g,!north,0,180);RingSurface(g);}
        if(product==MagnetProduct.ThickU||product==MagnetProduct.CrossU){
            var bottom=Group(g,"接收 U · 下层");Arc(bottom,north,180,360);
            var top=Group(g,"推入 U · 上层");top.localPosition=Vector3.up*.24f;Arc(top,!north,180,360);
            if(product==MagnetProduct.CrossU)top.localRotation=Quaternion.Euler(0,upperUYaw,0);
        }
        if(product==MagnetProduct.Cross){Box(g,new Vector3(0,.12f,0),new Vector3(cell,.24f,.24f),north);Box(g,new Vector3(0,.36f,0),new Vector3(.24f,.24f,cell),!north);}
        if(product==MagnetProduct.Lift){Arc(g,baseNorth,180,360);Box(g,new Vector3(0,cell*.5f,0),new Vector3(.24f,cell,.24f),!baseNorth);}
        if(product==MagnetProduct.Bridge||product==MagnetProduct.BridgeHalf){
            var basePart=Group(g,"U 型桥首");basePart.localPosition=new Vector3(-cell*.5f+.48f,0,0);basePart.localRotation=Quaternion.Euler(0,90,0);Arc(basePart,baseNorth,180,360);
            // Each rail remains one original bar long in the second occupied cell.
            // The short gap to the U sockets is an energy connection, not stretched material.
            float railCenter=cell,railStart=cell*.5f,linkStart=-cell*.5f+.48f;
            Box(g,new Vector3(railCenter,.12f,-.48f),new Vector3(cell,.24f,.22f),!baseNorth);
            if(product==MagnetProduct.Bridge)Box(g,new Vector3(railCenter,.12f,.48f),new Vector3(cell,.24f,.22f),secondRailNorth??!baseNorth);
            MagneticLink(g,-.48f,linkStart,railStart);
            if(product==MagnetProduct.Bridge){
                MagneticLink(g,.48f,linkStart,railStart);
                BridgeSurface(g,-cell*.5f,cell*1.5f);
            }
        }
        return g;
    }
    public static MagnetPiece Create(Transform parent,Vector3 position,MagnetShape shape,bool north,Quaternion pose,float cell,Transform island) {
        var root=Group(parent,"调试磁铁 "+(north?"N":"S")+" "+shape);root.position=position;
        var m=root.gameObject.AddComponent<MagnetPiece>();m.shape=shape;m.north=north;m.baseNorth=north;
        m.geometry=Build(root,shape,north,MagnetProduct.None,cell,north,Vector2Int.right);m.geometry.rotation=shape==MagnetShape.Horseshoe?MagnetPiece.FlatUPose(pose):pose;Ground(m);
        var binding=root.gameObject.AddComponent<IslandSurfaceAnchor>();binding.center=island;binding.Apply();return m;
    }
    public static void Ground(MagnetPiece m) {
        Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
        var origin=m.transform.position;
        foreach(var f in m.geometry.GetComponentsInChildren<MeshFilter>(true)){
            var matrix=f.transform.localToWorldMatrix;
            foreach(var v in CachedVertices(f.sharedMesh)){var p=matrix.MultiplyPoint3x4(v)-origin;min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
        }
        if(!float.IsInfinity(min.y)){var shift=Vector3.up*min.y;if(m.product==MagnetProduct.None||m.product==MagnetProduct.Ring)shift+=new Vector3((min.x+max.x)*.5f,0,(min.z+max.z)*.5f);m.geometry.position-=shift;}

    }
    public static void Product(MagnetPiece m,MagnetProduct product,float cell,Quaternion yaw,bool baseNorth,bool receiverOnTop=false,bool? secondRailNorth=null,float upperUYaw=90) {
        m.geometry.gameObject.SetActive(false);m.geometry=Build(m.transform,m.shape,m.north,product,cell,baseNorth,m.bridgeDirection,secondRailNorth,upperUYaw);
        if(product==MagnetProduct.Cross&&receiverOnTop){
            // The upright receiver falls onto the incoming flat bar, which stays below.
            m.geometry.GetChild(0).localPosition=new Vector3(0,.36f,0);
            m.geometry.GetChild(1).localPosition=new Vector3(0,.12f,0);
        }
        m.product=product;m.combined=true;m.baseNorth=baseNorth;m.walkable=product==MagnetProduct.WideBar||product==MagnetProduct.Ring||product==MagnetProduct.Bridge;
        m.transform.rotation=yaw;m.geometry.localRotation=Quaternion.identity;
        var anchor=m.GetComponent<IslandSurfaceAnchor>();if(anchor){anchor.enabled=false;anchor.enabled=true;}
    }
}
}
