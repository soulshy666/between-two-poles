"""Original rounded puzzle feedback study. No reference audio is reused."""
from pathlib import Path
import wave
import numpy as np

SR = 48000
OUT = Path(__file__).resolve().parent / 'dittori-study'
OUT.mkdir(parents=True, exist_ok=True)

def tone(freq=235):
    t = np.arange(int(.16 * SR)) / SR
    # A tiny settling glide, not vibrato; soft attack and a clean short release.
    f = freq * (1 + .055 * np.exp(-t / .012))
    phase = 2 * np.pi * np.cumsum(f) / SR
    attack = np.clip(t / .006, 0, 1)
    attack = attack * attack * (3 - 2 * attack)
    release = np.clip((.15 - t) / .055, 0, 1)
    release = release * release * (3 - 2 * release)
    env = attack * release * np.exp(-t / .085)
    return .34 * env * (np.sin(phase) + .085 * np.sin(2 * phase) + .018 * np.sin(3 * phase))

def save(name, x):
    assert np.isfinite(x).all() and np.max(np.abs(x)) < 1
    with wave.open(str(OUT / name), 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(np.rint(np.column_stack((x, x)) * 32767).astype('<i2').tobytes())
    print(f'{name}: {len(x)/SR:.2f}s, peak {np.max(np.abs(x)):.3f}')

save('minimal_blip_single.wav', tone())
demo = np.zeros(SR * 7)
# Slow repeats, quick repeats, then small pitch variations of the same timbre.
for onset, freq in [(.3,235),(.95,235),(1.6,235),(2.5,235),(2.72,235),(2.94,235),(3.16,235),(3.38,235),(4.3,220),(4.75,235),(5.2,247),(5.65,235),(6.1,220)]:
    clip = tone(freq)
    start = round(onset * SR)
    demo[start:start+len(clip)] += clip
save('minimal_blip_preview.wav', demo)
