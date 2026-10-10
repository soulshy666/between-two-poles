using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    readonly Dictionary<Vector2Int,Transform> junctionLinks=new Dictionary<Vector2Int,Transform>();
    readonly HashSet<Vector2Int> liveJunctionLinks=new HashSet<Vector2Int>();
    readonly List<Vector2Int> expiredJunctionLinks=new List<Vector2Int>();
    MaterialPropertyBlock junctionProperties;
    void UpdateBridgeJunctionLinks(){
        liveJunctionLinks.Clear();
        for(int i=0;i<magnets.Length;i++){
            var a=magnets[i];if(!JunctionBar(a))continue;
            for(int j=i+1;j<magnets.Length;j++){
                var b=magnets[j];if(!JunctionBar(b)||(!GapWideBar(a)&&!GapWideBar(b)))continue;
                var from=Cell(a.transform);var to=Cell(b.transform);var dir=to-from;
                if(Mathf.Abs(dir.x)+Mathf.Abs(dir.y)!=1||AlongDeck(a,dir)==AlongDeck(b,dir)
                    ||!SameHeight(DeckHeight(a),DeckHeight(b))||!BridgePassage(from,to))continue;
                if(MagnetStillAnimating(a)||MagnetStillAnimating(b))continue;
                var key=new Vector2Int(a.GetInstanceID(),b.GetInstanceID());liveJunctionLinks.Add(key);
                var direction=new Vector3(dir.x,0,dir.y);
                float start=AlongDeck(a,dir)?cellSize*.5f:.24f;
                float finish=cellSize-(AlongDeck(b,dir)?cellSize*.5f:.24f);
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
                var source=a.geometry.GetComponentInChildren<Renderer>();
                if(source){
                    if(junctionProperties==null)junctionProperties=new MaterialPropertyBlock();
                    source.GetPropertyBlock(junctionProperties);
                    foreach(var renderer in link.GetComponentsInChildren<Renderer>()){
                        renderer.enabled=source.enabled;renderer.SetPropertyBlock(junctionProperties);
                        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }
        }
        expiredJunctionLinks.Clear();
        foreach(var pair in junctionLinks)if(!liveJunctionLinks.Contains(pair.Key)){
            if(pair.Value){pair.Value.gameObject.SetActive(false);Destroy(pair.Value.gameObject);}expiredJunctionLinks.Add(pair.Key);
        }
        foreach(var key in expiredJunctionLinks)junctionLinks.Remove(key);
    }
    bool JunctionBar(MagnetPiece m){return m&&m.enabled&&m.gameObject.activeInHierarchy&&m.product==MagnetProduct.WideBar&&m.walkable&&m.geometry;}
    void ClearBridgeJunctionLinks(){
        foreach(var link in junctionLinks.Values)if(link){link.gameObject.SetActive(false);Destroy(link.gameObject);}
        junctionLinks.Clear();
    }
}
}
