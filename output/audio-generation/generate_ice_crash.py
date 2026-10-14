"""Deterministic original sound design; no external samples. Run from repo root."""
from pathlib import Path
import wave
import numpy as np

RATE = 48000
rng = np.random.default_rng(20261010)
out = Path('Assets/BetweenPoles/Resources/Audio/Intro')
out.mkdir(parents=True, exist_ok=True)

def noise(n, low, high):
    f = np.fft.rfftfreq(n, 1 / RATE)
    spectrum = np.fft.rfft(rng.normal(size=n))
    mask = (1 - np.exp(-(f / low) ** 4)) * np.exp(-(f / high) ** 4)
    result = np.fft.irfft(spectrum * mask, n=n)
    return result / max(np.std(result), 1e-8)

def smooth(x):
    x = np.clip(x, 0, 1)
    return x * x * (3 - 2 * x)

def stereo(mono, pan):
    return np.stack((mono * np.sqrt((1-pan)/2), mono * np.sqrt((1+pan)/2)), axis=1)

def write(name, data):
    data -= data.mean(axis=0)
    data *= .84 / max(np.max(np.abs(data)), 1e-8)
    data[:480] *= smooth(np.arange(480)/479)[:, None]
    data[-2400:] *= smooth(np.arange(2400)[::-1]/2399)[:, None]
    assert np.isfinite(data).all() and np.max(np.abs(data)) < 1
    with wave.open(str(out / name), 'wb') as wav:
        wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(RATE)
        wav.writeframes((data * 32767).astype('<i2').tobytes())
    print(name, 'seconds=', len(data)/RATE, 'peak=', round(float(np.abs(data).max()), 3))

# 0–2 s approach, 2–4 s one orbital pass, 4–5 s power failure and descent.
t = np.arange(5*RATE) / RATE
freq = np.where(t < 2, 72 + 35*smooth(t/2), 102 + 13*np.sin((t-2)*np.pi))
freq = np.where(t >= 4, 100 - 55*smooth((t-4)/.6), freq)
phase = 2*np.pi*np.cumsum(freq)/RATE
engine = (.32*np.sin(phase)+.10*np.sin(phase*2)+.055*np.sin(phase*3))
engine += noise(len(t), 70, 1400)*.14
engine *= (1+.09*np.sin(t*2*np.pi*17))
envelope = (.16+.62*smooth(t/2))*(1-.85*smooth((t-4)/.35))
pan = np.where(t<2, -.65+1.05*smooth(t/2), .4*np.cos((t-2)*np.pi))
flight = stereo(engine*envelope, pan)
dive = noise(len(t), 180, 2400)*.22*smooth((t-4)/.8)
flight += stereo(dive, .25*(1-smooth((t-4)/1)))
# A brief sputtering loss of power, kept below the approaching engine.
failure = np.exp(-((t-4.10)/.12)**2)*np.sin(phase*.57)*.12
flight += stereo(failure, 0)
write('ShuttleApproach.wav', flight)

# Rounded hull impact, fractured ice and small settling debris, no sharp bells.
t = np.arange(int(3.4*RATE)) / RATE
phase = 2*np.pi*(43*t+48*.075*(1-np.exp(-t/.075)))
body = .75*np.sin(phase)*np.exp(-t/.25)
body += .22*noise(len(t), 45, 450)*np.exp(-t/.7)
body += .27*noise(len(t), 450, 2600)*np.exp(-t/.105)
body *= smooth(t/.003)
impact = stereo(body, 0)
for start, gain in [(.12,.11),(.23,.09),(.39,.065),(.58,.05),(.84,.037),(1.18,.025)]:
    age = np.maximum(0,t-start)
    grain = noise(len(t), 500, 3200)*gain*np.exp(-age/.09)*smooth((t-start)/.003)
    impact += stereo(grain, rng.uniform(-.5,.5))
settle = noise(len(t), 180, 1600)*.025*np.exp(-t/1.1)*smooth(t/.2)
impact += stereo(settle, 0)
# Two quiet early reflections keep the strike compact instead of explosive.
dry = impact.copy()
for seconds, gain in [(.065,.13),(.13,.055)]:
    delay = int(seconds*RATE)
    impact[delay:] += dry[:-delay, ::-1]*gain
write('ShuttleImpact.wav', impact)
