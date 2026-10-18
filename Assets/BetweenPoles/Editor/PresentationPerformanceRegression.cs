using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BetweenPoles.EditorTools {
[InitializeOnLoad]
public static class PresentationPerformanceRegression {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string BatchKey="PresentationPerformanceRegression.Batch";
    static readonly List<GameObject> roots=new List<GameObject>();
    static int checks;
    static PresentationPerformanceRegression(){
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(BatchKey,false))return;
            SessionState.SetBool(BatchKey,false);
            EditorApplication.delayCall+=()=>{try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}};
        };
    }
    public static void RunBatch(){
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(BatchKey,true);EditorApplication.isPlaying=true;
    }
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static object Call(object target,string method,params object[] args){return target.GetType().GetMethod(method,Private).Invoke(target,args);}
    static GameObject Root(string name,bool active=true){var root=new GameObject(name);root.SetActive(active);roots.Add(root);return root;}
    static Transform Child(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static MeshRenderer Renderer(Transform parent){return Child(parent,"rendered").gameObject.AddComponent<MeshRenderer>();}
    static MagnetPiece Piece(Transform parent,Vector3 position){
        var go=Child(parent,"magnet");go.position=position;var piece=go.gameObject.AddComponent<MagnetPiece>();
        piece.geometry=Child(go,"geometry");Renderer(piece.geometry);Renderer(piece.geometry);return piece;
    }
    static GridTile Tile(Transform parent,Vector3 position){
        var root=Child(parent,"shore");root.position=position;var anchor=root.gameObject.AddComponent<IslandSurfaceAnchor>();anchor.center=root;
        return Child(root,"tile").gameObject.AddComponent<GridTile>();
    }
    static PrejoinedTestBridges.Link OriginalCrossing(PrejoinedTestBridges bridges,Vector3 position){
        foreach(var link in bridges.links){
            if(link==null||!link.shoreA||!link.shoreB||!link.magnet)continue;
            var a=link.shoreA.transform.position;var b=link.shoreB.transform.position;a.y=b.y=position.y=0;
            var direction=(b-a).normalized;a+=direction*(bridges.board.cellSize*.5f);b-=direction*(bridges.board.cellSize*.5f);
            var delta=b-a;if(delta.sqrMagnitude<.001f)continue;
            float t=Vector3.Dot(position-a,delta)/delta.sqrMagnitude;
            if(t>0&&t<1&&(position-Vector3.Lerp(a,b,t)).sqrMagnitude<.36f)return link;
        }
        return null;
    }
    static readonly MaterialPropertyBlock legacyBlock=new MaterialPropertyBlock();
    static void OriginalSet(PrejoinedTestBridges bridges,Renderer renderer,PrejoinedTestBridges.Link link){
        renderer.GetPropertyBlock(legacyBlock);legacyBlock.SetFloat("_BridgeEnabled",link!=null?1:0);
        if(link!=null){
            var direction=(link.shoreB.transform.position-link.shoreA.transform.position).normalized;
            legacyBlock.SetVector("_BridgeStart",link.shoreA.transform.position+direction*bridges.board.cellSize*.5f);
            legacyBlock.SetVector("_BridgeEnd",link.shoreB.transform.position-direction*bridges.board.cellSize*.5f);
            for(int i=0;i<2;i++){
                var binding=(i==0?link.shoreA:link.shoreB).GetComponentInParent<IslandSurfaceAnchor>();var center=binding.center.position;
                Vector4 focus=Shader.GetGlobalVector("_IceFocus");float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(Vector2.Distance(new Vector2(center.x,center.z),new Vector2(focus.x,focus.z))/6));
                string suffix=i==0?"A":"B";legacyBlock.SetVector("_BridgeIsland"+suffix,new Vector4(center.x,center.y,center.z,binding.flatten*weight));
                legacyBlock.SetVector("_BridgeOffset"+suffix,binding.visualOffset*weight);
            }
        }
        renderer.SetPropertyBlock(legacyBlock);
    }
    static void OriginalRefresh(PrejoinedTestBridges bridges,Renderer[] player){
        foreach(var link in bridges.links)if(link!=null&&link.shoreA&&link.shoreB&&link.magnet)foreach(var r in link.renderers)OriginalSet(bridges,r,link);
        var crossing=OriginalCrossing(bridges,bridges.board.player.position);foreach(var r in player)OriginalSet(bridges,r,crossing);
        foreach(var m in bridges.board.magnets){if(!m||!m.enabled||!m.gameObject.activeInHierarchy||!m.CanBePushed)continue;
            crossing=OriginalCrossing(bridges,m.transform.position);foreach(var r in m.GetComponentsInChildren<Renderer>())OriginalSet(bridges,r,crossing);
        }
    }
    static string Measure(Action run){
        long probe=GC.GetAllocatedBytesForCurrentThread();GC.KeepAlive(new byte[8192]);
        bool allocationCounterAvailable=GC.GetAllocatedBytesForCurrentThread()>probe;
        for(int i=0;i<8;i++)run();var watch=new System.Diagnostics.Stopwatch();long allocated=GC.GetAllocatedBytesForCurrentThread();watch.Start();
        for(int i=0;i<120;i++)run();watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        return $"{watch.Elapsed.TotalMilliseconds/120:F3} ms/refresh, "+(allocationCounterAvailable?$"{allocated/120} bytes/refresh":"allocation counter unavailable on this runtime");
    }
    [MenuItem("两极之间/测试/渲染缓存与桥接性能回归")]
    public static void Run(){
        if(!Application.isPlaying)throw new InvalidOperationException("Run this regression in Play mode.");
        checks=0;var report=new System.Text.StringBuilder();var savedFocus=Shader.GetGlobalVector("_IceFocus");
        try{
            var board=Root("performance board",false).AddComponent<GridPlayground>();board.enabled=false;board.cellSize=1;
            var world=Root("performance fixtures").transform;board.player=Child(world,"player");var playerRenderer=Renderer(board.player);
            var bridges=Child(world,"bridges").gameObject.AddComponent<PrejoinedTestBridges>();bridges.board=board;
            var magnets=new List<MagnetPiece>();var links=new List<PrejoinedTestBridges.Link>();
            for(int i=0;i<40;i++){
                var a=new Vector3((i%8)*8,0,(i/8)*8);var b=a+new Vector3(4,i%3,0);
                if(i%2==1)b=a+new Vector3(2,i%3,4);
                var magnet=Piece(world,a);magnet.combined=true;magnets.Add(magnet);
                links.Add(new PrejoinedTestBridges.Link{shoreA=Tile(world,a),shoreB=Tile(world,b),magnet=magnet,renderers=magnet.GetComponentsInChildren<Renderer>()});
            }
            for(int i=0;i<100;i++)magnets.Add(Piece(world,new Vector3((i%10)*5,0,(i/10)*5)));
            board.magnets=magnets.ToArray();bridges.links=links.ToArray();bridges.Refresh();
            var random=new System.Random(418);
            for(int i=0;i<12000;i++){
                var p=new Vector3((float)random.NextDouble()*90-10,7,(float)random.NextDouble()*70-10);
                Check(ReferenceEquals(OriginalCrossing(bridges,p),Call(bridges,"Crossing",p)),"Indexed crossing differs at "+p);
            }
            foreach(var link in links){
                var a=link.shoreA.transform.position;var b=link.shoreB.transform.position;a.y=b.y=0;var d=(b-a).normalized;
                foreach(float t in new[]{0f,.000001f,.5f,.999999f,1f})foreach(float offset in new[]{0f,.59999f,.6f,.60001f}){
                    var p=Vector3.Lerp(a+d*.5f,b-d*.5f,t)+new Vector3(-d.z,0,d.x)*offset;
                    Check(ReferenceEquals(OriginalCrossing(bridges,p),Call(bridges,"Crossing",p)),"Bridge boundary changed at "+p);
                }
            }
            var first=links[0];board.player.position=new Vector3(2,0,0);bridges.Refresh();
            var block=new MaterialPropertyBlock();playerRenderer.GetPropertyBlock(block);Check(block.GetFloat("_BridgeEnabled")==1,"Player bridge projection missing");
            block.SetFloat("_BridgeEnabled",0);block.SetFloat("_UnrelatedRegression",17);playerRenderer.SetPropertyBlock(block);bridges.Refresh();playerRenderer.GetPropertyBlock(block);
            Check(block.GetFloat("_BridgeEnabled")==1&&block.GetFloat("_UnrelatedRegression")==17,"Live property block was not repaired/preserved");
            var shore=first.shoreA.GetComponentInParent<IslandSurfaceAnchor>();shore.flatten=.8f;shore.visualOffset=new Vector3(2,3,4);
            Shader.SetGlobalVector("_IceFocus",new Vector4(20,0,20,0));bridges.Refresh();playerRenderer.GetPropertyBlock(block);
            var expectedIsland=block.GetVector("_BridgeIslandA");var expectedOffset=block.GetVector("_BridgeOffsetA");
            OriginalSet(bridges,playerRenderer,first);playerRenderer.GetPropertyBlock(block);
            Check(expectedIsland.Equals(block.GetVector("_BridgeIslandA"))&&expectedOffset.Equals(block.GetVector("_BridgeOffsetA")),"Moving camera/shore projection differs from legacy");
            first.shoreA.transform.parent.position+=Vector3.forward*60;first.shoreB.transform.parent.position+=Vector3.forward*60;bridges.Refresh();
            Check(Call(bridges,"Crossing",new Vector3(2,0,0))==null,"Moved bridge left a stale bucket");
            board.player.position=new Vector3(2,0,60);bridges.Refresh();playerRenderer.GetPropertyBlock(block);Check(block.GetFloat("_BridgeEnabled")==1,"Moved bridge projection missing");
            var cargo=board.magnets[40];cargo.transform.position=board.player.position;cargo.geometry.gameObject.SetActive(false);cargo.geometry=Child(cargo.transform,"replacement");var replacement=Renderer(cargo.geometry);
            bridges.Refresh();replacement.GetPropertyBlock(block);Check(block.GetFloat("_BridgeEnabled")==1,"Geometry replacement/undo cache stale");
            var carried=board.magnets[41];carried.transform.SetParent(cargo.transform,true);carried.enabled=false;bridges.Refresh();
            carried.geometry.gameObject.SetActive(false);carried.geometry=Child(carried.transform,"nested replacement");var nested=Renderer(carried.geometry);bridges.Refresh();nested.GetPropertyBlock(block);
            Check(block.GetFloat("_BridgeEnabled")==1,"Assembled child geometry was not invalidated");
            links.Insert(0,new PrejoinedTestBridges.Link{shoreA=first.shoreA,shoreB=first.shoreB,magnet=first.magnet,renderers=first.renderers});bridges.links=links.ToArray();bridges.Refresh();
            Check(ReferenceEquals(Call(bridges,"Crossing",board.player.position),links[0]),"Overlapping bridge order changed");
            var playerRenderers=new Renderer[]{playerRenderer};
            report.AppendLine("Legacy bridge refresh: "+Measure(()=>OriginalRefresh(bridges,playerRenderers)));
            report.AppendLine("Optimized bridge refresh: "+Measure(bridges.Refresh));
            bridges.enabled=false;replacement.GetPropertyBlock(block);Check(block.GetFloat("_BridgeEnabled")==0,"Disable did not clear bridge override");bridges.enabled=true;
            bridges.links=new PrejoinedTestBridges.Link[0];bridges.Refresh();playerRenderer.GetPropertyBlock(block);Check(block.GetFloat("_BridgeEnabled")==0,"Removing bridges left player projection");
            first.shoreA.transform.position=new Vector3(-100000,0,-100000);first.shoreB.transform.position=new Vector3(100000,0,100000);
            bridges.links=new[]{first};bridges.Refresh();Check(ReferenceEquals(Call(bridges,"Crossing",Vector3.zero),OriginalCrossing(bridges,Vector3.zero)),"Long bridge fallback differs");
            first.shoreB.transform.position=first.shoreA.transform.position;bridges.Refresh();Check(Call(bridges,"Crossing",first.shoreA.transform.position)==null,"Zero-length bridge should not cross");
            bridges.links=new PrejoinedTestBridges.Link[]{null,new PrejoinedTestBridges.Link(),first};bridges.Refresh();
            TestMergeCatchup(report);
            TestRingQueries(board,world,report);
            TestFraming(board,world);
            report.Insert(0,$"PASS: {checks} assertions in Unity Play mode.\n");
            Directory.CreateDirectory("output");File.WriteAllText("output/presentation-performance-result.txt",report.ToString());Debug.Log(report.ToString());
        }catch(Exception e){Directory.CreateDirectory("output");File.WriteAllText("output/presentation-performance-result.txt","FAIL: "+e);throw;}
        finally{Shader.SetGlobalVector("_IceFocus",savedFocus);foreach(var root in roots)if(root)UnityEngine.Object.DestroyImmediate(root);roots.Clear();}
    }
    static void TestMergeCatchup(System.Text.StringBuilder report){
        double legacyMs=0,optimizedMs=0;
        foreach(bool legacy in new[]{true,false})foreach(var direction in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var host=Root("ring merge catchup",false);var board=host.AddComponent<GridPlayground>();board.enabled=false;
            board.player=Child(host.transform,"actor");board.tiles=new GridTile[4];
            var push=new Vector3(direction.x,0,direction.y);var facing=Quaternion.LookRotation(push);
            for(int i=0;i<4;i++){var tile=Child(host.transform,"floor");tile.position=push*i*board.cellSize;board.tiles[i]=tile.gameObject.AddComponent<GridTile>();}
            var pieces=new MagnetPiece[145];
            pieces[0]=MagnetVisuals.Create(host.transform,push*board.cellSize,MagnetShape.Horseshoe,true,facing,board.cellSize,host.transform);
            pieces[1]=MagnetVisuals.Create(host.transform,push*board.cellSize*2,MagnetShape.Horseshoe,false,facing*Quaternion.Euler(0,180,0),board.cellSize,host.transform);
            for(int i=2;i<pieces.Length;i++)pieces[i]=MagnetVisuals.Create(host.transform,new Vector3(100+i*2,0,100),MagnetShape.Bar,true,Quaternion.identity,board.cellSize,host.transform);
            board.magnets=pieces;host.SetActive(true);
            Check(board.TryStep(direction),"Ring merge input rejected: "+board.LastRule);
            var incoming=typeof(GridPlayground).GetField("activeAssemblyIncoming",Private);var receiver=typeof(GridPlayground).GetField("activeAssemblyReceiver",Private);
            Check(incoming.GetValue(board)==pieces[0]&&receiver.GetValue(board)==pieces[1],"Assembly participant scope missing");
            // Null scope exercises the previous full-map capture path using the same animation.
            if(legacy){incoming.SetValue(board,null);receiver.SetValue(board,null);}
            var watch=System.Diagnostics.Stopwatch.StartNew();Call(board,"FinishPushWithPresentation",.12f,null);watch.Stop();
            if(legacy)legacyMs+=watch.Elapsed.TotalMilliseconds;else optimizedMs+=watch.Elapsed.TotalMilliseconds;
            Check(pieces[1].product==MagnetProduct.Ring&&!pieces[0].gameObject.activeSelf,"Catchup changed ring assembly result");
            Check((pieces[1].transform.position-push*board.cellSize*2).sqrMagnitude<.00001f,"Receiver moved to wrong cell");
            Check(incoming.GetValue(board)==null&&receiver.GetValue(board)==null,"Assembly scope survived completion");
            Check(board.UndoStep(),"Ring merge undo rejected");
            Check(pieces[0].gameObject.activeSelf&&pieces[0].enabled&&pieces[1].product==MagnetProduct.None,"Undo did not restore two U magnets");
            UnityEngine.Object.DestroyImmediate(host);roots.Remove(host);
        }
        report.AppendLine($"Ring merge catchup / 145 magnets / four directions: full-map capture {legacyMs/4:F3} ms, participant capture {optimizedMs/4:F3} ms (includes simulation and model swap).");
    }
    delegate bool FloorQuery(Vector2Int cell,out float height);
    static void TestRingQueries(GridPlayground board,Transform world,System.Text.StringBuilder report){
        var pieces=new MagnetPiece[145];
        for(int i=0;i<pieces.Length;i++)pieces[i]=Piece(world,new Vector3(i*2,0,(i%3)*3));
        board.magnets=pieces;board.tiles=new GridTile[0];board.InvalidateTileLookup();
        var space=(Func<MagnetPiece,bool>)Delegate.CreateDelegate(typeof(Func<MagnetPiece,bool>),board,typeof(GridPlayground).GetMethod("SpaceRing",Private));
        var animating=(Func<MagnetPiece,bool>)Delegate.CreateDelegate(typeof(Func<MagnetPiece,bool>),board,typeof(GridPlayground).GetMethod("MagnetStillAnimating",Private));
        var deckHeight=(Func<MagnetPiece,float>)Delegate.CreateDelegate(typeof(Func<MagnetPiece,float>),board,typeof(GridPlayground).GetMethod("DeckHeight",Private));
        var tileAt=(Func<Vector2Int,GridTile>)Delegate.CreateDelegate(typeof(Func<Vector2Int,GridTile>),board,typeof(GridPlayground).GetMethod("Tile",Private));
        var optimized=(FloorQuery)Delegate.CreateDelegate(typeof(FloorQuery),board,typeof(GridPlayground).GetMethod("TryPortalFloor",Private));
        // Original evaluation order, including the full support scan before cell rejection.
        FloorQuery legacy=delegate(Vector2Int cell,out float height){
            var tile=tileAt(cell);height=tile?tile.surfaceHeight:0;if(tile)return !tile.blocked;
            foreach(var ring in pieces)if(space(ring)&&ring.enabled&&ring.gameObject.activeInHierarchy&&ring.geometry&&ring.walkable&&MagnetPiece.FlatU(ring.Pose)&&!animating(ring)&&ring.Occupies(cell,board.cellSize)){height=deckHeight(ring);return true;}
            return false;
        };
        foreach(int count in new[]{1,12,40}){
            for(int i=0;i<pieces.Length;i++){pieces[i].product=i<count?MagnetProduct.Ring:MagnetProduct.None;pieces[i].walkable=i<count;}
            for(int x=-2;x<292;x++)for(int z=-1;z<8;z++){
                var cell=new Vector2Int(x,z);float before,after;bool expected=legacy(cell,out before),actual=optimized(cell,out after);
                Check(expected==actual&&before.Equals(after),"Ring floor query changed at "+cell);
            }
            report.AppendLine($"145 magnets / {count} rings, legacy empty-cell query: "+Measure(()=>{float h;legacy(new Vector2Int(-77,-77),out h);}));
            report.AppendLine($"145 magnets / {count} rings, optimized empty-cell query: "+Measure(()=>{float h;optimized(new Vector2Int(-77,-77),out h);}));
        }
    }
    static void TestFraming(GridPlayground board,Transform world){
        var cameraObject=Root("framing camera",false);cameraObject.AddComponent<Camera>();var island=cameraObject.AddComponent<IslandCamera>();island.board=board;
        var curved=cameraObject.AddComponent<CurvedTrialCamera>();curved.island=island;
        var a=Tile(world,new Vector3(0,0,0));var b=Tile(world,new Vector3(3,0,0));board.tiles=new[]{a,b};
        var centerA=a.transform.parent;var centerB=b.transform.parent;var centers=new[]{centerA,centerB};island.islandCenters=centers;
        Func<bool> changed=()=> (bool)Call(curved,"FramingInputsChanged",centers,2,1f,1.777f,6f,1.4f,null);
        Check(changed(),"First framing input must invalidate");Check(!changed(),"Stable framing did not reuse cache");
        b.surfaceHeight=2;Check(changed(),"Height edit did not invalidate");Check(!changed(),"Height edit failed to settle");
        b.transform.position+=Vector3.right;Check(changed(),"Moved tile did not invalidate");
        b.gameObject.SetActive(false);Check(changed(),"Inactive tile did not invalidate");b.gameObject.SetActive(true);Check(changed(),"Reactivated tile did not invalidate");
        b.GetComponentInParent<IslandSurfaceAnchor>().center=centerA;Check(changed(),"Ownership edit did not invalidate");
        centerA.position+=Vector3.one*10;Check(changed(),"Chapter rebase did not invalidate");
        cameraObject.transform.rotation=Quaternion.Euler(50,0,0);Check(changed(),"View rotation did not invalidate");
        board.cellSize=1.2f;Check(changed(),"Cell scale did not invalidate");
        board.tiles=new[]{b,a};Check(changed(),"In-place tile ordering did not invalidate");Check(!changed(),"Stable final framing did not reuse cache");
    }
}
}
