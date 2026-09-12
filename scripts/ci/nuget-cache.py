#!/usr/bin/env python3
"""Cache package archives as a local feed, never build outputs or extracted packages."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import xml.etree.ElementTree as ET


def fingerprint(root, profile, platform):
    tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
    projects = {p for p in tracked if p.endswith('.csproj')}
    if profile == 'publish':
        host = {'Linux': 'Linux', 'Windows': 'Windows', 'macOS': 'MacOS'}[platform]
        pending = [f'src/CrossMacro.UI.{host}/CrossMacro.UI.{host}.csproj']
        if platform == 'Linux':
            pending.append('src/CrossMacro.Daemon/CrossMacro.Daemon.csproj')
        selected = set()
        while pending:
            name = pending.pop()
            if name in selected:
                continue
            selected.add(name)
            for ref in ET.parse(root / name).getroot().iter('ProjectReference'):
                value = ref.get('Include', '').replace('\\', '/')
                if not value or any(c in value for c in '$*;'):
                    # Dynamic references cannot safely narrow the restore graph.
                    selected = projects
                    pending.clear()
                    break
                pending.append((root / name).parent.joinpath(value).resolve().relative_to(root.resolve()).as_posix())
        projects = selected
    inputs = projects | {p for p in tracked if p and (
        p.endswith(('.props', '.targets', 'packages.lock.json', '.sln', '.slnx'))
        or Path(p).name.lower() in {'global.json', 'nuget.config'})}
    digest = hashlib.sha256()
    for name in sorted(inputs):
        content = (root / name).read_text(encoding='utf-8-sig')
        # Ignore checkout CRLF, XML comments and indentation, preserve all properties,
        # conditions/imports (including restore-affecting build properties).
        if name.endswith(('.csproj', '.props', '.targets')):
            content = ET.canonicalize(content, strip_text=True)
        digest.update(name.encode() + b'\0' + content.encode() + b'\0')
    return digest.hexdigest()


def collect(packages, feed):
    """Keep only the package versions actually used by this successful job."""
    feed.mkdir(parents=True, exist_ok=True)
    used = set()
    for archive in packages.glob('*/*/*.nupkg'):
        if archive.is_symlink():
            continue
        used.add(archive.name)
        target = feed / archive.name
        if not target.exists():
            shutil.copyfile(archive, target)
    for archive in feed.glob('*.nupkg'):
        if archive.name not in used:
            archive.unlink()
    return len(used)


def append(path, values):
    with open(path, 'a', encoding='utf-8') as stream:
        for key, value in values.items():
            stream.write(f'{key}={value}\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['prepare', 'collect', 'restored', 'summary'])
    args = parser.parse_args()
    feed = Path(os.environ['RUNNER_TEMP']) / 'crossmacro-nuget-feed'
    state = Path(os.environ['RUNNER_TEMP']) / 'crossmacro-nuget-cache.json'
    if args.command == 'prepare':
        profile = os.environ['CACHE_PROFILE']
        if profile not in {'source', 'publish'}:
            raise ValueError('Unknown NuGet cache profile')
        feed.mkdir(parents=True, exist_ok=True)
        prefix = f"nuget-archives-v1-{os.environ['RUNNER_OS']}-{os.environ['RUNNER_ARCH']}-{profile}-{os.environ['CACHE_SDK']}-"
        key = prefix + fingerprint(Path.cwd(), profile, os.environ['RUNNER_OS'])
        writer = (os.environ.get('CACHE_WRITER') == 'true'
                  and os.environ.get('GITHUB_EVENT_NAME') in {'push', 'workflow_dispatch'}
                  and os.environ.get('GITHUB_REF') in {'refs/heads/dev', 'refs/heads/main'})
        append(os.environ['GITHUB_OUTPUT'], {'key': key, 'prefix': prefix, 'path': feed, 'writer': str(writer).lower()})
        # NuGet extracts and verifies packages normally; the configured remote
        # sources remain available for misses and vulnerability auditing.
        extra = os.environ.get('RestoreAdditionalProjectSources', '')
        append(os.environ['GITHUB_ENV'], {'RestoreAdditionalProjectSources': str(feed) + (';' + extra if extra else '')})
        state.write_text(json.dumps({'key': key, 'writer': writer, 'started': time.monotonic()}))
    elif args.command == 'restored':
        data = json.loads(state.read_text())
        data.update(restore_seconds=round(time.monotonic() - data['started'], 2),
                    matched=os.environ.get('CACHE_MATCHED', ''), hit=os.environ.get('CACHE_HIT', ''))
        state.write_text(json.dumps(data))
    elif args.command == 'collect':
        started = time.monotonic()
        count = collect(Path(os.environ['NUGET_PACKAGES']), feed)
        append(os.environ['GITHUB_OUTPUT'], {'has-packages': str(count > 0).lower()})
        data = json.loads(state.read_text())
        data.update(packages=count, collect_seconds=round(time.monotonic() - started, 2), save_started=time.monotonic())
        state.write_text(json.dumps(data))
    elif state.exists():
        data = json.loads(state.read_text())
        size = sum(p.stat().st_size for p in feed.glob('*.nupkg')) / 1024 ** 2
        result = 'exact hit' if data.get('hit') == 'true' else ('fallback hit' if data.get('matched') else 'miss')
        save_result = os.environ.get('CACHE_SAVE_RESULT', 'skipped')
        duration = round(time.monotonic() - data['save_started'], 2) if 'save_started' in data else 0
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as stream:
            stream.write(f"\n### NuGet package cache\n\n| Metric | Value |\n|---|---|\n"
                         f"| Restore | {result} |\n| Restore action + setup | {data.get('restore_seconds', '?')} s |\n"
                         f"| Archives on disk (uncompressed cache payload) | {size:.1f} MiB |\n"
                         f"| Writer allowed | {data['writer']} |\n| Save step | {save_result} |\n"
                         f"| Save action + setup | {duration} s |\n\nKey: `{data['key']}`\n")


if __name__ == '__main__':
    main()
