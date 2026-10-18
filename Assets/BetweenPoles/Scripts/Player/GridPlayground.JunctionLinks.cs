using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    readonly Dictionary<Vector2Int,Transform> junctionLinks=new Dictionary<Vector2Int,Transform>();
    readonly HashSet<Vector2Int> liveJunctionLinks=new HashSet<Vector2Int>();
    readonly List<Vector2Int> expiredJunctionLinks=new List<Vector2Int>();
    MaterialPropertyBlock junctionProperties;
    readonly List<(MagnetPiece piece,Transform geometry,Vector3 position,Quaternion pose,int flags,Vector2Int direction,bool animating)> junctionInputs=new List<(MagnetPiece,Transform,Vector3,Quaternion,int,Vector2Int,bool)>();
    int junctionTerrainRevision=-1;
    GridTile[] junctionTiles;
    float junctionCellSize;
    readonly Dictionary<Vector2Int,MagnetPiece> junctionSources=new Dictionary<Vector2Int,MagnetPiece>();
    struct JunctionCandidate {public MagnetPiece piece;public Vector2Int cell;public bool ring;}
    readonly List<JunctionCandidate> junctionCandidates=new List<JunctionCandidate>();
    bool JunctionInputsChanged(){
        bool changed=junctionTerrainRevision!=connectionTerrainRevision||junctionTiles!=tiles||junctionCellSize!=cellSize||junctionInputs.Count!=magnets.Length;
        junctionTerrainRevision=connectionTerrainRevision;junctionTiles=tiles;junctionCellSize=cellSize;
        for(int i=0;i<magnets.Length;i++){
            var m=magnets[i];
            var state=m?(m,m.geometry,m.transform.position,m.geometry?m.Pose:Quaternion.identity,
                ((int)m.product<<8)|((int)m.shape<<5)|(m.enabled?1:0)|(m.gameObject.activeInHierarchy?2:0)|(m.walkable?4:0)|(m.combined?8:0),m.bridgeDirection,MagnetStillAnimating(m))
                :(null,(Transform)null,Vector3.zero,Quaternion.identity,0,Vector2Int.zero,false);
            if(i>=junctionInputs.Count){junctionInputs.Add(state);changed=true;}
            else if(!junctionInputs[i].Equals(state)){junctionInputs[i]=state;changed=true;}
        }
        if(junctionInputs.Count>magnets.Length)junctionInputs.RemoveRange(magnets.Length,junctionInputs.Count-magnets.Length);
        return changed;
    }
    void RefreshJunctionAppearance(Vector2Int key,Transform link){
        if(!link||!junctionSources.TryGetValue(key,out var a)||!a||!a.geometry)return;
        var source=a.geometry.GetComponentInChildren<Renderer>();if(!source)return;
        if(junctionProperties==null)junctionProperties=new MaterialPropertyBlock();
        source.GetPropertyBlock(junctionProperties);
        foreach(var renderer in link.GetComponentsInChildren<Renderer>()){
            if(renderer.enabled!=source.enabled)renderer.enabled=source.enabled;
            renderer.SetPropertyBlock(junctionProperties);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
    void UpdateBridgeJunctionLinks(){
        if(!JunctionInputsChanged()){
            // Shader island frames still change as the camera moves. Only topology is cached.
            foreach(var pair in junctionLinks)RefreshJunctionAppearance(pair.Key,pair.Value);
            return;
        }
        liveJunctionLinks.Clear();
        junctionCandidates.Clear();
        for(int i=0;i<magnets.Length;i++){
            var piece=magnets[i];bool bar=JunctionBar(piece),ring=!bar&&RingShore(piece);
            if(bar||ring)junctionCandidates.Add(new JunctionCandidate{piece=piece,cell=Cell(piece.transform),ring=ring});
        }
        // Classify floating rings once. While carrying any magnet the input
        // changes every frame; do not repeat whole-board support scans per pair.
        for(int i=0;i<junctionCandidates.Count;i++){
            var first=junctionCandidates[i];var a=first.piece;
            for(int j=i+1;j<junctionCandidates.Count;j++){
                var second=junctionCandidates[j];var b=second.piece;
                var from=first.cell;var to=second.cell;var dir=to-from;
                if(Mathf.Abs(dir.x)+Mathf.Abs(dir.y)!=1||(!GapWideBar(a)&&!GapWideBar(b)))continue;
                bool ringA=first.ring,ringB=second.ring;
                bool sockets=ringA?AlongDeck(b,dir):ringB?AlongDeck(a,dir):AlongDeck(a,dir)!=AlongDeck(b,dir);
                if(!sockets
                    ||!SameHeight(DeckHeight(a),DeckHeight(b))||!BridgePassage(from,to))continue;
                if(MagnetStillAnimating(a)||MagnetStillAnimating(b))continue;
                var key=new Vector2Int(a.GetInstanceID(),b.GetInstanceID());liveJunctionLinks.Add(key);
                var direction=new Vector3(dir.x,0,dir.y);
                float start=ringA?.48f:AlongDeck(a,dir)?cellSize*.5f:.24f;
                float finish=cellSize-(ringB?.48f:AlongDeck(b,dir)?cellSize*.5f:.24f);
                Transform link;
                if(!junctionLinks.TryGetValue(key,out link)||!link){
                    link=new GameObject("宽条桥接缝电磁波").transform;
                    link.SetParent(a.transform,false);
                    foreach(float lane in new[]{-.12f,.12f})
                        MagnetVisuals.MagneticLink(link,lane,start,finish).GetComponent<MagneticLinkPulse>().horizontalOnly=true;
                    junctionLinks[key]=link;
                }
                // The bars are .24 high below their deck. Center the current in
                // that thickness; its local wave baseline is .16 above the root.
                link.position=new Vector3(from.x*cellSize,DeckHeight(a)-.28f,from.y*cellSize);
                link.rotation=Quaternion.FromToRotation(Vector3.right,direction);
                // Inherit the source island's bending, clipping and visibility.
                junctionSources[key]=a;RefreshJunctionAppearance(key,link);
            }
        }
        expiredJunctionLinks.Clear();
        foreach(var pair in junctionLinks)if(!liveJunctionLinks.Contains(pair.Key)){
            if(pair.Value){pair.Value.gameObject.SetActive(false);Destroy(pair.Value.gameObject);}expiredJunctionLinks.Add(pair.Key);
        }
        foreach(var key in expiredJunctionLinks){junctionLinks.Remove(key);junctionSources.Remove(key);}
    }
    bool JunctionBar(MagnetPiece m){return m&&m.enabled&&m.gameObject.activeInHierarchy&&m.product==MagnetProduct.WideBar&&m.walkable&&m.geometry;}
    void ClearBridgeJunctionLinks(){
        foreach(var link in junctionLinks.Values)if(link){link.gameObject.SetActive(false);Destroy(link.gameObject);}
        junctionLinks.Clear();
        junctionSources.Clear();junctionInputs.Clear();junctionTerrainRevision=-1;
        junctionCandidates.Clear();
    }
}
}
