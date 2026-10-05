using UnityEngine;
namespace BetweenPoles {
// Keep the authored ring plane while the globe turns beneath it.
[ExecuteAlways,DefaultExecutionOrder(200)]
public sealed class FixedPlanetRing : MonoBehaviour {
    public Quaternion fixedWorldRotation=Quaternion.identity;
    void LateUpdate(){Apply();}
    public void Apply(){transform.rotation=fixedWorldRotation;}
}
}
