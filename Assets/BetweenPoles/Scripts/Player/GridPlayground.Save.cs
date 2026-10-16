using System;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    [Serializable] public sealed class DiskPiece {
        public Vector3 position,scale,geoPosition,portalEntry;
        public Quaternion rotation,pose;
        public int parent=-1;
        public string owner,portal;
        public bool combined,walkable,enabled,active,north,baseNorth,originalGeometry;
        public MagnetShape shape;public MagnetProduct product;public Vector2Int direction;
        public float portalSpeed;
    }
    [Serializable] public sealed class DiskArrival {public int index;public string island;public bool active;public DiskPiece piece;}
    [Serializable] public sealed class DiskState {
        public int authoredCount;
        public DiskPiece[] pieces,initialPieces;
        public Vector3 playerPosition,entryPosition;
        public Quaternion playerRotation,entryRotation;
        public string island;
        public bool goal;
        public string[] stars;
        public DiskArrival[] arrivals,knockPoints;
    }
    Transform[] diskOriginalParents,diskOriginalGeometry;
    void InitializeDiskReferences(){
        diskOriginalParents=new Transform[magnets.Length];diskOriginalGeometry=new Transform[magnets.Length];
        for(int i=0;i<magnets.Length;i++){diskOriginalParents[i]=magnets[i].transform.parent;diskOriginalGeometry[i]=magnets[i].geometry;}
    }
    Vector3 DiskOrigin {get{foreach(var t in tiles)if(t)return t.transform.position;return transform.position;}}
    public bool CanSaveToDisk {get{return player&&initialState!=null&&!Busy&&!OpeningCinematic&&!RecoilFlying&&liveRings.Count==0&&knockedFlights.Count==0;}}
    static string DiskPath(Transform t){if(!t)return null;string p=t.name;while(t.parent){t=t.parent;p=t.name+"/"+p;}return p;}
    Transform DiskTransform(string path){if(string.IsNullOrEmpty(path))return null;foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(DiskPath(t)==path)return t;return null;}
    DiskPiece DiskEncode(Snapshot s,int index,Vector3 origin){
        int parent=-1;for(int j=0;j<magnets.Length;j++)if(magnets[j].transform==s.parent){parent=j;break;}
        return new DiskPiece{position=s.position-origin,rotation=s.rotation,scale=s.scale,geoPosition=s.geoPosition,pose=s.pose,parent=parent,owner=DiskPath(s.owner),portal=DiskPath(s.storedInPortal?s.storedInPortal.transform:null),portalEntry=s.portalEntryPosition-origin,portalSpeed=s.portalRollSpeed,combined=s.combined,walkable=s.walkable,enabled=s.enabled,active=s.active,north=s.north,baseNorth=s.baseNorth,shape=s.shape,product=s.product,direction=s.bridgeDirection,originalGeometry=index<diskOriginalGeometry.Length&&s.geometry==diskOriginalGeometry[index]};
    }
    public DiskState CaptureDiskState(){
        if(!CanSaveToDisk)throw new InvalidOperationException("Cannot save an action in progress");
        RestoreMagnetHover(null);var origin=DiskOrigin;var data=new DiskState{authoredCount=diskOriginalGeometry.Length,pieces=new DiskPiece[magnets.Length],playerPosition=player.position-origin,playerRotation=player.rotation,entryPosition=islandEntry-origin,entryRotation=islandEntryRotation,island=DiskPath(currentIsland),goal=ReachedGoal};
        for(int i=0;i<magnets.Length;i++)data.pieces[i]=DiskEncode(SavePiece(magnets[i]),i,origin);
        var stars=new List<string>();foreach(var star in collectedStars)if(star)stars.Add(DiskPath(star));data.stars=stars.ToArray();
        data.arrivals=new DiskArrival[recoilArrivals.Count];for(int i=0;i<data.arrivals.Length;i++){var a=recoilArrivals[i];data.arrivals[i]=new DiskArrival{index=a.index,island=DiskPath(a.island),active=a.active,piece=DiskEncode(a.piece,a.index,origin)};}
        data.initialPieces=new DiskPiece[initialState.pieces.Length];for(int i=0;i<data.initialPieces.Length;i++)data.initialPieces[i]=DiskEncode(initialState.pieces[i],i,origin);
        data.knockPoints=new DiskArrival[knockCheckpoints.Count];for(int i=0;i<data.knockPoints.Length;i++){var a=knockCheckpoints[i];data.knockPoints[i]=new DiskArrival{index=a.index,piece=DiskEncode(a.piece,a.index,origin)};}
        return data;
    }
    Snapshot DiskDecode(DiskPiece d,int index,Vector3 origin){
        var m=magnets[index];Transform geometry;
        if(d.originalGeometry&&index<diskOriginalGeometry.Length)geometry=diskOriginalGeometry[index];
        else{geometry=MagnetVisuals.Build(m.transform,d.shape,d.north,d.product,cellSize,d.baseNorth,d.direction);recordedGeometry.Add(geometry);geometry.gameObject.SetActive(false);}
        var portal=DiskTransform(d.portal);
        return new Snapshot{position=d.position+origin,rotation=d.rotation,scale=d.scale,geoPosition=d.geoPosition,pose=d.pose,parent=d.parent>=0?magnets[d.parent].transform:index<diskOriginalParents.Length?diskOriginalParents[index]:transform,owner=DiskTransform(d.owner),storedInPortal=portal?portal.GetComponent<BlackHolePortal>():null,portalEntryPosition=d.portalEntry+origin,portalRollSpeed=d.portalSpeed,combined=d.combined,walkable=d.walkable,enabled=d.enabled,active=d.active,north=d.north,baseNorth=d.baseNorth,shape=d.shape,product=d.product,bridgeDirection=d.direction,geometry=geometry};
    }
    public void RestoreDiskState(DiskState data){
        if(data==null||data.pieces==null||data.authoredCount!=diskOriginalGeometry.Length||data.pieces.Length<data.authoredCount)throw new InvalidOperationException("Scene layout no longer matches this save");
        foreach(var p in data.pieces)if(p==null||p.parent>=data.pieces.Length)throw new InvalidOperationException("Invalid magnet state");
        var all=new List<MagnetPiece>(magnets);
        while(all.Count<data.pieces.Length){var g=new GameObject("存档 · 跨章磁铁");g.transform.SetParent(transform,false);var m=g.AddComponent<MagnetPiece>();m.geometry=MagnetVisuals.Build(g.transform,MagnetShape.Bar,true,MagnetProduct.None,cellSize,true,Vector2Int.right);var anchor=g.AddComponent<IslandSurfaceAnchor>();anchor.ground=this;all.Add(m);}
        magnets=all.ToArray();var origin=DiskOrigin;var state=SaveWorld();state.pieces=new Snapshot[data.pieces.Length];
        for(int i=0;i<data.pieces.Length;i++)state.pieces[i]=DiskDecode(data.pieces[i],i,origin);
        var arrivals=new List<RecoilArrival>();if(data.arrivals!=null)foreach(var a in data.arrivals)if(a.index>=0&&a.index<magnets.Length)arrivals.Add(new RecoilArrival{index=a.index,island=DiskTransform(a.island),active=a.active,piece=DiskDecode(a.piece,a.index,origin)});
        state.arrivals=arrivals.ToArray();state.knockFlights=null;
        var points=new List<KnockCheckpoint>();if(data.knockPoints!=null)foreach(var a in data.knockPoints)if(a.index>=0&&a.index<magnets.Length)points.Add(new KnockCheckpoint{index=a.index,piece=DiskDecode(a.piece,a.index,origin)});state.knockPoints=points.ToArray();
        if(data.initialPieces!=null&&data.initialPieces.Length==magnets.Length){initialState.pieces=new Snapshot[magnets.Length];for(int i=0;i<magnets.Length;i++)initialState.pieces[i]=DiskDecode(data.initialPieces[i],i,origin);}
        state.playerPosition=data.playerPosition+origin;state.playerRotation=data.playerRotation;state.entryPosition=data.entryPosition+origin;state.entryRotation=data.entryRotation;state.island=DiskTransform(data.island);state.goal=data.goal;
        RestoreWorld(state);history.Clear();ReleaseUnusedGeometry();
        if(data.stars!=null)foreach(var path in data.stars){var star=DiskTransform(path);if(star){collectedStars.Add(star);star.gameObject.SetActive(false);}}
        var pose=player.GetComponent<PlayerPushPose>();if(pose)pose.End();
    }
}
}
