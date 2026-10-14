"""New-island revision: two answering gestures open into a warm G-add9 chord.

Original deterministic synthesis. Lower/broader than the golden-star reward;
Same clear/airy family as circle option 2, but a distinct rhythm, contour,
pluck envelope and harmonic resolution. Never writes the circle reveal asset.
"""
from pathlib import Path
import wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
t = np.arange(round(2.8 * SR)) / SR
x = np.zeros((len(t), 2))
rng = np.random.default_rng(2026101304)


def smooth(v):
    v = np.clip(v, 0, 1)
    return v*v*(3-2*v)


def add(signal, pan):
    x[:, 0] += signal*np.sqrt((1-pan)/2)
    x[:, 1] += signal*np.sqrt((1+pan)/2)


def note(start, midi, gain, pan, attack=.012, decay=.38):
    age = np.maximum(t-start, 0)
    f = 440*2**((midi-69)/12)
    tone = np.sin(2*np.pi*f*age)
    tone += .25*np.sin(2*np.pi*f*2*age)*np.exp(-age/.095)
    tone += .045*np.sin(2*np.pi*f*3*age)*np.exp(-age/.055)
    env = smooth((t-start)/attack)*np.exp(-age/decay)
    add(tone*env*gain, pan)


# A paired "question / answer" gesture, rather than the circle's rising scale.
# The upward fifth settles downward, then opens into a spacious reward.
for start, midi, gain, pan in [(0, 67, .16, -.2), (.10, 74, .18, .2),
                               (.36, 71, .15, -.15), (.48, 69, .12, .15),
                               (.82, 79, .14, 0)]:
    note(start, midi, gain, pan)

# A restrained air veil opens sideways, then disappears as the view clears.
for pan in [-.65, .65]:
    noise = sosfilt(butter(2, [700, 2700], btype='bandpass', fs=SR, output='sos'),
                    rng.normal(size=len(t)))
    noise /= np.std(noise)
    env = smooth((t-.3)/.28)*(1-smooth((t-.66)/.65))
    add(noise*env*.006, pan)

# G major with an open ninth: warm discovery, without a victory fanfare.
for midi, gain, pan in [(55, .075, 0), (62, .095, -.3), (67, .14, .3),
                        (71, .12, -.15), (81, .055, .2)]:
    note(.82, midi, gain, pan, attack=.075, decay=.67)
# Two soft answering notes complete the phrase as the camera settles.
note(1.24, 74, .095, .22, attack=.022, decay=.40)
note(1.43, 79, .070, -.18, attack=.025, decay=.43)

dry = x.copy()
for delay, gain in [(.053, .12), (.113, .085), (.197, .055), (.307, .03)]:
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
path = root/'Assets/BetweenPoles/Resources/Audio/Player/Actions/IslandDiscovery.wav'
with wave.open(str(path), 'wb') as wav:
    wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(SR)
    wav.writeframes((x*32767).astype('<i2').tobytes())
print(f'{path}: 2.80s stereo, peak={np.max(np.abs(x)):.3f}, RMS={np.sqrt(np.mean(x*x)):.3f}')
