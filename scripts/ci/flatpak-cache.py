#!/usr/bin/env python3
"""Drop obsolete NuGet downloads before saving the shared Flatpak source cache."""
import json
import os
from pathlib import Path


def prune(downloads, sources):
    keep = {(source.get('sha512') or source.get('sha256'), source['dest-filename'])
            for source in sources if source.get('type') == 'file'}
    removed = 0
    for directory in downloads.iterdir():
        if directory.is_symlink() or not directory.is_dir():
            continue
        for archive in directory.glob('*.nupkg'):
            if (directory.name, archive.name) not in keep:
                archive.unlink()
                removed += 1
        if not any(directory.iterdir()):
            directory.rmdir()
    return removed


if __name__ == '__main__':
    downloads = Path(os.environ['RUNNER_TEMP']) / 'crossmacro-flatpak-state/downloads'
    sources = json.loads(Path('flatpak/nuget-sources.json').read_text())
    removed = prune(downloads, sources)
    size = sum(p.stat().st_size for p in downloads.glob('*/*.nupkg')) / 1024 ** 2
    print(f'Flatpak cache: removed {removed} obsolete archives; current payload {size:.1f} MiB')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as stream:
        stream.write(f'\nFlatpak source cache payload: **{size:.1f} MiB**; obsolete archives removed: {removed}.\n')
