if(!Application.isPlaying)throw new System.Exception("Play mode required");
var failures=new System.Collections.Generic.List<string>();int cases=0;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var finish=typeof(BetweenPoles.GridPlayground).GetMethod("FinishMovement",flags);
System.Func<Vector2Int,float,Vector3> pos=(c,y)=>new Vector3(c.x*1.5f,y,c.y*1.5f);
foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})for(int type=0;type<2;type++)for(int level=0;level<2;level++)for(int layout=0;layout<9;layout++)for(int mode=0;mode<3;mode++){
 int id=cases++;float h=level*1.5f;var home=new Vector2Int(1500,1500);var d=dir;var side=new Vector2Int(-d.y,d.x);
 var root=new GameObject("Wide bridge route regression temporary");root.SetActive(false);root.hideFlags=HideFlags.DontSave;
 try{
 var b=root.AddComponent<BetweenPoles.GridPlayground>();b.enabled=false;
 var player=new GameObject("Player");player.transform.SetParent(root.transform,false);player.transform.position=pos(home-d*(mode==0?1:2),h);b.player=player.transform;
 var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
 System.Action<Vector2Int,float,bool> tile=(c,y,blocked)=>{var g=new GameObject("Floor");g.transform.SetParent(root.transform,false);g.transform.position=pos(c,y);var t=g.AddComponent<BetweenPoles.GridTile>();t.surfaceHeight=y;t.blocked=blocked;tiles.Add(t);};
 tile(home-d,h,false);tile(home-d*2,h,false);
 if(layout!=2&&layout!=8)tile(home+d*(layout>=6?2:1),h+(layout==3?1.5f:0),layout==4);
 if(layout==5){tile(home+side,h,false);tile(home-side,h,false);}
 b.tiles=tiles.ToArray();
 var magnets=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>();
 System.Func<Vector2Int,Quaternion,BetweenPoles.MagnetPiece> deck=(c,q)=>{var g=new GameObject("Bridge");g.transform.SetParent(root.transform,false);g.transform.position=pos(c,h);var m=g.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Bar;m.product=type==0?BetweenPoles.MagnetProduct.WideBar:BetweenPoles.MagnetProduct.None;m.combined=true;m.walkable=true;g.transform.rotation=q;var geo=GameObject.CreatePrimitive(PrimitiveType.Cube);geo.transform.SetParent(g.transform,false);geo.transform.localPosition=Vector3.up*.12f;geo.transform.localScale=new Vector3(1.5f,.24f,.48f);m.geometry=geo.transform;magnets.Add(m);return m;};
 var aligned=Quaternion.FromToRotation(Vector3.right,new Vector3(d.x,0,d.y));
 var bridge=deck(home,layout==1?Quaternion.AngleAxis(90,Vector3.up)*aligned:aligned);
 if(layout>=6)deck(home+d,layout==7?Quaternion.AngleAxis(90,Vector3.up)*aligned:aligned);
 if(layout==5){player.transform.position=pos(home-side*(mode==0?1:2),h);tile(home-side*2,h,false);b.tiles=tiles.ToArray();d=side;}
 BetweenPoles.MagnetPiece moving=null;
 if(mode!=0){var g=new GameObject("Material");g.transform.SetParent(root.transform,false);g.transform.position=pos(home-d,h);moving=g.AddComponent<BetweenPoles.MagnetPiece>();moving.shape=mode==1?BetweenPoles.MagnetShape.Bar:BetweenPoles.MagnetShape.Horseshoe;moving.geometry=BetweenPoles.MagnetVisuals.Build(g.transform,moving.shape,true,BetweenPoles.MagnetProduct.None,1.5f,true,Vector2Int.right);if(mode==2)BetweenPoles.MagnetVisuals.Product(moving,BetweenPoles.MagnetProduct.Ring,1.5f,Quaternion.identity,true);magnets.Add(moving);}
 b.magnets=magnets.ToArray();root.SetActive(true);
 bool expected=layout==0||layout==6;bool accepted=b.TryStep(d);
 if(accepted!=expected)failures.Add(id+" layout="+layout+" mode="+mode+" accepted="+accepted+": "+b.LastRule);
 if(!expected){if(b.Busy||b.UndoCount!=0||b.PlayerCell!=home-d*(mode==0?1:2))failures.Add(id+" rejected action changed state");}
 else{
 finish.Invoke(b,null);if(b.Busy)failures.Add(id+" first step unfinished");
 if(mode==0){int count=b.UndoCount;if(b.TryStep(side)||b.UndoCount!=count)failures.Add(id+" exited bridge sideways");}
 if(!b.UndoStep()||b.PlayerCell!=home-d*(mode==0?1:2))failures.Add(id+" undo failed");
 }
 }finally{UnityEngine.Object.DestroyImmediate(root);}
}
var result=cases+" wide-bridge route cases: 4 directions, 2 heights, wide/authored bars, walking/bar/ring pushing, correct axis, sideways, missing/high/blocked end, side entry, aligned/misaligned/open chain, undo. Failures="+failures.Count+"\n"+string.Join("\n",failures.ToArray());
System.IO.File.WriteAllText("D:/unity/between-two-poles/output/wide-bridge-route-result.md",result);return result;
