"""Local process-audio capture and sound-template experiments; no model calls."""
import argparse
import json
from pathlib import Path
import queue
import signal
import subprocess
import threading
import time
import wave


class AudioCapture:
    def __init__(self, binary, bundle_id, folder, seconds=60):
        self.folder = Path(folder)
        self.folder.mkdir(parents=True,exist_ok=True)
        self.caf = self.folder/'audio.caf'
        self.events = []
        self.messages = []
        self.ready = queue.Queue()
        self.proc = subprocess.Popen([str(binary),bundle_id,str(self.caf),str(seconds)],
            stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,
            start_new_session=True)
        self.reader=threading.Thread(target=self._read,daemon=True)
        self.reader.start()

    def _read(self):
        for line in self.proc.stdout:
            try: message = json.loads(line)
            except ValueError: continue
            self.messages.append(message)
            if message.get('status') == 'audio_ready': self.ready.put(message)
        self.ready.put(None)

    def wait_ready(self, timeout=10):
        try: result = self.ready.get(timeout=timeout)
        except queue.Empty: raise RuntimeError('Audio capture is awaiting permission or failed to start')
        if not result: raise RuntimeError('Audio capture ended before readiness')
        return result

    def mark(self, name):
        self.events.append({'name':name,'unix':time.time()})

    def stop(self):
        if self.proc.poll() is None:
            self.proc.send_signal(signal.SIGTERM)
            try: self.proc.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.proc.kill();self.proc.wait(timeout=3)
        stderr = self.proc.stderr.read()
        # The reader has reached EOF before the pipe closes; allow its last JSON
        # record to be consumed without touching the application UI.
        self.reader.join(timeout=1)
        metadata = {'messages':self.messages,'events':self.events,'stderr':stderr,
                    'returncode':self.proc.returncode,'target_only':True,'microphone':False}
        stopped = next((m for m in reversed(self.messages) if m.get('status')=='audio_stopped'),{})
        if self.proc.returncode == 0 and stopped.get('frames',0)>0:
            wav = self.folder/'audio.wav'
            result = subprocess.run(['/usr/bin/afconvert','-f','WAVE','-d','LEI16',str(self.caf),str(wav)],capture_output=True,text=True)
            metadata['conversion_returncode']=result.returncode
            if result.returncode==0:
                samples,rate = read_wav(wav)
                import numpy as np
                metadata.update(wav=str(wav),sample_rate=rate,samples=len(samples),
                                rms=float(np.sqrt(np.mean(samples*samples))) if len(samples) else 0,
                                peak=float(np.max(np.abs(samples))) if len(samples) else 0,
                                first_buffer_unix=stopped.get('first_buffer_unix'))
        metadata['usable_audio']=metadata.get('rms',0)>1e-6
        (self.folder/'audio.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n')
        return metadata


def read_wav(path):
    import numpy as np
    with wave.open(str(path),'rb') as f:
        if f.getsampwidth()!=2: raise ValueError('Expected 16-bit PCM WAV')
        samples=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(np.float32)/32768
        samples=samples.reshape(-1,f.getnchannels()).mean(axis=1)
        return samples,f.getframerate()


def fingerprint(samples, rate):
    """Log-frequency transients; retain timing and suppress steady background."""
    import numpy as np
    if rate%16000: raise ValueError('Resample audio to a multiple of 16 kHz')
    samples=np.asarray(samples,dtype=np.float32)[::rate//16000]
    width,hop=400,160
    if len(samples)<width: raise ValueError('Audio is too short')
    frames=np.lib.stride_tricks.sliding_window_view(samples,width)[::hop]
    spectrum=np.abs(np.fft.rfft(frames*np.hanning(width),axis=1))**2
    frequencies=np.fft.rfftfreq(width,1/16000)
    edges=np.geomspace(160,7900,33)
    values=[]
    for a,b in zip(edges[:-1],edges[1:]):
        left=int(np.searchsorted(frequencies,a));right=max(left+1,int(np.searchsorted(frequencies,b)))
        values.append(np.log1p(spectrum[:,left:right].mean(axis=1)))
    bands=np.stack(values,axis=1)
    # Positive changes make a repeating music bed less influential. This is a
    # candidate detector, not proof that the intended game state was reached.
    return np.maximum(np.diff(bands,axis=0,prepend=bands[:1]),0)


def score_template(samples, rate, template, template_rate):
    """Gain/offset-invariant waveform matching, with local time alignment.

    Short game effects repeat their waveform more faithfully than their mixture
    with background music. Match position, rather than API dispatch timestamps.
    """
    import numpy as np
    if rate%16000 or template_rate%16000:raise ValueError('Expected a multiple of 16 kHz')
    x=np.asarray(samples,dtype=float)[::rate//16000]
    t=np.asarray(template,dtype=float)[::template_rate//16000]
    n,m=len(x),len(t)
    if not m or n<m:return {'score':0.0,'offset_seconds':None}
    t=t-t.mean();norm=float(np.sum(t*t))
    if norm<1e-10:return {'score':0.0,'offset_seconds':None}
    size=1<<(n+m-2).bit_length()
    correlation=np.fft.irfft(np.fft.rfft(x,size)*np.fft.rfft(t[::-1],size),size)[m-1:n]
    total=np.concatenate([[0],np.cumsum(x)]);square=np.concatenate([[0],np.cumsum(x*x)])
    variance=np.maximum(0,square[m:]-square[:-m]-(total[m:]-total[:-m])**2/m)
    scores=np.clip(np.abs(correlation)/np.sqrt(variance*norm+1e-15),0,1)
    best=int(np.argmax(scores))
    return {'score':float(scores[best]),'offset_seconds':best/16000,'method':'normalized_waveform'}


def classify(path, templates, threshold=.8, margin=.08):
    samples,rate=read_wav(path)
    ranked=[]
    for name,template_path in templates.items():
        template,tr=read_wav(template_path)
        ranked.append(dict(event=name,**score_template(samples,rate,template,tr)))
    ranked.sort(key=lambda d:d['score'],reverse=True)
    best=ranked[0] if ranked else {'score':0}
    second=ranked[1]['score'] if len(ranked)>1 else 0
    accepted=best['score']>=threshold and best['score']-second>=margin
    return {'status':'candidate' if accepted else 'uncertain','ranked':ranked,
            'threshold':threshold,'margin':margin,'semantic_state_verified':False}


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('command',choices=['capture','match'])
    p.add_argument('--binary',type=Path);p.add_argument('--bundle-id')
    p.add_argument('--folder',type=Path);p.add_argument('--seconds',type=float,default=8)
    p.add_argument('--wav',type=Path);p.add_argument('--templates',type=Path)
    a=p.parse_args()
    if a.command=='capture':
        capture=AudioCapture(a.binary,a.bundle_id,a.folder,a.seconds)
        try:
            print(json.dumps(capture.wait_ready()),flush=True)
            capture.proc.wait(timeout=a.seconds+15)
        finally: print(json.dumps(capture.stop(),ensure_ascii=False),flush=True)
    else:
        print(json.dumps(classify(a.wav,json.loads(a.templates.read_text())),ensure_ascii=False))


if __name__=='__main__': main()
