using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
[DefaultExecutionOrder(600)]
public sealed class IslandDiscoveryView:MonoBehaviour {
    FiveIslandWindow window;
    Texture2D mask;
    Vector4 bounds;
    int revision=-1;
    bool overview;
    readonly HashSet<Renderer> affected=new HashSet<Renderer>();
    MaterialPropertyBlock block;
    public void SetOverview(bool value){overview=value;revision=-1;Refresh();}
    void LateUpdate(){Refresh();}
    void Refresh(){
        if(!window)window=GetComponent<FiveIslandWindow>();
        if(!window||!window.board)return;
        if(revision==SessionExploration.Revision&&mask)return;
        BuildMask();
    }
    void BuildMask(){
        revision=SessionExploration.Revision;
        if(block==null)block=new MaterialPropertyBlock();
        float cell=Mathf.Max(.1f,window.board.cellSize);
        var owners=new Dictionary<Vector2Int,Transform>();
        var known=new HashSet<Transform>();
        foreach(var room in window.rooms)if(room.center&&(SessionExploration.GetInt(ChapterAtlas.Key(gameObject.scene.path,room.id))>0||SessionExploration.GetInt(ChapterAtlas.Key(gameObject.scene.path,room.center.name))>0))known.Add(room.center);
        var min=new Vector2Int(int.MaxValue,int.MaxValue);var max=new Vector2Int(int.MinValue,int.MinValue);
        foreach(var tile in window.board.tiles){
            if(!tile)continue;
            var anchor=tile.GetComponentInParent<IslandSurfaceAnchor>();if(!anchor||!anchor.center)continue;
            var p=tile.transform.position;var key=new Vector2Int(Mathf.RoundToInt(p.x/cell),Mathf.RoundToInt(p.z/cell));
            owners[key]=anchor.center;min=Vector2Int.Min(min,key);max=Vector2Int.Max(max,key);
        }
        if(owners.Count==0)return;
        // Extend ownership across the bevel outside each outer tile, preventing bright rims.
        min-=Vector2Int.one;max+=Vector2Int.one;
        var expanded=new Dictionary<Vector2Int,Transform>(owners);
        foreach(var pair in owners)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
            var key=pair.Key+new Vector2Int(x,y);
            if(!expanded.ContainsKey(key))expanded.Add(key,pair.Value);
        }
        int width=max.x-min.x+1,height=max.y-min.y+1;
        var pixels=new Color[width*height];
        foreach(var pair in expanded)pixels[(pair.Key.y-min.y)*width+pair.Key.x-min.x]=new Color(known.Contains(pair.Value)?0:1,0,0,1);
        if(mask)Destroy(mask);
        mask=new Texture2D(width,height,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        mask.SetPixels(pixels);mask.Apply(false,false);
        bounds=new Vector4((min.x-.5f)*cell,(min.y-.5f)*cell,width*cell,height*cell);
        foreach(var room in window.rooms)foreach(var r in room.surfaces)Apply(r);
        foreach(var anchor in FindObjectsOfType<IslandSurfaceAnchor>())if(anchor.gameObject.scene==gameObject.scene)foreach(var r in anchor.GetComponentsInChildren<Renderer>())Apply(r);
    }
    void Apply(Renderer renderer){
        if(!renderer)return;affected.Add(renderer);renderer.GetPropertyBlock(block);
        block.SetTexture("_DiscoveryMask",mask);
        block.SetVector("_DiscoveryBounds",bounds);block.SetFloat("_DiscoveryEnabled",overview?1:0);renderer.SetPropertyBlock(block);
    }
    void OnDisable(){if(block==null)return;foreach(var r in affected)if(r){r.GetPropertyBlock(block);block.SetFloat("_DiscoveryEnabled",0);r.SetPropertyBlock(block);}revision=-1;}
    void OnDestroy(){if(mask)Destroy(mask);}
}
}
