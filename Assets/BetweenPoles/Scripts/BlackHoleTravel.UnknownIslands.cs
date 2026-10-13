using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    readonly List<Vector3> unknownIslandMarkers=new List<Vector3>();
    void BeginUnknownIslands(GridPlayground board){
        EndUnknownIslands();
        if(!navigationWindow)return;
        SessionExploration.RecordStandingIsland(board);
        var discovery=navigationWindow.GetComponent<IslandDiscoveryView>();if(discovery)discovery.SetOverview(true);
        foreach(var room in navigationWindow.rooms){
            if(!room.center||VisitedRoom(room.id)||VisitedRoom(room.center.name))continue;
            Vector3 sum=Vector3.zero;int count=0;
            foreach(var tile in board.tiles){
                if(!tile)continue;var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();
                if(owner&&owner.center==room.center){sum+=tile.transform.position;count++;}
            }
            // Choose an actual tile near the center, so concave islands don't label empty space.
            Vector3 marker=count>0?sum/count:room.center.position;float best=float.PositiveInfinity;
            foreach(var tile in board.tiles){
                if(!tile)continue;var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();
                if(!owner||owner.center!=room.center)continue;
                float d=(tile.transform.position-(count>0?sum/count:room.center.position)).sqrMagnitude;
                if(d<best){best=d;marker=tile.transform.position;}
            }
            unknownIslandMarkers.Add(marker);

        }

    }
    void EndUnknownIslands(){unknownIslandMarkers.Clear();if(navigationWindow){var discovery=navigationWindow.GetComponent<IslandDiscoveryView>();if(discovery)discovery.SetOverview(false);}}
    void DrawUnknownIslands(){
        if(!viewCamera||Event.current.type!=EventType.Repaint)return;
        var style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=Mathf.RoundToInt(Mathf.Clamp(Screen.height/24f,24,44))};
        style.normal.textColor=Color.white;
        foreach(var world in unknownIslandMarkers){
            var point=NavigationScreenPoint(world);
            if(point.z<=0||point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)continue;
            GUI.Label(new Rect(point.x-26,Screen.height-point.y-30,52,60),"?",style);
        }
    }
}
}
