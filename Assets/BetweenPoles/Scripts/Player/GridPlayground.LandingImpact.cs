using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    struct KnockFlight {
        public MagnetPiece magnet;
        public Vector2Int direction;
        public Vector3 start,end,offset;
        public float elapsed,duration;
        public bool lands;
    }
    struct KnockCheckpoint { public int index; public Snapshot piece; }
    readonly List<KnockFlight> knockedFlights=new List<KnockFlight>();
    readonly List<KnockCheckpoint> knockCheckpoints=new List<KnockCheckpoint>();
    static int Along(Vector2Int cell,Vector2Int direction){return cell.x*direction.x+cell.y*direction.y;}
    public bool IsKnockedFlying(MagnetPiece magnet){return knockedFlights.Exists(f=>f.magnet==magnet);}
    void KnockRendering(MagnetPiece magnet,bool flying,Vector3 offset){
        if(recoilBlock==null)recoilBlock=new MaterialPropertyBlock();
        foreach(var r in magnet.GetComponentsInChildren<Renderer>(true)){
            r.GetPropertyBlock(recoilBlock);recoilBlock.SetFloat("_RecoilFlying",flying?1:0);
            recoilBlock.SetVector("_RecoilDisplayOffset",offset);r.SetPropertyBlock(recoilBlock);
            if(flying){var bounds=r.localBounds;bounds.Encapsulate(new Bounds(Vector3.zero,Vector3.one*300));r.localBounds=bounds;}
        }
    }
    IEnumerable<Vector2Int> Footprint(MagnetPiece magnet,Vector2Int at){
        yield return at;
        if(magnet.product==MagnetProduct.Bridge||magnet.product==MagnetProduct.BridgeHalf)yield return at+magnet.bridgeDirection;
    }
    // Reserve both arrival cells before moving any struck magnets. Search all logical
    // islands, including those outside the current camera's presentation window.
    void LaunchLandingMagnets(MagnetPiece incoming,Vector2Int shore,Vector2Int direction){
        var reserved=new HashSet<Vector2Int>{shore,shore+direction};
        foreach(var cell in Footprint(incoming,shore))reserved.Add(cell);
        var struck=new List<MagnetPiece>();
        foreach(var m in magnets){
            if(!m||!m.enabled||!m.gameObject.activeInHierarchy||m==incoming||IsKnockedFlying(m))continue;
            foreach(var cell in reserved)if(m.Occupies(cell,cellSize)){struck.Add(m);break;}
        }
        // The foremost object moves first, so two occupied landing cells cannot overlap.
        struck.Sort((a,b)=>Along(Cell(b.transform),direction).CompareTo(Along(Cell(a.transform),direction)));
        var visited=new HashSet<MagnetPiece>();
        foreach(var m in struck)PlanKnock(m,direction,reserved,visited,incoming);
    }
    void PlanKnock(MagnetPiece magnet,Vector2Int direction,HashSet<Vector2Int> reserved,HashSet<MagnetPiece> visited,MagnetPiece incoming){
        if(!visited.Add(magnet))return;
        var from=Cell(magnet.transform);GridTile landing=null;int nearest=int.MaxValue;
        foreach(var tile in tiles){
            if(!tile||!tile.gameObject.activeInHierarchy||tile.blocked||IsWreckCell(Cell(tile.transform)))continue;
            var cell=Cell(tile.transform);var delta=cell-from;int along=Along(delta,direction);
            if(along<=0||along>=nearest||delta.x*direction.y!=delta.y*direction.x)continue;
            bool fits=true;
            foreach(var part in Footprint(magnet,cell)){
                var ground=Tile(part);
                if(reserved.Contains(part)||!ground||!ground.gameObject.activeInHierarchy||ground.blocked||IsWreckCell(part)){fits=false;break;}
            }
            if(fits){landing=tile;nearest=along;}
        }
        var start=magnet.transform.position;
        var end=landing?Position(Cell(landing.transform),landing.surfaceHeight):start+new Vector3(direction.x,0,direction.y)*cellSize;
        if(landing){
            foreach(var part in Footprint(magnet,Cell(landing.transform)))reserved.Add(part);
            // A further occupied cell transfers the impact onward as well.
            foreach(var other in magnets){
                if(!other||!other.enabled||!other.gameObject.activeInHierarchy||other==incoming||visited.Contains(other)||IsKnockedFlying(other))continue;
                foreach(var part in Footprint(magnet,Cell(landing.transform)))if(other.Occupies(part,cellSize)){PlanKnock(other,direction,reserved,visited,incoming);break;}
            }
        }
        var flight=new KnockFlight{magnet=magnet,direction=direction,start=start,end=end,offset=SurfaceOffset(start),lands=landing,duration=Mathf.Max(.16f,Vector3.Distance(start,end)/(cellSize*5f))};
        knockedFlights.Add(flight);KnockRendering(magnet,true,flight.offset);
    }
    void TickKnockedMagnets(){
        for(int i=knockedFlights.Count-1;i>=0;i--){
            var f=knockedFlights[i];if(!f.magnet){knockedFlights.RemoveAt(i);continue;}
            f.elapsed+=Time.deltaTime;
            if(!f.lands){
                // No tile on this line: the magnet keeps drifting; it does not restart
                // the player's island. Its authored island can still recall it with R.
                f.magnet.transform.position=f.start+new Vector3(f.direction.x,0,f.direction.y)*(f.elapsed*cellSize*5f);
                knockedFlights[i]=f;continue;
            }
            float t=Mathf.Clamp01(f.elapsed/f.duration);
            f.magnet.transform.position=Vector3.Lerp(f.start,f.end,t);
            KnockRendering(f.magnet,true,Vector3.Lerp(f.offset,SurfaceOffset(f.end),t));
            if(t<1){knockedFlights[i]=f;continue;}
            KnockRendering(f.magnet,false,Vector3.zero);
            var owner=Owner(Tile(CellAt(f.end)));var binding=f.magnet.GetComponent<IslandSurfaceAnchor>();if(binding){binding.center=owner;binding.Apply();}
            RecordKnockCheckpoint(f.magnet);RecordRecoilArrival(f.magnet,owner);
            knockedFlights.RemoveAt(i);
        }
    }
    void RecordKnockCheckpoint(MagnetPiece magnet){
        for(int i=0;i<magnets.Length;i++)if(magnets[i]&&magnets[i].transform.IsChildOf(magnet.transform)&&!knockCheckpoints.Exists(c=>c.index==i))
            knockCheckpoints.Add(new KnockCheckpoint{index=i,piece=SavePiece(magnets[i])});
    }
    void ClearKnockFlights(){foreach(var f in knockedFlights)if(f.magnet)KnockRendering(f.magnet,false,Vector3.zero);knockedFlights.Clear();}
    bool SnapshotOccupies(Snapshot piece,Vector2Int cell){
        var home=CellAt(piece.position);return home==cell||((piece.product==MagnetProduct.Bridge||piece.product==MagnetProduct.BridgeHalf)&&home+piece.bridgeDirection==cell);
    }
    bool ResetSpotOccupied(int index,Snapshot candidate,Snapshot[] restore,bool[] affected){
        var cells=new List<Vector2Int>{CellAt(candidate.position)};
        if(candidate.product==MagnetProduct.Bridge||candidate.product==MagnetProduct.BridgeHalf)cells.Add(cells[0]+candidate.bridgeDirection);
        foreach(var cell in cells){
            // The player's incoming berth is also reserved when a foreign arrival remains.
            if(cell==CellAt(islandEntry)&&recoilArrivals.Exists(a=>a.active&&a.island==currentIsland))return true;
            for(int j=0;j<magnets.Length;j++){
                if(j==index||!magnets[j])continue;
                var other=affected[j]?restore[j]:SavePiece(magnets[j]);
                if(other.enabled&&other.active&&SnapshotOccupies(other,cell))return true;
            }
        }
        return false;
    }
    void ResolveKnockReset(Snapshot[] restore,bool[] affected){
        foreach(var checkpoint in knockCheckpoints){
            int i=checkpoint.index;if(!affected[i])continue;
            if(ResetSpotOccupied(i,restore[i],restore,affected)){
                // Restore the material's original form, at its first displaced landing.
                var state=restore[i];state.position=checkpoint.piece.position;state.owner=checkpoint.piece.owner;restore[i]=state;
            }
        }
        for(int i=knockedFlights.Count-1;i>=0;i--){
            int index=System.Array.IndexOf(magnets,knockedFlights[i].magnet);
            if(index>=0&&affected[index]){
                // Without a first landing there is no fallback cell yet. Keep drifting
                // rather than resetting on top of the new arrivals.
                if(!knockCheckpoints.Exists(c=>c.index==index)&&ResetSpotOccupied(index,restore[index],restore,affected)){affected[index]=false;continue;}
                KnockRendering(knockedFlights[i].magnet,false,Vector3.zero);knockedFlights.RemoveAt(i);
            }
        }
    }
}
}
