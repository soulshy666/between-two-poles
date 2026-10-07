// Isolated synchronous sampling: no input, scene switching or main-board mutation.
var root=new GameObject("Assembly player sync check");root.SetActive(false);
var results=new System.Collections.Generic.List<string>();
try{
 var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
 for(int variant=0;variant<7;variant++){
  var host=new GameObject("Case "+variant);host.transform.SetParent(root.transform,false);
  var b=host.AddComponent<BetweenPoles.GridPlayground>();b.enabled=false;
  var p=new GameObject("Player");p.transform.SetParent(host.transform,false);b.player=p.transform;
  var visual=new GameObject("Visual");visual.transform.SetParent(p.transform,false);
  var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
  for(int x=0;x<4;x++){var g=new GameObject("tile");g.transform.SetParent(host.transform,false);g.transform.position=Vector3.right*x*1.5f;tiles.Add(g.AddComponent<BetweenPoles.GridTile>());}
  b.tiles=tiles.ToArray();
  System.Func<int,BetweenPoles.MagnetPiece> make=index=>{
   var g=new GameObject("magnet");g.transform.SetParent(host.transform,false);g.transform.position=Vector3.right*(index+1)*1.5f;
   var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.north=index==0;
   m.shape=variant>=2&&variant<=4?BetweenPoles.MagnetShape.Horseshoe:(variant>=5&&index==1?BetweenPoles.MagnetShape.Horseshoe:BetweenPoles.MagnetShape.Bar);
   m.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,m.shape,m.north,BetweenPoles.MagnetProduct.None,1.5f,m.north,Vector2Int.right);return m;
  };
  var incoming=make(0);var receiver=make(1);b.magnets=new[]{incoming,receiver};
  var product=variant==0?BetweenPoles.MagnetProduct.WideBar:variant==1?BetweenPoles.MagnetProduct.Cross:variant<=4?BetweenPoles.MagnetProduct.Ring:variant==5?BetweenPoles.MagnetProduct.BridgeHalf:BetweenPoles.MagnetProduct.Lift;
  if(variant==1||variant==6)incoming.geometry.rotation=Quaternion.Euler(0,0,90);
  if(variant==3){incoming.geometry.rotation=Quaternion.Euler(0,-90,0);receiver.geometry.rotation=Quaternion.Euler(0,90,0);}
  if(variant==4){incoming.geometry.rotation=Quaternion.Euler(0,180,0);receiver.geometry.rotation=Quaternion.Euler(0,90,0);}
  b.GetType().GetField("<Busy>k__BackingField",flags).SetValue(b,true);
  b.GetType().GetField("recordingPush",flags).SetValue(b,true);
  var routine=(System.Collections.IEnumerator)b.GetType().GetMethod("Assemble",flags).Invoke(b,new object[]{incoming,receiver,Vector2Int.right,incoming.Pose,product,Vector2Int.right,Vector2Int.up});
  var stack=new System.Collections.Generic.Stack<System.Collections.IEnumerator>();stack.Push(routine);int frames=0;bool movingWithPush=false;
  while(stack.Count>0){var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}var child=current.Current as System.Collections.IEnumerator;if(child!=null){stack.Push(child);continue;}frames++;
   if(b.player.position.x>.001f&&b.player.position.x<1.499f&&visual.transform.localEulerAngles.x>=5
      &&receiver.product==BetweenPoles.MagnetProduct.None)movingWithPush=true;
   if(frames>1000)throw new System.Exception("Timeout");
  }
  if(!movingWithPush)throw new System.Exception("Missing simultaneous movement and push pose case "+variant);
  if((b.player.position-Vector3.right*1.5f).sqrMagnitude>.00001f||b.Busy||receiver.product!=product)throw new System.Exception("Final state failed case "+variant);
  if(Quaternion.Angle(visual.transform.localRotation,Quaternion.identity)>.01f)throw new System.Exception("Push pose not reset");
  results.Add(variant+" "+product+": PASS");
 }
 return string.Join("; ",results);
}finally{UnityEngine.Object.DestroyImmediate(root);}
