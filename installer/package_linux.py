#!/usr/bin/env python3
"""Linux download for SoloStone, from a stage folder (installer/stage.py).

The Linux launcher is a native program; the game is still the Windows client, which it runs under Wine in a network
namespace. This builds that launcher and puts beside it the same payload that build.ps1 puts beside the Windows one:
the loader files listed in loader-inputs.json (hash-checked again), the plugin and engine, client.config,
doorstop_config.ini and payload.json. Then the licence texts and SOURCE.txt, and a tarball of the whole folder.

    python3 installer/stage.py --out <stage folder> --version <version>
    python3 installer/package_linux.py --staged <stage folder> --version <version> --out <folder> [--dotnet <dotnet>]

Leaves <out>/SoloStone-linux-<version>/ and <out>/SoloStone-linux-<version>.tar.gz. No Windows, no private keys.
"""
import argparse
import hashlib
import json
import os
import shutil
import struct
import subprocess
import sys
import tarfile
from pathlib import Path

sys.dont_write_bytecode = True

INSTALLER = Path(__file__).resolve().parent
PRODUCT = INSTALLER.parent
LEGAL = ['NOTICE', 'LICENSE', 'LICENSE-ADDITIONAL-PERMISSION.md', 'SOURCE.txt', 'LICENSES']
# The same text build.ps1 writes. Doorstop loads BepInEx's preloader, with the full Mono class libraries first on the search path.
DOORSTOP_CONFIG = ('[General]\r\nenabled=true\r\ntarget_assembly=BepInEx\\core\\BepInEx.Preloader.dll\r\nredirect_output_log=false\r\n'
                   'ignore_disable_switch=false\r\n\r\n[UnityMono]\r\ndll_search_path_override=BepInEx\\unstripped_corlib\r\n')


def fail(message):
    print('PACKAGE REFUSED: ' + message, file=sys.stderr)
    sys.exit(1)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def regular(path):
    if path.is_symlink() or not path.is_file():
        fail('not a regular file: %s' % path)
    return path


def build_launcher(dotnet, app):
    """Self-contained, one file: a player needs no .NET installed."""
    command = [dotnet, 'publish', str(INSTALLER / 'SoloStone.Linux' / 'SoloStone.Linux.csproj'), '-c', 'Release', '-r', 'linux-x64',
               '--self-contained', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=none',
               '-p:NuGetAudit=false', '-o', str(app), '-nologo', '-v', 'q']
    result = subprocess.run(command, cwd=PRODUCT, capture_output=True, text=True)
    if result.returncode != 0:
        fail('the Linux launcher did not build:\n' + result.stdout + result.stderr)
    regular(app / 'solostone')


def payload(staged, app, revision):
    loader_list = json.loads((INSTALLER / 'loader-inputs.json').read_text(encoding='utf-8'))
    if loader_list.get('schema') != 1:
        fail('loader-inputs.json has an unknown schema')
    listed = {entry['path']: entry['sha256'].upper() for entry in loader_list['files']}
    loader = staged / 'loader'
    present = {p.relative_to(loader).as_posix() for p in loader.rglob('*')
               if (p.is_file() or p.is_symlink()) and p.name != '.solostone-loader'}  # loader_inputs.py's own marker
    if present != set(listed):
        fail('the stage loader folder differs from loader-inputs.json: %s' % sorted(present ^ set(listed)))
    target = app / 'payload'
    for path, expected in listed.items():
        source = regular(loader / path)
        if sha256(source) != expected:
            fail('loader file differs from loader-inputs.json: ' + path)
        (target / path).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target / path)
    plugins = target / 'BepInEx' / 'plugins'
    plugins.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(regular(staged / 'plugin' / 'SoloStone.Plugin.dll'), plugins / 'SoloStone.Plugin.dll')
    shutil.copyfile(regular(staged / 'plugin' / 'SabberStoneCore.dll'), plugins / 'SabberStoneCore.dll')
    shutil.copyfile(regular(PRODUCT / 'client.config'), target / 'client.config')
    (target / 'doorstop_config.ini').write_bytes(DOORSTOP_CONFIG.encode('ascii'))
    pins = {p.relative_to(target).as_posix(): sha256(p) for p in sorted(target.rglob('*')) if p.is_file()}
    record = {'schema': 1, 'bepInExVersion': loader_list['bepInExVersion'], 'doorstopVersion': loader_list['doorstopVersion'],
              'sourceRevision': revision, 'engineSha256': pins['BepInEx/plugins/SabberStoneCore.dll'],
              'pluginSha256': pins['BepInEx/plugins/SoloStone.Plugin.dll'], 'files': pins}
    (app / 'payload.json').write_text(json.dumps(record, indent=1) + '\n', encoding='ascii')


def icon(app):
    """The launcher icon's largest image, for the applications menu. Every image in launcher.ico is a stored PNG."""
    data = (INSTALLER / 'assets' / 'launcher.ico').read_bytes()
    best = None
    for index in range(struct.unpack_from('<H', data, 4)[0]):
        width, _, _, _, _, _, size, offset = struct.unpack_from('<BBBBHHII', data, 6 + 16 * index)
        if data[offset:offset + 8] == b'\x89PNG\r\n\x1a\n' and (best is None or (width or 256) > best[0]):
            best = (width or 256, data[offset:offset + size])
    if best is None:
        fail('launcher.ico holds no PNG image')
    (app / 'solostone.png').write_bytes(best[1])


def tarball(folder, path):
    """Sorted names, fixed owner and times: the same inputs give the same bytes."""
    def clean(info):
        info.uid = info.gid = 0
        info.uname = info.gname = ''
        info.mtime = 0
        return info
    entries = sorted(folder.rglob('*'))
    with tarfile.open(path, 'w:gz', format=tarfile.PAX_FORMAT) as tar:
        tar.add(folder, arcname=folder.name, recursive=False, filter=clean)
        for entry in entries:
            tar.add(entry, arcname=folder.name + '/' + entry.relative_to(folder).as_posix(), recursive=False, filter=clean)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--staged', required=True)
    parser.add_argument('--version', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--dotnet', default=shutil.which('dotnet') or 'dotnet')
    args = parser.parse_args()
    staged, out = Path(args.staged).resolve(), Path(args.out).resolve()
    for needed in ('plugin/SoloStone.Plugin.dll', 'plugin/SabberStoneCore.dll', 'release.json', 'NOTICE.txt', 'loader', 'legal'):
        if not (staged / needed).exists():
            fail('the stage folder is missing ' + needed)
    release = json.loads((staged / 'release.json').read_text(encoding='ascii'))
    if release.get('schema') != 1 or release.get('version') != args.version:
        fail('release.json does not match --version')
    if out == PRODUCT or PRODUCT in out.parents:
        fail('choose an output folder outside the checkout')
    app = out / ('SoloStone-linux-' + args.version)
    if app.exists():
        shutil.rmtree(app)
    app.mkdir(parents=True)
    build_launcher(args.dotnet, app)
    payload(staged, app, release['sourceRevision'])
    icon(app)
    shutil.copyfile(staged / 'release.json', app / 'release.json')
    shutil.copyfile(staged / 'NOTICE.txt', app / 'NOTICE.txt')
    for name in LEGAL:
        source = staged / 'legal' / name
        if source.is_dir():
            shutil.copytree(source, app / name)
        else:
            shutil.copyfile(regular(source), app / name)
    archive = out / (app.name + '.tar.gz')
    tarball(app, archive)
    print('Linux download: %s (%s)' % (archive, sha256(archive)))


if __name__ == '__main__':
    main()
