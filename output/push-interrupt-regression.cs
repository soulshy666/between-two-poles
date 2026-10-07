// Execute through Unity MCP in Play mode. Uses isolated objects and removes them.
if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play mode required");
var root=new GameObject("Push interrupt regression");root.SetActive(false);
var board=root.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
var avatar=new GameObject("Player");avatar.transform.SetParent(root.transform,false);
board.player=avatar.transform;board.player.position=new Vector3(150,0,150);
var home=board.player.position;
var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
for(int x=-1;x<=3;x++)for(int z=-1;z<=1;z++){
 var tile=new GameObject("Tile");tile.transform.SetParent(root.transform,false);
 tile.transform.position=home+new Vector3(x*1.5f,0,z*1.5f);
 tiles.Add(tile.AddComponent<BetweenPoles.GridTile>());
}
board.tiles=tiles.ToArray();
var go=new GameObject("Bar");go.transform.SetParent(root.transform,false);go.transform.position=home+Vector3.right*1.5f;
var bar=go.AddComponent<BetweenPoles.MagnetPiece>();bar.shape=BetweenPoles.MagnetShape.Bar;bar.north=true;
bar.geometry=BetweenPoles.MagnetVisuals.Build(go.transform,bar.shape,true,BetweenPoles.MagnetProduct.None,1.5f,true,Vector2Int.right);
board.magnets=new[]{bar};
root.SetActive(true);board.CaptureInitialState();
var pose=bar.Pose;var magnetStart=bar.transform.position;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var held=typeof(BetweenPoles.GridPlayground).GetField("heldDirection",flags);
int phase=0;float started=Time.unscaledTime;var failures=new System.Collections.Generic.List<string>();
BetweenPoles.MagnetPiece receiver=null;
System.Action<bool,string> check=(ok,message)=>{if(!ok)failures.Add(message);};
UnityEditor.SessionState.SetString("push-interrupt-result","running");
check(board.TryStep(Vector2Int.right),"initial push rejected");
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
 try{
  float elapsed=Time.unscaledTime-started;
  if(phase==0&&elapsed>.12f){
   check(board.Busy,"push already finished");
   held.SetValue(board,Vector2Int.up);
   check(board.TryStep(Vector2Int.up),"perpendicular interrupt rejected");
   check((bar.transform.position-magnetStart).sqrMagnitude<.00001f,"magnet position not restored");
   check(Quaternion.Angle(bar.Pose,pose)<.01f,"magnet pose not restored");
   check((Vector2Int)held.GetValue(board)==Vector2Int.up,"held direction lost");
   phase=1;started=Time.unscaledTime;
  }else if(phase==1&&!board.Busy){
   check((board.player.position-home-Vector3.forward*1.5f).sqrMagnitude<.00001f,"wrong redirected landing");
   check(board.UndoCount==1,"cancelled push left history entry");
   board.UndoStep();check((board.player.position-home).sqrMagnitude<.00001f,"undo did not restore origin");
   check(board.TryStep(Vector2Int.right),"second push rejected");phase=2;started=Time.unscaledTime;
  }else if(phase==2&&elapsed>.12f){
   var blocked=board.TileAt(new Vector2Int(100,99));blocked.blocked=true;
   check(board.TryStep(Vector2Int.down),"blocked-direction cancellation rejected");phase=3;started=Time.unscaledTime;
  }else if(phase==3&&!board.Busy){
   check((board.player.position-home).sqrMagnitude<.00001f,"blocked direction moved through wall");
   check(board.UndoCount==0,"blocked cancellation polluted history");
   check((bar.transform.position-magnetStart).sqrMagnitude<.00001f,"blocked cancellation lost magnet");
   var ug=new GameObject("U receiver");ug.transform.SetParent(root.transform,false);ug.transform.position=home+Vector3.right*3;
   receiver=ug.AddComponent<BetweenPoles.MagnetPiece>();receiver.shape=BetweenPoles.MagnetShape.Horseshoe;receiver.north=false;
   receiver.geometry=BetweenPoles.MagnetVisuals.Build(ug.transform,receiver.shape,false,BetweenPoles.MagnetProduct.None,1.5f,false,Vector2Int.right);
   board.magnets=new[]{bar,receiver};board.CaptureInitialState();
   check(board.TryStep(Vector2Int.right),"bridge assembly rejected");phase=4;started=Time.unscaledTime;
  }else if(phase==4&&elapsed>.12f){
   check(board.Busy,"assembly already finished");check(board.TryStep(Vector2Int.left),"assembly interrupt rejected");
   check(bar.gameObject.activeSelf&&!bar.combined&&bar.enabled,"incoming material lost on cancellation");
   check(receiver.product==BetweenPoles.MagnetProduct.None,"partial assembly committed");
   check((bar.transform.position-magnetStart).sqrMagnitude<.00001f,"assembly cancellation lost incoming position");
   phase=5;started=Time.unscaledTime;
  }else if(phase==5&&!board.Busy){
   check((board.player.position-home+Vector3.right*1.5f).sqrMagnitude<.00001f,"assembly redirect landing invalid");
   check(board.UndoCount==1,"assembly cancellation history invalid");
   UnityEditor.SessionState.SetString("push-interrupt-result",failures.Count==0?"PASS: perpendicular/reverse interrupt, magnet restoration, held input, undo, blocked direction, unfinished bridge assembly":string.Join("; ",failures));
   UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);
  }
  if(elapsed>5)throw new System.Exception("Timed out at phase "+phase);
 }catch(System.Exception e){UnityEditor.SessionState.SetString("push-interrupt-result","FAIL: "+e);UnityEditor.EditorApplication.update-=tick;UnityEngine.Object.Destroy(root);}
};
UnityEditor.EditorApplication.update+=tick;
return "Push interruption regression started";
