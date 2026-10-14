"""A short boot contact on packed snow; replaces ice footstep variant 2 only."""
from pathlib import Path
import wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
duration = .42
t = np.arange(round(SR*duration))/SR
rng = np.random.default_rng(2026101405)


def smooth(x):
    x = np.clip(x, 0, 1)
    return x*x*(3-2*x)


def texture(low, high):
    x = sosfilt(butter(2, [low, high], btype='bandpass', fs=SR, output='sos'), rng.normal(size=len(t)))
    return x/np.std(x)


# A damped sole impact with a slight downward pitch contour, not a musical note.
phase = 2*np.pi*(105*t+37*.025*(1-np.exp(-t/.025)))
impact = smooth(t/.004)*np.exp(-t/.042)
body = .48*np.sin(phase)*impact+.17*texture(85, 440)*impact
# Brief compact snow compression; no sustained sand/gravel rubbing layer.
crust = np.zeros_like(t)
for onset, level, decay in [(0,.075,.024),(.024,.052,.020),(.055,.034,.022),(.092,.018,.023)]:
    age = np.maximum(t-onset, 0)
    envelope = smooth((t-onset)/.003)*np.exp(-age/decay)
    crust += texture(380, 1550)*envelope*level
settle = .052*texture(110, 580)*smooth((t-.039)/.008)*np.exp(-np.maximum(t-.039,0)/.043)
mono = body+crust+settle
mono = sosfilt(butter(2, 1900, fs=SR, output='sos'), mono)
mono *= smooth(t/.002)*(1-smooth((t-.22)/.16))
stereo = np.stack([mono, mono*.985], axis=1)
stereo *= .66/np.max(np.abs(stereo))
assert np.isfinite(stereo).all() and np.max(np.abs(stereo)) < 1
path = Path(__file__).resolve().parents[2]/'Assets/BetweenPoles/Resources/Audio/Player/Footsteps/Chapter01_IceWorld_Footstep2.wav'
with wave.open(str(path), 'wb') as wav:
    wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(SR)
    wav.writeframes(np.rint(stereo*32767).astype('<i2').tobytes())
print(f'Packed-snow step: {duration:.2f}s, stereo 48kHz, peak {np.max(np.abs(stereo)):.2f}, RMS {np.sqrt(np.mean(stereo**2)):.3f}')
