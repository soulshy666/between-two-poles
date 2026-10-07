// Isolated deterministic sampling of the real repulsion coroutine.
var root=new GameObject("Repulsion timing regression");root.SetActive(false);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
int cases=0;
try{
 for(int variant=0;variant<4;variant++)for(int directionIndex=0;directionIndex<4;directionIndex++){
  var dir=new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}[directionIndex];
  var forward=new Vector3(dir.x,0,dir.y);
  var host=new GameObject("Repulsion case");host.transform.SetParent(root.transform,false);
  var board=host.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
  var player=new GameObject("Player");player.transform.SetParent(host.transform,false);board.player=player.transform;
  var visual=new GameObject("Visual");visual.transform.SetParent(player.transform,false);
  var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
  for(int i=0;i<4;i++){var tile=new GameObject("Tile");tile.transform.SetParent(host.transform,false);tile.transform.position=forward*i*board.cellSize;tiles.Add(tile.AddComponent<BetweenPoles.GridTile>());}
  board.tiles=tiles.ToArray();
  var magnets=new BetweenPoles.MagnetPiece[2];
  for(int i=0;i<2;i++){
   var go=new GameObject("Magnet");go.transform.SetParent(host.transform,false);go.transform.position=forward*(i+1)*board.cellSize;
   var magnet=go.AddComponent<BetweenPoles.MagnetPiece>();magnet.north=directionIndex%2==0;
   magnet.shape=((variant>>i)&1)==0?BetweenPoles.MagnetShape.Bar:BetweenPoles.MagnetShape.Horseshoe;
   magnet.geometry=BetweenPoles.MagnetVisuals.Build(go.transform,magnet.shape,magnet.north,BetweenPoles.MagnetProduct.None,board.cellSize,magnet.north,dir);
   magnets[i]=magnet;
  }
  board.magnets=magnets;
  var nearPath=BetweenPoles.MagnetPiece.RollPath(magnets[0].shape,magnets[0].Pose,dir);
  var farPath=BetweenPoles.MagnetPiece.RollPath(magnets[1].shape,magnets[1].Pose,dir);
  board.GetType().GetField("recordingPush",flags).SetValue(board,true);
  board.GetType().GetField("<Busy>k__BackingField",flags).SetValue(board,true);
  var motion=(System.Collections.IEnumerator)board.GetType().GetMethod("Repel",flags).Invoke(board,new object[]{magnets[0],magnets[1],nearPath,farPath,dir});
  int frame=0,nearFirst=-1,farFirst=-1;bool pushSeen=false;float nearAtFarStart=0;
  while(motion.MoveNext()){
   float near=Vector3.Dot(magnets[0].transform.position,forward)/board.cellSize-1;
   float far=Vector3.Dot(magnets[1].transform.position,forward)/board.cellSize-2;
   float playerProgress=Vector3.Dot(player.transform.position,forward)/board.cellSize;
   if(near>.0001f&&nearFirst<0)nearFirst=frame;
   if(far>.0001f&&farFirst<0){farFirst=frame;nearAtFarStart=near;}
   if(Mathf.Abs(playerProgress-near)>.0001f)throw new System.Exception("Player desynchronized");
   if(near-far>.35f)throw new System.Exception("Excessive gap compression");
   pushSeen|=visual.transform.localEulerAngles.x>5;
   if(++frame>150)throw new System.Exception("Timeout");
  }
  if(nearFirst<0||farFirst<=nearFirst||nearAtFarStart<.1f||nearAtFarStart>.35f)throw new System.Exception("Repulsion order/approach distance incorrect");
  if(!pushSeen||board.Busy)throw new System.Exception("Push pose/busy state incorrect");
  if((player.transform.position-forward*board.cellSize).sqrMagnitude>.00001f)throw new System.Exception("Wrong player landing");
  for(int i=0;i<2;i++){
   if((magnets[i].transform.position-forward*(i+2)*board.cellSize).sqrMagnitude>.00001f)throw new System.Exception("Wrong magnet landing");
   if(Quaternion.Angle(magnets[i].Pose,(i==0?nearPath:farPath)[0].pose)>.01f)throw new System.Exception("Wrong final roll pose");
   if(magnets[i].combined)throw new System.Exception("Repulsion changed recipe state");
  }
  cases++;
 }
 return "PASS: "+cases+" cases, four directions, both poles, Bar/U combinations; near-first delay, concurrent player push, bounded gap compression, exact final cells and roll poses.";
}finally{UnityEngine.Object.DestroyImmediate(root);}
