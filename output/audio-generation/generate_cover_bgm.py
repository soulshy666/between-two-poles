"""Original two-minute cover score using the accepted warm gameplay timbre."""
from pathlib import Path
import json, wave, re, uuid
import numpy as np
from scipy.signal import butter, sosfilt
import generate_sparse_bgm_v3 as synth

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/BetweenPoles/Resources/Audio/CoverBackgroundMusic.wav'
dry=np.zeros((synth.N,2))
# New motif and answers; irregular pauses, no drums or full silence.
starts=[.7,12.3,23.0,35.1,46.0,58.4,70.0,81.4,93.2,105.0]
motifs=[[66,69,64,62,69],[64,71,69,66],[62,66,69,64,66],
        [67,69,64,62],[66,64,69,71,66]]
for i,start in enumerate(starts):
    offsets=[0,1.15,2.65,4.1,5.5]
    for j,note in enumerate(motifs[i%5]):
        synth.add_note(dry,start+offsets[j],note,[.12,.09,.105,.08,.085][j],-.18 if j%2==0 else .18)
    bass=[50,52,55,57,50][i%5]
    synth.add_note(dry,start+.12,bass,.045,-.1)
    synth.add_note(dry,start+3.25,bass+7,.028,.2)
music=sosfilt(butter(2,2600,fs=synth.SR,output='sos'),synth.room(dry),axis=0)
music*=.40/np.max(np.abs(music))
audio=music+synth.air_layer(np.arange(synth.N)/synth.SR)
assert np.isfinite(audio).all() and np.max(np.abs(audio))<.95
with wave.open(str(OUT),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(synth.SR)
    w.writeframes(np.rint(audio*32767).astype('<i2').tobytes())
meta=Path(str(OUT)+'.meta')
if not meta.exists():
    template=(OUT.parent/'GameBackgroundMusic.wav.meta').read_text(encoding='utf-8-sig')
    meta.write_text(re.sub(r'guid: \w+','guid: '+uuid.uuid4().hex,template),encoding='utf-8')
levels=np.sqrt(np.mean(audio.reshape(-1,4800,2)**2,axis=(1,2)))
report={'seconds':120,'peak':float(np.max(np.abs(audio))),'silent_100ms_blocks':int(np.sum(levels==0)),
        'quietest_100ms_dbfs':float(20*np.log10(levels.min())),
        'loop_seam_step':float(np.max(np.abs(audio[0]-audio[-1]))),'notes':synth.SCORE}
(Path(__file__).parent/'cover_bgm_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='notes'}))
