"""Light leather-boot pair on firm ice: short resonant heel, muted sole, no hiss."""
from pathlib import Path
import wave
import numpy as np
from scipy.signal import butter, sosfilt
SR=48000
rng=np.random.default_rng(2026101505)
t=np.arange(round(.29*SR))/SR

def smooth(x):
    x=np.clip(x,0,1)
    return x*x*(3-2*x)

x=np.zeros_like(t)
for onset,gain,tuning in [(0,1,1),(.132,.83,.94)]:
    age=np.maximum(t-onset,0)
    env=smooth((t-onset)/.0025)*(1-smooth((age-.065)/.035))
    heel=np.zeros_like(t)
    # Inharmonic, quickly damped surface modes; no long bell or pitched beep.
    for f,a,decay in [(430,.15,.014),(790,.10,.009),(1260,.055,.006),(1830,.018,.004)]:
        heel+=a*np.sin(2*np.pi*f*tuning*age)*np.exp(-age/decay)
    sole=sosfilt(butter(2,[280,950],btype='bandpass',fs=SR,output='sos'),rng.normal(size=len(t)))
    sole/=np.std(sole)
    heel+=.023*sole*smooth((age-.009)/.004)*np.exp(-np.maximum(age-.009,0)/.012)
    x+=gain*heel*env
x=sosfilt(butter(2,2600,fs=SR,output='sos'),x)
x*=smooth(t/.001)*(1-smooth((t-.24)/.035))
x*=.52/np.max(np.abs(x))
stereo=np.column_stack((x,x))
out=Path(__file__).resolve().parents[2]/'Assets/BetweenPoles/Resources/Audio/Player/Footsteps/Chapter01_IceWorld.wav'
with wave.open(str(out),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR)
    w.writeframes(np.rint(stereo*32767).astype('<i2').tobytes())
print('Leather boot on firm ice: 0.29s, two contacts 132ms apart.')
