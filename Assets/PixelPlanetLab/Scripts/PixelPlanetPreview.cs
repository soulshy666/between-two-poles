using UnityEngine;
using System.IO;

namespace BetweenPoles.PixelLab
{
    [ExecuteAlways]
    public class PixelPlanetPreview : MonoBehaviour
    {
        [Header("像素星球 · 中文实验室")]
        [InspectorName("海洋、大陆、云层")] public Renderer[] layers;
        [InspectorName("预览相机")] public Camera previewCamera;
        [Range(32,256), InspectorName("像素精度")] public int pixels = 100;
        [Range(-3,3), InspectorName("自转速度")] public float speed = 1;
        [Range(0,1), InspectorName("陆地覆盖率")] public float land = .43f;
        [Range(0,1), InspectorName("云层覆盖率")] public float clouds = .36f;
        [Range(1,10), InspectorName("地形种子")] public float seed = 7.947f;
        [Range(-180,180), InspectorName("星球倾角")] public float tilt = 12;
        [InspectorName("光源位置（左上为 0,0）")] public Vector2 lightOrigin = new Vector2(.3f,.3f);
        [InspectorName("暂停旋转")] public bool paused;
        [InspectorName("显示中文面板")] public bool showPanel = true;
        [SerializeField, HideInInspector] int palette;
        float phase;
        double lastTime;
        MaterialPropertyBlock block;
        Font chineseFont;
        string status = "拖动滑块可实时预览；编辑状态也会旋转。";
        public static readonly string[] Names = { "蓝绿地球", "冰蓝世界", "赤色荒星", "紫色异星" };
        static readonly string[][] Palettes = {
            new[]{"92e8c0","4fa4b8","2c354d","c8d45d","63ab3f","2f5753","283540","dfe0e8","a3a7c2","686f99","404973"},
            new[]{"97dbf5","477dba","202b55","ecfaf4","a2dcd9","548baf","314166","ffffff","cadceb","7998c6","43557e"},
            new[]{"b56345","73434c","292338","ebc080","be7957","854758","412a41","eed4b3","c79b94","865f83","483750"},
            new[]{"77c5d5","49599b","221c3b","d4a7e5","a26db9","63538d","302440","f5e1ec","c4a3d3","8366ac","443360"}
        };
        void OnEnable() {
            lastTime = Clock(); Apply();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }
        void OnDisable() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
            if(chineseFont) { if(Application.isPlaying) Destroy(chineseFont); else DestroyImmediate(chineseFont); }
        }
        double Clock() {
#if UNITY_EDITOR
            if(!Application.isPlaying) return UnityEditor.EditorApplication.timeSinceStartup;
#endif
            return Time.realtimeSinceStartupAsDouble;
        }
        void Update() { if(Application.isPlaying) Tick(); }
#if UNITY_EDITOR
        void EditorTick() { if(Application.isPlaying || this == null) return; if(Clock()-lastTime < 1.0/30) return; Tick(); UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); UnityEditor.SceneView.RepaintAll(); }
#endif
        void Tick() { double now=Clock(); if(!paused) phase += (float)System.Math.Min(.1,now-lastTime)*speed; lastTime=now; Apply(); }
        void OnValidate() { pixels=Mathf.Clamp(pixels,32,256); Apply(); }
        public void SelectPalette(int index) { palette=Mathf.Clamp(index,0,3); Apply(); }
        public void Randomize() { seed=Random.Range(1f,10f); Apply(); }
        public void Apply() {
            if(layers==null || layers.Length!=3) return;
            if(block==null) block=new MaterialPropertyBlock();
            int[] offsets={0,3,7};
            for(int i=0;i<3;i++) {
                if(!layers[i]) continue;
                block.Clear(); block.SetFloat("pixels",pixels); block.SetFloat("rotation",tilt*Mathf.Deg2Rad);
                block.SetVector("light_origin", new Vector4(lightOrigin.x,lightOrigin.y,0,0));
                block.SetFloat("time",phase); block.SetFloat("time_speed", i==2?.24f:.32f);
                block.SetFloat("size",i==0?5.228f:i==1?4.292f:7.745f);
                block.SetInt("OCTAVES",i==0?3:i==1?6:2);
                block.SetFloat("seed",seed); block.SetFloat("dither_size",2);
                block.SetFloat("should_dither",1); block.SetFloat("land_cutoff",Mathf.Lerp(.85f,.25f,land));
                block.SetFloat("cloud_cover",Mathf.Lerp(.65f,.15f,clouds));
                block.SetFloat("stretch",2); block.SetFloat("cloud_curve",1.3f);
                block.SetFloat("light_border_1",i==0?.4f:i==1?.32f:.52f);
                block.SetFloat("light_border_2",i==0?.6f:i==1?.534f:.62f);
                var colors=new Vector4[i==0?3:4];
                for(int c=0;c<colors.Length;c++) { Color color; ColorUtility.TryParseHtmlString("#"+Palettes[palette][offsets[i]+c],out color); colors[c]=QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color; }
                block.SetVectorArray("colors",colors); layers[i].SetPropertyBlock(block);
            }
        }
        float Slider(string title,float value,float min,float max,string format="F2") { GUILayout.Label(title+"   "+value.ToString(format),GUILayout.Height(25)); return GUILayout.HorizontalSlider(value,min,max,GUILayout.Height(22)); }
        void OnGUI() {
            if(!showPanel) return;
            if(!chineseFont) chineseFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Noto Sans CJK SC"},16);
            var oldFont=GUI.skin.font; GUI.skin.font=chineseFont;
            Matrix4x4 old=GUI.matrix; float scale=Mathf.Min(Screen.width/1100f,Screen.height/760f); GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUILayout.BeginArea(new Rect(22,22,285,716),GUI.skin.box);
            GUILayout.Space(12); GUILayout.Label("两极之间 / 像素星球实验室",new GUIStyle(GUI.skin.label){fontSize=18},GUILayout.Height(32));
            GUILayout.Label("Deep Fold · 类地星球 Unity 移植",GUILayout.Height(26)); GUILayout.Space(14);
            int next=GUILayout.SelectionGrid(palette,Names,2,GUILayout.Height(62)); if(next!=palette) SelectPalette(next);
            GUILayout.Space(12); pixels=Mathf.RoundToInt(Slider("像素精度",pixels,32,256,"F0"));
            speed=Slider("自转速度",speed,-3,3); land=Slider("陆地覆盖率",land,0,1); clouds=Slider("云层覆盖率",clouds,0,1);
            tilt=Slider("星球倾角",tilt,-180,180,"F0"); lightOrigin.x=Slider("光源 · 左右",lightOrigin.x,0,1); lightOrigin.y=Slider("光源 · 上下",lightOrigin.y,0,1);
            GUILayout.BeginHorizontal(); if(GUILayout.Button(paused?"继续旋转":"暂停旋转",GUILayout.Height(32))) paused=!paused; if(GUILayout.Button("随机地形",GUILayout.Height(32))) Randomize(); GUILayout.EndHorizontal();
            if(GUILayout.Button("导出透明 PNG",GUILayout.Height(36))) { try { status="已导出："+ExportPng(); } catch(System.Exception e) { status="导出失败："+e.Message; Debug.LogException(e); } }
            GUILayout.Label(status,new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=13});
            GUILayout.EndArea(); GUI.matrix=old; GUI.skin.font=oldFont; Apply();
        }
        public string ExportPng() {
            if(!previewCamera) throw new System.InvalidOperationException("未指定预览相机");
            var rt=RenderTexture.GetTemporary(pixels,pixels,24,RenderTextureFormat.ARGB32);
            var oldTarget=previewCamera.targetTexture; var oldActive=RenderTexture.active; var oldPos=previewCamera.transform.position;
            var oldColor=previewCamera.backgroundColor; float oldSize=previewCamera.orthographicSize; var oldFlags=previewCamera.clearFlags;
            Texture2D tex=null;
            try {
                previewCamera.targetTexture=rt; previewCamera.transform.position=new Vector3(transform.position.x,transform.position.y,-10);
                previewCamera.orthographicSize=2.55f; previewCamera.clearFlags=CameraClearFlags.SolidColor; previewCamera.backgroundColor=Color.clear;
                previewCamera.Render(); RenderTexture.active=rt; tex=new Texture2D(pixels,pixels,TextureFormat.RGBA32,false); tex.ReadPixels(new Rect(0,0,pixels,pixels),0,0); tex.Apply();
                string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../PixelPlanetExports")); Directory.CreateDirectory(dir);
                string path=Path.Combine(dir,"星球_"+System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".png"); File.WriteAllBytes(path,tex.EncodeToPNG()); return path;
            } finally { previewCamera.targetTexture=oldTarget; previewCamera.transform.position=oldPos; previewCamera.orthographicSize=oldSize; previewCamera.backgroundColor=oldColor; previewCamera.clearFlags=oldFlags; RenderTexture.active=oldActive; RenderTexture.ReleaseTemporary(rt); if(tex){if(Application.isPlaying) Destroy(tex);else DestroyImmediate(tex);} }
        }
    }
}
