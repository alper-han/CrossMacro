#!/usr/bin/env python3
"""Deterministic window macros for CrossMacro's existing Shell action.

Requires the user's installed CUA MCP runtime; does not bundle it or call models.
Only fixed observe/click operations are supported. All identity and machine
configuration belongs outside the repository, as do screenshots and run logs.
"""
import argparse
import base64
import fcntl
import json
import os
from pathlib import Path
import queue
import shlex
import signal
import socket
import stat
import subprocess
import sys
import threading
import time
import unicodedata
import uuid


def normalize(text):
    return ''.join(unicodedata.normalize('NFKC', text).split())


def require_text(text, required):
    missing = [s for s in required if not s or normalize(s) not in normalize(text)]
    if missing:
        raise RuntimeError('OCR condition failed: ' + ', '.join(missing))


def validate_workflow(workflow):
    size = workflow['reference_size']
    if len(size) != 2 or any(type(v) is not int or v <= 0 for v in size):
        raise ValueError('reference_size must contain two positive integers')
    names = set()
    for step in workflow['steps']:
        if step['name'] in names:
            raise ValueError('Step names must be unique')
        names.add(step['name'])
        if step['action'] not in ('observe', 'click') or not step.get('before'):
            raise ValueError('Every step requires an observe/click action and OCR preconditions')
        if step['action'] == 'click':
            point = step['point']
            if len(point) != 2 or any(type(v) is not int or not 0 <= v < size[i] for i, v in enumerate(point)):
                raise ValueError('Click is outside reference window')
            if not step.get('after'):
                raise ValueError('Every click requires OCR completion conditions')


class CuaMcp:
    def __init__(self, config):
        server = json.loads(Path(config['mcp_manifest']).read_text())['mcpServers']['cua_repl']
        env = {k: os.environ[k] for k in ('HOME','PATH','TMPDIR','USER','LOGNAME','LANG') if k in os.environ}
        # No caller session identity, sockets, tokens, or approval context.
        # Copy only launcher settings, not arbitrary secret environment values.
        for key, value in server.get('env', {}).items():
            if key.startswith(('NODE_REPL_', 'CUA_REPL_', 'BROWSER_USE_')) or key in ('SKY_CUA_SERVICE_PATH','CODEX_CLI_PATH'):
                env[key] = value
        env['CUA_REPL_ENABLED_SURFACES'] = 'computer'
        self.proc = subprocess.Popen([server['command'], *server['args']], env=env,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, start_new_session=True, cwd=config['log_dir'])
        self.config, self.messages, self.errors, self.next_id = config, queue.Queue(), [], 0
        self.deadline = time.monotonic() + min(120, config.get('timeout_seconds',45))
        threading.Thread(target=self._read,daemon=True).start()
        threading.Thread(target=self._read_errors,daemon=True).start()

    def _read(self):
        for line in self.proc.stdout:
            try:
                self.messages.put(json.loads(line))
            except ValueError:
                pass
        self.messages.put({'closed':True})

    def _read_errors(self):
        for line in self.proc.stderr:
            if len(self.errors) < 30:
                self.errors.append(line[:1000])

    def send(self, message):
        self.proc.stdin.write(json.dumps(message) + '\n')
        self.proc.stdin.flush()

    def request(self, method, params):
        self.next_id += 1
        request_id = self.next_id
        self.send({'jsonrpc':'2.0','id':request_id,'method':method,'params':params})
        while True:
            remaining = self.deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError('MCP operation deadline exceeded')
            try:
                message = self.messages.get(timeout=remaining)
            except queue.Empty as exc:
                raise TimeoutError('MCP timed out during %s: %s' % (method, ''.join(self.errors)[-1500:])) from exc
            if message.get('closed'):
                raise RuntimeError('MCP closed: ' + ''.join(self.errors)[-1500:])
            if message.get('method') == 'elicitation/create':
                meta = message.get('params',{}).get('_meta',{})
                # Local config represents the user's authorization. Never
                # accept a different connector, application, or capability.
                allowed = (meta.get('connector_id') == 'computer-use'
                    and meta.get('tool_params',{}).get('app') == self.config['target']['bundle_id']
                    and meta.get('tool_name') in set(self.config.get('authorized_tools',[])) & {'get_app_state','get_app_screenshot','click'})
                self.send({'jsonrpc':'2.0','id':message['id'],'result':
                    {'action':'accept','content':{}} if allowed else {'action':'decline'}})
                continue
            if 'method' in message:
                if 'id' in message:
                    self.send({'jsonrpc':'2.0','id':message['id'],'error':{'code':-32601,'message':'Unsupported client method'}})
                continue
            if message.get('id') == request_id:
                if 'error' in message:
                    raise RuntimeError(str(message['error']))
                result = message['result']
                if result.get('isError'):
                    raise RuntimeError('MCP operation failed: ' + str(result.get('content'))[:1000])
                return result

    def start(self):
        self.request('initialize', {'protocolVersion':'2025-06-18',
            'capabilities':{'elicitation':{'form':{}}},
            'clientInfo':{'name':'crossmacro-window-macro','version':'0.1'}})
        self.send({'jsonrpc':'2.0','method':'notifications/initialized'})
        self.js('var target = await cua.getApp(%s);' % json.dumps(self.config['target']['app_path']))

    def js(self, code):
        return self.request('tools/call', {'name':'js','arguments':{'code':code,'title':'CrossMacro authorized window macro'}})

    def observe(self, folder, label, expected_size):
        result = self.js('await target.getAXStateAndScreenshot();')
        images = [c for c in result.get('content',[]) if c.get('type') == 'image']
        if len(images) != 1:
            raise RuntimeError('Expected one target-window screenshot')
        data = base64.b64decode(images[0]['data'], validate=True)
        mime = images[0].get('mimeType')
        if mime not in ('image/png','image/jpeg'):
            raise RuntimeError('Unsupported screenshot image type: ' + str(mime))
        path = folder / (label + ('.png' if mime == 'image/png' else '.jpg'))
        path.write_bytes(data)
        raw = subprocess.run([self.config['ocr_binary'],str(path),self.config.get('language','en-US')],
            capture_output=True,text=True,check=True,timeout=15)
        ocr = json.loads(raw.stdout)
        (folder/(label+'-ocr.json')).write_text(json.dumps(ocr,ensure_ascii=False,indent=2))
        size = [ocr['width'],ocr['height']]
        if size != expected_size:
            raise RuntimeError('Window size changed: %s; expected %s. Recalibrate before input.' % (size,expected_size))
        return '\n'.join(line['text'] for line in ocr['lines'])

    def close(self):
        if self.proc.poll() is None:
            os.killpg(self.proc.pid, signal.SIGTERM)
            try:
                self.proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(self.proc.pid, signal.SIGKILL)
                self.proc.wait(timeout=5)


def execute(config, workflow, step_name, backend=None):
    validate_workflow(workflow)
    steps = workflow['steps'] if step_name == 'all' else [s for s in workflow['steps'] if s['name'] == step_name]
    if not steps:
        raise ValueError('Unknown step: ' + step_name)
    log_dir = Path(config['log_dir']).expanduser().resolve()
    log_dir.mkdir(parents=True, exist_ok=True)
    with (log_dir/'.window-macro.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        guard = config.get('guard_command',[])
        if guard:
            state = json.loads(subprocess.check_output(guard,text=True,timeout=10))
            if state.get('active') is not None:
                raise RuntimeError('Existing game coordinator has an active executor')
        folder = log_dir / (time.strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:8])
        folder.mkdir()
        report = {'status':'running','step':step_name,'started_unix':time.time(),
            'runtime_model_calls':0,'chat_identity_supplied':False,'steps':[]}
        client = backend
        try:
            if client is None:
                client = CuaMcp(config)
                client.start()
            else:
                client.deadline = time.monotonic() + min(120,config.get('timeout_seconds',45))
            for index, step in enumerate(steps):
                before = client.observe(folder, '%02d-before'%index, workflow['reference_size'])
                require_text(before, step['before'])
                if step.get('verify_identity'):
                    identity = config.get('identity_text',[])
                    if not identity:
                        raise RuntimeError('Identity assertions are required in local config')
                    require_text(before, identity)
                entry = {'name':step['name'],'before_verified':True}
                report['steps'].append(entry)
                if step['action'] == 'click':
                    entry['click_unix'] = time.time()
                    client.js('await target.click(%s); await target.getAXState();' % json.dumps(step['point']))
                    # Re-observe after the action; no blind retries or model.
                    after = client.observe(folder,'%02d-after'%index, workflow['reference_size'])
                    require_text(after,step['after'])
                    entry['after_verified'] = True
                print(json.dumps({'status':'step_passed','step':step['name']},ensure_ascii=False),flush=True)
            report['status'] = 'passed'
        except (Exception, KeyboardInterrupt) as exc:
            report.update(status='failed',error=str(exc))
            raise
        finally:
            if client is not None and backend is None:
                client.close()
            report['ended_unix'] = time.time()
            (folder/'result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
            print(json.dumps({'status':report['status'],'result':str(folder/'result.json')}),flush=True)


def export_macro(config_path, workflow_path, output):
    workflow = json.loads(workflow_path.read_text())
    validate_workflow(workflow)
    lines = ['# Name: '+workflow['name'], '# Format: CrossMacroFormatV4', '# IsAbsolute: False',
             '# SkipInitialZero: True', '[Script]']
    for step in workflow['steps']:
        argv = [sys.executable,str(Path(__file__).resolve()),'request','--config',str(config_path.resolve()),
                '--workflow',str(workflow_path.resolve()),'--step',step['name']]
        # CrossMacro expands $variables before /bin/sh parses the command.
        # Reject such paths rather than emitting a subtly different command.
        command = shlex.join(argv)
        if '$' in command or '\n' in command or '\r' in command:
            raise ValueError('Export paths cannot contain $, CR or LF')
        lines.append('shell '+json.dumps(command,ensure_ascii=False)+' 0 0 60000')
    lines.append('[Events]')
    output.write_text('\n'.join(lines)+'\n')
    print(str(output.resolve()))


def receive_json(connection):
    data = bytearray()
    while b'\n' not in data:
        chunk = connection.recv(4096)
        if not chunk:
            raise RuntimeError('Worker connection closed')
        data.extend(chunk)
        if len(data) > 65536:
            raise ValueError('Worker message too large')
    return json.loads(data.split(b'\n',1)[0])


def worker_request(config, step):
    with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as connection:
        connection.settimeout(100)
        connection.connect(config['worker_socket'])
        connection.sendall((json.dumps({'step':step})+'\n').encode())
        result = receive_json(connection)
        print(json.dumps(result,ensure_ascii=False),flush=True)
        if not result.get('ok'):
            raise RuntimeError(result.get('error','Window worker failed'))


def serve(config, workflow):
    """Serialize fixed workflow requests; no arbitrary code or config over IPC."""
    validate_workflow(workflow)
    path = Path(config['worker_socket'])
    if not path.is_absolute():
        raise ValueError('worker_socket must be absolute')
    path.parent.mkdir(mode=0o700,parents=True,exist_ok=True)
    if path.parent.stat().st_uid != os.getuid() or stat.S_IMODE(path.parent.stat().st_mode) & 0o077:
        raise ValueError('Worker socket directory must be private to the current user (0700)')
    if path.exists():
        # Do not replace an active worker or a non-socket file.
        if not stat.S_ISSOCK(path.stat().st_mode):
            raise ValueError('Refusing to replace a non-socket file')
        with socket.socket(socket.AF_UNIX,socket.SOCK_STREAM) as probe:
            try:
                probe.connect(str(path))
            except ConnectionRefusedError:
                path.unlink()
            else:
                raise RuntimeError('Window worker is already running')
    with socket.socket(socket.AF_UNIX,socket.SOCK_STREAM) as server:
        server.bind(str(path))
        os.chmod(path,0o600)
        server.listen(1)
        backend = CuaMcp(config)
        try:
            backend.start()
            print(json.dumps({'status':'worker_ready','socket':str(path),'pid':os.getpid()}),flush=True)
            while True:
                connection, _ = server.accept()
                with connection:
                    connection.settimeout(100)
                    try:
                        request = receive_json(connection)
                        if set(request) != {'step'} or not isinstance(request['step'],str):
                            raise ValueError('Only a fixed workflow step name is accepted')
                        execute(config,workflow,request['step'],backend=backend)
                        result = {'ok':True,'step':request['step']}
                    except Exception as exc:
                        result = {'ok':False,'error':str(exc)}
                    connection.sendall((json.dumps(result)+'\n').encode())
        finally:
            backend.close()
            path.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['execute','export','serve','request'])
    parser.add_argument('--config',type=Path,required=True)
    parser.add_argument('--workflow',type=Path,required=True)
    parser.add_argument('--step',default='all')
    parser.add_argument('--output',type=Path)
    args = parser.parse_args()
    if args.command == 'export':
        if args.output is None:
            parser.error('export requires --output')
        export_macro(args.config,args.workflow,args.output)
    elif args.command == 'serve':
        serve(json.loads(args.config.read_text()),json.loads(args.workflow.read_text()))
    elif args.command == 'request':
        worker_request(json.loads(args.config.read_text()),args.step)
    else:
        execute(json.loads(args.config.read_text()),json.loads(args.workflow.read_text()),args.step)


if __name__ == '__main__':
    def interrupted(signum, frame):
        raise KeyboardInterrupt('Window macro cancelled')
    signal.signal(signal.SIGTERM, interrupted)
    try:
        main()
    except (Exception, KeyboardInterrupt) as exc:
        print(str(exc),file=sys.stderr)
        sys.exit(1)
