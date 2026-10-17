"""Offline execution audit. Produces recheck work, never repeats game actions."""
import argparse,json
from pathlib import Path


def check(paths, expected_steps=()):
    issues=[];observed=[];screens=0;ocr=0;resolved=[]
    reports=sorted([(Path(p),json.loads(Path(p).read_text())) for p in paths],key=lambda pair:pair[1].get('started_unix',0))
    entries=[s for _,report in reports for s in report.get('steps',[])]
    following={id(s):entries[i+1] if i+1<len(entries) else {} for i,s in enumerate(entries)}
    for path,report in reports:
        observed.extend(s['name'] for s in report.get('steps',[]))
        if report.get('status')!='passed':
            issues.append({'type':'execution_failure','step':report.get('step'),'report':str(path),'reason':report.get('error','Missing success evidence')})
        screens+=sum(1 for p in path.parent.iterdir() if p.suffix in ('.jpg','.png'))
        ocr+=len(list(path.parent.glob('*-ocr.json')))
        for step in report.get('steps',[]):
            if step.get('click_unix') and not step.get('after_verified'):
                next_step=following[id(step)];required=step.get('deferred_after_conditions')
                seen=next_step.get('verified_before_conditions')
                if (step.get('after_deferred_to_check') and isinstance(required,list) and isinstance(seen,list)
                    and next_step.get('before_verified') and not next_step.get('skipped')
                    and set(required).issubset(seen)):
                    resolved.append({'step':step['name'],'verified_by':next_step['name'],'method':'next_visual_precondition'})
                else:
                    issues.append({'type':'unverified_postcondition','step':step['name'],'report':str(path),'reason':'Click or audio feedback alone does not confirm the intended page state'})
            if step.get('audio_feedback',{}).get('status') in ('uncertain','unavailable'):
                issues.append({'type':'audio_uncertain','step':step['name'],'report':str(path),'reason':'Inspect local final-state evidence before retrying'})
        if report.get('audio_capture') and not report['audio_capture'].get('usable_audio'):
            issues.append({'type':'audio_unavailable','step':report.get('step'),'report':str(path),'reason':'No usable target-app audio; keep visual verification'})
    for name in expected_steps:
        if name not in observed:issues.append({'type':'missing_step','step':name,'reason':'Not observed in this run'})
    return {'status':'needs_review' if issues else 'checked','issues':issues,
            'resolved_deferred_checks':resolved,
            'screenshots':screens,'ocr_calls':ocr,'model_calls':0,
            'recheck_queue':[{'step':i.get('step'),'source':i.get('report'),'reason':i['reason']} for i in issues],
            'automatic_game_actions':False,'model_review_input':'Read issues and referenced local evidence only when review is needed.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('reports',nargs='+',type=Path)
    p.add_argument('--workflow',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    expected=[s['name'] for s in json.loads(a.workflow.read_text())['steps']] if a.workflow else []
    result=check(a.reports,expected);a.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps(result,ensure_ascii=False))
