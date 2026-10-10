using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public static partial class IceRockVisuals {
    static readonly Dictionary<int,Mesh> lowVariants=new Dictionary<int,Mesh>();
    // Coordinates use the same footprint and 1.12 vertical scale as the legacy rocks.
    static Mesh LowVariant(int theme,int variant){
        int key=theme*10+variant;Mesh cached;if(lowVariants.TryGetValue(key,out cached))return cached;
        var vertices=new List<Vector3>();var groups=new List<int>[palettes[theme].Length];
        for(int i=0;i<groups.Length;i++)groups[i]=new List<int>();
        System.Action<Vector3,Vector3,Vector3,int> face=(a,b,c,mat)=>{
            int index=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            groups[mat].Add(index);groups[mat].Add(index+1);groups[mat].Add(index+2);
        };
        const int count=12;
        float[] heights={0f,.12f,.29f,.42f,.49f};
        float[] outline=variant==1
            ?new[]{.45f,.48f,.44f,.40f,.46f,.49f,.53f,.55f,.50f,.43f,.46f,.49f}
            :new[]{.48f,.50f,.46f,.42f,.48f,.52f,.45f,.43f,.49f,.52f,.47f,.44f};
        float[] crownLevels={.46f,.50f,.48f,.43f,.47f,.52f,.49f,.44f,.46f,.51f,.48f,.45f};
        var rings=new Vector3[heights.Length][];
        for(int k=0;k<rings.Length;k++){
            rings[k]=new Vector3[count];
            for(int i=0;i<count;i++){
                float angle=i*Mathf.PI*2/count+Mathf.Sin(i*1.9f)*.055f;
                // Local erosion, not a rotationally symmetric hourglass. The back
                // remains a solid buttress while only one flank is undercut.
                float erosion=variant==2?Mathf.Max(0,Mathf.Cos(angle+.6f))*.14f:0;
                float scale=k==0?.88f:k==1?.97f:k==2?1-erosion:k==3?1.02f:.87f;
                float radius=outline[i]*scale;
                float h=k==4?crownLevels[(i+variant)%count]:heights[k]+(k==0?0:Mathf.Sin(i*1.6f+k)*.045f);
                // Integrate concept 04's lower shoulder into the watertight body.
                if(variant==1&&i>=6&&i<=8){radius*=k<2?1.06f:k==2?1:.84f;h*=k>=2?.79f:1;}
                rings[k][i]=new Vector3(Mathf.Cos(angle)*radius+.018f*k,h,Mathf.Sin(angle)*radius*.88f);
            }
        }
        for(int k=0;k<rings.Length-1;k++)for(int i=0;i<count;i++){
            int j=(i+1)%count;int mat=(i==2||i==7)?0:(i==4||i==10)?2:1;
            if((theme==1||theme==2)&&k>=2){
                float coverage=theme==1?(i%3==0?.85f:.35f):(i%3==1?.1f:.65f);
                var a=rings[k][i];var b=rings[k][j];var c=rings[k+1][i];var d=rings[k+1][j];
                float edge=k==3?1-coverage:1-coverage*.4f;
                var e=Vector3.Lerp(a,c,edge);var f=Vector3.Lerp(b,d,Mathf.Clamp01(edge+.13f*Mathf.Sin(i*2)));
                face(a,e,f,mat);face(a,f,b,mat);face(e,c,d,3);face(e,d,f,3);
                continue;
            }
            if(theme==3){
                // Pale, uneven sediment seams are part of the surface, not decals.
                var a=rings[k][i];var b=rings[k][j];var c=rings[k+1][i];var d=rings[k+1][j];
                float split=.45f+.17f*Mathf.Sin(i*1.8f+k);
                var e=Vector3.Lerp(a,c,split);var f=Vector3.Lerp(b,d,split);
                var g=Vector3.Lerp(a,c,split+.16f);var h=Vector3.Lerp(b,d,split+.16f);
                face(a,e,f,mat);face(a,f,b,mat);face(e,g,h,3);face(e,h,f,3);
                face(g,c,d,mat);face(g,d,h,mat);
            }else{
                face(rings[k][i],rings[k+1][i],rings[k+1][j],mat);
                face(rings[k][i],rings[k+1][j],rings[k][j],mat);
            }
        }
        var crown=rings[rings.Length-1];
        var inner=new Vector3[count];
        float[] innerLevels={.51f,.53f,.55f,.52f,.50f,.515f,.54f,.53f,.505f,.52f,.55f,.535f};
        for(int i=0;i<count;i++)inner[i]=new Vector3(crown[i].x*.43f,innerLevels[i],crown[i].z*.43f);
        var center=new Vector3(.018f,.55f,-.02f);
        // Partition the crown itself, rather than laying coplanar decals on it.
        // Broad irregular patches remain readable at the game's pixel resolution.
        System.Action<Vector3,Vector3,Vector3,int,int> topFace=null;
        topFace=(a,b,c,baseMaterial,depth)=>{
            if(theme!=0&&depth>0){
                var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;
                topFace(a,ab,ca,baseMaterial,depth-1);topFace(ab,b,bc,baseMaterial,depth-1);
                topFace(ca,bc,c,baseMaterial,depth-1);topFace(ab,bc,ca,baseMaterial,depth-1);return;
            }
            var p=(a+b+c)/3;float noise=Mathf.PerlinNoise(p.x*8+variant*3,p.z*8+7);
            int mat=baseMaterial;
            if(theme==1){
                // A snow cap with two exposed rocky notches, not a white rim.
                bool bare=(p.x>.12f&&p.z<-.04f&&noise<.62f)||(p.x<-.23f&&p.z>.10f&&noise<.58f);
                mat=bare?baseMaterial:3;
            }else if(theme==2){
                if(noise+p.x*.4f>.47f)mat=3;
            }else if(theme==3){
                if(noise+p.z*.6f>.52f)mat=3;
            }else if(theme==4){
                float vein=Mathf.Abs(p.x+.085f*Mathf.Sin(p.z*19+variant)-.23f);
                if(vein<.02f&&p.sqrMagnitude>.045f)mat=4;
                else if(noise>.58f)mat=0;
            }
            face(a,b,c,mat);
        };
        for(int i=0;i<count;i++){
            int j=(i+1)%count;
            topFace(crown[i],inner[i],inner[j],2,2);topFace(crown[i],inner[j],crown[j],i%3==0?1:2,2);
            topFace(inner[i],center,inner[j],2,2);
            // Close the underside so low camera angles never reveal an open shell.
            face(rings[0][i],rings[0][j],Vector3.zero,0);
        }
        // Small irregular faceted masses serve as the shoulder, snow banks,
        // moss patches, sand drifts and obsidian fragments without new colliders.
        System.Action<Vector3,Vector3,int> mound=(p,size,mat)=>{
            var rim=new Vector3[7];
            for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;rim[i]=p+new Vector3(Mathf.Cos(a)*size.x,0,Mathf.Sin(a)*size.z);}
            var peak=p+new Vector3(size.x*.12f,size.y,0);
            for(int i=0;i<7;i++)face(rim[i],peak,rim[(i+1)%7],mat);
        };
        if(theme>0)for(int i=0;i<count;i++){
            int j=(i+1)%count;var edge=(crown[i]+crown[j])*.5f;
            var foot=rings[0][i];foot.y=.008f;
            if(theme==1||theme==2||theme==3){
                // Keep decoration low and at the perimeter, leaving the crown's center exposed.
                if(theme==1||i%3!=1){
                    if(i%3==0)mound(foot,new Vector3(.11f,.065f,.085f),3);
                }
            }
            if(theme==1&&i%2==0){
                var tip=edge+new Vector3(0,-.13f,0);var side=(crown[j]-crown[i]).normalized*.026f;
                face(edge-side,edge+side,tip,3);face(edge+side,edge+Vector3.forward*.035f,tip,2);
                mound(foot+new Vector3(.04f,0,.03f),new Vector3(.035f,.065f,.04f),2);
            }
            if(theme==2){
                // Low fern fans on selected shoulders, visible from overhead.
                if(i==2||i==7||i==10){
                    var root=Vector3.Lerp(rings[2][i],rings[3][i],.25f);
                    var outward=new Vector3(root.x,0,root.z).normalized;
                    var tangent=Vector3.Cross(Vector3.up,outward);
                    for(int leaf=0;leaf<5;leaf++){
                        var end=root+outward*.12f+tangent*((leaf-2)*.038f)+Vector3.up*(.09f-Mathf.Abs(leaf-2)*.02f);
                        var middle=Vector3.Lerp(root,end,.6f);
                        face(root,middle+tangent*.024f,end,3);face(root,end,middle-tangent*.024f,3);
                    }
                }
                // A vine follows the actual side profile, with pairs of angular leaves.
                for(int k=0;k<3;k++){
                    var a=Vector3.Lerp(rings[k][i],rings[k][j],.45f);
                    var b=Vector3.Lerp(rings[k+1][i],rings[k+1][j],.53f);
                    var outward=new Vector3(a.x,0,a.z).normalized*.007f;a+=outward;b+=outward;
                    var side=Vector3.Cross(Vector3.up,outward).normalized*.009f;
                    face(a-side,b-side,b+side,4);face(a-side,b+side,a+side,4);
                    var leaf=(a+b)*.5f;var spread=side.normalized*.055f;
                    face(leaf,leaf+spread+Vector3.up*.035f,leaf+Vector3.up*.055f,3);
                    face(leaf,leaf+Vector3.up*.045f,leaf-spread+Vector3.up*.025f,3);
                }
                if(i%3==0)mound(foot+Vector3.up*.055f,new Vector3(.055f,.025f,.05f),2);
            }
            if(theme==3){
                mound(foot,new Vector3(.035f,.032f,.045f),i%2);
                if(i==1||i==5||i==9){
                    var shelf=Vector3.Lerp(crown[i],inner[i],.18f);shelf.y-=.004f;
                    mound(shelf,new Vector3(.032f,.025f,.027f),0);
                    mound(shelf+new Vector3(.038f,-.004f,.023f),new Vector3(.021f,.019f,.025f),1);
                }
                if(i%4==0)for(int blade=0;blade<4;blade++){
                    var root=foot+Vector3.right*(blade-2)*.015f;
                    face(root,root+new Vector3((blade-1.5f)*.035f,.105f,.012f),root+Vector3.right*.014f,3);
                }
            }
            if(theme==4){
                mound(foot,new Vector3(.065f,.04f,.05f),0);
                if(i%3==0){
                    var shelf=Vector3.Lerp(crown[i],inner[i],.12f);shelf.y-=.008f;
                    mound(shelf,new Vector3(.055f,.03f,.037f),0);
                }
                if(i%2==0)for(int k=0;k<3;k++){
                    var a=Vector3.Lerp(rings[k][i],rings[k][j],.35f);
                    var b=Vector3.Lerp(rings[k+1][i],rings[k+1][j],.5f);
                    var normal=new Vector3(a.x,0,a.z).normalized*.009f;a+=normal;b+=normal;
                    var side=Vector3.Cross(Vector3.up,normal).normalized*.018f;
                    face(a-side,b-side,b+side,4);face(a-side,b+side,a+side,4);
                }
            }
        }
        for(int i=0;i<vertices.Count;i++){var v=vertices[i];v.y=Mathf.Clamp(v.y,0,.55f)/1.12f;vertices[i]=v;}
        var mesh=new Mesh{name="Low rock concept "+(variant==1?"04":"05")+" theme "+theme};
        mesh.SetVertices(vertices);mesh.subMeshCount=groups.Length;
        for(int i=0;i<groups.Length;i++)mesh.SetTriangles(groups[i],i);
        mesh.RecalculateNormals();mesh.RecalculateBounds();lowVariants[key]=mesh;return mesh;
    }
}
}
