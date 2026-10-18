using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    struct HoverCargoPlatform {
        public MagnetPiece piece;
        public Vector3 position;
        public float height,bob;
    }
    readonly List<HoverCargoPlatform> hoverCargoPlatforms=new List<HoverCargoPlatform>();
    readonly Dictionary<MagnetPiece,float> uLandingBounces=new Dictionary<MagnetPiece,float>();
    readonly List<MagnetPiece> expiredUBounces=new List<MagnetPiece>();
    void RenderULandingBounces(){
        expiredUBounces.Clear();
        float duration=UFlipDuration()*(1-UTouchdownPhase);
        foreach(var entry in uLandingBounces){
            var m=entry.Key;float p=(Time.time-entry.Value)/duration;
            if(!m||!m.geometry||!m.enabled||!m.gameObject.activeInHierarchy||!PlainU(m)||m.storedInPortal
                ||p>=1||pushHoverParticipants.Contains(m)||MagnetStillAnimating(m)||portalAnimatedMagnets.Contains(m)){
                expiredUBounces.Add(m);continue;
            }
            var geometry=m.geometry;
            if(!hoverRenderPositions.ContainsKey(geometry))hoverRenderPositions[geometry]=geometry.position;
            geometry.position+=Vector3.up*(cellSize*.035f*4*p*(1-p));
        }
        foreach(var m in expiredUBounces)uLandingBounces.Remove(m);
    }
    MagnetPiece blockedUNudge;
    Vector3 blockedUNudgeDirection;
    float blockedUNudgeOffset;
    public void BeginBlockedUNudge(MagnetPiece magnet,Vector2Int direction){
        blockedUNudge=magnet;blockedUNudgeDirection=new Vector3(direction.x,0,direction.y);blockedUNudgeOffset=0;
    }
    public void SampleBlockedUNudge(float time){
        float press=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/.25f));
        float release=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.65f,time));
        blockedUNudgeOffset=cellSize*.07f*press*release;
    }
    public void ClearBlockedUNudge(){RestoreMagnetHover(null);blockedUNudge=null;blockedUNudgeOffset=0;}
    void RenderBlockedUNudge(){
        if(!blockedUNudge||!blockedUNudge.geometry||!blockedUNudge.gameObject.activeInHierarchy||MagnetStillAnimating(blockedUNudge))return;
        var geometry=blockedUNudge.geometry;
        if(!hoverRenderPositions.ContainsKey(geometry))hoverRenderPositions[geometry]=geometry.position;
        geometry.position+=blockedUNudgeDirection*blockedUNudgeOffset;
    }
    const float BarRockSeconds=.95f;
    sealed class BarRock {public Vector3 axis;public float started,velocity;public bool touchdown,undoBounce;}
    readonly Dictionary<MagnetPiece,BarRock> barRocks=new Dictionary<MagnetPiece,BarRock>();
    readonly Dictionary<Transform,Quaternion> rockRenderRotations=new Dictionary<Transform,Quaternion>();
    static float BarRockAngle(float age,float velocity){
        float a=Mathf.Clamp01(age/BarRockSeconds);
        float frequency=3*Mathf.PI/BarRockSeconds;
        // Continue the lift's angular velocity through vertical, then dissipate it.
        return velocity/frequency*Mathf.Sin(age*frequency)*Mathf.Pow(1-a,2);
    }
    void StartBarRock(MagnetPiece m,Vector3 axis){barRocks[m]=new BarRock{axis=axis,started=Time.time,velocity=90*.53f*pushFlowSpeed};}
    static float BarTouchdownAngle(float age){
        if(age<0||age>=.52f)return 0;
        bool second=age>=.32f;
        float p=second?(age-.32f)/.2f:age/.32f;
        return -(second?2.5f:7f)*4*p*(1-p);
    }
    void StartBarTouchdown(MagnetPiece m,Vector3 axis,float age){
        barRocks[m]=new BarRock{axis=axis,started=Time.time-age,touchdown=true};
    }
    static float SettlingAngle(BarRock rock,float age){
        if(rock.undoBounce){
            if(age<0||age>=.7f)return 0;
            bool second=age>=.4f;
            float p=second?(age-.4f)/.3f:age/.4f;
            return -(second?4f:8f)*4*p*(1-p);
        }
        return rock.touchdown?BarTouchdownAngle(age):BarRockAngle(age,rock.velocity);
    }
    void StartUndoBarSettle(MagnetPiece m,Vector3 previous){
        if(!m||!m.geometry||!m.gameObject.activeInHierarchy||m.storedInPortal)return;
        if(PlainU(m)){
            // Undo has already restored the logical pose. Reuse the non-blocking
            // landing bounce, only for a flat U resting on a valid support.
            if(m.enabled&&MagnetPiece.FlatU(m.Pose)&&!BlackHolePortal.At(this,Cell(m.transform))&&Supported(m))
                uLandingBounces[m]=Time.time;
            return;
        }
        bool ring=m.product==MagnetProduct.Ring;
        if(!ring&&(m.combined||m.shape!=MagnetShape.Bar||m.product!=MagnetProduct.None))return;
        var tile=Tile(Cell(m.transform));if(!tile||tile.blocked||!SameHeight(tile.surfaceHeight,m.transform.position.y))return;
        bool standing=ring?!MagnetPiece.FlatU(m.Pose):MagnetPiece.VerticalBar(m.Pose);
        var direction=standing||ring?previous-m.transform.position:m.Pose*Vector3.right;direction.y=0;
        // An upright ring settles by tilting across its face, not by rolling
        // along its circumference when undoing a sideways push.
        if(ring&&standing){direction=m.Pose*Vector3.up;direction.y=0;}
        if(direction.sqrMagnitude<.001f){direction=m.Pose*Vector3.up;direction.y=0;}
        if(direction.sqrMagnitude<.001f)direction=Vector3.right;
        direction.Normalize();
        barRocks[m]=new BarRock{axis=Vector3.Cross(Vector3.up,direction),started=Time.time,
            velocity=90*.53f/Mathf.Max(.01f,barPushSeconds),touchdown=!standing,undoBounce=!standing};
    }
    float TakeBarRock(MagnetPiece m,Vector3 axis){
        if(!barRocks.TryGetValue(m,out var rock))return 0;
        barRocks.Remove(m);return SettlingAngle(rock,Time.time-rock.started)*Vector3.Dot(axis,rock.axis);
    }
    void ApplyBarRock(MagnetPiece m,Vector3 axis,float angle){
        // Rock about the contacting bottom edge, not the mesh centre. Switching
        // edges at zero tilt keeps the base planted without sinking into the floor.
        var bounds=AssemblyBounds(AssemblyBodies(m.geometry));
        var direction=Vector3.Cross(axis,Vector3.up).normalized;
        float radius=Mathf.Abs(direction.x)*bounds.extents.x+Mathf.Abs(direction.z)*bounds.extents.z;
        var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)+direction*(Mathf.Sign(angle)*radius);
        var turn=Quaternion.AngleAxis(angle,axis);
        m.geometry.position=pivot+turn*(m.geometry.position-pivot);
        m.geometry.rotation=turn*m.geometry.rotation;
    }
    readonly List<MagnetPiece> rockRenderKeys=new List<MagnetPiece>();
    void RenderBarRocks(){
        rockRenderKeys.Clear();
        foreach(var key in barRocks.Keys)rockRenderKeys.Add(key);
        foreach(var m in rockRenderKeys){
            var rock=barRocks[m];float age=Time.time-rock.started;
            if(!m||!m.geometry||!m.gameObject.activeInHierarchy||m.storedInPortal||(m.product!=MagnetProduct.None&&m.product!=MagnetProduct.Ring)
                ||(m.product==MagnetProduct.Ring?!MagnetPiece.FlatU(m.Pose):MagnetPiece.VerticalBar(m.Pose))==rock.touchdown||age>=(rock.undoBounce?.7f:rock.touchdown?.52f:BarRockSeconds)){barRocks.Remove(m);continue;}
            if(MagnetStillAnimating(m)||portalAnimatedMagnets.Contains(m))continue;
            var geometry=m.geometry;hoverRenderPositions[geometry]=geometry.position;rockRenderRotations[geometry]=geometry.rotation;
            ApplyBarRock(m,rock.axis,SettlingAngle(rock,age));
        }
    }
    const float MagnetHoverAmplitude=.09f,MagnetHoverPeriod=2.8f;
    readonly Dictionary<MagnetPiece,float> hoveringMagnets=new Dictionary<MagnetPiece,float>();
    readonly HashSet<MagnetPiece> pushHoverParticipants=new HashSet<MagnetPiece>();
    readonly Dictionary<Transform,Vector3> hoverRenderPositions=new Dictionary<Transform,Vector3>();
    readonly List<MagnetPiece> expiredHovers=new List<MagnetPiece>();
    readonly Dictionary<MagnetPiece,float> hoverLevels=new Dictionary<MagnetPiece,float>();
    FiveIslandWindow hoverWindow;
    bool NeedsHoverPresentation(MagnetPiece piece){
        if(portalMagnetPresentation||!Application.isPlaying)return true;
        if(!hoverWindow)TryGetComponent<FiveIslandWindow>(out hoverWindow);
        return !hoverWindow||hoverWindow.NeedsMagnetPresentation(piece.transform.position);
    }
    readonly Dictionary<Vector2Int,MagnetPiece> hoverNeighbors=new Dictionary<Vector2Int,MagnetPiece>();
    bool portalMagnetPresentation;
    int portalHoverFrame=-1;
    readonly HashSet<MagnetPiece> portalAnimatedMagnets=new HashSet<MagnetPiece>();
    public float PortalMagnetOffset(MagnetPiece magnet){
        IndexHoverNeighbors();
        float time=hoveringMagnets.TryGetValue(magnet,out var age)?age:0;
        return EdgeHoverLevel(magnet)+MagnetHoverAmplitude*Mathf.Sin(time*Mathf.PI*2/MagnetHoverPeriod)
            *Mathf.SmoothStep(0,1,Mathf.Clamp01(time/.4f));
    }
    public void AnimatePortalMagnet(MagnetPiece magnet,bool active){
        RestoreMagnetHover(null);
        if(active)portalAnimatedMagnets.Add(magnet);else portalAnimatedMagnets.Remove(magnet);
    }
    // Portal selection disables gameplay, but bridge geometry must retain the
    // same render-only shore alignment throughout navigation and exit playback.
    public void BeginPortalMagnetPresentation(){
        portalMagnetPresentation=true;
        Camera.onPreCull-=RenderMagnetHover;Camera.onPostRender-=RestoreMagnetHover;
        Camera.onPreCull+=RenderMagnetHover;Camera.onPostRender+=RestoreMagnetHover;
    }
    public void EndPortalMagnetPresentation(){
        portalMagnetPresentation=false;
        RestoreMagnetHover(null);
        if(!isActiveAndEnabled)ClearMagnetHover();
    }
    void IndexHoverNeighbors(){
        hoverNeighbors.Clear();
        foreach(var m in magnets){
            if(!m||!m.enabled||!m.gameObject.activeInHierarchy||MagnetFlightOwnsHeight(m))continue;
            var cell=Cell(m.transform);IndexHoverCell(cell,m);
            if(m.product==MagnetProduct.Bridge||m.product==MagnetProduct.BridgeHalf)IndexHoverCell(cell+m.bridgeDirection,m);
        }
    }
    void IndexHoverCell(Vector2Int cell,MagnetPiece m){
        hoverNeighbors.TryGetValue(cell,out var previous);
        if(!previous||m.transform.position.y>previous.transform.position.y+.01f
            ||(SameHeight(m.transform.position.y,previous.transform.position.y)&&previous.walkable&&!m.walkable))hoverNeighbors[cell]=m;
    }
    float EdgeHoverLevel(MagnetPiece m){
        if(ShoreLevelBridge(m))return DeckHeight(m)-MagnetGeometryTop(m);
        if(m.combined||m.product!=MagnetProduct.None)return 0;
        var cell=Cell(m.transform);
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            hoverNeighbors.TryGetValue(cell+dir,out var neighbor);
            if(neighbor&&!neighbor.combined&&neighbor.product==MagnetProduct.None&&neighbor.north==m.north
                &&SameHeight(neighbor.transform.position.y,m.transform.position.y)&&Supported(neighbor))return 0;
        }
        // Chapter meshes deliberately enlarge their culling bounds for the planet
        // shader. Measure actual vertices, never Renderer.bounds or Mesh.bounds.
        // Logical Y remains the shore level; only the displayed body sits lower.
        return m.transform.position.y-MagnetGeometryTop(m);
    }
    float MagnetGeometryTop(MagnetPiece m){
        float top=float.NegativeInfinity;
        foreach(var filter in m.geometry.GetComponentsInChildren<MeshFilter>()){
            if(!filter.sharedMesh)continue;
            var renderer=filter.GetComponent<Renderer>();if(renderer&&!renderer.enabled)continue;
            var vertices=MagnetVisuals.CachedBoundsVertices(filter.sharedMesh);
            // Read the transform once per mesh, not once for every vertex.
            var matrix=filter.transform.localToWorldMatrix;
            foreach(var vertex in vertices)top=Mathf.Max(top,matrix.m10*vertex.x+matrix.m11*vertex.y+matrix.m12*vertex.z+matrix.m13);
        }
        return float.IsNegativeInfinity(top)?m.transform.position.y:top;
    }
    void OnEnable(){
        Camera.onPreCull-=RenderMagnetHover;Camera.onPostRender-=RestoreMagnetHover;
        Camera.onPreCull+=RenderMagnetHover;Camera.onPostRender+=RestoreMagnetHover;
    }
    void OnDestroy(){ClearMagnetHover();}
    bool HoverPassage(MagnetPiece m){
        if(!m.walkable)return false;
        // Floating rings are stable, four-way floor cells, even at a single shore.
        if(SpaceRing(m)&&MagnetPiece.FlatU(m.Pose))return true;
        if(m.product==MagnetProduct.WideBar||m.product==MagnetProduct.None)return WideBarConnected(m);
        if(m.product==MagnetProduct.Bridge)
            return HoverEndConnected(m,m.bridgeDirection)&&HoverEndConnected(m,-m.bridgeDirection);
        if(m.product==MagnetProduct.Ring)
            return (HoverEndConnected(m,Vector2Int.right)&&HoverEndConnected(m,Vector2Int.left))
                ||(HoverEndConnected(m,Vector2Int.up)&&HoverEndConnected(m,Vector2Int.down));
        return false;
    }
    bool HoverEndConnected(MagnetPiece m,Vector2Int direction){
        var cell=Cell(m.transform);
        // Start beyond the outer socket of a two-cell bridge.
        while(m.Occupies(cell+direction,cellSize))cell+=direction;
        for(int i=0;i<=magnets.Length*2;i++){
            cell+=direction;var tile=Tile(cell);
            if(tile)return SameHeight(tile.surfaceHeight,m.transform.position.y)
                ||SameHeight(tile.surfaceHeight,DeckHeight(m));
            var deck=Deck(cell,m,true);
            if(!deck||!AlongDeck(deck,direction)||!SameHeight(DeckHeight(deck),DeckHeight(m))
                ||(deck.product!=MagnetProduct.WideBar&&deck.product!=MagnetProduct.Bridge
                    &&!(deck.product==MagnetProduct.None&&deck.combined&&deck.shape==MagnetShape.Bar)))return false;
        }
        return false;
    }
    // Airborne motion owns the whole displayed height, including gaps between islands.
    bool MagnetFlightOwnsHeight(MagnetPiece m){return (RecoilFlying&&RecoilMagnet==m)||IsKnockedFlying(m);}
    bool ShouldHover(MagnetPiece m){
        return m&&m.enabled&&m.gameObject.activeInHierarchy&&m.geometry&&m.geometry.gameObject.activeInHierarchy
            &&!MagnetFlightOwnsHeight(m)&&!Supported(m)&&(!HoverPassage(m)||FloatingRing(m))&&!MagnetStillAnimating(m);
    }
    bool FloatingRing(MagnetPiece m){return SpaceRing(m)&&m.walkable&&MagnetPiece.FlatU(m.Pose);}
    bool FloatingWidePlatform(MagnetPiece m){return m&&m.product==MagnetProduct.WideBar&&GapWideBar(m)&&m.walkable;}
    float RingRiderWeight(MagnetPiece ring){
        if(!player||BlackHoleTravel.InTransit||RecoilFlying)return 0;
        var delta=player.position-ring.transform.position;
        float distance=new Vector2(delta.x,delta.z).magnitude;
        float horizontal=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cellSize*.35f,cellSize,distance));
        float vertical=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,.3f,Mathf.Abs(delta.y)));
        return horizontal*vertical;
    }
    void RenderRingRider(float offset){
        if(!player||Mathf.Abs(offset)<.000001f)return;
        var visual=player.Find("Visual");if(!visual)return;
        if(!hoverRenderPositions.ContainsKey(visual))hoverRenderPositions[visual]=visual.position;
        visual.position+=Vector3.up*offset;
    }
    void UpdateMagnetHover(){
        RestoreMagnetHover(null);
        IndexHoverNeighbors();
        IndexHoverDecks();useHoverDecks=true;
        try{UpdateMagnetHoverIndexed();}finally{useHoverDecks=false;}
    }
    void UpdateMagnetHoverIndexed(){
        expiredHovers.Clear();
        foreach(var entry in hoveringMagnets)
            if(!entry.Key||!entry.Key.enabled||!entry.Key.gameObject.activeInHierarchy
                ||(NeedsHoverPresentation(entry.Key)&&!ShouldHover(entry.Key)))expiredHovers.Add(entry.Key);
        foreach(var m in expiredHovers){hoveringMagnets.Remove(m);hoverLevels.Remove(m);}
        foreach(var m in magnets){
            if(!m||!NeedsHoverPresentation(m)||!ShouldHover(m))continue;
            if(portalAnimatedMagnets.Contains(m))continue;
            if(!hoveringMagnets.ContainsKey(m))hoveringMagnets.Add(m,0);
            float target=EdgeHoverLevel(m);
            if(!hoverLevels.ContainsKey(m))hoverLevels[m]=0;
            hoverLevels[m]=Mathf.MoveTowards(hoverLevels[m],target,(portalMagnetPresentation?Time.unscaledDeltaTime:Time.deltaTime)*.8f);
            // Docking/rolling has priority; restart gently once the push settles.
            hoveringMagnets[m]=Busy&&interruptiblePush&&pushHoverParticipants.Contains(m)
                ?0:hoveringMagnets[m]+(portalMagnetPresentation?Time.unscaledDeltaTime:Time.deltaTime);
        }
    }
    void RenderMagnetHover(Camera camera){
        RestoreMagnetHover(null);
        if(!Application.isPlaying||!gameObject.activeInHierarchy||(!enabled&&!portalMagnetPresentation))return;
        if(portalMagnetPresentation&&!enabled&&portalHoverFrame!=Time.frameCount){
            portalHoverFrame=Time.frameCount;UpdateMagnetHover();
        }
        RenderBarRocks();
        RenderULandingBounces();
        RenderBlockedUNudge();
        float riderOffset=0,riderWeight=0;
        foreach(var entry in hoveringMagnets){
            var m=entry.Key;if(!m||!m.geometry||!m.gameObject.activeInHierarchy||!NeedsHoverPresentation(m)||portalAnimatedMagnets.Contains(m)||MagnetFlightOwnsHeight(m))continue;
            float t=entry.Value;
            float bob=MagnetHoverAmplitude*Mathf.Sin(t*Mathf.PI*2/MagnetHoverPeriod)
                *Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.4f));
            float offset=(hoverLevels.TryGetValue(m,out float level)?level:0)+bob;
            if(FloatingRing(m)||FloatingWidePlatform(m)){
                float weight=RingRiderWeight(m);riderOffset+=bob*weight;riderWeight+=weight;
            }
            var geometry=m.geometry;
            hoverRenderPositions[geometry]=geometry.position;
            geometry.position+=Vector3.up*offset;
        }
        // Riders use the platform's bob, not its geometry-to-shore alignment offset.
        // Roots retain logical grid heights for pushing, collision and undo.
        // Classify after all platform offsets are saved, using RingRestCentre's
        // original centres. Rebuild for each render so movement, undo and portal
        // changes cannot leave stale support/pose data between cameras or frames.
        hoverCargoPlatforms.Clear();
        foreach(var entry in hoveringMagnets){
            var platform=entry.Key;
            if(!platform||!NeedsHoverPresentation(platform))continue;
            if(!FloatingWidePlatform(platform)&&!FloatingRing(platform))continue;
            float t=entry.Value;
            hoverCargoPlatforms.Add(new HoverCargoPlatform{
                piece=platform,position=platform.transform.position,height=DeckHeight(platform),
                bob=MagnetHoverAmplitude*Mathf.Sin(t*Mathf.PI*2/MagnetHoverPeriod)*Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.4f))
            });
        }
        foreach(var cargo in magnets){
            if(!cargo||!cargo.enabled||!cargo.gameObject.activeInHierarchy||!cargo.geometry
                ||!NeedsHoverPresentation(cargo)
                ||hoveringMagnets.ContainsKey(cargo)||MagnetFlightOwnsHeight(cargo)||portalAnimatedMagnets.Contains(cargo))continue;
            float cargoBob=0,totalWeight=0;
            foreach(var platform in hoverCargoPlatforms){
                if(platform.piece==cargo)continue;
                var delta=cargo.transform.position-platform.position;
                if(!SameHeight(cargo.transform.position.y,platform.height))continue;
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cellSize*.35f,cellSize,new Vector2(delta.x,delta.z).magnitude));
                cargoBob+=weight*platform.bob;
                totalWeight+=weight;
            }
            if(totalWeight<=0)continue;
            if(!hoverRenderPositions.ContainsKey(cargo.geometry))hoverRenderPositions[cargo.geometry]=cargo.geometry.position;
            cargo.geometry.position+=Vector3.up*(cargoBob/Mathf.Max(1,totalWeight));
        }
        RenderRingRider(riderOffset/Mathf.Max(1,riderWeight));
        // Connected bridges do not bob, but their deck must still sit flush with
        // the shore. Keep this display offset out of assembly and undo data.
        foreach(var m in magnets){
            if(!m||!m.enabled||!m.gameObject.activeInHierarchy||!m.geometry
                ||!NeedsHoverPresentation(m)
                ||MagnetFlightOwnsHeight(m)||hoveringMagnets.ContainsKey(m)||!ShoreLevelBridge(m)||MagnetStillAnimating(m))continue;
            float offset=DeckHeight(m)-MagnetGeometryTop(m);
            hoverRenderPositions[m.geometry]=m.geometry.position;
            m.geometry.position+=Vector3.up*offset;
        }
    }
    // Render-only offsets never enter movement, assembly bounds or undo snapshots.
    void RestoreMagnetHover(Camera camera){
        foreach(var entry in rockRenderRotations)if(entry.Key)entry.Key.rotation=entry.Value;
        rockRenderRotations.Clear();
        foreach(var entry in hoverRenderPositions)if(entry.Key)entry.Key.position=entry.Value;
        hoverRenderPositions.Clear();
    }
    void ClearMagnetHover(){
        hoverCargoPlatforms.Clear();
        blockedUNudge=null;blockedUNudgeOffset=0;
        barRocks.Clear();
        portalMagnetPresentation=false;
        portalAnimatedMagnets.Clear();
        Camera.onPreCull-=RenderMagnetHover;Camera.onPostRender-=RestoreMagnetHover;
        RestoreMagnetHover(null);hoveringMagnets.Clear();hoverLevels.Clear();
    }
}
}
