if(!Application.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Bridge transport regression temporary");root.hideFlags=HideFlags.DontSave;
var boards=new System.Collections.Generic.List<BetweenPoles.GridPlayground>();
var movers=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var decks=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
var homes=new System.Collections.Generic.List<Vector2Int>();var dirs=new System.Collections.Generic.List<Vector2Int>();
var lengths=new System.Collections.Generic.List<int>();var heights=new System.Collections.Generic.List<float>();var kinds=new System.Collections.Generic.List<int>();
var failure=new System.Collections.Generic.HashSet<string>();
System.Action<bool,string> check=(ok,label)=>{if(!ok)failure.Add(label);};
System.Func<Vector2Int,float,Vector3> pos=(c,y)=>new Vector3(c.x*1.5f,y,c.y*1.5f);
System.Func<Transform,Vector2Int> cell=t=>new Vector2Int(Mathf.RoundToInt(t.position.x/1.5f),Mathf.RoundToInt(t.position.z/1.5f));
System.Func<Transform,Vector2Int,float,BetweenPoles.MagnetShape,BetweenPoles.MagnetPiece> add=(parent,c,h,shape)=>{
 var go=new GameObject("Magnet");go.transform.SetParent(parent,false);go.transform.position=pos(c,h);
 var m=go.AddComponent<BetweenPoles.MagnetPiece>();m.shape=shape;m.geometry=BetweenPoles.MagnetVisuals.Build(go.transform,shape,true,BetweenPoles.MagnetProduct.None,1.5f,true,Vector2Int.right);BetweenPoles.MagnetVisuals.Ground(m);return m;
};
Vector2Int[] directions={Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
foreach(var d in directions)for(int type=0;type<3;type++)for(int kind=0;kind<4;kind++)for(int order=0;order<2;order++){
 int id=boards.Count;var home=new Vector2Int(200+id*10,500);float h=order*1.5f;int length=type==1?2:1;
 var go=new GameObject("Transport "+id);go.SetActive(false);go.transform.SetParent(root.transform,false);var b=go.AddComponent<BetweenPoles.GridPlayground>();b.enabled=false;
 b.stepSeconds=.01f;b.barPushSeconds=.03f;b.uRollSeconds=.02f;b.walkSeconds=.01f;
 var player=new GameObject("Player");player.transform.SetParent(go.transform,false);player.transform.position=pos(home-d,h);b.player=player.transform;
 var ts=new System.Collections.Generic.List<BetweenPoles.GridTile>();
 foreach(int offset in new[]{-1,0,length+1,length+2}){var tgo=new GameObject("Floor");tgo.transform.SetParent(go.transform,false);tgo.transform.position=pos(home+d*offset,h);var t=tgo.AddComponent<BetweenPoles.GridTile>();t.surfaceHeight=h;ts.Add(t);}
 b.tiles=ts.ToArray();
 var m=add(go.transform,home,h,kind==0?BetweenPoles.MagnetShape.Bar:BetweenPoles.MagnetShape.Horseshoe);
 if(kind>=2)BetweenPoles.MagnetVisuals.Product(m,BetweenPoles.MagnetProduct.Ring,1.5f,Quaternion.identity,true);
 if(kind==3){m.geometry.rotation=Quaternion.FromToRotation(Vector3.up,new Vector3(-d.y,0,d.x));m.walkable=false;BetweenPoles.MagnetVisuals.Ground(m);}
 var deck=add(go.transform,home+d,h,BetweenPoles.MagnetShape.Bar);deck.bridgeDirection=d;
 BetweenPoles.MagnetVisuals.Product(deck,type==1?BetweenPoles.MagnetProduct.Bridge:BetweenPoles.MagnetProduct.WideBar,1.5f,Quaternion.FromToRotation(Vector3.right,new Vector3(d.x,0,d.y)),true);
 if(type==2){deck.product=BetweenPoles.MagnetProduct.None;var bridge=go.AddComponent<BetweenPoles.PrejoinedTestBridges>();bridge.enabled=false;bridge.board=b;bridge.links=new[]{new BetweenPoles.PrejoinedTestBridges.Link{magnet=deck,shoreA=ts[1],shoreB=ts[2],renderers=new Renderer[0]}};}
 b.magnets=order==0?new[]{deck,m}:new[]{m,deck};go.SetActive(true);
 boards.Add(b);movers.Add(m);decks.Add(deck);homes.Add(home);dirs.Add(d);lengths.Add(length);heights.Add(h);kinds.Add(kind);
 check(b.TryStep(d),id+" initial push: "+b.LastRule);
}
int phase=1,frames=0;double started=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
 try{
 frames++;bool busy=false;foreach(var b in boards)busy|=b.Busy;if(UnityEditor.EditorApplication.timeSinceStartup-started>60)throw new System.Exception("Timeout");if(busy)return;
 for(int i=0;i<boards.Count;i++){
  var b=boards[i];var m=movers[i];var deck=decks[i];var home=homes[i];var d=dirs[i];int length=lengths[i];float h=heights[i];bool rolling=kinds[i]==3;
  int steps=rolling?length+3:System.Math.Min(phase,length+2);
  check(cell(m.transform)==home+d*steps,i+" wrong magnet cell at phase "+phase);
  float expected=steps<=length?h+(deck.product==BetweenPoles.MagnetProduct.None?0:.24f):h;
  check(Mathf.Abs(m.transform.position.y-expected)<.005f,i+" magnet height phase "+phase);
  check(cell(deck.transform)==home+d&&Mathf.Abs(deck.transform.position.y-h)<.005f&&deck.walkable,i+" deck moved/consumed");
  if(!rolling){
   int playerSteps=steps-1;check(b.PlayerCell==home+d*playerSteps,i+" player cell phase "+phase);
   float py=playerSteps>0&&playerSteps<=length?h+(deck.product==BetweenPoles.MagnetProduct.None?0:.24f):h;
   check(Mathf.Abs(b.player.position.y-py)<.005f,i+" player height phase "+phase);
  }
  if(phase==1&&!rolling){check(b.MagnetAt(home+d)==m,i+" bridge hid material");}
  if(phase<length+2&&!rolling)check(b.TryStep(d),i+" continuing phase "+phase+": "+b.LastRule);
 }
 if(phase<4){phase++;return;}
 for(int i=0;i<boards.Count;i++){
  var b=boards[i];var m=movers[i];int count=b.UndoCount;
  while(b.UndoCount>0)check(b.UndoStep(),i+" undo rejected");
  check(cell(m.transform)==homes[i]&&Mathf.Abs(m.transform.position.y-heights[i])<.005f&&b.PlayerCell==homes[i]-dirs[i],i+" undo restore");
  check(count>(kinds[i]==3?0:1),i+" multiple pushes missing");
  check(b.TryStep(dirs[i]),i+" replay rejected");b.ResetPuzzle();check(cell(m.transform)==homes[i]&&!b.Busy,i+" reset during push");
 }
 string result=boards.Count+" bridge transport cases; bar/U/tipping ring/rolling ring, 4 directions, wide/two-cell/authored bridges, 2 heights and both array orders, enter/continue/exit, deck unchanged, undo/reset. Frames="+frames+" Failures="+failure.Count+"\n"+string.Join("\n",new System.Collections.Generic.List<string>(failure).ToArray());
 UnityEditor.SessionState.SetString("BridgeTransportRegression",result);System.IO.File.WriteAllText("D:/unity/between-two-poles/output/bridge-transport-result.md",result);
 UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
 }catch(System.Exception e){UnityEditor.SessionState.SetString("BridgeTransportRegression","ERROR "+e);UnityEditor.EditorApplication.update-=tick;if(root)UnityEngine.Object.Destroy(root);}
};
UnityEditor.SessionState.SetString("BridgeTransportRegression","RUNNING");UnityEditor.EditorApplication.update+=tick;
return "Started "+boards.Count+" cases";
