"""Magnetic recoil v6: brief rising charge, then a clean force release."""
from pathlib import Path
import wave
import numpy as np

sr = 48000
t = np.arange(int(.65*sr))/sr
rng = np.random.default_rng(20261011)
def smooth(x):
    x = np.clip(x, 0, 1)
    return x*x*(3-2*x)
def noise(low, high):
    f = np.fft.rfftfreq(len(t), 1/sr)
    mask = (1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    x = np.fft.irfft(np.fft.rfft(rng.normal(size=len(t)))*mask, n=len(t))
    return x/np.std(x)

# One dry, band-limited snap. No periodic modulation, pitch sweep or echoes.
attack = smooth(t/.0015)
snap = .16*noise(800,3200)*np.exp(-t/.023)*attack
body = .24*np.sin(2*np.pi*210*t)*np.exp(-t/.037)*attack
pressure = .085*noise(180,1000)*np.exp(-t/.047)*attack
# A brief unpitched rush starts under the snap and then clears quickly.
env = smooth(t/.009)*np.exp(-t/.105)*(1-smooth((t-.32)/.25))
air = .10*noise(600,2600)*env
mono = snap+body+pressure+air
spread = .012*noise(900,2400)*env*smooth(t/.025)
x = np.stack((mono+spread,mono-spread),axis=1)
x -= x.mean(axis=0)
x *= .48/np.max(np.abs(x))
x[:72] *= smooth(np.arange(72)/71)[:,None]
x[-2400:] *= smooth(np.arange(2400)[::-1]/2399)[:,None]
# Preserve headroom at the requested 200% playback gain.
assert np.isfinite(x).all() and np.max(np.abs(x))*2 < 1
path = Path('Assets/BetweenPoles/Resources/Audio/Player/Actions/MagnetRecoil.wav')
with wave.open(str(path),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(sr)
    w.writeframes((x*32767).astype('<i2').tobytes())
print('MagnetRecoil v6 release: 0.65 seconds; peak', float(np.max(np.abs(x))))

# Separate charge allows release to stay exactly on the gameplay takeoff event.
# A smooth upward tension sweep with no vibrato, rhythmic pulses or spring wobble.
t = np.arange(int(.4*sr))/sr
p = t/.4
phase = 2*np.pi*np.cumsum(270+600*p*p)/sr
env = smooth(t/.04)*(.18+.82*p**1.4)*(1-smooth((t-.375)/.025))
charge = (.13*np.sin(phase)+.025*np.sin(phase*2)+.035*noise(450,2200))*env
x = np.stack((charge,charge),axis=1)
x -= x.mean(axis=0)
x *= .28/np.max(np.abs(x))
x[:144] *= smooth(np.arange(144)/143)[:,None]
x[-240:] *= smooth(np.arange(240)[::-1]/239)[:,None]
assert np.isfinite(x).all() and np.max(np.abs(x))*2 < 1
path = path.with_name('MagnetRecoilCharge.wav')
with wave.open(str(path),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(sr)
    w.writeframes((x*32767).astype('<i2').tobytes())
print('MagnetRecoil charge: 0.4 seconds; peak', float(np.max(np.abs(x))))
