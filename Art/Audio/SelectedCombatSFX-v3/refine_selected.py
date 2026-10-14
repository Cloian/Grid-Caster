"""Refine selected M4 only; preserve the five other selected WAVs exactly."""
from pathlib import Path
import importlib.util, json, shutil, wave, hashlib
import numpy as np

ROOT=Path(__file__).resolve().parent
PREVIOUS=ROOT.parent/"CombatSFX-v2"
spec=importlib.util.spec_from_file_location("sfx_v2",PREVIOUS/"generate_candidates.py")
s=importlib.util.module_from_spec(spec);spec.loader.exec_module(s)
manifest=json.loads((PREVIOUS/"asset-manifest.json").read_text())
selected=["M4","S1","H4","P4","K5","F2"]
file_names=[
    "01_magic_soft_laser.wav","02_magic_hit_soft.wav","03_magic_hit_hard.wav",
    "04_player_hurt.wav","05_knight_land.wav","06_fire_trail.wav"
]
s.USED=set();s.LAYERS=[]
# Preserve the selected descending electric gesture, but soften its modulation,
# round the onset and spread the pitch movement across a longer gesture.
t=s.time(.39)
freq=210+(950-210)*np.exp(-t/.100)
phase=2*np.pi*np.cumsum(freq)/s.SR
mod_index=2.05*np.exp(-t/.085)
carrier=np.sin(phase+mod_index*np.sin(phase*2.71))
carrier*=s.env(t,attack=.014,decay=.078)
mix=s.Mix(.55)
mix.add(s.band(carrier,100,4700),.46,role="rounded laser body")
mix.sample("r:cloth4",.09,.024,speed=1.30,cap=.24,lo=600,hi=3200,role="soft motion")
tail=s.modes(.31,[1450,2175],.055)
tail[:round(.009*s.SR)]*=np.linspace(0,1,round(.009*s.SR))
mix.add(tail,.026,.015,role="subtle tail")
# Very quiet early reflection supports continuity without a separate second shot.
mix.add(s.echoes(carrier,(.034,),(.055,)),.035,.010,role="short reflection")
refined=s.finish(s.band(mix.y,75,4700))
(ROOT/file_names[0]).write_bytes(s.wav_bytes(refined))
reference=ROOT/"references";reference.mkdir(exist_ok=True)
old=next(a for a in manifest["assets"] if a["id"]=="M4")
shutil.copy2(PREVIOUS/old["file"],reference/"M4-original.wav")
assets=[]
for index,(candidate,name) in enumerate(zip(selected,file_names)):
    origin=next(a for a in manifest["assets"] if a["id"]==candidate)
    if index:
        shutil.copy2(PREVIOUS/origin["file"],ROOT/name)
        assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==origin["sha256"]
    y=s.wav_read(ROOT/name)
    values=s.stats(y)
    assert values["clipped_samples"]==0
    assert values["tail_10ms_dbfs"]<-32
    assert y[0]==0 and y[-1]==0
    assets.append({
        "file":name,"selected_candidate":candidate,"category":origin["category"],
        "status":"revised_pending_review" if index==0 else "user_selected",
        "source_file":str((PREVIOUS/origin["file"]).relative_to(ROOT.parent.parent.parent)),
        "sha256":hashlib.sha256((ROOT/name).read_bytes()).hexdigest(),
        "sources":sorted(s.USED) if index==0 else origin["sources"],
        "layers":s.LAYERS if index==0 else origin["layers"],
        **values
    })
# 3 old repetitions, a longer separator, then 3 new repetitions.
a=s.wav_read(reference/"M4-original.wav")
comparison=[np.zeros(round(.3*s.SR))]
for sound in [a,refined]:
    for repeat in range(3):
        comparison.extend([sound,np.zeros(round((.45 if repeat<2 else 1.0)*s.SR))])
(ROOT/"00_magic_before_after.wav").write_bytes(s.wav_bytes(np.concatenate(comparison)))
all_sounds=[np.zeros(round(.3*s.SR))]
for item in assets:
    y=s.wav_read(ROOT/item["file"])
    for repeat in range(2):
        all_sounds.extend([y,np.zeros(round((.35 if repeat==0 else .8)*s.SR))])
(ROOT/"00_selected_six.wav").write_bytes(s.wav_bytes(np.concatenate(all_sounds)))
out={
    "date":"2026-10-03","selected_candidates":selected,"active_sound_count":6,
    "pending":["rook_charge","rook_fire"],"game_integrated":False,
    "selection_authority":"latest user selection in this chat",
    "magic_revision":{
        "base":"M4","original_seconds":old["duration_seconds"],
        "revised_seconds":assets[0]["duration_seconds"],
        "changes":["rounded 14ms onset","slower 100ms pitch curve",
                   "reduced FM sharpness","longer 78ms body decay",
                   "motion noise reduced","4700Hz upper filtering"],
        "note":"Audio timing only; projectile/game timing has not been changed."
    },
    "assets":assets,"source_license":"CC0 samples + original synthesis; see ../CombatSFX-v2/CREDITS.md"
}
(ROOT/"selection-manifest.json").write_text(json.dumps(out,ensure_ascii=False,indent=2)+"\n")
print(json.dumps({"selected":selected,"old_seconds":old["duration_seconds"],
                 "new_seconds":assets[0]["duration_seconds"],
                 "peak_dbfs":assets[0]["peak_dbfs"],
                 "unchanged_copies_verified":5,"clipping":0},ensure_ascii=False,indent=2))

