using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class PlayerPushPose {
    bool readingMap;
    bool[] mapHead;
    float mapProgress;
    Transform mapTablet;
    Material mapCaseMaterial,mapScreenMaterial;
    static readonly Vector3 MapShoulder=new Vector3(.235f,.635f,0);
    static readonly Vector3 MapHand=new Vector3(.265f,.365f,0);
    static readonly Vector3 MapGrip=new Vector3(.13f,-.04f,0);
    static readonly Vector3 MapDockGrip=new Vector3(.13f,.04f,0);
    static readonly Quaternion MapDockRotation=Quaternion.Euler(0,0,90)*Quaternion.Euler(180,0,0);
    static Quaternion MapArm(Vector3 direction){return Quaternion.FromToRotation(MapHand-MapShoulder,direction);}
    static readonly Quaternion MapDockArm=MapArm(MapDockPosition+MapDockRotation*MapDockGrip-MapShoulder);
    static Vector3 MapPalm(Quaternion arm,float lengthScale=1){return MapShoulder+arm*((MapHand-MapShoulder)*lengthScale);}
    public static Vector3 MapDockPosition {get{return new Vector3(.19f,.52f,-.244f);}}
    public bool ReadingMap {get{return readingMap;}}
    public static PlayerPushPose BeginMap(Transform player){
        var pose=GetPose(player);pose.End();pose.readingMap=true;pose.mapProgress=0;pose.BuildMapTablet();pose.SampleMap(0);return pose;
    }
    public IEnumerator AnimateMap(bool opening){
        float start=mapProgress,end=opening?1:0;
        const float seconds=1.5f;
        for(float time=0;time<seconds;time+=Time.unscaledDeltaTime){
            mapProgress=Mathf.Lerp(start,end,time/seconds);SampleMap(mapProgress);yield return null;
        }
        mapProgress=end;SampleMap(end);
        if(!opening){readingMap=false;End();}
    }
    void BuildMapTablet(){
        if(mapTablet||!body)return;
        mapTablet=body.transform.Find("电子地图板");
        if(!mapTablet){
            var source=body.GetComponent<Renderer>();
            mapCaseMaterial=new Material(source.sharedMaterial){color=new Color(.045f,.075f,.11f)};
            mapScreenMaterial=new Material(source.sharedMaterial){color=new Color(.12f,.85f,1f)};
            mapTablet=CreateMountedMapTablet(body.transform,mapCaseMaterial,mapScreenMaterial);
        }
        var properties=new MaterialPropertyBlock();body.GetComponent<Renderer>().GetPropertyBlock(properties);
        foreach(var renderer in mapTablet.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(properties);
        DockMapTablet();
    }
    public static Transform CreateMountedMapTablet(Transform parent,Material casing,Material screen){
        var tablet=new GameObject("电子地图板").transform;tablet.SetParent(parent,false);
        TabletPart(tablet,"外壳",Vector3.zero,new Vector3(.26f,.025f,.18f),casing);
        TabletPart(tablet,"电子屏",new Vector3(0,.016f,0),new Vector3(.22f,.006f,.14f),screen);
        TabletPart(tablet,"背面状态灯",new Vector3(0,-.016f,.055f),new Vector3(.15f,.006f,.012f),screen);
        for(int i=0;i<3;i++)TabletPart(tablet,"地图网格",new Vector3((i-1)*.055f,.021f,0),new Vector3(.004f,.003f,.13f),casing);
        for(int i=0;i<3;i++)TabletPart(tablet,"地图网格",new Vector3(0,.021f,(i-1)*.04f),new Vector3(.21f,.003f,.004f),casing);
        tablet.localPosition=MapDockPosition;tablet.localRotation=MapDockRotation;
        return tablet;
    }
    static void TabletPart(Transform tablet,string name,Vector3 position,Vector3 scale,Material material){
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(tablet,false);
        part.transform.localPosition=position;part.transform.localScale=scale;
        if(Application.isPlaying)Destroy(part.GetComponent<Collider>());else DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial=material;
    }
    void DockMapTablet(){if(mapTablet){mapTablet.gameObject.SetActive(true);mapTablet.localPosition=MapDockPosition;mapTablet.localRotation=MapDockRotation;}}
    void SampleMap(float progress){
        if(!visual)return;
        float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.3f,progress));
        float bring=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.88f,progress));
        float look=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,progress));
        // Both reaches pass outside the torso. The same grip point drives the hand and prop.
        var outside=MapArm(new Vector3(.28f,-.07f,0));
        var hold=MapArm(new Vector3(-.075f,-.08f,.27f));
        var pickup=reach<.5f?Quaternion.Slerp(Quaternion.identity,outside,reach*2):Quaternion.Slerp(outside,MapDockArm,(reach-.5f)*2);
        var carry=bring<.5f?Quaternion.Slerp(MapDockArm,outside,bring*2):Quaternion.Slerp(outside,hold,(bring-.5f)*2);
        var right=progress<=.3f?pickup:carry;
        float dockReach=(MapDockPosition+MapDockRotation*MapDockGrip-MapShoulder).magnitude/(MapHand-MapShoulder).magnitude;
        float holdBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1,bring));
        const float holdLength=1.22f;
        float rightLength=Mathf.Lerp(1,dockReach,reach*(1-bring))+(holdLength-1)*holdBlend;
        // Keep the board vertical until clear of the backpack, then turn the screen up.
        var tabletRotation=Quaternion.Slerp(MapDockRotation,Quaternion.Euler(-12,0,0),holdBlend);
        var grip=Vector3.Lerp(MapDockGrip,MapGrip,holdBlend);
        visual.localPosition=restPosition;visual.localRotation=restRotation;
        if(mapTablet){
            mapTablet.gameObject.SetActive(true);
            mapTablet.localPosition=bring<=0?MapDockPosition:MapPalm(right,rightLength)-tabletRotation*grip;
            mapTablet.localRotation=tabletRotation;
        }
        if(!posed)return;
        var leftPivot=new Vector3(-MapShoulder.x,MapShoulder.y,0);
        var leftRest=new Vector3(-MapHand.x,MapHand.y,0);
        var leftTarget=MapPalm(hold,holdLength)-Quaternion.Euler(-12,0,0)*new Vector3(MapGrip.x*2,0,0);
        var leftHold=Quaternion.FromToRotation(leftRest-leftPivot,leftTarget-leftPivot);
        float leftBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,bring));
        var left=Quaternion.Slerp(Quaternion.identity,leftHold,leftBlend);
        float leftLength=Mathf.Lerp(1,(leftTarget-leftPivot).magnitude/(leftRest-leftPivot).magnitude,leftBlend);
        for(int i=0;i<vertices.Length;i++){
            var v=vertices[i];var n=i<normals.Length?normals[i]:Vector3.up;
            if(arms[i]){
                bool isRight=v.x>0;var pivot=isRight?MapShoulder:leftPivot;var turn=isRight?right:left;
                var offset=v-pivot;var axis=((isRight?MapHand:leftRest)-pivot).normalized;
                offset+=axis*Vector3.Dot(offset,axis)*((isRight?rightLength:leftLength)-1);
                v=pivot+turn*offset;n=turn*n;
            }else if(mapHead[i]){var turn=Quaternion.Euler(14*look,0,0);var neck=new Vector3(0,.74f,0);v=neck+turn*(v-neck);n=turn*n;}
            workVertices[i]=v;if(i<normals.Length)workNormals[i]=n;
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
    void DestroyMapMaterials(){if(mapCaseMaterial)Destroy(mapCaseMaterial);if(mapScreenMaterial)Destroy(mapScreenMaterial);}
}
}
