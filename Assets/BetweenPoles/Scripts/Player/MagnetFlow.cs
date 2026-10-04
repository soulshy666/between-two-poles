using UnityEngine;
namespace BetweenPoles {
public sealed class MagnetFlow:MonoBehaviour {
    public bool active;public float height=1.5f;Transform[] marks;
    void Start(){marks=new Transform[5];var material=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));material.color=new Color(.65f,.95f,1);
        for(int i=0;i<marks.Length;i++){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="向上磁流";g.transform.SetParent(transform,false);g.transform.localScale=new Vector3(.045f,.13f,.045f);Destroy(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;marks[i]=g.transform;}
        var owner=GetComponent<IslandSurfaceAnchor>();if(owner){owner.enabled=false;owner.enabled=true;}
    }
    void Update(){if(marks==null)return;for(int i=0;i<marks.Length;i++){marks[i].gameObject.SetActive(active);marks[i].localPosition=new Vector3(.43f,.24f+Mathf.Repeat(Time.time*.5f+i*.2f,1)*Mathf.Max(.1f,height-.24f),-.43f);}}
}
}
