using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
// Replace only obstacle artwork; retain tile ownership, renderer references and grid rules.
public static class IceRockVisuals {
    const string MeshName="Ice rock obstacle";
    static Mesh mesh;
    static Mesh tallMesh;
    static Mesh lowMesh;
    static readonly Dictionary<int,Mesh[]> meshes=new Dictionary<int,Mesh[]>();
    static readonly Dictionary<int,Material[]> palettes=new Dictionary<int,Material[]>();
    static Material[] materials;
    public static void Apply(GridPlayground board){
        foreach(var tile in board.tiles){
            if(!tile||!tile.blocked)continue;
            var debug=tile.transform.Find("调试高台");
            if(debug&&debug.gameObject.activeSelf)Replace(debug,tile,board.cellSize);
            if(tile.transform.parent)foreach(Transform sibling in tile.transform.parent){
                if(sibling.name!="rock")continue;
                var delta=sibling.position-tile.transform.position;delta.y=0;
                if(delta.sqrMagnitude<.001f)Replace(sibling,tile,board.cellSize);
            }
        }
    }
    static void Replace(Transform root,GridTile tile,float cell){
        var filters=root.GetComponentsInChildren<MeshFilter>();
        if(filters.Length==0)return;
        var target=filters[0];var renderer=target.GetComponent<MeshRenderer>();if(!renderer)return;
        int theme=Mathf.Clamp(tile.rockTheme,0,4);
        if(!meshes.ContainsKey(theme))Build(theme);
        var selectedMesh=meshes[theme][Mathf.Clamp(tile.rockStyle,0,2)];
        if(target.sharedMesh==selectedMesh)return;
        // Reuse renderers registered with island visibility and curvature systems.
        foreach(var filter in filters)if(filter!=target)filter.sharedMesh=null;
        target.sharedMesh=selectedMesh;renderer.sharedMaterials=palettes[theme];
        target.transform.position=new Vector3(tile.transform.position.x,tile.surfaceHeight,tile.transform.position.z);
        target.transform.rotation=Quaternion.identity;
        Vector3 scale=target.transform.parent?target.transform.parent.lossyScale:Vector3.one;
        float width=cell*.86f;
        target.transform.localScale=new Vector3(width/scale.x,1.12f/scale.y,width/scale.z);
    }
    static void Build(int theme){
        var colors=new[]{new Color(.25f,.30f,.33f),new Color(.34f,.40f,.43f),new Color(.44f,.49f,.51f),new Color(.90f,.95f,.97f)};
        if(theme==0)colors=new[]{new Color(.27f,.29f,.30f),new Color(.38f,.40f,.40f),new Color(.50f,.51f,.50f),new Color(.50f,.51f,.50f)};
        if(theme==2)colors=new[]{new Color(.23f,.28f,.22f),new Color(.36f,.40f,.29f),new Color(.49f,.51f,.36f),new Color(.31f,.48f,.12f)};
        if(theme==3)colors=new[]{new Color(.43f,.29f,.17f),new Color(.62f,.45f,.27f),new Color(.76f,.60f,.39f),new Color(.88f,.74f,.50f)};
        if(theme==4)colors=new[]{new Color(.15f,.12f,.17f),new Color(.24f,.20f,.26f),new Color(.36f,.30f,.34f),new Color(.29f,.24f,.29f)};
        materials=new Material[colors.Length];
        for(int i=0;i<colors.Length;i++){
            var m=new Material(Shader.Find("BetweenPoles/PaintedIceTrial")){name="Rock tone "+i,color=colors[i]};
            m.SetFloat("_Painted",0);m.SetFloat("_Snow",0);m.SetFloat("_Grid",0);materials[i]=m;
        }
        var vertices=new List<Vector3>();var indices=new List<int>[colors.Length];for(int i=0;i<indices.Length;i++)indices[i]=new List<int>();
        System.Action<Vector3,Vector3,Vector3,int> tri=(a,b,c,material)=>{
            int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            indices[material].Add(start);indices[material].Add(start+1);indices[material].Add(start+2);
        };
        // Hand-shaped, asymmetric outlines: a tall rear mass and a broad low
        // front face, rather than concentric rings or a central pointed peak.
        var bottom=new[]{
            new Vector3(.33f,0,-.34f),new Vector3(.43f,0,.04f),
            new Vector3(.25f,0,.33f),new Vector3(-.14f,0,.35f),
            new Vector3(-.36f,0,.17f),new Vector3(-.40f,0,-.20f),
            new Vector3(-.22f,0,-.39f)};
        // Broaden the ground contact for both variants without enlarging the crown.
        for(int i=0;i<bottom.Length;i++)bottom[i]*=1.25f;
        var shoulder=new[]{
            new Vector3(.43f,.49f,-.33f),new Vector3(.48f,.57f,.06f),
            new Vector3(.27f,.66f,.38f),new Vector3(-.17f,.70f,.39f),
            new Vector3(-.43f,.56f,.19f),new Vector3(-.47f,.29f,-.24f),
            new Vector3(-.25f,.27f,-.46f)};
        var crown=new[]{
            new Vector3(.32f,0,-.09f),new Vector3(.26f,0,.17f),
            new Vector3(.09f,0,.30f),new Vector3(-.19f,0,.27f),
            new Vector3(-.29f,0,.12f),new Vector3(-.32f,0,-.09f),
            new Vector3(-.10f,0,-.14f)};
        // One sloping top plane keeps a broad cut surface without a star of facets.
        for(int i=0;i<crown.Length;i++)crown[i].y=.84f+.24f*crown[i].z-.16f*crown[i].x;
        // Uneven snow tongues descend from the crown, leaving broad exposed
        // rock faces. Shared boundary points avoid overlapping coplanar layers.
        float[] snowLine={.88f,.68f,.92f,.18f,.78f,.90f,.16f};
        float[] tongueDepth={.76f,.12f,.56f,.22f,.69f,.78f,.48f};
        float[] cuts={0,.19f,.38f,.53f,.72f,.87f,1};
        for(int i=0;i<bottom.Length;i++){
            int j=(i+1)%bottom.Length;
            int lower=i==4?1:0,upper=i==4?2:1;
            tri(bottom[i],shoulder[i],shoulder[j],lower);
            tri(bottom[i],shoulder[j],bottom[j],lower);
            // Several uneven lobes per face, instead of a straight snow hem.
            // Shared endpoints keep adjacent faces sealed; deep tongues and
            // high bare notches remain visible at the game's pixel resolution.
            for(int k=0;k<cuts.Length-1;k++){
                float u=cuts[k],v=cuts[k+1];
                float depthA=Mathf.Clamp01(Mathf.Lerp(snowLine[i],snowLine[j],u)
                    -Mathf.Sin(u*Mathf.PI)*tongueDepth[i]
                    +Mathf.Sin(u*Mathf.PI*4)*.19f);
                float depthB=Mathf.Clamp01(Mathf.Lerp(snowLine[i],snowLine[j],v)
                    -Mathf.Sin(v*Mathf.PI)*tongueDepth[i]
                    +Mathf.Sin(v*Mathf.PI*4)*.19f);
                var lowA=Vector3.Lerp(shoulder[i],shoulder[j],u);
                var lowB=Vector3.Lerp(shoulder[i],shoulder[j],v);
                var highA=Vector3.Lerp(crown[i],crown[j],u);
                var highB=Vector3.Lerp(crown[i],crown[j],v);
                var edgeA=Vector3.Lerp(lowA,highA,Mathf.Clamp(depthA,.035f,.97f));
                var edgeB=Vector3.Lerp(lowB,highB,Mathf.Clamp(depthB,.035f,.97f));
                tri(lowA,edgeA,edgeB,upper);tri(lowA,edgeB,lowB,upper);
                tri(edgeA,highA,highB,theme==0?upper:3);tri(edgeA,highB,edgeB,theme==0?upper:3);
            }
        }
        for(int i=1;i<crown.Length-1;i++)tri(crown[0],crown[i+1],crown[i],3);
        // Overlapping, uneven banks hug most of the base, with small bare gaps.
        for(int edge=0;edge<bottom.Length&&(theme==1||theme==3);edge++){
            var outward=bottom[edge].normalized;
            var tangent=new Vector3(-outward.z,0,outward.x);
            var center=Vector3.Lerp(bottom[edge],bottom[(edge+1)%bottom.Length],.38f)+outward*.025f;
            float width=.22f+(edge%3)*.035f;
            float spread=.11f+(edge%2)*.025f;
            var mound=center-outward*.025f+Vector3.up*(.075f+(edge%3)*.022f);
            for(int k=0;k<7;k++){
                float a=k*Mathf.PI*2/7,b=(k+1)*Mathf.PI*2/7;
                float ra=k%2==0?1:.83f,rb=(k+1)%2==0?1:.83f;
                var p=center+(tangent*(Mathf.Cos(a)*width)+outward*(Mathf.Sin(a)*spread))*ra+Vector3.up*.004f;
                var q=center+(tangent*(Mathf.Cos(b)*width)+outward*(Mathf.Sin(b)*spread))*rb+Vector3.up*.004f;
                tri(mound,p,q,3);
            }
        }
        // Thin surface-following ribbons: vines or hot cracks, not a snow recolour.
        if(theme==2||theme==4){
            var expanded=new Material[5];materials.CopyTo(expanded,0);
            expanded[4]=new Material(materials[0]){name=theme==2?"Vines":"Lava fissures",color=theme==2?new Color(.14f,.27f,.07f):new Color(1,.28f,.025f)};
            materials=expanded;System.Array.Resize(ref indices,5);indices[4]=new List<int>();
            for(int face=0;face<bottom.Length;face+=2){
                int next=(face+1)%bottom.Length;var normal=Vector3.Cross(shoulder[face]-bottom[face],bottom[next]-bottom[face]).normalized;
                var across=(bottom[next]-bottom[face]).normalized*(theme==2?.022f:.012f);
                for(int k=0;k<6;k++){
                    float a=k/6f,b=(k+1)/6f;
                    var p=Vector3.Lerp(Vector3.Lerp(bottom[face],bottom[next],.46f),Vector3.Lerp(shoulder[face],shoulder[next],.46f),a)+normal*.005f+across*Mathf.Sin(k*2);
                    var q=Vector3.Lerp(Vector3.Lerp(bottom[face],bottom[next],.46f),Vector3.Lerp(shoulder[face],shoulder[next],.46f),b)+normal*.005f+across*Mathf.Sin((k+1)*2);
                    tri(p-across,q-across,q+across,4);tri(p-across,q+across,p+across,4);
                    if(theme==2&&k%2==1){var leaf=p+across*4+Vector3.up*.025f;tri(p,leaf,p+Vector3.up*.08f,3);tri(p,p+Vector3.up*.08f,leaf,3);}
                }
            }
        }
        mesh=new Mesh{name=MeshName+" theme "+theme};mesh.SetVertices(vertices);mesh.subMeshCount=indices.Length;
        for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        // A narrower, taller mass with a high sloping crown, retaining the
        // reference's broad front ledge and near-vertical lower sides.
        tallMesh=Object.Instantiate(mesh);tallMesh.name="Tall rock obstacle";
        var tallVertices=tallMesh.vertices;
        for(int i=0;i<tallVertices.Length;i++){
            var v=tallVertices[i];
            v.x=v.x*.84f+.055f*Mathf.Clamp01(v.y/.7f);
            v.z*=.88f;v.y*=1.8f;tallVertices[i]=v;
        }
        tallMesh.vertices=tallVertices;tallMesh.RecalculateNormals();tallMesh.RecalculateBounds();
        lowMesh=Object.Instantiate(mesh);lowMesh.name="Low flat-topped rock theme "+theme;
        var lowVertices=lowMesh.vertices;
        for(int i=0;i<lowVertices.Length;i++){
            var v=lowVertices[i];
            // Flatten the entire crown to 0.55 world units; broaden it for a future foothold.
            float top=Mathf.Clamp01((v.y-.70f)/.06f);
            v.x*=Mathf.Lerp(1,1.16f,top);v.z*=Mathf.Lerp(1,1.16f,top);
            v.y=Mathf.Min(v.y/.76f,1)*(.55f/1.12f);lowVertices[i]=v;
        }
        lowMesh.vertices=lowVertices;lowMesh.RecalculateNormals();lowMesh.RecalculateBounds();
        meshes[theme]=new[]{mesh,tallMesh,lowMesh};palettes[theme]=materials;
    }
}
}
