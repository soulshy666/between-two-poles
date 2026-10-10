using UnityEngine;
using System.Collections.Generic;
namespace BetweenPoles {
 // Authoring helper: island focus follows the occupied ground, never its old placeholder anchor.
 [ExecuteAlways,DefaultExecutionOrder(-150)] public sealed class IslandGroundCenters:MonoBehaviour {
  public CurvedTrialCamera view;
  public Transform editIsland;
  GridTile[] lastTiles;Vector3 lastBoardPosition;
  public void RefreshCenters(){
   if(!view||!view.island||!view.island.board)return;
   lastTiles=view.island.board.tiles;lastBoardPosition=view.island.board.transform.position;
   var min=new Dictionary<Transform,Vector3>();var max=new Dictionary<Transform,Vector3>();
   foreach(var tile in view.island.board.tiles){if(!tile||!tile.gameObject.activeInHierarchy)continue;var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();if(!binding||!binding.center)continue;var owner=binding.center;Vector3 p=tile.transform.position;
    if(!min.ContainsKey(owner)){min[owner]=p;max[owner]=p;}else{min[owner]=Vector3.Min(min[owner],p);max[owner]=Vector3.Max(max[owner],p);}
   }
   foreach(var pair in min){Vector3 center=(pair.Value+max[pair.Key])*.5f;center.y=0;pair.Key.position=center;}
   if(!Application.isPlaying&&editIsland){view.editFocus=editIsland.position;view.transform.position=view.editFocus+view.island.viewingOffset;view.transform.LookAt(view.editFocus);view.Apply(view.editFocus);}
  }
  void OnEnable(){RefreshCenters();}
  void Update(){
   if(!view||!view.island||!view.island.board)return;
   if(!Application.isPlaying||lastTiles!=view.island.board.tiles||lastBoardPosition!=view.island.board.transform.position)RefreshCenters();
  }
 }
}
