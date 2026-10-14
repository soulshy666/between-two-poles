"""Release-only revision; never touches the approved MagnetRecoilCharge.wav."""
from pathlib import Path
import wave
import numpy as np

sr = 48000
t = np.arange(int(.42*sr))/sr
rng = np.random.default_rng(202610112)

def smooth(v):
    v = np.clip(v, 0, 1)
    return v*v*(3-2*v)

def noise(low, high):
    f = np.fft.rfftfreq(len(t), 1/sr)
    mask = (1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    x = np.fft.irfft(np.fft.rfft(rng.normal(size=len(t)))*mask, n=len(t))
    return x/np.std(x)

# A compact "pwip": fast, monotonic energy release with no pitch wobble.
phase = 2*np.pi*np.cumsum(290+760*np.exp(-t/.016))/sr
core = .23*np.sin(phase)*smooth(t/.002)*np.exp(-t/.038)
edge = .07*noise(1100,3400)*smooth(t/.0015)*np.exp(-t/.013)
body = .09*np.sin(2*np.pi*175*t)*smooth(t/.003)*np.exp(-t/.027)
env = smooth(t/.006)*np.exp(-t/.063)*(1-smooth((t-.2)/.19))
air = .08*noise(650,2600)*env
mono = core+edge+body+air
width = .016*noise(900,2200)*env*smooth(t/.018)
x = np.stack((mono+width,mono-width),axis=1)
x -= x.mean(axis=0)
x *= .48/np.max(np.abs(x))
x[:72] *= smooth(np.arange(72)/71)[:,None]
x[-1920:] *= smooth(np.arange(1920)[::-1]/1919)[:,None]
assert np.isfinite(x).all() and np.max(np.abs(x))*2 < 1
path = Path('Assets/BetweenPoles/Resources/Audio/Player/Actions/MagnetRecoil.wav')
with wave.open(str(path),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(sr)
    w.writeframes((x*32767).astype('<i2').tobytes())
print('Release: 0.42 seconds; peak', float(np.max(np.abs(x))))
