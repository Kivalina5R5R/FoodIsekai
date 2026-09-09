from pathlib import Path
import math,random,wave,struct,uuid,json,re,sys
folder=Path('Assets/Audio/SFX');folder.mkdir(parents=True,exist_ok=True)
for p in [folder.parent,folder]:
 if not p.with_suffix('.meta').exists():p.with_suffix('.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n')
rate=48000
rng=random.Random(92417)
def blank(sec): return [0.0]*int(rate*sec)
def tone(out,start,freq,length,gain=0.25,kind='bell'):
 first=int(start*rate);n=min(int(length*rate),len(out)-first)
 if kind=='pluck':
  delay=max(3,int(rate/freq));ring=[rng.uniform(-1,1) for _ in range(delay)];old=0
  for i in range(n):
   pos=i%delay;v=ring[pos]; ring[pos]=(v+ring[(pos+1)%delay])*0.497
   attack=min(1,i/(rate*.005));tail=min(1,(n-i)/(rate*.035))
   out[first+i]+=v*gain*attack*tail
  return
 partials=[(1,1,1),(2.756,.32,.55),(5.404,.1,.25)] if kind=='bell' else [(1,1,1),(2,.22,.55),(3,.09,.3)]
 for i in range(n):
  t=i/rate;attack=min(1,t/.004);tail=min(1,(n-i)/(rate*.045))
  v=sum(a*math.sin(2*math.pi*freq*ratio*t)*math.exp(-t/(length*.27*decay)) for ratio,a,decay in partials)
  out[first+i]+=v*gain*attack*tail

def rustle(out,start,length,gain):
 low=0;first=int(start*rate)
 for i in range(min(int(length*rate),len(out)-first)):
  t=i/(length*rate);low=.85*low+.15*rng.uniform(-1,1)
  out[first+i]+=low*gain*math.sin(math.pi*t)**2

def melody(out,notes,step=.095,gain=.23,kind='bell',length=.5,start=0):
 for i,f in enumerate(notes):tone(out,start+i*step,f,length,gain,kind)
def bubble(out,start,frequency,sweep,length,gain):
 first=int(start*rate);n=min(int(length*rate),len(out)-first);tau=.022
 for i in range(n):
  sec=i/rate
  phase=2*math.pi*(frequency*sec+sweep*(sec-tau*(1-math.exp(-sec/tau))))
  envelope=(1-math.exp(-sec/.002))*math.exp(-sec/(length*.19))*min(1,(n-i)/(rate*.025))
  out[first+i]+=gain*(math.sin(phase)+.13*math.sin(phase*2.07))*envelope

clips={}
a=blank(.28);tone(a,0,520,.19,.44,'wood');rustle(a,0,.09,.24);tone(a,.025,1560,.13,.07);clips['FoodPickup']=a
a=blank(.95);melody(a,[523.25,659.25,783.99],.09,.24,'bell',.6);tone(a,0,196,.2,.14,'wood');clips['FoodServed']=a
a=blank(.42);melody(a,[277.18,233.08],.1,.32,'wood',.25);clips['WrongFood']=a
a=blank(.22);rustle(a,.01,.11,.5);tone(a,0,380,.095,.045,'wood');clips['OrderArrived']=a
a=blank(.6);melody(a,[1760,2349,1976,2637],.045,.16,'bell',.32);rustle(a,0,.18,.18);clips['MoneyPaid']=a
a=blank(.65);melody(a,[1174.66,1568,2349],.05,.2,'bell',.42);rustle(a,0,.2,.17);clips['MoneyCollected']=a
a=blank(1.05);tone(a,0,130.81,.24,.25,'wood');rustle(a,0,.24,.24);melody(a,[1318.5,1568,2093,2637],.05,.16,'bell',.46,start=.03);melody(a,[392,523.25,659.25],.09,.24,'pluck',.7,start=.14);clips['BankDeposit']=a
a=blank(.5);tone(a,0,523.25,.32,.23);tone(a,.12,392,.28,.22);tone(a,0,196,.1,.13,'wood');clips['CustomerExpired']=a
a=blank(1.4);melody(a,[392,523.25,659.25,783.99],.15,.26,'pluck',.8);tone(a,.47,1046.5,.75,.18);clips['WaveStart']=a
a=blank(1.1);melody(a,[783.99,659.25,523.25],.16,.23,'pluck',.68);tone(a,.34,392,.7,.16);clips['WaveBreak']=a
a=blank(1.85);melody(a,[523.25,659.25,783.99,1046.5],.15,.21,'bell',1.05);melody(a,[261.63,329.63,392,523.25],.15,.33,'pluck',1.1);clips['ServiceComplete']=a
a=blank(.7);melody(a,[880,880],.19,.17,'wood',.3);clips['TimeWarning']=a
a=blank(.24);bubble(a,0,280,180,.11,.34);clips['EmojiBubble']=a
a=blank(.38);melody(a,[1480,1760,1976],.045,.12,'bell',.24);rustle(a,0,.12,.1);clips['MoneyReminder']=a
a=blank(.23);tone(a,0,900,.04,.285,'wood');rustle(a,0,.035,.48);tone(a,.006,460,.05,.21,'wood');clips['SuccessPop']=a
report={}
only=set(sys.argv[1:])
for name,a in clips.items():
 if only and name not in only: continue
 # Subtle room reflection and click-free edges, with shared gain to preserve cue balance.
 dry=a[:]
 for delay,amount in [(.037,.12),(.071,.07)]:
  offset=int(delay*rate)
  for i in range(offset,len(a)):a[i]+=dry[i-offset]*amount
 peak=max(abs(x) for x in a)
 scale=min(1.5,.78/max(peak,1e-6))
 for i in range(len(a)):a[i]*=scale*min(1,i/128,(len(a)-1-i)/256)
 with wave.open(str(folder/(name+'.wav')),'wb') as w:
  w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes(b''.join(struct.pack('<h',round(max(-1,min(1,x))*32767)) for x in a))
 (folder/(name+'.wav.meta')).write_text('fileFormatVersion: 2\nguid: '+(re.search(r'guid: (\w+)', (folder/(name+'.wav.meta')).read_text())[1] if (folder/(name+'.wav.meta')).exists() else uuid.uuid4().hex)+'''\nAudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  ambisonic: 0
  3D: 0
  loadInBackground: 0
  userData:
  assetBundleName:
  assetBundleVariant:
''')
 report[name]={'duration':round(len(a)/rate,2),'peak':round(max(abs(x) for x in a),4),'rms':round(math.sqrt(sum(x*x for x in a)/len(a)),4)}
(folder/'README.md').write_text('''# Tavern gameplay sound effects

Original synthesized sounds created for FoodIsekaiZ. No external recordings or licensed samples are used.
48 kHz, mono, 16-bit PCM WAV; shared headroom and softened attacks. Generated with seeded string excitation, damped resonators, metallic bell partials and filtered rustle.

Cues follow GameSoundCue order. Food pickup uses a plate tap/rustle boosted by 6 dB; accepted service resolves upward; wrong food uses a low wooden double tap. Visible order panels use a short paper flick with a muted wooden tick. Only the first emoji popup per NPC uses an audible low water bubble; face changes and later re-shows stay silent. Menu dismissal is silent. A short dry pop boosted by 3.5 dB plays when the successful-order particle burst starts after collapse, with a 0.18-second shared cooldown. Distinct NPC reveals queue up to six bubbles with 0.12-second spacing, instead of muting the second NPC; menu sounds have a separate 0.6-second cooldown. Payout, collection and banking use different coin clusters. Expiry uses an audible descending bell pair with playback priority. Uncollected piles use a short coin rattle when their reminder animation starts; simultaneous reminders share a 0.35-second cooldown; wave opening, break and completion have separate musical cadences. TimeWarning plays once at 10 and 5 seconds remaining.

Scene object Gameplay Sound Effects holds an independent 2D pool of six voices, master volume 0.65 and per-cue cooldowns. A dedicated BGM source lives on the same Gameplay Sound Effects object at volume 0.18, separate from the six effect voices. No sound plays for an empty bank visit or an unsuccessful pickup.

Generation source: Tools/Audio/GenerateTavernSounds.py (Python standard library; regenerates these authored assets).
''')
if not (folder/'README.md.meta').exists(): (folder/'README.md.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
Path('Temp/tavern-sfx-validation.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
