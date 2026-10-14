"""Original two-minute puzzle-game sketch; renders previews, never game assets.

Procedural plucked-string/modal instrumentation; no sampled reference music.
The score contains deliberate silent windows, including a seamless loop boundary.
"""
from pathlib import Path
import json
import subprocess
import wave
import numpy as np
from scipy.signal import butter, sosfilt
import imageio_ffmpeg

SR = 48000
DURATION = 120
OUT = Path('C:/Users/Lenovo/Desktop/BGM试听-留白太空解谜-20261014')
RNG = np.random.default_rng(20261014)
MIX = np.zeros((SR*DURATION, 2), dtype=np.float64)
EVENTS = []


def smooth(x):
    x = np.clip(x, 0, 1)
    return x*x*(3-2*x)


def voice(midi, kind, length):
    t = np.arange(round(length*SR))/SR
    f = 440*2**((midi-69)/12)
    y = np.zeros(len(t))
    if kind == 'string':
        # A restrained, slightly inharmonic string with faster high partial decay.
        for h in range(1, 19):
            hz = f*h*np.sqrt(1+.000025*h*h)
            if hz > 10000:
                break
            body = 1 + .45*np.exp(-((hz-680)/330)**2)
            amplitude = np.sin(h*np.pi*.23)/(h**1.32)*body
            decay = .48/(1+.11*(h-1))
            y += amplitude*np.sin(2*np.pi*hz*t)*np.exp(-t/decay)
        contact = sosfilt(butter(2, [500, 2600], btype='bandpass', fs=SR, output='sos'), RNG.normal(size=len(t)))
        y += .014*contact*np.exp(-t/.012)
        y *= smooth(t/.004)
    elif kind == 'wood':
        # Warm modal contact beneath the melody, without sub-bass or a drum pulse.
        for ratio, gain, decay in [(1, 1, .32), (2, .14, .12), (3.98, .055, .045)]:
            y += gain*np.sin(2*np.pi*f*ratio*t)*np.exp(-t/decay)
        y *= smooth(t/.006)
    else:
        # Sparse high harmonics: no bell-like inharmonic metallic overtones.
        for h, gain in [(1, 1), (2, .10), (3, .018)]:
            y += gain*np.sin(2*np.pi*f*h*t)*np.exp(-t/(.38/h**.3))
        y *= smooth(t/.025)
    y *= 1-smooth((t-length+.12)/.12)
    return y


def place(signal, start, pan, gain):
    offset = round(start*SR)
    count = min(len(signal), len(MIX)-offset)
    if count <= 0:
        return
    MIX[offset:offset+count, 0] += signal[:count]*gain*np.sqrt((1-pan)/2)
    MIX[offset:offset+count, 1] += signal[:count]*gain*np.sqrt((1+pan)/2)


def note(start, midi, gain=.18, pan=0, kind='string', length=1.28):
    signal = voice(midi, kind, length)
    place(signal, start, pan, gain)
    # A finite room response gives depth but leaves the silent windows empty.
    for delay, level in [(.041, .12), (.079, .085), (.137, .055), (.223, .032), (.347, .018)]:
        place(signal, start+delay, -pan*.75, gain*level)
    EVENTS.append(dict(start=round(start, 3), midi=midi, kind=kind, length=length))


def render():
    # Related motifs, irregular pauses and question/answer contours, no ostinato.
    phrases = [
        (2.0,  [(0,69),(.48,74),(1.24,76),(2.05,71),(3.30,74)], [50,57]),
        (14.2, [(0,66),(.63,69),(1.41,74),(2.60,71),(3.55,69)], [55,62]),
        (26.4, [(0,71),(.37,74),(1.32,69),(2.25,66),(3.62,64)], [52,59]),
        (38.6, [(0,69),(.55,76),(1.18,74),(2.34,71),(3.10,69),(4.02,66)], [50,57]),
        (51.1, [(0,74),(.80,71),(1.55,66),(2.85,69)], [55,62]),
        (64.2, [(0,66),(.43,69),(1.15,71),(2.18,76),(3.52,74)], [52,59]),
        (76.8, [(0,69),(.58,74),(1.52,78),(2.45,76),(3.80,71)], [50,57]),
        (89.4, [(0,71),(.71,69),(1.48,66),(2.75,64),(3.65,69)], [55,62]),
        (101.2,[(0,66),(.50,71),(1.32,74),(2.38,69),(3.48,66)], [52,59]),
        (112.9,[(0,69),(.61,74),(1.65,71),(2.85,74)], [50,57]),
    ]
    for index, (start, melody, bass) in enumerate(phrases):
        for j, (offset, midi) in enumerate(melody):
            timing = 0 if j == 0 else RNG.uniform(-.018, .018)
            gain = [.185,.15,.17,.135,.16,.125][j]*RNG.uniform(.95,1.04)
            note(start+offset+timing, midi, gain, -.16+RNG.uniform(-.12,.12))
        note(start+.075, bass[0], .115, .13, 'wood', 1.10)
        note(start+1.72, bass[1], .075, .23, 'string', 1.20)
        if index in [0,3,6,9]:
            offset, midi = melody[-1]
            note(start+offset+.10, midi+12, .027, .38, 'harmonic', 1.35)
        elif index in [2,5,8]:
            note(start+2.63, 78 if index == 5 else 76, .026, .34, 'harmonic', 1.05)

    audio = sosfilt(butter(2, 6200, fs=SR, output='sos'), MIX, axis=0)
    # Headroom for gameplay sound effects; linear gain, no pumping compression.
    audio *= .46/np.max(np.abs(audio))
    audio[np.abs(audio)<1/8388608] = 0
    assert audio.shape == (5760000, 2) and np.isfinite(audio).all()
    pcm = np.rint(audio*32767).astype('<i2')
    OUT.mkdir(parents=True, exist_ok=True)
    wav_path = OUT/'BetweenPoles_SparseOrbit_120s.wav'
    with wave.open(str(wav_path), 'wb') as wav:
        wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(SR)
        wav.writeframes(pcm.tobytes())
    mp3_path = OUT/'BetweenPoles_SparseOrbit_120s.mp3'
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), '-y', '-v', 'error', '-i', str(wav_path),
                    '-c:a', 'libmp3lame', '-b:a', '256k', str(mp3_path)], check=True)
    blocks = audio.reshape(-1, SR//10, 2)
    rms = np.sqrt(np.mean(blocks**2, axis=(1,2)))
    quiet_seconds = float(np.sum(rms<10**(-60/20))*.1)
    report = dict(duration_seconds=120, sample_rate=SR, channels=2,
                  peak_dbfs=round(20*np.log10(np.max(np.abs(audio))),2),
                  seconds_below_minus60_dbfs=round(quiet_seconds,1),
                  first_sample=pcm[0].tolist(), last_sample=pcm[-1].tolist(),
                  phrase_starts=[p[0] for p in phrases], events=EVENTS)
    (OUT/'generation_details.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    (OUT/'试听说明.txt').write_text(
        '两极之间 · 留白太空解谜 BGM 试听版\n'
        '原创编曲与程序化音色合成，未使用参考视频的音乐片段。\n'
        '120 秒，48 kHz，立体声；WAV 为游戏导入版，MP3 为试听版。\n'
        '10 段短乐句，拨弦为主，间隔约 6–8 秒，含自然衰减后的静音。\n'
        '开头和结尾保留静音，可循环播放；建议用 WAV 避免 MP3 编码延迟。\n'
        '这是合成拨弦音色，不是真人小提琴录音。\n'
        '当前游戏 BGM 和音量设置未修改。\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='events'}, indent=2))
    print(str(wav_path))
    print(str(mp3_path))


if __name__ == '__main__':
    render()
