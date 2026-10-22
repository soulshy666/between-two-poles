using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetweenPoles {
public sealed partial class ChapterAtlas {
    readonly Dictionary<Transform,Vector3> hoverScales=new Dictionary<Transform,Vector3>();
    Transform hoveredPlanet;

    bool HoverAllowed => showcaseCamera&&Application.isFocused&&!MobileUI.Enabled&&Input.touchCount==0
        &&!IsInspecting&&!returning&&!chartTransitioning&&!TelevisionSceneTransition.Busy;

    void UpdatePlanetHover(){
        if(!HoverAllowed){ClearPlanetHover();return;}
        var mouse=Input.mousePosition;
        bool inside=mouse.x>=0&&mouse.y>=0&&mouse.x<Screen.width&&mouse.y<Screen.height;
        hoveredPlanet=inside&&(!EventSystem.current||!EventSystem.current.IsPointerOverGameObject())?PlanetAt(mouse):null;
        var generator=hoveredPlanet?hoveredPlanet.GetComponent<Generators.PixelGeneratorLab>():null;
        if(generator&&generator.model==11)hoveredPlanet=null; // The sun is not a hover button.
        float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*16);
        foreach(var planet in chartPlanets){
            if(!planet)continue;
            if(!hoverScales.TryGetValue(planet,out var original)){original=planet.localScale;hoverScales.Add(planet,original);}
            planet.localScale=Vector3.Lerp(planet.localScale,original*(planet==hoveredPlanet?1.12f:1),blend);
        }
    }
    void ClearPlanetHover(){
        hoveredPlanet=null;
        foreach(var pair in hoverScales)if(pair.Key)pair.Key.localScale=pair.Value;
    }
    void OnDisable(){ClearPlanetHover();}
    void ReleasePlanetHover(){
        ClearPlanetHover();hoverScales.Clear();
    }
}
}
