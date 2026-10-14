"""Original 120-second warm-keyboard sketch with a continuous quiet space bed.

Writes audition files only. No reference samples or game assets are changed.
"""
from pathlib import Path
import json
import subprocess
import wave
import numpy as np
from scipy.signal import butter, sosfilt
import imageio_ffmpeg

SR = 48000
SECONDS = 120
N = SR*SECONDS
OUT = Path('C:/Users/Lenovo/Desktop/BGM试听-留白太空解谜-20261014/第三版-约20%留白')
RNG = np.random.default_rng(2026101402)
SCORE = []


def smooth(x):
    x = np.clip(x, 0, 1)
    return x*x*(3-2*x)


def keyboard(midi, velocity):
    t = np.arange(round(3.6*SR))/SR
    frequency = 440*2**((midi-69)/12)
    # Rounded electric-keyboard model: mostly fundamental, no bright metallic tine.
    y = np.zeros(len(t))
    for harmonic, level, decay in [(1, 1, .83), (2, .17, .48), (3, .033, .24), (4, .007, .11)]:
        y += level*np.sin(2*np.pi*frequency*harmonic*t)*np.exp(-t/decay)
    y *= smooth(t/.021)*(1-smooth((t-2.8)/.8))*velocity
    return y


def add_note(mix, when, midi, gain=.13, pan=0):
    x = keyboard(midi, gain)
    first = round(when*SR)
    count = min(len(x), N-first)
    mix[first:first+count, 0] += x[:count]*np.sqrt((1-pan)/2)
    mix[first:first+count, 1] += x[:count]*np.sqrt((1+pan)/2)
    SCORE.append(dict(time=round(when, 3), midi=midi, gain=round(gain, 4)))


def room(dry):
    # Dense, low-level early reflections; soften their brightness and stop by 2 s.
    wet = np.zeros_like(dry)
    softened = sosfilt(butter(2, 1600, fs=SR, output='sos'), dry, axis=0)
    delays = np.linspace(.033, 1.65, 39) + RNG.uniform(-.008, .008, 39)
    levels = np.exp(-delays/.43)
    levels *= .30/levels.sum()
    for i, (delay, level) in enumerate(zip(delays, levels)):
        offset = round(delay*SR)
        wet[offset:] += softened[:-offset, ::-1 if i%2 else 1]*level
    return dry+wet


def air_layer(t):
    # Periodic filtered noise is a seamless two-minute loop, not a rising engine.
    frequencies = np.fft.rfftfreq(N, 1/SR)
    spectrum_shape = (frequencies/180)**2/(1+(frequencies/180)**2)
    spectrum_shape *= np.exp(-(frequencies/1250)**2)
    spectrum_shape /= np.maximum(frequencies, 80)**.72
    channels = []
    for _ in range(2):
        noise = RNG.normal(size=N)
        spectrum = np.fft.rfft(noise)*spectrum_shape
        spectrum[0] = 0
        x = np.fft.irfft(spectrum, n=N)
        x /= np.sqrt(np.mean(x*x))
        channels.append(x)
    bed = np.stack(channels, axis=1)
    bed[:, 1] = .45*bed[:, 0]+.893*bed[:, 1]
    envelope = .9+.065*np.sin(2*np.pi*t/40)+.035*np.cos(2*np.pi*t/60)
    bed *= (.0048*envelope)[:, None]
    # Very faint open consonance adds distance without a continuous foreground pad.
    for i, (midi, gain) in enumerate([(50,.0017),(57,.0011),(64,.0007),(69,.00045)]):
        frequency = round(440*2**((midi-69)/12)*SECONDS)/SECONDS
        phase = .71*i
        drift = .78+.22*np.sin(2*np.pi*t/(60 if i%2 else 40)+phase)
        tone = gain*drift*np.sin(2*np.pi*frequency*t+phase)
        bed[:, 0] += tone
        bed[:, 1] += gain*drift*np.sin(2*np.pi*frequency*t+phase+.17)
    return bed


def render():
    dry = np.zeros((N, 2))
    # Lower register, asymmetric timing, recurring ideas with different answers.
    phrases = [
        (3.1, [(0,62),(.86,69),(2.08,66),(3.53,64),(4.62,69)], [50,57]),
        (16.4,[(0,66),(1.17,64),(2.66,69),(4.02,62)], [55,62]),
        (28.0,[(0,64),(.72,69),(1.86,71),(3.57,66),(4.90,64)], [52,59]),
        (42.8,[(0,62),(.97,69),(2.27,66),(3.10,64),(4.72,62)], [50,57]),
        (54.9,[(0,66),(1.25,69),(2.16,67),(3.98,64)], [55,62]),
        (68.6,[(0,64),(.79,66),(2.36,71),(3.79,69),(5.02,66)], [52,59]),
        (83.2,[(0,62),(1.11,66),(2.54,69),(4.22,64)], [50,57]),
        (95.5,[(0,66),(.87,64),(2.15,69),(3.61,67),(4.93,64)], [55,62]),
        (109.0,[(0,64),(1.09,69),(2.58,66),(4.17,62)], [50,57]),
    ]
    # Twelve related phrases instead of nine, retaining the same note density
    # and touch within each phrase. Shorter pauses, no added percussion/pad gain.
    phrases.extend([
        (0, [(0,66),(1.02,69),(2.35,64),(3.66,62),(4.81,66)], [55,62]),
        (0, [(0,64),(.92,66),(2.13,71),(3.58,69),(4.72,64)], [52,59]),
        (0, [(0,62),(1.12,69),(2.42,66),(3.81,64),(4.91,62)], [50,57]),
    ])
    starts = [1.4,11.1,21.6,31.3,41.6,51.3,61.7,71.8,81.6,91.9,101.7,111.2]
    phrases = [(start, [(offset*1.06,midi) for offset,midi in melody], lower)
               for start, (_,melody,lower) in zip(starts,phrases)]
    for index, (start, melody, lower) in enumerate(phrases):
        for j, (offset, midi) in enumerate(melody):
            time = start+offset+(RNG.uniform(-.028,.028) if j else 0)
            gain = [.15,.115,.13,.102,.115][j]*RNG.uniform(.94,1.04)
            add_note(dry, time, midi, gain, -.17+RNG.uniform(-.08,.08))
        add_note(dry, start+.11, lower[0], .068, .17)
        add_note(dry, start+2.82, lower[1], .041, .24)
        if index in [1,4,7]:
            add_note(dry, start+4.40, 62, .028, .30)

    music = room(dry)
    music = sosfilt(butter(2, 2800, fs=SR, output='sos'), music, axis=0)
    music *= .37/np.max(np.abs(music))
    t = np.arange(N)/SR
    ambience = air_layer(t)
    audio = music+ambience
    assert np.isfinite(audio).all() and np.max(np.abs(audio)) < .95
    pcm = np.rint(audio*32767).astype('<i2')
    OUT.mkdir(parents=True, exist_ok=True)
    target = OUT/'BetweenPoles_WarmSpace_V3_120s.wav'
    with wave.open(str(target), 'wb') as wav:
        wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(SR)
        wav.writeframes(pcm.tobytes())
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-v','error','-i',str(target),
                    '-c:a','libmp3lame','-b:a','256k',str(target.with_suffix('.mp3'))],check=True)
    blocks = audio.reshape(-1, SR//10, 2)
    levels = np.sqrt(np.mean(blocks**2, axis=(1,2)))
    music_levels = np.sqrt(np.mean(music.reshape(-1, SR//10, 2)**2, axis=(1,2)))
    report = dict(duration_seconds=SECONDS, peak_dbfs=float(20*np.log10(np.max(np.abs(audio)))),
                  quietest_100ms_dbfs=float(20*np.log10(levels.min())),
                  ambience_rms_dbfs=float(20*np.log10(np.sqrt(np.mean(ambience**2)))),
                  seconds_with_music_below_minus55_dbfs=float(np.sum(music_levels<10**(-55/20))*.1),
                  musical_space_percent=float(np.sum(music_levels<10**(-55/20))*.1/SECONDS*100),
                  silent_100ms_blocks=int(np.sum(levels==0)),
                  boundary_step=float(np.max(np.abs(audio[0]-audio[-1]))),
                  events=SCORE)
    (OUT/'generation_details.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    (OUT/'试听说明.txt').write_text(
        '第三版：柔和电钢琴与环境底声 · 约20%留白\n'
        '全新合成键盘音色，以中低音区为主；削弱尖锐高频，放缓起音。\n'
        '乐句之间保留连续的微弱空气感与远处共鸣，不会完全静音。\n'
        '十二段不等长短乐句，余音自然消散；无鼓点、无人声。\n'
        'WAV：48kHz / 16-bit / 立体声 / 120秒，适合游戏循环。\n'
        '原创程序化编曲与合成，并非真人钢琴录音；未取用参考录屏音频。\n'
        '当前游戏内BGM与音量未改动；上一版试听保留。\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='events'},indent=2))
    print(target)


if __name__ == '__main__':
    render()
