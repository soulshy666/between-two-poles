"""Four original light-space-fold portal cues, 48 kHz stereo PCM16.

Exit files contain emergence at zero and landing at 1.05s. PortalAudio slices
these phases in memory and triggers landing from actual animation contact.
"""
from pathlib import Path
import wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'Assets/BetweenPoles/Resources/Audio/Player/Actions'


def smooth(v):
    v = np.clip(v, 0, 1)
    return v*v*(3-2*v)


def make(name, duration, metal, entry, seed):
    t = np.arange(round(duration*SR))/SR
    rng = np.random.default_rng(seed)
    def noise(low, high):
        n = sosfilt(butter(2, [low, high], btype='bandpass', fs=SR,
                          output='sos'), rng.normal(size=len(t)))
        return n/np.std(n)
    def tone(freq, age, decay):
        return np.sin(2*np.pi*freq*age)*np.exp(-age/decay)
    if entry:
        u = t/duration
        # Broad, light suction gathers inward and vanishes with the object.
        env = smooth(u/.13)*(.24+.76*smooth(u/.7))*(1-smooth((u-.70)/.30))
        airy = .065*noise(650 if metal else 900, 3100)*(1-.55*smooth(u))
        airy += .065*noise(320 if metal else 500, 1600)*smooth(u)
        # One restrained falling contour, no vibrato or sustained engine note.
        freq = (620 if metal else 920)-(390 if metal else 510)*smooth(u)
        phase = 2*np.pi*np.cumsum(freq)/SR
        body = .035*np.sin(phase)
        if metal:
            body += .022*np.sin(phase*1.48)*(1-smooth((u-.65)/.35))
        mono = (airy+body)*env
        side = .022*noise(750, 2400)*env*(1-smooth(u))
    else:
        # Small, expanding release; leave the flight itself quiet.
        u = t/.34
        env = smooth(t/.006)*(1-smooth((t-.04)/.30))
        freq = (320 if metal else 470)+(330 if metal else 500)*smooth(u)
        phase = 2*np.pi*np.cumsum(freq)/SR
        mono = (.070*noise(550 if metal else 850, 2800)+.045*np.sin(phase))*env
        side = .022*noise(800, 2500)*env*smooth(u/.4)
        # Real contact is a muted body/fabric tap or a short rounded metal clack.
        age = np.maximum(0, t-1.05)
        attack = smooth((t-1.05)/.002)
        contact = .14*tone(175 if metal else 145, age, .037)
        contact += .09*noise(480 if metal else 350, 2600 if metal else 1800)*np.exp(-age/.025)
        if metal:
            contact += .045*tone(740, age, .058)+.023*tone(1120, age, .043)
        else:
            contact += .025*noise(650, 1800)*np.exp(-age/.070)
        mono += contact*attack*(1-smooth((age-.18)/.16))
    x = np.stack((mono+side, mono-side), axis=1)
    x = sosfilt(butter(2, 4400, fs=SR, output='sos'), x, axis=0)
    x -= x.mean(axis=0)
    x *= smooth(t/.003)[:, None]*(1-smooth((t-duration+.025)/.025))[:, None]
    x *= (.73 if metal else .65)/np.max(np.abs(x))
    assert np.isfinite(x).all() and np.max(np.abs(x)) < 1
    with wave.open(str(OUT/(name+'.wav')), 'wb') as wav:
        wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(SR)
        wav.writeframes((x*32767).astype('<i2').tobytes())
    print(f'{name}: {duration:.2f}s, peak={np.max(np.abs(x)):.3f}, RMS={np.sqrt(np.mean(x*x)):.3f}')


OUT.mkdir(parents=True, exist_ok=True)
make('PortalPlayerEnter', .95, False, True, 202610131)
make('PortalPlayerExit', 1.40, False, False, 202610132)
make('PortalMagnetEnter', .65, True, True, 202610133)
make('PortalMagnetExit', 1.40, True, False, 202610134)
