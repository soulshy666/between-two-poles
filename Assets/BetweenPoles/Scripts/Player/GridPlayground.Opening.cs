using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    public bool IsWreckCell(Vector2Int cell){var intro=GetComponent<IceCrashIntro>();return intro&&intro.BlocksCell(cell);}
    public bool OpeningCinematic {get;private set;}
    public void SetOpeningCinematic(bool locked){
        OpeningCinematic=locked;Busy=locked;ClearMovementInput();
    }
    public void FinishOpeningLanding(){
        SetOpeningCinematic(false);CaptureInitialState();NotifyLanding(PlayerCell);
    }
}
}
