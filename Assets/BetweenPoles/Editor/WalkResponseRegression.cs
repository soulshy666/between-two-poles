using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BetweenPoles.EditorTools {
[InitializeOnLoad] static class WalkResponseRegression {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Request="output/walk-response.run",Report="output/walk-response.txt";
    static WalkResponseRegression(){EditorApplication.update+=Poll;}
    static void Poll(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        File.Delete(Request);Run();
    }
    static object Call(GridPlayground board,string name,params object[] args){return typeof(GridPlayground).GetMethod(name,Private).Invoke(board,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static bool AdvanceCardinal(IEnumerator move,GridPlayground board){
        var before=board.player.position;bool running=move.MoveNext();var after=board.player.position;
        var delta=after-before;
        Check(Mathf.Abs(delta.x)<.00001f||Mathf.Abs(delta.z)<.00001f,"Diagonal displacement during a turn");
        Check(Mathf.Abs(after.x/board.cellSize-Mathf.Round(after.x/board.cellSize))<.00001f
            ||Mathf.Abs(after.z/board.cellSize-Mathf.Round(after.z/board.cellSize))<.00001f,"Player left grid centre lines");
        return running;
    }
    [MenuItem("两极之间/验证行走即时反向")]
    static void Run(){
        // An inactive, isolated preview scene never changes the user's live board.
        var scene=EditorSceneManager.NewPreviewScene();
        try{
            var root=new GameObject("Walk response regression");root.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var board=root.AddComponent<GridPlayground>();
            board.player=new GameObject("Player").transform;board.player.SetParent(root.transform);
            var cells=new[]{Vector2Int.zero,Vector2Int.right,new Vector2Int(2,0),Vector2Int.up,Vector2Int.one,new Vector2Int(2,1)};
            board.magnets=new MagnetPiece[0];board.tiles=new GridTile[cells.Length];
            for(int i=0;i<cells.Length;i++){
                var tile=new GameObject("Tile "+i);tile.transform.SetParent(root.transform);
                tile.transform.position=new Vector3(cells[i].x*board.cellSize,0,cells[i].y*board.cellSize);board.tiles[i]=tile.AddComponent<GridTile>();
            }
            // Use the existing deterministic 60 Hz movement clock.
            typeof(GridPlayground).GetField("recordingPush",Private).SetValue(board,true);
            int landings=0;GridTile landed=null;board.Landed+=tile=>{landings++;landed=tile;};
            foreach(int frames in new[]{1,5,11}){
                board.player.SetPositionAndRotation(Vector3.zero,Quaternion.LookRotation(Vector3.left));
                landings=0;landed=null;
                var move=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);
                for(int i=0;i<frames;i++)Check(move.MoveNext()&&move.Current==null,"Walk waited for a turn before moving");
                var before=board.player.position;
                Check(before.x>0&&before.x<board.cellSize,"First-frame movement or intermediate position invalid");
                float facingBefore=Quaternion.Angle(board.player.rotation,Quaternion.LookRotation(Vector3.left));
                Check(board.TryStep(Vector2Int.left),"Reverse input rejected");
                Check(board.player.position==before,"Reverse teleported the player");
                Check(Quaternion.Angle(board.player.rotation,Quaternion.LookRotation(Vector3.left))<facingBefore||facingBefore<.01f,"Reverse facing was delayed");
                move.MoveNext();Check(board.player.position.x<before.x,"Reverse continued toward the old destination");
                int guard=100;while(move.MoveNext())Check(--guard>0,"Reversed walk never finished");
                Check(board.player.position==Vector3.zero&&!board.Busy,"Reverse did not finish at the origin");
                Check(landings==1&&landed==board.tiles[0],"Reverse notified the wrong landing tile");
            }
            var repeat=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);
            for(int i=0;i<6;i++)repeat.MoveNext();
            Check(board.TryStep(Vector2Int.left),"First repeated reversal rejected");repeat.MoveNext();
            var pivot=board.player.position;
            Check(board.TryStep(Vector2Int.right),"Second repeated reversal rejected");repeat.MoveNext();
            Check(board.player.position.x>pivot.x,"Latest direction did not win");
            int remaining=100;while(repeat.MoveNext())Check(--remaining>0,"Repeated reversal never finished");
            Check(board.player.position==board.tiles[1].transform.position&&!board.Busy,"Repeated reversal landed incorrectly");
            board.player.position=Vector3.zero;
            var turn=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);turn.MoveNext();turn.MoveNext();
            var turnStart=board.player.position;
            Check(board.TryStep(Vector2Int.up),"Open-ground turn rejected");
            Check(board.player.position==turnStart,"Turn teleported the player");
            AdvanceCardinal(turn,board);Check(board.player.position.z==turnStart.z,"Turn cut diagonally before reaching the centre");
            remaining=100;while(AdvanceCardinal(turn,board))Check(--remaining>0,"Turn never finished");
            Check(board.PlayerCell==Vector2Int.one&&!board.Busy,"Turn missed target centre");
            board.player.position=Vector3.zero;board.tiles[3].blocked=true;
            var blocked=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);blocked.MoveNext();
            board.TryStep(Vector2Int.up);blocked.MoveNext();
            Check(board.player.position.z==0,"Turn cut through the blocked corner");
            Call(board,"ClearMovementInput");remaining=100;while(blocked.MoveNext())Check(--remaining>0,"Blocked turn never settled");
            board.tiles[3].blocked=false;board.player.position=Vector3.zero;
            var continuous=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);continuous.MoveNext();
            Call(board,"PumpMovementInput",Vector2Int.right,false);
            var pose=board.player.GetComponent<PlayerPushPose>();
            bool crossed=false;remaining=100;
            while(continuous.MoveNext()){
                Check(--remaining>0,"Continuous walk never stopped");
                if(board.player.position.x>=board.cellSize){crossed=true;Check((bool)typeof(PlayerPushPose).GetField("walking",Private).GetValue(pose),"Gait reset between cells");}
            }
            Check(crossed&&board.player.position==board.tiles[2].transform.position&&!board.Busy,"Buffered walk did not stop at the second cell");
            board.player.position=Vector3.zero;
            var spam=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);AdvanceCardinal(spam,board);
            var directions=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down,Vector2Int.up};
            foreach(var direction in directions){
                Check(board.Busy,"Rapid-input fixture ended early");
                board.TryStep(direction);AdvanceCardinal(spam,board);
            }
            remaining=100;while(AdvanceCardinal(spam,board))Check(--remaining>0,"Rapid-input walk never settled");
            Check(!board.Busy&&Vector3.Distance(board.player.position,new Vector3(board.PlayerCell.x*board.cellSize,0,board.PlayerCell.y*board.cellSize))<.00001f,"Rapid inputs did not settle at a cell centre");
            int normalFrames=0,tapFrames=0;
            foreach(bool fresh in new[]{false,true}){
                Call(board,"ClearMovementInput");board.player.position=Vector3.zero;
                var accelerated=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);accelerated.MoveNext();
                var beforeTap=board.player.position;
                Call(board,"PumpMovementInput",Vector2Int.right,fresh);
                Check(board.player.position==beforeTap,"Fresh tap teleported player");
                float gaitBefore=(float)typeof(PlayerPushPose).GetField("walkDistance",Private).GetValue(pose);
                AdvanceCardinal(accelerated,board);
                float travelled=Vector3.Distance(beforeTap,board.player.position);
                Check(travelled<=board.cellSize/board.walkSeconds*board.maxWalkSpeedMultiplier/60f+.00001f,"Walking exceeded speed cap");
                float gaitAfter=(float)typeof(PlayerPushPose).GetField("walkDistance",Private).GetValue(pose);
                Check(Mathf.Abs(gaitAfter-gaitBefore-travelled)<.00001f,"Gait did not follow accelerated movement");
                if(fresh)Check(travelled>board.cellSize/board.walkSeconds/60f*1.1f,"Fresh tap did not accelerate");
                else Check(Mathf.Abs(travelled-board.cellSize/board.walkSeconds/60f)<.00001f,"Held input accelerated");
                int frames=1;while(AdvanceCardinal(accelerated,board))Check(++frames<100,"Tap walk stuck");
                Check(board.PlayerCell==new Vector2Int(2,0)&&!board.Busy,"Tap did not finish exactly two steps");
                if(fresh)tapFrames=frames;else normalFrames=frames;
            }
            Check(tapFrames<normalFrames,"Fresh tap throughput unchanged");
            Call(board,"ClearMovementInput");board.player.position=Vector3.zero;board.tiles[2].blocked=true;
            var wall=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);wall.MoveNext();
            Call(board,"PumpMovementInput",Vector2Int.right,true);
            remaining=100;while(AdvanceCardinal(wall,board))Check(--remaining>0,"Accelerated wall approach stuck");
            Check(board.PlayerCell==Vector2Int.right&&!board.Busy,"Accelerated walk crossed obstacle");
            board.tiles[2].blocked=false;Call(board,"ClearMovementInput");
            board.player.position=Vector3.zero;board.CaptureInitialState();
            var undoWalk=(IEnumerator)Call(board,"MovePlayer",Vector2Int.right);undoWalk.MoveNext();
            Call(board,"PumpMovementInput",Vector2Int.right,true);
            remaining=100;while(undoWalk.MoveNext())Check(--remaining>0,"Undo setup stuck");
            Check(board.UndoCount==1&&board.UndoStep()&&board.PlayerCell==Vector2Int.right,"Accelerated continuation lost per-cell undo");
            ValidateRecoilCullingBounds(board,root);
            ValidateStoneBackedRecoil(board,root);
            ValidateLocalRecoil(board,root);
            File.WriteAllText(Report,"PASS: same-island stone recoil in four directions, nearest stone, exact player/magnet landing and undo; same-direction fresh taps accelerate; held timing unchanged; gait tracks actual distance; no teleport or obstacle bypass; per-cell undo; reversals; cardinal turns; uninterrupted gait; exact landing and unlock.");
        }catch(Exception ex){File.WriteAllText(Report,"FAIL: "+ex);Debug.LogException(ex);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void ValidateRecoilCullingBounds(GridPlayground board,GameObject root){
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Art/AstronautV1/AstronautVisual.prefab");
        var visual=UnityEngine.Object.Instantiate(prefab,board.player,false);visual.name="Visual";
        board.playerVisual=visual.transform;
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
        foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(0,0,90)}){
            var origin=new Vector3(dir.x,0,dir.y)*board.cellSize;
            var magnet=MagnetVisuals.Create(root.transform,origin,MagnetShape.Bar,true,rotation,board.cellSize,root.transform);
            var filters=magnet.geometry.GetComponentsInChildren<MeshFilter>();
            var meshes=new Mesh[filters.Length];
            for(int i=0;i<filters.Length;i++){meshes[i]=UnityEngine.Object.Instantiate(filters[i].sharedMesh);filters[i].sharedMesh=meshes[i];}
            var baseline=new System.Collections.Generic.List<Vector3>();
            try{
                for(int pass=0;pass<2;pass++){
                    if(pass==1)foreach(var mesh in meshes)mesh.bounds=new Bounds(Vector3.zero,Vector3.one*200);
                    board.player.position=Vector3.zero;magnet.transform.position=origin;
                    var motion=(IEnumerator)Call(board,"Recoil",magnet,dir,Vector2Int.zero);
                    int frame=0;
                    try{
                        while(motion.MoveNext()){
                            var cargo=magnet.transform.position;var body=visual.transform.position;
                            Check(Vector3.Distance(cargo,origin)<board.cellSize*2,"Recoil contact launched magnet out of reach");
                            if(pass==0){baseline.Add(cargo);baseline.Add(body);}
                            else{
                                Check(frame*2+1<baseline.Count,"Culling bounds changed recoil timing");
                                Check(Vector3.Distance(cargo,baseline[frame*2])<.0001f&&Vector3.Distance(body,baseline[frame*2+1])<.0001f,
                                    "Culling bounds changed recoil contact or player clearance");
                            }
                            frame++;Check(frame<200,"Recoil contact did not finish");
                            // Stop at the nested flight: this test isolates chest contact.
                            if(motion.Current is IEnumerator)break;
                        }
                        Check(frame*2==baseline.Count,"Recoil sample count changed");
                    }finally{(motion as IDisposable)?.Dispose();}
                }
            }finally{
                UnityEngine.Object.DestroyImmediate(magnet.gameObject);
                foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
    static void ValidateStoneBackedRecoil(GridPlayground board,GameObject root){
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var near=MagnetVisuals.Create(root.transform,new Vector3(dir.x,0,dir.y)*board.cellSize,MagnetShape.Bar,true,Quaternion.identity,board.cellSize,root.transform);
            var far=MagnetVisuals.Create(root.transform,new Vector3(dir.x,0,dir.y)*board.cellSize*2,MagnetShape.Bar,true,Quaternion.identity,board.cellSize,root.transform);
            var stone=new GameObject("Recoil backstop");stone.transform.SetParent(root.transform);
            stone.transform.position=new Vector3(dir.x,0,dir.y)*board.cellSize*3;
            var tile=stone.AddComponent<GridTile>();tile.blocked=true;
            // A stone alone cannot anchor two floating bars.
            board.tiles=new[]{tile};board.magnets=new[]{near,far};
            Check(!(bool)Call(board,"StoneBackedRecoilPair",near,far,dir),"Unsupported pair recoiled");
            var shoreObject=new GameObject("Recoil support");shoreObject.transform.SetParent(root.transform);
            shoreObject.transform.position=near.transform.position;
            var shore=shoreObject.AddComponent<GridTile>();board.tiles=new[]{tile,shore};
            foreach(var pose in new[]{Quaternion.identity,Quaternion.Euler(0,0,90)}){
                near.geometry.rotation=pose;
                Check((bool)Call(board,"StoneBackedRecoilPair",near,far,dir),"Floating stone-backed pair failed: "+dir+" / "+pose);
            }
            // Two-cell bridge spans the gap from the player's shore to the backstop.
            shoreObject.transform.position=Vector3.zero;
            var deck=MagnetVisuals.Create(root.transform,near.transform.position,MagnetShape.Horseshoe,false,Quaternion.identity,board.cellSize,root.transform);
            // Deck queries intentionally ignore inactive objects; keep this deck active in the preview scene.
            deck.transform.SetParent(null,true);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(deck.gameObject,root.scene);
            deck.product=MagnetProduct.Bridge;deck.combined=true;deck.walkable=true;deck.bridgeDirection=dir;
            board.magnets=new[]{near,far,deck};
            Check((bool)Call(board,"StoneBackedRecoilPair",near,far,dir),"Connected bridge did not support recoil");
            deck.bridgeDirection=new Vector2Int(-dir.y,dir.x);
            Check(!(bool)Call(board,"StoneBackedRecoilPair",near,far,dir),"Crosswise bridge supported recoil");
            deck.bridgeDirection=dir;board.tiles=new[]{tile};
            Check(!(bool)Call(board,"StoneBackedRecoilPair",near,far,dir),"Unconnected bridge supported recoil");
            // A single connected bar span can support just the near magnet.
            deck.product=MagnetProduct.None;deck.shape=MagnetShape.Bar;
            deck.geometry.rotation=Quaternion.FromToRotation(Vector3.right,new Vector3(dir.x,0,dir.y));
            var endObject=new GameObject("Bar span end");endObject.transform.SetParent(root.transform);endObject.transform.position=far.transform.position;
            var end=endObject.AddComponent<GridTile>();board.tiles=new[]{tile,shore,end};
            near.transform.position+=Vector3.up*.24f;
            Check((bool)Call(board,"RecoilSupported",near,dir),"Connected bar did not support recoil");
            Check(!(bool)Call(board,"RecoilSupported",near,new Vector2Int(-dir.y,dir.x)),"Crosswise bar supported recoil");
            near.transform.position-=Vector3.up*.24f;
            deck.gameObject.SetActive(false);endObject.SetActive(false);board.magnets=new[]{near,far};
            shoreObject.transform.position=near.transform.position;board.tiles=new[]{tile,shore};
            far.north=false;
            Check(!(bool)Call(board,"StoneBackedRecoilPair",near,far,dir),"Opposite poles incorrectly triggered recoil");
            far.north=true;tile.blocked=false;
            Check(!(bool)Call(board,"StoneBackedRecoilPair",near,far,dir),"Missing stone incorrectly triggered recoil");
            tile.blocked=true;far.transform.position+=Vector3.up;
            Check(!(bool)Call(board,"StoneBackedRecoilPair",near,far,dir),"Different heights incorrectly triggered recoil");
            near.gameObject.SetActive(false);far.gameObject.SetActive(false);stone.SetActive(false);shoreObject.SetActive(false);
        }
    }
    static void ValidateLocalRecoil(GridPlayground board,GameObject root){
        foreach(var direction in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            board.tiles=new GridTile[8];board.magnets=new MagnetPiece[0];
            for(int i=0;i<8;i++){
                var tile=new GameObject("Recoil tile");tile.transform.SetParent(root.transform);
                tile.transform.position=new Vector3(direction.x*(i-3)*board.cellSize,0,direction.y*(i-3)*board.cellSize);
                board.tiles[i]=tile.AddComponent<GridTile>();board.tiles[i].blocked=i==0||i>=6;
            }
            var piece=new GameObject("Recoil magnet");piece.transform.SetParent(root.transform);
            piece.transform.position=new Vector3(-direction.x*board.cellSize,0,-direction.y*board.cellSize);
            var magnet=piece.AddComponent<MagnetPiece>();magnet.geometry=new GameObject("Geometry").transform;magnet.geometry.SetParent(piece.transform,false);
            board.magnets=new[]{magnet};board.player.position=Vector3.zero;board.CaptureInitialState();
            object[] query={magnet,Vector2Int.zero,direction,Vector2Int.zero,Vector2Int.zero};
            Check((bool)Call(board,"FindLocalRecoilStop",query),"Same-island stone was ignored");
            Check((Vector2Int)query[3]==direction*2&&(Vector2Int)query[4]==direction,"Incorrect local recoil cells");
            Call(board,"RecordHistory",Call(board,"SaveWorld"));
            var motion=(IEnumerator)Call(board,"RecoilIntoObstacle",magnet,direction,(Vector2Int)query[3],(Vector2Int)query[4],Vector3.zero,magnet.transform.position,Quaternion.LookRotation(new Vector3(-direction.x,0,-direction.y)));
            int guard=300;while(motion.MoveNext())Check(--guard>0,"Local recoil never finished");
            Check(board.PlayerCell==direction*2&&!board.Busy&&!board.RecoilFlying,"Local recoil failed to unlock at stone");
            Check(Vector3.Distance(magnet.transform.position,new Vector3(direction.x*board.cellSize,0,direction.y*board.cellSize))<.001f,"Released magnet missed chest-side cell");
            Check(board.UndoStep()&&board.PlayerCell==Vector2Int.zero,"Local recoil undo failed");
            Check(Vector3.Distance(magnet.transform.position,new Vector3(-direction.x*board.cellSize,0,-direction.y*board.cellSize))<.001f,"Recoil undo lost magnet");
            board.tiles[6].blocked=board.tiles[7].blocked=false;
            Check(!(bool)Call(board,"FindLocalRecoilStop",query),"Open floor unexpectedly stopped recoil");
            board.tiles[5].surfaceHeight=.25f;
            Check((bool)Call(board,"FindLocalRecoilStop",query)&&(Vector2Int)query[3]==direction,"Raised ground did not stop recoil before the higher cell");
            board.tiles[5].surfaceHeight=0;
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.SetParent(root.transform);
            var obstaclePiece=obstacle.AddComponent<MagnetPiece>();obstaclePiece.geometry=obstacle.transform;
            board.magnets=new[]{magnet,obstaclePiece};
            foreach(float topHeight in new[]{.12f,1.5f}){
                obstacle.transform.position=new Vector3(direction.x*2*board.cellSize,topHeight*.5f,direction.y*2*board.cellSize);
                obstacle.transform.localScale=new Vector3(.5f,topHeight,.5f);
                Check((bool)Call(board,"FindLocalRecoilStop",query)&&(Vector2Int)query[3]==direction,"Magnet height did not block recoil: "+topHeight);
                Check(!(bool)Call(board,"RecoilCellBlocksFlight",direction*2,topHeight+.1f),"An obstacle below flight height incorrectly blocked flight");
            }
            obstacle.SetActive(false);board.magnets=new[]{magnet};
            board.tiles[6].blocked=true;
            var gap=board.tiles[4];board.tiles[4]=null;gap.gameObject.SetActive(false);board.CaptureInitialState();
            Check((bool)Call(board,"FindLocalRecoilStop",query)&&(Vector2Int)query[3]==direction*2&&(Vector2Int)query[4]==direction,
                "A floor gap hid the stone beyond it or rejected the hovering magnet cell");
            motion=(IEnumerator)Call(board,"RecoilIntoObstacle",magnet,direction,direction*2,direction,Vector3.zero,magnet.transform.position,Quaternion.LookRotation(new Vector3(-direction.x,0,-direction.y)));
            guard=300;while(motion.MoveNext())Check(--guard>0,"Gap-crossing recoil never finished");
            Check(board.PlayerCell==direction*2&&Mathf.Abs(magnet.transform.position.y)<.001f,"Gap recoil missed floor or changed magnet hover height");
            board.player.position=Vector3.zero;magnet.transform.position=new Vector3(-direction.x*board.cellSize,0,-direction.y*board.cellSize);
            var stopGap=board.tiles[5];board.tiles[5]=null;stopGap.gameObject.SetActive(false);board.CaptureInitialState();
            Check((bool)Call(board,"FindLocalRecoilStop",query),"Unsupported impact cell cancelled obstacle detection");
            motion=(IEnumerator)Call(board,"RecoilIntoObstacle",magnet,direction,direction*2,direction,Vector3.zero,magnet.transform.position,Quaternion.LookRotation(new Vector3(-direction.x,0,-direction.y)));
            guard=300;while(motion.MoveNext())Check(--guard>0,"Airborne recoil never recovered");
            Check(board.PlayerCell==Vector2Int.zero&&!board.Busy,"Airborne impact left player stranded above missing floor");
            board.tiles[5]=stopGap;stopGap.gameObject.SetActive(true);
            foreach(var shape in new[]{MagnetShape.Bar,MagnetShape.Horseshoe})foreach(bool blocked in new[]{false,true}){
                board.tiles[6].blocked=blocked;
                board.player.position=Vector3.zero;
                var cargoOrigin=new Vector3(-direction.x*board.cellSize,0,-direction.y*board.cellSize);
                magnet.transform.position=cargoOrigin;
                var target=MagnetVisuals.Create(root.transform,new Vector3(direction.x*2*board.cellSize,0,direction.y*2*board.cellSize),shape,true,Quaternion.identity,board.cellSize,root.transform);
                board.magnets=new[]{magnet,target};board.CaptureInitialState();
                var initialPose=target.Pose;float degrees;
                var expectedPose=MagnetPiece.Rolled(shape,initialPose,direction,out degrees);
                Check((bool)Call(board,"CanRecoilRoll",target,direction,0f)==!blocked,"Impact roll clearance incorrect");
                Call(board,"RecordHistory",Call(board,"SaveWorld"));
                motion=(IEnumerator)Call(board,"RecoilIntoMagnet",magnet,target,Vector2Int.zero,direction,Vector3.zero,cargoOrigin,Quaternion.LookRotation(new Vector3(-direction.x,0,-direction.y)));
                guard=600;while(motion.MoveNext())Check(--guard>0,"Magnet impact never settled");
                Check(board.PlayerCell==(blocked?Vector2Int.zero:direction*2)&&!board.Busy,"Magnet impact player landing incorrect");
                var expectedCargo=blocked?cargoOrigin:new Vector3(direction.x*board.cellSize,0,direction.y*board.cellSize);
                Check(Vector3.Distance(magnet.transform.position,expectedCargo)<.001f,"Impact lost carried magnet");
                Check(Quaternion.Angle(target.Pose,blocked?initialPose:expectedPose)<.01f,"Struck magnet ended in wrong orientation");
                Check(board.UndoStep()&&board.PlayerCell==Vector2Int.zero&&Vector3.Distance(magnet.transform.position,cargoOrigin)<.001f,"Impact undo lost original state");
                Check(Quaternion.Angle(target.Pose,initialPose)<.01f,"Impact undo lost target pose");
                target.gameObject.SetActive(false);
            }
        }
    }
}
}
