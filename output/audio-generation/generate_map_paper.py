"""Original short paper unfolding/folding Foley, synthesized without samples."""
from pathlib import Path
import uuid
import wave
import numpy as np
from scipy.signal import butter, sosfilt

SR=48000
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/BetweenPoles/Resources/Audio/Player/Actions'

def make(name,closing,seed):
    rng=np.random.default_rng(seed)
    seconds=.30 if closing else .38
    t=np.arange(round(seconds*SR))/SR
    x=np.zeros_like(t)
    # One continuous gesture, with texture inside it rather than separate swishes.
    gestures=[(0,seconds-.015,1)]
    for onset,duration,gain in gestures:
        age=(t-onset)/duration
        env=np.sin(np.pi*np.clip(age,0,1))**1.7
        noise=sosfilt(butter(2,[550,4800],btype='bandpass',fs=SR,output='sos'),rng.normal(size=len(t)))
        knots=np.arange(0,seconds+.04,.025)
        flutter=np.interp(t,knots,rng.uniform(.85,1.05,len(knots)))
        x+=gain*.16*noise*env*flutter
    for onset in rng.uniform(seconds*.25,seconds*.65,5):
        age=np.maximum(t-onset,0)
        env=(t>=onset)*(1-np.exp(-age/.0007))*np.exp(-age/.004)
        grain=sosfilt(butter(2,[1000,6500],btype='bandpass',fs=SR,output='sos'),rng.normal(size=len(t)))
        x+=rng.uniform(.008,.018)*grain*env
    x=sosfilt(butter(2,6200,fs=SR,output='sos'),x)
    x*=np.clip(t/.004,0,1)*np.clip((seconds-t)/.035,0,1)
    x*=.40/np.max(np.abs(x))
    path=OUT/(name+'.wav')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR)
        w.writeframes(np.rint(np.column_stack((x,x))*32767).astype('<i2').tobytes())
    meta=Path(str(path)+'.meta')
    if not meta.exists():
        template=(OUT/'JumpDownLand.wav.meta').read_text(encoding='utf-8-sig')
        import re
        meta.write_text(re.sub(r'guid: \w+', 'guid: '+uuid.uuid4().hex,template),encoding='utf-8')
    print(name,seconds,'seconds, peak',round(float(max(abs(x))),3))

make('MapPaperOpen',False,2026101601)
make('MapPaperClose',True,2026101602)
