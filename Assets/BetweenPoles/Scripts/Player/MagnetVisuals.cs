using UnityEngine;
namespace BetweenPoles {
// Small runtime meshes use the same bent-world material as the imported islands.
public static class MagnetVisuals {
    static Material red,blue;
    static Material Color(bool north) {
        var m=north?red:blue;if(m)return m;
        m=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));m.color=north?new Color(.92f,.23f,.32f):new Color(.18f,.48f,.73f);
        if(north)red=m;else blue=m;return m;
    }
    static Transform Group(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static void Box(Transform parent,Vector3 p,Vector3 size,bool north){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=north?"N 极实体":"S 极实体";g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;Object.Destroy(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=Color(north);}
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
            float start=-cell*.5f+.48f,end=cell*1.5f;
            Box(g,new Vector3((start+end)*.5f,.12f,-.48f),new Vector3(end-start,.24f,.22f),!baseNorth);
            if(product==MagnetProduct.Bridge)Box(g,new Vector3((start+end)*.5f,.12f,.48f),new Vector3(end-start,.24f,.22f),!baseNorth);
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
