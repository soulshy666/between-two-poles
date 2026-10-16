"""Single soft sole contact for height transitions; no takeoff or second impact."""
from pathlib import Path
import wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 48000
t = np.arange(round(.22 * SR)) / SR
rng = np.random.default_rng(20261015)

def smooth(v):
    v = np.clip(v, 0, 1)
    return v * v * (3 - 2 * v)

# A rounded, textured attack rather than a pitched click or bass-heavy thud.
noise = rng.normal(size=len(t))
sole = sosfilt(butter(2, [330, 2200], btype='bandpass', fs=SR, output='sos'), noise)
sole /= np.std(sole)
body = sosfilt(butter(2, [150, 600], btype='bandpass', fs=SR, output='sos'), noise)
body /= np.std(body)
x = (.65 * sole * np.exp(-t / .018) + .25 * body * np.exp(-t / .03))
x *= smooth(t / .003) * (1 - smooth((t - .11) / .06))
x = sosfilt(butter(2, 3000, fs=SR, output='sos'), x)
x *= .38 / np.max(np.abs(x))
out = Path(__file__).resolve().parents[2] / 'Assets/BetweenPoles/Resources/Audio/Player/Actions/JumpDownLand.wav'
pcm = np.rint(np.column_stack((x, x)) * 32767).astype('<i2')
with wave.open(str(out), 'wb') as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(pcm.tobytes())
print(f'{out}: duration={len(t)/SR:.2f}s, peak={np.max(np.abs(x)):.3f}, RMS={np.sqrt(np.mean(x*x)):.4f}')
