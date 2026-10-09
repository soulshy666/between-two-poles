// Run with execute_code in Unity Play mode, with the application focused.
if(!Application.isPlaying||!Application.isFocused)throw new System.Exception("Focused Play mode required");
var root=new GameObject("Idle spacewalk regression");root.SetActive(false);
try{
    var board=root.AddComponent<BetweenPoles.GridPlayground>();board.enabled=false;
    var player=new GameObject("Player");player.transform.SetParent(root.transform,false);board.player=player.transform;
    player.transform.position=new Vector3(150,0,150);var home=player.transform.position;
    var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BetweenPoles/Art/AstronautV1/AstronautVisual.prefab"),player.transform);visual.name="Visual";
    var tileObject=new GameObject("Tile");tileObject.transform.SetParent(root.transform,false);tileObject.transform.position=home;
    board.tiles=new[]{tileObject.AddComponent<BetweenPoles.GridTile>()};board.magnets=new BetweenPoles.MagnetPiece[0];root.SetActive(true);
    var pose=player.GetComponent<BetweenPoles.PlayerPushPose>();
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var type=typeof(BetweenPoles.PlayerPushPose);
    var wait=type.GetField("idleWait",flags);var active=type.GetField("idlePlaying",flags);
    var elapsed=type.GetField("idleElapsed",flags);var tick=type.GetMethod("UpdateIdle",flags);
    System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);};
    check(pose!=null,"idle must initialize before first movement");
    board.enabled=true;wait.SetValue(pose,0f);tick.Invoke(pose,null);
    check(!(bool)active.GetValue(pose),"idle began before five seconds");
    wait.SetValue(pose,5.01f);tick.Invoke(pose,null);check((bool)active.GetValue(pose),"idle did not start after timeout");
    var restPosition=visual.transform.localPosition;var restRotation=visual.transform.localRotation;
    var original=(Vector3[])type.GetField("vertices",flags).GetValue(pose);
    var mesh=(Mesh)type.GetField("posed",flags).GetValue(pose);
    var body=(MeshFilter)type.GetField("body",flags).GetValue(pose);
    bool inverted=false,airborne=false,kneeling=false;
    for(int frame=0;frame<=360;frame++){
        float t=frame/60f;pose.SampleIdleSpacewalk(t);
        check(player.transform.position==home&&!board.Busy&&board.UndoCount==0,"idle modified gameplay");
        if(t>1&&t<4&&Vector3.Dot(visual.transform.up,Vector3.up)<-.8f)inverted=true;
        if(t>1&&t<4&&visual.transform.position.y-player.transform.position.y>.7f)airborne=true;
        if(t>4.75f&&t<5.15f){
            float bottom=float.PositiveInfinity;foreach(var v in mesh.vertices)bottom=Mathf.Min(bottom,body.transform.TransformPoint(v).y-home.y);
            check(bottom>-.005f&&bottom<.03f,"kneeling penetrated or floated above ground");
            kneeling=visual.transform.localPosition.y<restPosition.y-.05f;
        }
    }
    check(inverted&&airborne&&kneeling,"missing backflip, lift or kneeling phase");
    elapsed.SetValue(pose,6.01f);tick.Invoke(pose,null);
    check(!(bool)active.GetValue(pose)&&visual.transform.localPosition==restPosition&&Quaternion.Angle(visual.transform.localRotation,restRotation)<.01f,"idle failed to finish");
    foreach(float time in new[]{.2f,1.8f,3f,4.9f,5.5f}){
        wait.SetValue(pose,5.01f);tick.Invoke(pose,null);pose.SampleIdleSpacewalk(time);
        board.TryStep(Vector2Int.left); // Even a blocked move must cancel idle.
        check(!(bool)active.GetValue(pose)&&visual.transform.localPosition==restPosition,"input failed to cancel pose");
        var current=mesh.vertices;for(int i=0;i<current.Length;i++)check(Vector3.Distance(current[i],original[i])<.00001f,"residual limb deformation");
        var balance=player.GetComponent<BetweenPoles.PlayerPushPose>();balance.End();
    }
    wait.SetValue(pose,5.01f);board.SetOpeningCinematic(true);tick.Invoke(pose,null);
    check(!(bool)active.GetValue(pose),"idle started during cinematic");board.SetOpeningCinematic(false);
    return "PASS: five-second trigger, full airborne backflip, grounded kneeling, no movement/history, natural recovery, blocked input cancellation at five phases and cinematic exclusion";
}finally{UnityEngine.Object.Destroy(root);}
