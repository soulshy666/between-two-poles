using UnityEngine;
using System.Collections.Generic;

namespace BetweenPoles {
    // Connects presentation across two rigid island frames, without changing the logical grid.
    [ExecuteAlways, DefaultExecutionOrder(200)]
    public sealed class PrejoinedTestBridges : MonoBehaviour {
        [System.Serializable] public class Link {
            public GridTile shoreA, shoreB;
            public MagnetPiece magnet;
            public Renderer[] renderers;
        }
        public GridPlayground board;
        public Link[] links;
        MaterialPropertyBlock block;
        Renderer[] playerRenderers;
        Transform cachedPlayer;
        int playerChildren;
        sealed class PieceView {
            public Transform geometry,parent;
            public int revision,children;
            public Renderer[] renderers;
            public MagnetPiece[] ancestors;
        }
        readonly Dictionary<MagnetPiece,PieceView> pieceViews=new Dictionary<MagnetPiece,PieceView>();
        MagnetPiece[] cachedMagnets,cachedSlots;
        sealed class Span {
            public Link link;
            public Vector3 start,end,delta,crossingStart,crossingEnd;
            public float lengthSquared;
            public IslandSurfaceAnchor shoreA,shoreB;
            public Vector4 islandA,islandB,offsetA,offsetB;
        }
        readonly List<Span> spans=new List<Span>();
        readonly Dictionary<Link,Span> spanByLink=new Dictionary<Link,Span>();
        readonly Dictionary<Vector2Int,List<Span>> cells=new Dictionary<Vector2Int,List<Span>>();
        readonly List<Span> largeSpans=new List<Span>();
        float bucketSize;
        static readonly int EnabledId=Shader.PropertyToID("_BridgeEnabled"),StartId=Shader.PropertyToID("_BridgeStart"),EndId=Shader.PropertyToID("_BridgeEnd"),
            IslandAId=Shader.PropertyToID("_BridgeIslandA"),IslandBId=Shader.PropertyToID("_BridgeIslandB"),
            OffsetAId=Shader.PropertyToID("_BridgeOffsetA"),OffsetBId=Shader.PropertyToID("_BridgeOffsetB");
        static readonly Unity.Profiling.ProfilerMarker refreshMarker=new Unity.Profiling.ProfilerMarker("Islands.BridgePresentation");
        // Authored gap connectors are flush with their shores. Puzzle products retain their height.
        public bool TryGetShoreLevel(MagnetPiece magnet,out float height){
            height=0;
            if(!magnet||!magnet.combined||!magnet.walkable||magnet.product!=MagnetProduct.None||links==null)return false;
            foreach(var link in links){
                if(link==null||link.magnet!=magnet||!link.shoreA||!link.shoreB)continue;
                if(Mathf.Abs(link.shoreA.surfaceHeight-link.shoreB.surfaceHeight)>.001f)return false;
                height=link.shoreA.surfaceHeight;return true;
            }
            return false;
        }
        void OnEnable() { playerRenderers=null; }
        void Set(Renderer r, Link link) {
            if(!r)return;
            if(block==null)block=new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            // Read the live block: other presentation systems may have changed it.
            // Preserve their fields and skip only genuinely redundant native writes.
            if(link==null){
                if(block.GetFloat(EnabledId)==0)return;
                block.SetFloat(EnabledId,0);
            }else {
                var span=spanByLink[link];
                if(block.GetFloat(EnabledId)==1&&block.GetVector(StartId).Equals((Vector4)span.start)&&
                    block.GetVector(EndId).Equals((Vector4)span.end)&&block.GetVector(IslandAId).Equals(span.islandA)&&
                    block.GetVector(IslandBId).Equals(span.islandB)&&block.GetVector(OffsetAId).Equals(span.offsetA)&&
                    block.GetVector(OffsetBId).Equals(span.offsetB))return;
                block.SetFloat(EnabledId,1);block.SetVector(StartId,span.start);block.SetVector(EndId,span.end);
                block.SetVector(IslandAId,span.islandA);block.SetVector(IslandBId,span.islandB);
                block.SetVector(OffsetAId,span.offsetA);block.SetVector(OffsetBId,span.offsetB);
            }
            r.SetPropertyBlock(block);
        }
        static void Shore(IslandSurfaceAnchor binding,Vector4 focus,out Vector4 island,out Vector4 offset){
            if(!binding||!binding.center){island=offset=Vector4.zero;return;}
            Vector3 center=binding.center.position;
            float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(Vector2.Distance(new Vector2(center.x,center.z),new Vector2(focus.x,focus.z))/6));
            island=new Vector4(center.x,center.y,center.z,binding.flatten*weight);
            offset=binding.visualOffset*weight;
        }
        Vector2Int Bucket(Vector3 p){return new Vector2Int(Mathf.FloorToInt(p.x/bucketSize),Mathf.FloorToInt(p.z/bucketSize));}
        void RefreshSpans(){
            float size=Mathf.Max(1,Mathf.Abs(board.cellSize)*4);bool changed=size!=bucketSize;bucketSize=size;
            int index=0;
            var focus=Shader.GetGlobalVector("_IceFocus");
            foreach(var link in links){
                if(link==null||!link.shoreA||!link.shoreB||!link.magnet)continue;
                if(index==spans.Count){spans.Add(new Span());changed=true;}
                var span=spans[index++];
                Vector3 a=link.shoreA.transform.position,b=link.shoreB.transform.position;
                var direction=(b-a).normalized;
                var start=a+direction*(board.cellSize*.5f);var end=b-direction*(board.cellSize*.5f);
                if(span.link!=link||!span.start.Equals(start)||!span.end.Equals(end))changed=true;
                span.link=link;span.start=start;span.end=end;
                // Crossing historically projects the shores to XZ before trimming.
                // Keep that exact rule, even for shores with different heights.
                a.y=b.y=0;direction=(b-a).normalized;
                var crossingStart=a+direction*(board.cellSize*.5f);var crossingEnd=b-direction*(board.cellSize*.5f);
                if(!span.crossingStart.Equals(crossingStart)||!span.crossingEnd.Equals(crossingEnd))changed=true;
                span.crossingStart=crossingStart;span.crossingEnd=crossingEnd;
                span.delta=crossingEnd-crossingStart;span.lengthSquared=span.delta.sqrMagnitude;
                // Resolve once per bridge, not once per renderer or transported magnet.
                span.shoreA=link.shoreA.GetComponentInParent<IslandSurfaceAnchor>();
                span.shoreB=link.shoreB.GetComponentInParent<IslandSurfaceAnchor>();
                Shore(span.shoreA,focus,out span.islandA,out span.offsetA);Shore(span.shoreB,focus,out span.islandB,out span.offsetB);
            }
            if(index!=spans.Count){spans.RemoveRange(index,spans.Count-index);changed=true;}
            if(!changed)return;
            cells.Clear();largeSpans.Clear();spanByLink.Clear();
            foreach(var span in spans){
                spanByLink[span.link]=span;
                var a=span.crossingStart;var b=span.crossingEnd;
                var min=Bucket(Vector3.Min(a,b)-new Vector3(.6f,0,.6f));var max=Bucket(Vector3.Max(a,b)+new Vector3(.6f,0,.6f));
                if(((long)max.x-min.x+1)*((long)max.y-min.y+1)>4096){largeSpans.Add(span);continue;}
                for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++){
                    var key=new Vector2Int(x,y);if(!cells.TryGetValue(key,out var list)){list=new List<Span>();cells.Add(key,list);}list.Add(span);
                }
            }
        }
        static bool Contains(Span span,Vector3 position){
            if(span.lengthSquared<.001f)return false;
            var a=span.crossingStart;position.y=0;
            float t=Vector3.Dot(position-a,span.delta)/span.lengthSquared;
            return t>0&&t<1&&(position-Vector3.Lerp(a,span.crossingEnd,t)).sqrMagnitude<.36f;
        }
        Link Crossing(Vector3 position){
            // Preserve authored ordering where bridge spans overlap.
            Span found=null;
            if(cells.TryGetValue(Bucket(position),out var candidates))foreach(var span in candidates)if(Contains(span,position)){found=span;break;}
            foreach(var span in largeSpans)if(Contains(span,position)&&(found==null||spans.IndexOf(span)<spans.IndexOf(found))){found=span;break;}
            return found==null?null:found.link;
        }
        public void Refresh() {
            if(!board||!board.player||links==null)return;
            using(refreshMarker.Auto()){
            RefreshSpans();RefreshPieceViews();
            foreach(var link in links) {
                if(link==null||!link.shoreA||!link.shoreB||!link.magnet)continue;
                foreach(var r in link.renderers)Set(r,link);
            }
            if(playerRenderers==null||cachedPlayer!=board.player||playerChildren!=board.player.childCount){cachedPlayer=board.player;playerChildren=cachedPlayer.childCount;playerRenderers=cachedPlayer.GetComponentsInChildren<Renderer>(true);}
            var crossing=Crossing(board.player.position);
            foreach(var r in playerRenderers)Set(r,crossing);
            // Transported magnets use the same inter-island projection as the
            // player, instead of remaining bent around their departure island.
            foreach(var magnet in board.magnets){
                if(!magnet||!magnet.enabled||!magnet.gameObject.activeInHierarchy||!magnet.CanBePushed)continue;
                var span=Crossing(magnet.transform.position);
                foreach(var r in pieceViews[magnet].renderers)if(r&&r.gameObject.activeInHierarchy)Set(r,span);
            }
            }
        }
        void RefreshPieceViews(){
            bool replaced=cachedMagnets!=board.magnets||cachedSlots==null||cachedSlots.Length!=board.magnets.Length;
            if(!replaced)for(int i=0;i<cachedSlots.Length;i++)if(!ReferenceEquals(cachedSlots[i],board.magnets[i])){replaced=true;break;}
            if(replaced){pieceViews.Clear();cachedMagnets=board.magnets;cachedSlots=(MagnetPiece[])board.magnets.Clone();}
            foreach(var m in board.magnets){
                if(!m)continue;
                if(!pieceViews.TryGetValue(m,out var view)){view=new PieceView();pieceViews.Add(m,view);}
                int children=m.geometry?m.geometry.childCount:0;
                if(!Application.isPlaying||view.geometry!=m.geometry||view.revision!=m.VisualHierarchyRevision||view.children!=children||view.parent!=m.transform.parent){
                    // Only this piece and its previous/current assembled parents can
                    // contain the changed renderers. Do not rescan the whole chapter.
                    if(view.ancestors!=null)foreach(var ancestor in view.ancestors)
                        if(ancestor&&pieceViews.TryGetValue(ancestor,out var owner))owner.renderers=null;
                    var ancestors=new List<MagnetPiece>();
                    for(var parent=m.transform.parent;parent;parent=parent.parent){
                        var ancestor=parent.GetComponent<MagnetPiece>();if(!ancestor)continue;
                        ancestors.Add(ancestor);if(pieceViews.TryGetValue(ancestor,out var owner))owner.renderers=null;
                    }
                    view.ancestors=ancestors.ToArray();view.parent=m.transform.parent;
                    view.geometry=m.geometry;view.revision=m.VisualHierarchyRevision;view.children=children;view.renderers=null;
                }
            }
            foreach(var pair in pieceViews)if(pair.Key&&pair.Value.renderers==null)pair.Value.renderers=pair.Key.GetComponentsInChildren<Renderer>(true);
        }
        void LateUpdate(){Refresh();}
        void OnDisable(){if(playerRenderers!=null)foreach(var r in playerRenderers)Set(r,null);if(links!=null)foreach(var link in links)if(link!=null&&link.renderers!=null)foreach(var r in link.renderers)Set(r,null);if(board&&board.magnets!=null)foreach(var magnet in board.magnets)if(magnet&&magnet.CanBePushed)foreach(var r in magnet.GetComponentsInChildren<Renderer>())Set(r,null);}
    }
}
