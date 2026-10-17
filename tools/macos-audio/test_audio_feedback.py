import json,tempfile,unittest,wave
from pathlib import Path
try:
    import numpy as np
except ImportError:
    np=None
from audio_feedback import score_template,classify
from check_run import check


@unittest.skipIf(np is None,'NumPy is required for optional audio feedback tests')
class AudioFeedbackTests(unittest.TestCase):
    def signal(self, frequency):
        t=np.arange(16000)/16000
        return .3*np.sin(2*np.pi*frequency*t)*np.exp(-((t-.25)/.04)**2)

    def write(self,path,samples):
        with wave.open(str(path),'wb') as f:
            f.setparams((1,2,16000,0,'NONE','not compressed'))
            f.writeframes((samples*32767).astype('<i2').tobytes())

    def test_matches_delayed_sound_with_background(self):
        template=self.signal(1300)
        query=np.concatenate([np.zeros(8000),template,np.zeros(8000)])
        query+=np.random.default_rng(2).normal(0,.001,len(query))
        result=score_template(query,16000,template,16000)
        self.assertGreater(result['score'],.95)
        self.assertAlmostEqual(.5,result['offset_seconds'],delta=.02)

    def test_silence_does_not_confirm_success(self):
        result=score_template(np.zeros(32000),16000,self.signal(1300),16000)
        self.assertEqual(0,result['score'])

    def test_distinguishes_synthetic_events_without_semantic_success(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);templates={}
            for name,frequency in [('click',1300),('reward',4300)]:
                path=root/(name+'.wav');self.write(path,self.signal(frequency));templates[name]=str(path)
            query=root/'query.wav';self.write(query,np.concatenate([np.zeros(8000),self.signal(4300)]))
            result=classify(query,templates)
            self.assertEqual('candidate',result['status'])
            self.assertEqual('reward',result['ranked'][0]['event'])
            self.assertFalse(result['semantic_state_verified'])

    def test_audit_records_missing_failed_and_uncertain_steps(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'result.json'
            path.write_text(json.dumps({'status':'failed','step':'claim','error':'unknown state',
                'steps':[{'name':'claim','audio_feedback':{'status':'uncertain'}}]}))
            result=check([path],['claim','return'])
            self.assertEqual('needs_review',result['status'])
            self.assertEqual({'execution_failure','audio_uncertain','missing_step'},
                             {i['type'] for i in result['issues']})
            self.assertFalse(result['automatic_game_actions'])

    def test_later_visual_check_resolves_audio_transition_but_not_arbitrary_audio(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'result.json'
            data={'status':'passed','steps':[
                {'name':'open','click_unix':1,'after_deferred_to_check':True,'deferred_after_conditions':['Profile']},
                {'name':'identity','before_verified':True,'verified_before_conditions':['Profile'],'identity_verified':True}]}
            path.write_text(json.dumps(data));result=check([path])
            self.assertEqual('checked',result['status'])
            self.assertEqual(1,len(result['resolved_deferred_checks']))
            data['steps'][1]['verified_before_conditions']=['Unexpected dialog']
            path.write_text(json.dumps(data));self.assertEqual('needs_review',check([path])['status'])
