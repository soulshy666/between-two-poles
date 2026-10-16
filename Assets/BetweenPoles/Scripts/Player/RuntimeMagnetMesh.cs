using UnityEngine;
namespace BetweenPoles {
// Own only the procedurally created mesh, never an imported/shared asset.
public sealed class RuntimeMagnetMesh:MonoBehaviour {
    public Mesh ownedMesh;
    void OnDestroy(){
        if(!ownedMesh)return;
        if(Application.isPlaying)Destroy(ownedMesh);else DestroyImmediate(ownedMesh);
        ownedMesh=null;
    }
}
}
