using UnityEngine;

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
        void OnEnable() { if(board) playerRenderers=board.player.GetComponentsInChildren<Renderer>(true); }
        void Set(Renderer r, Link link) {
            if(!r)return;
            if(block==null)block=new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetFloat("_BridgeEnabled",link!=null?1:0);
            if(link!=null) {
                Vector3 direction=(link.shoreB.transform.position-link.shoreA.transform.position).normalized;
                block.SetVector("_BridgeStart",link.shoreA.transform.position+direction*(board.cellSize*.5f));
                block.SetVector("_BridgeEnd",link.shoreB.transform.position-direction*(board.cellSize*.5f));
                SetShore(link.shoreA,"A");SetShore(link.shoreB,"B");
            }
            r.SetPropertyBlock(block);
        }
        void SetShore(GridTile tile,string suffix){
            var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();Vector3 center=binding.center.position;
            Vector4 focus=Shader.GetGlobalVector("_IceFocus");
            float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(Vector2.Distance(new Vector2(center.x,center.z),new Vector2(focus.x,focus.z))/6));
            block.SetVector("_BridgeIsland"+suffix,new Vector4(center.x,center.y,center.z,binding.flatten*weight));
            block.SetVector("_BridgeOffset"+suffix,binding.visualOffset*weight);
        }
        Link Crossing(Vector3 position){
            foreach(var link in links){
                if(link==null||!link.shoreA||!link.shoreB||!link.magnet)continue;
                Vector3 a=link.shoreA.transform.position,b=link.shoreB.transform.position;
                a.y=b.y=position.y=0;
                Vector3 direction=(b-a).normalized;
                a+=direction*(board.cellSize*.5f);b-=direction*(board.cellSize*.5f);
                Vector3 delta=b-a;if(delta.sqrMagnitude<.001f)continue;
                float t=Vector3.Dot(position-a,delta)/delta.sqrMagnitude;
                if(t>0&&t<1&&(position-Vector3.Lerp(a,b,t)).sqrMagnitude<.36f)return link;
            }
            return null;
        }
        public void Refresh() {
            if(!board||links==null)return;
            foreach(var link in links) {
                if(link==null||!link.shoreA||!link.shoreB||!link.magnet)continue;
                foreach(var r in link.renderers)Set(r,link);
            }
            if(playerRenderers==null)playerRenderers=board.player.GetComponentsInChildren<Renderer>(true);
            var crossing=Crossing(board.player.position);
            foreach(var r in playerRenderers)Set(r,crossing);
            // Transported magnets use the same inter-island projection as the
            // player, instead of remaining bent around their departure island.
            foreach(var magnet in board.magnets){
                if(!magnet||!magnet.enabled||!magnet.gameObject.activeInHierarchy||!magnet.CanBePushed)continue;
                var span=Crossing(magnet.transform.position);
                foreach(var r in magnet.GetComponentsInChildren<Renderer>())Set(r,span);
            }
        }
        void LateUpdate(){Refresh();}
        void OnDisable(){if(playerRenderers!=null)foreach(var r in playerRenderers)Set(r,null);if(links!=null)foreach(var link in links)if(link!=null&&link.renderers!=null)foreach(var r in link.renderers)Set(r,null);if(board&&board.magnets!=null)foreach(var magnet in board.magnets)if(magnet&&magnet.CanBePushed)foreach(var r in magnet.GetComponentsInChildren<Renderer>())Set(r,null);}
    }
}
