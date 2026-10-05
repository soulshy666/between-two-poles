using UnityEngine;

namespace BetweenPoles {
public sealed class MagneticLinkPulse:MonoBehaviour {
    float begin,end,lane;
    Transform[] waveSegments,pulses;

    public void Configure(float start,float finish,float z) {
        begin=start;end=finish;lane=z;Cache();
    }

    void OnEnable(){Cache();}

    void Cache() {
        var waves=new System.Collections.Generic.List<Transform>();
        var packets=new System.Collections.Generic.List<Transform>();
        for(int i=0;i<transform.childCount;i++){
            var child=transform.GetChild(i);
            if(child.name=="波动磁流")waves.Add(child);
            else if(child.name=="传输脉冲")packets.Add(child);
        }
        waveSegments=waves.ToArray();pulses=packets.ToArray();
    }

    void Update() {
        if(waveSegments==null||waveSegments.Length==0||pulses==null||pulses.Length==0)Cache();
        float time=Time.time;
        for(int i=0;i<waveSegments.Length;i++){
            Vector3 from=WavePoint(i/(float)waveSegments.Length,time);
            Vector3 to=WavePoint((i+1)/(float)waveSegments.Length,time);
            Vector3 delta=to-from;
            waveSegments[i].localPosition=(from+to)*.5f;
            waveSegments[i].localRotation=Quaternion.FromToRotation(Vector3.right,delta.normalized);
            waveSegments[i].localScale=new Vector3(delta.magnitude+.02f,.045f,.065f);
        }
        for(int i=0;i<pulses.Length;i++){
            float progress=Mathf.Repeat(time*1.25f+i/(float)pulses.Length,1);
            pulses[i].localPosition=WavePoint(progress,time)+Vector3.up*.025f;
            float strength=.78f+Mathf.Sin(progress*Mathf.PI)*.3f;
            pulses[i].localScale=new Vector3(.17f*strength,.085f*strength,.12f*strength);
        }
    }

    Vector3 WavePoint(float progress,float time) {
        // The envelope reaches zero at both sockets, so the animated current never disconnects.
        float envelope=Mathf.Sin(progress*Mathf.PI);
        float wave=Mathf.Sin(progress*Mathf.PI*4-time*8f)*envelope;
        return new Vector3(Mathf.Lerp(begin,end,progress),.16f+wave*.025f,lane+wave*.065f);
    }
}
}
