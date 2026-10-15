"""Golden-star achievement cue, synchronized to GridPlayground.Star.

The 4.20-second clip begins at hand contact (animation t=.55).
Toss is at clip t=.90, burst at 1.90, absorption resolves at 3.30.
No external samples; deterministic synthesis, 48 kHz stereo PCM16.
"""
from pathlib import Path
import wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
T = np.arange(round(4.20 * SR)) / SR
mix = np.zeros((len(T), 2))
rng = np.random.default_rng(20261013)


def smooth(v):
    v = np.clip(v, 0, 1)
    return v * v * (3 - 2 * v)


def pan_add(signal, pan=0):
    mix[:, 0] += signal * np.sqrt((1 - pan) / 2)
    mix[:, 1] += signal * np.sqrt((1 + pan) / 2)


def note(start, midi, gain, pan=0, decay=.35, attack=.009):
    age = np.maximum(T - start, 0)
    freq = 440 * 2 ** ((midi - 69) / 12)
    # Rounded crystalline pluck, harmonic rather than metallic/inharmonic.
    tone = np.sin(2 * np.pi * freq * age)
    tone += .22 * np.sin(4 * np.pi * freq * age) * np.exp(-age / .095)
    tone += .055 * np.sin(6 * np.pi * freq * age) * np.exp(-age / .04)
    env = smooth((T - start) / attack) * np.exp(-age / decay)
    pan_add(gain * tone * env, pan)


def air(start, length, gain, low=650, high=2400):
    noise = sosfilt(butter(2, [low, high], fs=SR, btype='bandpass', output='sos'),
                    rng.normal(size=len(T)))
    noise /= np.std(noise)
    u = (T - start) / length
    env = smooth(u / .35) * (1 - smooth((u - .60) / .40))
    pan_add(noise * env * gain)


# Touch: a small, readable two-note pickup, not a cash-register strike.
note(0, 74, .18, -.18, .25)
note(.075, 81, .10, .18, .28)
air(0, .16, .008)

# Hands lift and throw: progressively brighter steps, with restrained airy motion.
for start, midi, gain, pan in [(.90, 74, .14, -.25), (1.13, 78, .14, -.1),
                               (1.35, 81, .13, .1), (1.56, 83, .105, .25),
                               (1.74, 86, .08, .1)]:
    note(start, midi, gain, pan, .27)
air(.90, 1.0, .014)

# The golden flash opens into a compact D-major-sixth achievement chord.
# A firm warm body carries the reward; the high layer remains restrained.
for midi, gain, pan in [(50, .12, 0), (62, .17, -.2), (69, .11, .2),
                        (74, .20, -.3), (78, .14, .05), (83, .085, .3)]:
    note(1.90, midi, gain, pan, .58 if midi < 70 else .67, .008)
air(1.90, .24, .025, 450, 2800)

# Star fragments converge into the chest, then a confident soft resolution.
for start, midi, gain, pan in [(2.23, 86, .065, .4), (2.46, 81, .07, -.3),
                               (2.70, 78, .075, .2), (2.96, 74, .08, -.1)]:
    note(start, midi, gain, pan, .24)
for midi, gain, pan in [(62, .10, -.15), (69, .075, .15), (78, .065, 0)]:
    note(3.30, midi, gain, pan, .38, .025)

# Short stereo reflections give outer-space breadth without a long wash.
dry = mix.copy()
for delay, gain in [(.047, .11), (.089, .07), (.153, .045), (.229, .025)]:
    samples = round(delay * SR)
    mix[samples:] += dry[:-samples, ::-1] * gain
mix = sosfilt(butter(2, 5500, fs=SR, output='sos'), mix, axis=0)
mix -= mix.mean(axis=0)
mix *= smooth(T / .003)[:, None]
mix *= (1 - smooth((T - 3.75) / .45))[:, None]
mix *= .78 / np.max(np.abs(mix))
# Bake the requested +50% base gain into the clip so AudioSource seeking still
# works (AudioSource.volume clamps at 1). Round only the loudest flash peaks.
mix *= 1.5
level = np.abs(mix)
mix = np.sign(mix) * np.where(level > .90,
    .90 + .08 * np.tanh((level - .90) / .08), level)
assert np.isfinite(mix).all() and np.max(np.abs(mix)) < 1

root = Path(__file__).resolve().parents[2]
path = root / 'Assets/BetweenPoles/Resources/Audio/Player/Actions/StarCollect.wav'
path.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(path), 'wb') as wav:
    wav.setnchannels(2)
    wav.setsampwidth(2)
    wav.setframerate(SR)
    wav.writeframes((mix * 32767).astype('<i2').tobytes())
print(f'{path}: {len(T)/SR:.2f}s, peak={np.max(np.abs(mix)):.3f}, '
      f'RMS={np.sqrt(np.mean(mix**2)):.3f}')
