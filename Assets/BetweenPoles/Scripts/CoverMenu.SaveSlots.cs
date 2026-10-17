using UnityEngine;
namespace BetweenPoles {
public sealed partial class CoverMenu {
    [Header("存档页面预览")]
    public bool previewSavePage;
    bool savePage;
    int saveSelection,deleteSlot=-1;
    readonly bool[] occupiedSlots=new bool[3];
    string saveError;
    void OpenSavePage(){savePage=true;saveSelection=0;deleteSlot=-1;saveError=null;RefreshSlots();}
    void RefreshSlots(){for(int i=0;i<3;i++)occupiedSlots[i]=Application.isPlaying&&GameSaveSlots.Exists(i);}
    void ChooseSlot(int slot){
        if(!Application.isPlaying||loading||deleteSlot>=0)return;
        PlayMenuClick();
        string scene;if(GameSaveSlots.Select(slot,out scene)){saveError=null;StartCoroutine(Begin(scene));}
        else saveError=GameSaveSlots.LastError;
    }
    void UpdateSavePage(){
        if(Input.GetKeyDown(KeyCode.Escape)){if(deleteSlot>=0)deleteSlot=-1;else savePage=false;return;}
        if(deleteSlot>=0)return;
        if(Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.S))saveSelection=(saveSelection+1)%3;
        if(Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.W))saveSelection=(saveSelection+2)%3;
        if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))ChooseSlot(saveSelection);
    }
    void DrawSavePage(){
        var old=GUI.color;GUI.color=Color.white;
        bool interactive=Application.isPlaying&&!loading;
        if(deleteSlot>=0){
            PixelText(new Rect(50,330,600,64),"确定删除存档 "+(deleteSlot+1)+" 吗？",3);
            if(ExitChoice(new Rect(130,440,160,54),"取消")&&interactive)deleteSlot=-1;
            if(ExitChoice(new Rect(370,440,165,54),"确定")&&interactive){
                if(GameSaveSlots.Delete(deleteSlot)){deleteSlot=-1;saveError=null;RefreshSlots();}else saveError=GameSaveSlots.LastError;
            }
        }else{
            PixelText(new Rect(70,280,520,56),"选择存档",3);
            for(int i=0;i<3;i++){
                var row=new Rect(100,350+i*85,370,66);
                bool hot=interactive&&row.Contains(Event.current.mousePosition);
                if(hot&&Event.current.type==EventType.MouseMove)saveSelection=i;
                GUI.color=i==saveSelection?Color.white:new Color(.75f,.82f,.85f);
                PixelText(new Rect(row.x,row.y,110,row.height),"0"+(i+1),3,false);
                PixelText(new Rect(row.x+110,row.y,260,row.height),occupiedSlots[i]?"继续游戏":"新游戏",3,false);
                if(i==saveSelection&&triangle)GUI.DrawTexture(new Rect(row.x-24,row.y+25,10,14),triangle);
                if(GUI.Button(row,GUIContent.none,GUIStyle.none)&&interactive){saveSelection=i;ChooseSlot(i);}
                if(occupiedSlots[i]){
                    var cross=new Rect(510,row.y+8,50,50);GUI.color=cross.Contains(Event.current.mousePosition)?new Color(1,.48f,.42f):new Color(.8f,.8f,.8f);
                    PixelText(cross,"×",3);
                    if(GUI.Button(cross,GUIContent.none,GUIStyle.none)&&interactive){PlayMenuClick();deleteSlot=i;saveError=null;}
                }
            }
            GUI.color=Color.white;if(ExitChoice(new Rect(245,627,160,48),"返回")&&interactive)savePage=false;
        }
        if(!string.IsNullOrEmpty(saveError)){GUI.color=new Color(1,.55f,.45f);PixelText(new Rect(50,695,600,42),saveError,2);}
        GUI.color=old;
    }
}
}
