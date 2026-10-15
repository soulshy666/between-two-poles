using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace BetweenPoles {
public static class MobileControlRegression {
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    [MenuItem("Between Poles/Tests/Mobile direction pad")]
    public static void Run() {
        var root = new GameObject("Mobile input regression");
        try {
            var pad = root.AddComponent<MobileDirectionPad>();
            var type = typeof(MobileDirectionPad);
            type.GetField("area",Private).SetValue(pad,new Rect(100,100,200,200));
            var hit = type.GetMethod("DirectionAt",Private);
            Func<Vector2,Vector2Int> direction = p => (Vector2Int)hit.Invoke(pad,new object[]{p});
            Check(direction(new Vector2(200,280))==Vector2Int.up,"Up touch");
            Check(direction(new Vector2(280,200))==Vector2Int.right,"Right touch");
            Check(direction(new Vector2(200,120))==Vector2Int.down,"Down touch");
            Check(direction(new Vector2(120,200))==Vector2Int.left,"Left touch");
            Check(direction(new Vector2(200,200))==Vector2Int.zero,"Neutral center");
            Check(direction(new Vector2(90,200))==Vector2Int.zero,"Dragging outside releases direction");
            Check(direction(new Vector2(265,250))==Vector2Int.right,"Diagonal chooses one cardinal direction");
            var board = root.AddComponent<GridPlayground>();
            typeof(GridPlayground).GetField("mobilePad",Private).SetValue(board,pad);
            type.GetField("<Held>k__BackingField",Private).SetValue(pad,Vector2Int.up);
            type.GetField("<Pressed>k__BackingField",Private).SetValue(pad,Vector2Int.up);
            Check((bool)typeof(GridPlayground).GetMethod("DirectionHeld",Private).Invoke(board,new object[]{Vector2Int.up}),"Held touch reaches existing movement controller");
            Check((bool)typeof(GridPlayground).GetMethod("DirectionPressed",Private).Invoke(board,new object[]{Vector2Int.up}),"Fresh touch reaches existing movement controller");
            type.GetMethod("OnApplicationFocus",Private).Invoke(pad,new object[]{false});
            Check(pad.Held==Vector2Int.zero&&pad.Pressed==Vector2Int.zero,"Focus loss clears touch state");
            type.GetField("<Held>k__BackingField",Private).SetValue(pad,Vector2Int.left);
            type.GetMethod("OnApplicationPause",Private).Invoke(pad,new object[]{true});
            Check(pad.Held==Vector2Int.zero,"Backgrounding clears touch state");
            Check(!PlayerSettings.allowedAutorotateToPortrait&&!PlayerSettings.allowedAutorotateToPortraitUpsideDown,"Landscape-only configuration");
            foreach(var scene in EditorBuildSettings.scenes)if(scene.enabled)Check(File.Exists(scene.path),"Missing build scene: "+scene.path);
            Directory.CreateDirectory("output");
            File.WriteAllText("output/mobile-control-regression.txt","PASS: cardinal touch regions, neutral center, drag outside, diagonal restriction, movement-controller held/press integration, focus/background release, landscape settings, build scene paths. Device touch and gameplay still require Android testing.");
            Debug.Log("MobileControlRegression PASS");
        } finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
}
