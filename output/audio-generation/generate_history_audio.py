"""Five quiet, dry undo notes and a short island-reset settling cue."""
from pathlib import Path
import wave
import numpy as np

SR = 48000
OUT = Path(__file__).resolve().parents[2] / 'Assets/BetweenPoles/Resources/Audio/Player/Actions'


def smooth(x):
    x = np.clip(x, 0, 1)
    return x*x*(3-2*x)


def note(frequency, duration, decay):
    t = np.arange(round(SR*duration))/SR
    # Single pitch, rounded attack, no echo/reverb or harsh high harmonics.
    signal = np.sin(2*np.pi*frequency*t) + .07*np.sin(2*np.pi*frequency*2*t)
    envelope = smooth(t/.003)*np.exp(-t/decay)*(1-smooth((t-duration+.022)/.022))
    return signal*envelope


def save(name, signal, peak):
    signal *= peak/np.max(np.abs(signal))
    assert np.isfinite(signal).all() and np.max(np.abs(signal)) < 1
    pcm = (signal*32767).astype('<i2')
    with wave.open(str(OUT/(name+'.wav')), 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(SR)
        wav.writeframes(pcm.tobytes())
    print(f'{name}: {len(signal)/SR:.3f}s, peak {np.max(np.abs(signal)):.2f}')


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    # A consonant pentatonic family also stays pleasant with rapid random presses.
    for index, frequency in enumerate([587.33, 659.25, 783.99, 880.00, 1046.50], 1):
        save(f'Undo{index}', note(frequency, .09, .027), .60)
    reset = np.zeros(round(SR*.24))
    for onset, frequency, duration, level in [(0, 783.99, .09, .65), (.068, 587.33, .172, 1)]:
        tone = note(frequency, duration, .045)*level
        start = round(onset*SR)
        reset[start:start+len(tone)] += tone
    save('IslandReset', reset, .65)
