using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    readonly List<Bounds> mapIslands=new List<Bounds>();
    readonly Vector4[] islandLights=new Vector4[64],portalLights=new Vector4[64];
    Material mapLighting;
    float mapLightingStart;
    float mapLightingExitWeight=1f;
    void BeginMapLighting(GridPlayground board){
        mapIslands.Clear();
        mapLightingExitWeight=1f;
        if(inspectionBoard||testPortals==null)return;
        var rooms=new HashSet<string>();
        foreach(var portal in testPortals){
            if(!portal||!rooms.Add(portal.Room))continue;
            var bounds=new Bounds(portal.transform.position,Vector3.one*board.cellSize);
            foreach(var tile in board.tiles){
                if(!tile)continue;
                var anchor=tile.GetComponentInParent<IslandSurfaceAnchor>();
                if(anchor&&anchor.center&&anchor.center.name==portal.Room)
                    bounds.Encapsulate(new Bounds(tile.transform.position,Vector3.one*board.cellSize));
            }
            mapIslands.Add(bounds);
        }
        mapLightingStart=Time.unscaledTime;
    }
    public static bool MapLightingActive(Camera camera){
        return instance&&Selecting&&!instance.inspectionBoard&&instance.viewCamera==camera&&instance.mapIslands.Count>0;
    }
    public static bool ApplyMapLighting(RenderTexture source,RenderTexture destination,Camera camera){
        if(!MapLightingActive(camera))return false;
        return instance.DrawMapLighting(source,destination,camera);
    }
    bool DrawMapLighting(RenderTexture source,RenderTexture destination,Camera camera){
        if(!mapLighting){
            var shader=Resources.Load<Shader>("Shaders/BlackHoleMapLighting");
            if(!shader||!shader.isSupported)return false;
            mapLighting=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        int count=Mathf.Min(64,mapIslands.Count);
        for(int n=0;n<count;n++){
            var bounds=mapIslands[n];var min=Vector2.one*float.PositiveInfinity;var max=Vector2.one*float.NegativeInfinity;
            for(int corner=0;corner<8;corner++){
                var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                var screen=(Vector2)camera.WorldToViewportPoint(CrashColorReveal.DisplayPoint(p));
                min=Vector2.Min(min,screen);max=Vector2.Max(max,screen);
            }
            var center=(min+max)*.5f;var size=(max-min)*.65f;
            islandLights[n]=new Vector4(center.x,center.y,Mathf.Max(.001f,size.x),Mathf.Max(.001f,size.y));
        }
        int portals=0;
        foreach(var portal in testPortals){
            if(!portal||portals==64)continue;
            var center=camera.WorldToViewportPoint(CrashColorReveal.DisplayPoint(portal.transform.position));
            if(center.z<=0)continue;
            var edge=camera.WorldToViewportPoint(CrashColorReveal.DisplayPoint(portal.transform.position+camera.transform.right*portal.board.cellSize*.65f));
            portalLights[portals++]=new Vector4(center.x,center.y,Mathf.Max(.002f,Mathf.Abs(edge.x-center.x)),0);
        }
        mapLighting.SetVectorArray("_Islands",islandLights);mapLighting.SetInt("_IslandLightCount",count);
        mapLighting.SetVectorArray("_Portals",portalLights);mapLighting.SetInt("_PortalLightCount",portals);
        mapLighting.SetFloat("_Aspect",(float)source.width/source.height);
        mapLighting.SetFloat("_Strength",Mathf.SmoothStep(0,1,(Time.unscaledTime-mapLightingStart)/.8f)*mapLightingExitWeight);
        Graphics.Blit(source,destination,mapLighting);return true;
    }
    void ReleaseMapLighting(){
        mapIslands.Clear();if(!mapLighting)return;
        if(Application.isPlaying)Destroy(mapLighting);else DestroyImmediate(mapLighting);
        mapLighting=null;
    }
}
}
