using UnityEngine;
namespace BetweenPoles {
// Small runtime meshes use the same bent-world material as the imported islands.
public static class MagnetVisuals {
    const string LinkName="磁流连接",BridgeSurfaceName="通路电屏障";
    static Material red,blue,link,bridgeSurface;
    static Material Color(bool north) {
        var m=north?red:blue;if(m)return m;
        m=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));m.color=north?new Color(.88f,.22f,.29f):new Color(.18f,.55f,.78f);
        m.SetFloat("_Painted",0);m.SetFloat("_Snow",0);m.SetFloat("_Grid",0);
        if(north)red=m;else blue=m;return m;
    }
    static Transform Group(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static void Box(Transform parent,Vector3 p,Vector3 size,Material material,string name){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;Object.Destroy(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;}
    static void Box(Transform parent,Vector3 p,Vector3 size,bool north){Box(parent,p,size,Color(north),north?"N 极实体":"S 极实体");}
    static Material LinkMaterial() {
        if(link)return link;
        link=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));link.color=new Color(1,.78f,.16f);
        link.SetFloat("_Painted",0);link.SetFloat("_Snow",0);link.SetFloat("_Grid",0);return link;
    }
    static Material BridgeSurfaceMaterial() {
        if(bridgeSurface)return bridgeSurface;
        bridgeSurface=new Material(Shader.Find("BetweenPoles/BridgeEnergyGlass"));bridgeSurface.color=new Color(.12f,.58f,1f,.30f);
        return bridgeSurface;
    }
    static void MagneticLink(Transform parent,float z,float begin,float end) {
        var root=Group(parent,LinkName);
        const int waveSegments=12,pulseCount=3;
        for(int i=0;i<waveSegments;i++)Box(root,new Vector3(begin,.16f,z),new Vector3(.1f,.045f,.065f),LinkMaterial(),"波动磁流");
        Box(root,new Vector3(begin,.16f,z),new Vector3(.13f,.08f,.14f),LinkMaterial(),"U 型接口");
        Box(root,new Vector3(end,.16f,z),new Vector3(.13f,.08f,.14f),LinkMaterial(),"长条接口");
        for(int i=0;i<pulseCount;i++)Box(root,new Vector3(begin,.18f,z),new Vector3(.17f,.085f,.12f),LinkMaterial(),"传输脉冲");
        root.gameObject.AddComponent<MagneticLinkPulse>().Configure(begin,end,z);
    }
    static void BridgeSurface(Transform parent,float begin,float end) {
        var root=Group(parent,BridgeSurfaceName);
        // Cover the complete two-cell assembly, including both rails and the U-shaped base.
        Box(root,new Vector3((begin+end)*.5f,.247f,0),new Vector3(end-begin,.012f,1.2f),BridgeSurfaceMaterial(),"蓝色桥面");
    }
    public static bool IsMagneticEffect(Renderer renderer) {
        for(var current=renderer?renderer.transform:null;current;current=current.parent)
            if(current.name==LinkName||current.name==BridgeSurfaceName)return true;
        return false;
    }
    static void Arc(Transform parent,bool north,float begin,float end) {
        for(int i=0;i<20;i++){float a=Mathf.Lerp(begin,end,(i+.5f)/20)*Mathf.Deg2Rad;
            var part=Group(parent,"磁弧");part.localPosition=new Vector3(Mathf.Cos(a)*.48f,.12f,Mathf.Sin(a)*.48f);
            part.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);Box(part,Vector3.zero,new Vector3(.22f,.24f,.09f),north);
        }
    }
    public static Transform Build(Transform owner,MagnetShape shape,bool north,MagnetProduct product,float cell,bool baseNorth,Vector2Int direction) {
        var g=Group(owner,"磁铁实体 · "+product);
        if(product==MagnetProduct.None){if(shape==MagnetShape.Bar)Box(g,new Vector3(0,.12f,0),new Vector3(cell,.24f,.24f),north);else Arc(g,north,180,360);}
        if(product==MagnetProduct.WideBar){Box(g,new Vector3(0,.12f,-.12f),new Vector3(cell,.24f,.24f),north);Box(g,new Vector3(0,.12f,.12f),new Vector3(cell,.24f,.24f),!north);}
        if(product==MagnetProduct.Ring){Arc(g,north,0,180);Arc(g,!north,180,360);}
        if(product==MagnetProduct.Cross){Box(g,new Vector3(0,.12f,0),new Vector3(cell,.24f,.24f),north);Box(g,new Vector3(0,.36f,0),new Vector3(.24f,.24f,cell),!north);}
        if(product==MagnetProduct.Lift){Arc(g,baseNorth,180,360);Box(g,new Vector3(0,cell*.5f,0),new Vector3(.24f,cell,.24f),!baseNorth);}
        if(product==MagnetProduct.Bridge||product==MagnetProduct.BridgeHalf){
            var basePart=Group(g,"U 型桥首");basePart.localPosition=new Vector3(-cell*.5f+.48f,0,0);basePart.localRotation=Quaternion.Euler(0,90,0);Arc(basePart,baseNorth,180,360);
            // Each rail remains one original bar long in the second occupied cell.
            // The short gap to the U sockets is an energy connection, not stretched material.
            float railCenter=cell,railStart=cell*.5f,linkStart=-cell*.5f+.48f;
            Box(g,new Vector3(railCenter,.12f,-.48f),new Vector3(cell,.24f,.22f),!baseNorth);
            if(product==MagnetProduct.Bridge)Box(g,new Vector3(railCenter,.12f,.48f),new Vector3(cell,.24f,.22f),!baseNorth);
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
        m.geometry=Build(root,shape,north,MagnetProduct.None,cell,north,Vector2Int.right);m.geometry.rotation=pose;Ground(m);
        var binding=root.gameObject.AddComponent<IslandSurfaceAnchor>();binding.center=island;binding.Apply();return m;
    }
    public static void Ground(MagnetPiece m) {
        Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
        foreach(var f in m.geometry.GetComponentsInChildren<MeshFilter>(true))foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v)-m.transform.position;min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
        if(!float.IsInfinity(min.y)){var shift=Vector3.up*min.y;if(m.product==MagnetProduct.None)shift+=new Vector3((min.x+max.x)*.5f,0,(min.z+max.z)*.5f);m.geometry.position-=shift;}

    }
    public static void Product(MagnetPiece m,MagnetProduct product,float cell,Quaternion yaw,bool baseNorth,bool receiverOnTop=false) {
        m.geometry.gameObject.SetActive(false);m.geometry=Build(m.transform,m.shape,m.north,product,cell,baseNorth,m.bridgeDirection);
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
