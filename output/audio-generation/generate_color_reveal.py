"""Approved circle reveal, option 2: parted air and open harmony.

Original deterministic synthesis. Lower/broader than the golden-star reward;
no engine rumble, sharp cash-register bell, percussion, or adventure fanfare.
"""
from pathlib import Path
import wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
t = np.arange(round(2.8 * SR)) / SR
x = np.zeros((len(t), 2))
rng = np.random.default_rng(2026101302)


def smooth(v):
    v = np.clip(v, 0, 1)
    return v*v*(3-2*v)


def add(signal, pan):
    x[:, 0] += signal*np.sqrt((1-pan)/2)
    x[:, 1] += signal*np.sqrt((1+pan)/2)


def note(start, midi, gain, pan, attack=.018, decay=.55):
    age = np.maximum(t-start, 0)
    f = 440*2**((midi-69)/12)
    tone = np.sin(2*np.pi*f*age)
    tone += .16*np.sin(2*np.pi*f*2*age)*np.exp(-age/.16)
    tone += .025*np.sin(2*np.pi*f*3*age)*np.exp(-age/.075)
    env = smooth((t-start)/attack)*np.exp(-age/decay)
    add(tone*env*gain, pan)


# A clear arrival seed spreads into a short rising discovery phrase.
for start, midi, gain, pan in [(0, 69, .16, 0), (.12, 74, .18, -.25),
                               (.34, 76, .14, -.1), (.57, 78, .16, .15),
                               (.83, 81, .12, .3)]:
    note(start, midi, gain, pan)

# A restrained air veil opens sideways, then disappears as the view clears.
for pan in [-.65, .65]:
    noise = sosfilt(butter(2, [700, 2700], btype='bandpass', fs=SR, output='sos'),
                    rng.normal(size=len(t)))
    noise /= np.std(noise)
    env = smooth(t/.16)*(1-smooth((t-.38)/.62))
    add(noise*env*.009, pan)

# Warm open fifth, then a major-sixth bloom: recognition, not a victory march.
for midi, gain, pan in [(50, .065, 0), (62, .15, -.35), (69, .10, .35),
                        (78, .13, -.15), (83, .065, .2)]:
    note(.68, midi, gain, pan, attack=.13, decay=.73)
note(1.18, 74, .10, 0, attack=.035, decay=.56)

dry = x.copy()
for delay, gain in [(.061, .15), (.127, .10), (.223, .065), (.359, .035)]:
    n = round(delay*SR)
    x[n:] += dry[:-n, ::-1]*gain
x = sosfilt(butter(2, 5000, fs=SR, output='sos'), x, axis=0)
x -= x.mean(axis=0)
x *= smooth(t/.004)[:, None]*(1-smooth((t-2.0)/.8))[:, None]
x *= .88/np.max(np.abs(x))
# Requested +30% base gain, with a soft ceiling only on the loudest peaks.
x *= 1.3
level = np.abs(x)
x = np.sign(x)*np.where(level > .90, .90+.08*np.tanh((level-.90)/.08), level)
assert np.isfinite(x).all() and np.max(np.abs(x)) < 1

root = Path(__file__).resolve().parents[2]
path = root/'Assets/BetweenPoles/Resources/Audio/Intro/ColorReveal.wav'
with wave.open(str(path), 'wb') as wav:
    wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(SR)
    wav.writeframes((x*32767).astype('<i2').tobytes())
print(f'{path}: 2.80s stereo, peak={np.max(np.abs(x)):.3f}, RMS={np.sqrt(np.mean(x*x)):.3f}')
