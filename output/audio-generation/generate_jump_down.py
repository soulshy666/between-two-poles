"""Shared stone/magnet jump-down: cloth/air lift and a soft boot landing."""
from pathlib import Path
import wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
OUT = Path(__file__).resolve().parents[2]/'Assets/BetweenPoles/Resources/Audio/Player/Actions'

def smooth(v):
    v = np.clip(v, 0, 1)
    return v*v*(3-2*v)

def create(name, duration, landing, seed):
    t = np.arange(round(duration*SR))/SR
    rng = np.random.default_rng(seed)
    def noise(lo, hi):
        x = sosfilt(butter(2, [lo, hi], btype='bandpass', fs=SR, output='sos'), rng.normal(size=len(t)))
        return x/np.std(x)
    if landing:
        # Rounded boot contact, a restrained sole scuff, then body settling.
        env = smooth(t/.002)*np.exp(-t/.042)
        mono = (.16*np.sin(2*np.pi*155*t)+.045*np.sin(2*np.pi*265*t))*env
        mono += .075*noise(350, 1900)*env
        mono += .018*noise(550, 2300)*smooth(t/.018)*np.exp(-t/.075)
        width = .006*noise(650, 1600)*env
    else:
        env = smooth(t/.025)*(1-smooth((t-.10)/.30))
        mono = (.037*noise(600, 2300)+.017*noise(200, 800))*env
        width = .009*noise(800, 2500)*env
    x = np.stack((mono+width, mono-width), axis=1)
    x -= x.mean(axis=0)
    x *= smooth(t/.002)[:, None]*(1-smooth((t-duration+.04)/.04))[:, None]
    x *= (.70 if landing else .34)/np.max(np.abs(x))
    assert np.isfinite(x).all() and np.max(np.abs(x)) < 1
    with wave.open(str(OUT/(name+'.wav')), 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x*32767).astype('<i2').tobytes())
    print(f'{name}: {duration}s, peak {np.max(np.abs(x)):.3f}')

create('JumpDownAir', .42, False, 2026101319)
create('JumpDownLand', .28, True, 2026101320)
