"""Original, deterministic feedback sounds; no external recordings."""
from pathlib import Path
import wave
import numpy as np

RATE = 48000
rng = np.random.default_rng(20261011)

def noise(n, low, high):
    f = np.fft.rfftfreq(n, 1/RATE)
    mask = (1-np.exp(-(f/low)**4))*np.exp(-(f/high)**4)
    x = np.fft.irfft(np.fft.rfft(rng.normal(size=n))*mask, n=n)
    return x / max(np.std(x), 1e-8)

def smooth(x):
    x = np.clip(x, 0, 1)
    return x*x*(3-2*x)

def write(path, x, peak):
    x -= x.mean(axis=0)
    x *= peak/np.max(np.abs(x))
    x[:480] *= smooth(np.arange(480)/479)[:,None]
    x[-2400:] *= smooth(np.arange(2400)[::-1]/2399)[:,None]
    assert np.isfinite(x).all() and np.max(np.abs(x)) < 1
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(RATE)
        w.writeframes((x*32767).astype('<i2').tobytes())
    print(path, len(x)/RATE, 'seconds; peak', round(float(np.max(np.abs(x))),3))

# Use the revised world-awakening sound, not the retired engine-like bloom.
import runpy
runpy.run_path(str(Path(__file__).with_name('generate_color_reveal.py')))

# Hand contact followed by two constrained friction efforts; no moving boulder.
t = np.arange(int(1.2*RATE))/RATE
contact = .24*np.sin(2*np.pi*105*t)*np.exp(-t/.045)
contact += .10*noise(len(t), 250, 1800)*np.exp(-t/.028)
effort = .8*np.exp(-((t-.25)/.16)**2)+.65*np.exp(-((t-.7)/.19)**2)
grit = noise(len(t), 160, 1800)*.10*effort*(.8+.2*np.sin(2*np.pi*19*t))
body = .065*np.sin(2*np.pi*(64*t+4*t*t))*effort
x = (contact+grit+body)*(1-smooth((t-.95)/.25))
write('Assets/BetweenPoles/Resources/Audio/Player/Actions/StoneBlocked.wav',
      np.stack((x,x),axis=1), .62)
