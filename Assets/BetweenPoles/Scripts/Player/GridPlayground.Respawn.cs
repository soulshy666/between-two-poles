using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    struct PlayerIslandEntry {
        public Vector2Int cell;
        public float height;
        public Quaternion rotation;
    }
    // First visits belong to this play session, not to the undoable puzzle state.
    readonly Dictionary<Transform,PlayerIslandEntry> playerIslandEntries=new Dictionary<Transform,PlayerIslandEntry>();
    void CapturePlayerIslandEntries(){
        playerIslandEntries.Clear();
        RememberPlayerIslandEntry();
    }
    void RememberPlayerIslandEntry(){
        var entry=new PlayerIslandEntry{cell=Cell(player),height=player.position.y,rotation=player.rotation};
        if(currentIsland){
            if(playerIslandEntries.TryGetValue(currentIsland,out var first))entry=first;
            else playerIslandEntries.Add(currentIsland,entry);
        }
        islandEntry=Position(entry.cell,entry.height);islandEntryRotation=entry.rotation;
    }
    void ResetPlayerOnCurrentIsland(){
        // Undo may restore an older entry field; the first visit remains authoritative.
        if(currentIsland&&playerIslandEntries.TryGetValue(currentIsland,out var entry)){
            islandEntry=Position(entry.cell,entry.height);islandEntryRotation=entry.rotation;
        }
        var spawn=islandEntry;
        // Resolve after restoring magnets so an occupied entry puts the feet on top.
        // This also handles blocked rock tiles and the solid rim of hollow magnets.
        if(BlackHolePortal.TrySurface(this,CellAt(spawn),out var tile,out var top))spawn=top;
        player.SetPositionAndRotation(spawn,islandEntryRotation);
    }
}
}
