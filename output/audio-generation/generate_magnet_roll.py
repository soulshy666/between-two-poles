"""Seamless original metal-on-ground rolling loop, no sampled recordings."""
from pathlib import Path
import uuid
import re
import wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
SR=48000
N=SR
t=np.arange(N)/SR
rng=np.random.default_rng(2026101619)
# Circular spectral filtering makes the texture continuous across the loop seam.
f=np.fft.rfftfreq(N,1/SR)
def texture(low,high):
    a=np.fft.rfft(rng.normal(size=N))
    gain=(1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    x=np.fft.irfft(a*gain,n=N)
    return x/np.std(x)
x=.055*texture(150,900)+.018*texture(700,2600)
# Six soft contacts per loop, with a subdued metallic body rather than a rattle.
for onset in np.arange(6)/6:
    age=(t-onset)%1
    env=(1-np.exp(-age/.003))*np.exp(-age/.022)
    x+=env*(.12*np.sin(2*np.pi*410*age)+.035*np.sin(2*np.pi*930*age))
x*=.85+.15*np.cos(2*np.pi*6*t)
x*=.32/max(abs(x))
out=ROOT/'Assets/BetweenPoles/Resources/Audio/Player/Actions/MagnetRollLoop.wav'
with wave.open(str(out),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR)
    w.writeframes(np.rint(np.column_stack((x,x))*32767).astype('<i2').tobytes())
meta=Path(str(out)+'.meta')
if not meta.exists():
    template=(out.parent/'JumpDownLand.wav.meta').read_text(encoding='utf-8-sig')
    meta.write_text(re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,template),encoding='utf-8')
script=ROOT/'Assets/BetweenPoles/Scripts/Player/MagnetRollAudio.cs.meta'
if not script.exists():script.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
assert np.isfinite(x).all() and max(abs(x))<1
print('Loop: 1s, 48kHz stereo PCM16, peak',max(abs(x)), 'seam delta',abs(x[0]-x[-1]))
