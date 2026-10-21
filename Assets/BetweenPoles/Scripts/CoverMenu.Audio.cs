using UnityEngine;
namespace BetweenPoles {
public sealed partial class CoverMenu {
 AudioSource menuAudio;
 AudioClip menuClick;
 AudioSource coverMusic;
 void UpdateCoverMusic(){
  if(!coverMusic){
   var host=new GameObject("主菜单 · 太空背景音乐");host.transform.SetParent(transform,false);
   coverMusic=host.AddComponent<AudioSource>();coverMusic.playOnAwake=false;
   coverMusic.loop=true;coverMusic.spatialBlend=0;coverMusic.dopplerLevel=0;
   coverMusic.volume=0;coverMusic.ignoreListenerPause=true;
   coverMusic.clip=Resources.Load<AudioClip>("Audio/CoverBackgroundMusic");
   if(coverMusic.clip)coverMusic.Play();
  }
  if(!coverMusic.clip)return;
  if(!loading&&!coverMusic.isPlaying)coverMusic.Play();
  coverMusic.volume=Mathf.MoveTowards(coverMusic.volume,loading?0:.75f,
   Time.unscaledDeltaTime*(loading?1.7f:.5f));
 }
 void StopCoverMusic(){if(coverMusic){coverMusic.Stop();coverMusic.volume=0;}}
 void PlayMenuClick(){
  if(!Application.isPlaying||loading||TelevisionSceneTransition.Busy||AudioListener.volume<=0)return;
  if(!menuAudio){
   var host=new GameObject("主菜单 · 按钮音效");host.transform.SetParent(transform,false);
   menuAudio=host.AddComponent<AudioSource>();menuAudio.playOnAwake=false;
   menuAudio.spatialBlend=0;menuAudio.ignoreListenerPause=true;
  }
  if(!menuClick){
   const int rate=22050;const float duration=.10f;
   var samples=new float[Mathf.CeilToInt(rate*duration)];
   for(int i=0;i<samples.Length;i++){
    float t=i/(float)rate,frequency=t<.045f?880:1320;
    float envelope=Mathf.Min(1,t/.003f)*Mathf.Min(1,(duration-t)/.015f);
    samples[i]=Mathf.Sign(Mathf.Sin(t*frequency*Mathf.PI*2))*.05f*envelope;
   }
   menuClick=AudioClip.Create("主菜单 · 像素点击",samples.Length,1,rate,false);menuClick.SetData(samples,0);
  }
  menuAudio.PlayOneShot(menuClick);
 }
 System.Collections.IEnumerator QuitAfterClick(){
  loading=true;
  yield return new WaitForSecondsRealtime(.12f);
#if UNITY_EDITOR
  UnityEditor.EditorApplication.isPlaying=false;
#else
  Application.Quit();
#endif
 }
}
}
