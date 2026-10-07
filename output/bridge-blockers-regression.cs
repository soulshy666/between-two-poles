var root=new GameObject("Bridge blockers temporary");root.hideFlags=HideFlags.DontSave;
var failures=new System.Collections.Generic.List<string>();int cases=0;
System.Func<Vector2Int,float,Vector3> pos=(c,y)=>new Vector3(c.x*1.5f,y,c.y*1.5f);
System.Func<Transform,Vector2Int,float,BetweenPoles.MagnetProduct,BetweenPoles.MagnetPiece> add=(parent,c,h,product)=>{
 var go=new GameObject("Magnet");go.transform.SetParent(parent,false);go.transform.position=pos(c,h);
 var m=go.AddComponent<BetweenPoles.MagnetPiece>();m.shape=BetweenPoles.MagnetShape.Horseshoe;m.geometry=BetweenPoles.MagnetVisuals.Build(go.transform,m.shape,true,BetweenPoles.MagnetProduct.None,1.5f,true,Vector2Int.right);
 if(product!=BetweenPoles.MagnetProduct.None)BetweenPoles.MagnetVisuals.Product(m,product,1.5f,Quaternion.identity,true);
 return m;
};
try{
foreach(var d in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})for(int kind=0;kind<8;kind++)for(int ring=0;ring<2;ring++){
 var home=new Vector2Int(1000+cases*8,1000);int id=cases++;
 var go=new GameObject("Blocker "+id);go.SetActive(false);go.transform.SetParent(root.transform,false);var b=go.AddComponent<BetweenPoles.GridPlayground>();b.enabled=false;
 var p=new GameObject("Player");p.transform.SetParent(go.transform,false);p.transform.position=pos(home-d,0);b.player=p.transform;
 var tiles=new System.Collections.Generic.List<BetweenPoles.GridTile>();
 foreach(int offset in new[]{-1,1}){var tgo=new GameObject("Floor");tgo.transform.SetParent(go.transform,false);tgo.transform.position=pos(home+d*offset,0);tiles.Add(tgo.AddComponent<BetweenPoles.GridTile>());}
 var m=add(go.transform,home,kind==3||kind==4?0:.24f,ring==1?BetweenPoles.MagnetProduct.Ring:BetweenPoles.MagnetProduct.None);
 var deck=add(go.transform,home,0,BetweenPoles.MagnetProduct.WideBar);var ms=new System.Collections.Generic.List<BetweenPoles.MagnetPiece>{deck,m};
 if(kind==0)tiles[1].blocked=true;
 if(kind==1)tiles[1].surfaceHeight=1.5f;
 if(kind==2)ms.Add(add(go.transform,home+d,0,BetweenPoles.MagnetProduct.Cross));
 if(kind==3||kind==4){deck.gameObject.SetActive(false);if(kind==3){var tgo=new GameObject("Floor");tgo.transform.SetParent(go.transform,false);tgo.transform.position=pos(home,0);tiles.Add(tgo.AddComponent<BetweenPoles.GridTile>());ms.Add(add(go.transform,home+d,0,BetweenPoles.MagnetProduct.WideBar));}}
 if(kind==5){var other=add(go.transform,home+d,0,BetweenPoles.MagnetProduct.Ring);other.geometry.rotation=Quaternion.Euler(90,0,0);other.walkable=false;ms.Add(other);}
 if(kind==6)ms.Add(add(go.transform,home+d,0,BetweenPoles.MagnetProduct.BridgeHalf));
 if(kind==7){tiles.RemoveAt(1);var otherDeck=add(go.transform,home+d,0,BetweenPoles.MagnetProduct.WideBar);ms.Add(otherDeck);ms.Add(add(go.transform,home+d,.24f,BetweenPoles.MagnetProduct.Lift));}
 b.tiles=tiles.ToArray();b.magnets=ms.ToArray();go.SetActive(true);var start=m.transform.position;
 if(b.TryStep(d)||b.Busy||b.UndoCount!=0||m.transform.position!=start||b.PlayerCell!=home-d)failures.Add(id+" unexpectedly moved: "+b.LastRule);
 b.StopAllCoroutines();
}
var result=cases+" bridge blocker cases; stone, higher shore, Cross, raised land wide bar, unsupported source, upright ring, incomplete bridge, occupied deck. Failures="+failures.Count+"\n"+string.Join("\n",failures.ToArray());
UnityEditor.SessionState.SetString("BridgeBlockers",result);System.IO.File.WriteAllText("D:/unity/between-two-poles/output/bridge-blockers-result.md",result);return result;
}finally{UnityEngine.Object.DestroyImmediate(root);}
