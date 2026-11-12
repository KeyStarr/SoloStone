#!/usr/bin/env python3
"""Mac-side staging for the SoloStone installer.

Builds the launcher and the plugin on this machine, fetches the loader files named in installer/loader-inputs.json
(Doorstop, BepInEx, the Mono class libraries: each archive and file hash-checked, none taken from a lab machine) and
leaves a folder that installer/build.ps1 -Staged finishes on Windows (payload list, update zip, Inno Setup).
No Windows access, no private keys.

    python3 installer/stage.py --out ~/solostone-stage [--hooks] [--version 0.1.0] \
        [--root-key RW... --signing-key RW... --repo owner/name --seq-floor N] [--tested-file FILE] [--loader-cache DIR]

Release builds never get --hooks. After building, the launcher is scanned for any trace of the live-test hooks and the
stage stops if a release build has one, or if a hook build lacks them.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True

INSTALLER = Path(__file__).resolve().parent
sys.path.insert(0, str(INSTALLER))
import loader_inputs  # noqa: E402
PRODUCT = INSTALLER.parent


def git_toplevel():
    """The git work tree that holds this repository, or PRODUCT when it is not a git checkout."""
    result = subprocess.run(['git', '-C', str(PRODUCT), 'rev-parse', '--show-toplevel'], capture_output=True, text=True)
    return Path(result.stdout.strip()).resolve() if result.returncode == 0 and result.stdout.strip() else PRODUCT


CHECKOUT = git_toplevel()
# An explicit DOTNET wins; otherwise whatever `dotnet` is on the PATH.
DEFAULT_DOTNET = os.environ.get('DOTNET') or (shutil.which('dotnet') or 'dotnet')
# Licence texts that ride with every download (stage folder `legal/`, then the app folder). SOURCE.txt is made from SOURCE.template.txt.
LEGAL_FILES = ('NOTICE', 'LICENSE', 'LICENSE-ADDITIONAL-PERMISSION.md')
LEGAL_FOLDER = 'LICENSES'
HOOK_MARKERS = [b'SOLOSTONE_TEST_CONTROL', b'SOLOSTONE_TEST_DISCOVERY', b'SOLOSTONE_TEST_UPDATE_BASE', b'elevate-request.txt',
                b'TestControl', b'TestElevation', b'TestDiscovery', b'BFFM', b'Battle' b'Net\\Hearthstone', b'HSOffline' b'Probe']
PLUGIN_MARKERS = [b'enable-test-control', b'TcpListener', b'HS test control', b'Battle' b'Net\\Hearthstone', b'HSOffline' b'Probe', b'enable-playtest',
                  b'benchmark-ranks', b'playtest-opponent', b'guide-cards-fail.txt', b'test-loaner-group', b'screenshot-request', b'restore-request',
                  b'hs-native-ui-' b'prototype', b'HSNative' b'UIProbe', b'HearthstoneOffline', b'local.hs.native.ui.probe', b'local.hs.offline.probe']
KEY_SHAPE = re.compile(r'^(RW[A-Za-z0-9+/=]{54})?$')


def fail(message):
    print('STAGE FAILED: ' + message, file=sys.stderr)
    sys.exit(1)


def outside_checkout(path):
    path = Path(path).expanduser().resolve()
    if path == CHECKOUT or CHECKOUT in path.parents:
        fail('Build output must be outside the checkout: ' + str(path))
    if path in CHECKOUT.parents:
        fail('Build output must not contain the checkout: ' + str(path))
    return path


def run(command, **kwargs):
    result = subprocess.run(command, capture_output=True, text=True, **kwargs)
    if result.returncode != 0:
        tail = '\n'.join((result.stdout + result.stderr).strip().splitlines()[-25:])
        fail('command failed (exit %d): %s\n%s' % (result.returncode, ' '.join(map(str, command[:4])), tail))
    return result.stdout


def save_schema():
    text = (PRODUCT / 'plugin' / 'LocalProfile.cs').read_text(encoding='utf-8')
    match = re.search(r'private const int CurrentVersion = (\d+);', text)
    if not match:
        fail('Cannot read the save schema (CurrentVersion) from plugin/LocalProfile.cs')
    return int(match.group(1))


def source_revision():
    revision = run(['git', '-C', str(PRODUCT), 'rev-parse', 'HEAD']).strip()
    dirty = run(['git', '-C', str(PRODUCT), 'status', '--porcelain', '--', '.']).strip() != ''
    return revision + ('-dirty' if dirty else '')


def has_hook_traces(exe, markers=None):
    data = exe.read_bytes()
    found = []
    for marker in markers or HOOK_MARKERS:
        if marker in data or marker.decode('ascii').encode('utf-16-le') in data:
            found.append(marker.decode('ascii'))
    return found


def no_machine_paths():
    """Release builds carry no path of the build machine: no PDB (its path sits in the PE debug directory) and source paths
    (CallerFilePath, deterministic metadata) mapped to /_/. Hook builds keep symbols for live-test stack traces."""
    return ['-p:DebugType=none', '-p:DebugSymbols=false', '-p:Deterministic=true', '-p:PathMap=%s=/_/' % PRODUCT.resolve()]


def build_launcher(dotnet, work, out, args):
    """One launcher build in its own intermediate folder, so variants never share stale compiled values."""
    # Each variant compiles different values into the exe, so none may reuse another's intermediate files.
    project = INSTALLER / 'SoloStone.Launcher'
    for folder in ('obj', 'bin'):
        shutil.rmtree(project / folder, ignore_errors=True)
    inputs = ['-p:NuGetAudit=false', '--no-incremental',
              '-p:SoloStoneRootKey=' + args.root_key, '-p:SoloStoneSigningKey=' + args.signing_key,
              '-p:SoloStoneRepo=' + args.repo, '-p:SoloStoneSeqFloor=' + str(args.seq_floor)]
    if args.hooks:
        inputs.append('-p:TestHooks=true')
    else:
        inputs += no_machine_paths()
    if args.tested_file:
        inputs.append('-p:TestedClientFile=' + str(Path(args.tested_file).resolve()))
    run([dotnet, 'build', str(project / 'SoloStone.Launcher.csproj'), '-c', 'Release', '--output', str(work / 'raw')] + inputs)
    shutil.rmtree(project / 'obj', ignore_errors=True)
    out.mkdir(parents=True, exist_ok=True)
    for item in sorted((work / 'raw').iterdir()):
        if item.is_file() and item.suffix != '.pdb':   # symbols carry the build machine's paths
            shutil.copy2(item, out / item.name)
    exe = out / 'SoloStone.exe'
    traces = has_hook_traces(exe)
    # A hook build must trip several markers. A scan that finds almost none means HOOK_MARKERS no longer matches the code, and the
    # "release build has no traces" check below would then pass for nothing.
    if args.hooks and len(traces) < 3:
        fail('A hook build should carry the test hooks, but the scan found %s' % traces)
    if not args.hooks and traces:
        fail('A RELEASE build carries traces of the live-test hooks: %s' % traces)
    return traces


def reference_properties(references):
    """MSBuild properties that point the plugin at a reference folder (game/ and bepinex/ inside it); none means the default
    `references/` next to plugin/."""
    if not references:
        return []
    return ['-p:GameReferences=' + str(Path(references) / 'game'), '-p:BepInExReferences=' + str(Path(references) / 'bepinex')]


def build_plugin(dotnet, work, out, hooks=False, references=None):
    """The installer-flavoured plugin (Installer=true, owned engine source). plugin/ itself is not touched.
    Debug, as engine/README.md says to ship it: Debug keeps the shipped engine's exception semantics."""
    run([dotnet, 'build', str(PRODUCT / 'plugin' / 'OfflineProbe.csproj'), '-c', 'Debug', '-p:EngineSource=true', '-p:Installer=true',
         '-p:NuGetAudit=false', '--artifacts-path', str(work)] + (['-p:TestHooks=true'] if hooks else no_machine_paths()) + reference_properties(references),
        cwd=str(PRODUCT))
    plugins = list((work / 'bin' / 'OfflineProbe').rglob('SoloStone.Plugin.dll'))
    if len(plugins) != 1:
        fail('Expected exactly one installer plugin build, found %d' % len(plugins))
    out.mkdir(parents=True, exist_ok=True)
    shutil.copy2(plugins[0], out / 'SoloStone.Plugin.dll')
    traces = has_hook_traces(plugins[0], PLUGIN_MARKERS)
    if not hooks and traces:
        fail('A RELEASE plugin carries test control or lab paths: %s' % traces)
    engine = plugins[0].parent / 'SabberStoneCore.dll'
    if not engine.exists():
        fail('The plugin build did not produce SabberStoneCore.dll')
    shutil.copy2(engine, out / 'SabberStoneCore.dll')
    # Anything this plugin adds beyond the default build must exist on Unity's Mono. Fails the stage, not a player.
    tests = Path(os.environ.get('SOLOSTONE_TOOL_DLL', ''))
    if not tests.is_file():
        print('Mono guard skipped: SOLOSTONE_TOOL_DLL is not set. installer_live.py stage and build.ps1 always run it.')
    else:
        run([dotnet, 'build', str(PRODUCT / 'plugin' / 'OfflineProbe.csproj'), '-c', 'Debug', '-p:EngineSource=true',
             '-p:NuGetAudit=false', '--artifacts-path', str(work / 'default')] + reference_properties(references), cwd=str(PRODUCT))
        baseline = list((work / 'default' / 'bin' / 'OfflineProbe').rglob('HSNativeUIProbe.dll'))
        if len(baseline) != 1:
            fail('Expected exactly one default plugin build, found %d' % len(baseline))
        shutil.copy2(baseline[0], out.parent / 'default-plugin-baseline.dll')  # kept beside plugin/, so it never reaches a payload
        print(run([dotnet, str(tests), 'tool', 'mono-guard', str(out / 'SoloStone.Plugin.dll'), str(baseline[0]), str(INSTALLER / 'mono-assumed.txt'), str(references or PRODUCT / 'references')]).strip())


def legal_root():
    """The licence files at the repository root: NOTICE, LICENSE, LICENSE-ADDITIONAL-PERMISSION.md and LICENSES/."""
    if all((PRODUCT / name).is_file() for name in LEGAL_FILES) and (PRODUCT / LEGAL_FOLDER).is_dir():
        return PRODUCT
    fail('The licence files (%s and %s/) are not in %s' % (', '.join(LEGAL_FILES), LEGAL_FOLDER, PRODUCT))


def source_text(version, revision, repo):
    """SOURCE.txt for the download: where the source of this build is. Without a repository name the line says so; a release
    build always has one (a release build always passes --repo)."""
    line = 'https://github.com/' + repo if repo else '(the repository address was not set when this was built)'
    template = (INSTALLER / 'SOURCE.template.txt').read_text(encoding='utf-8')
    return template.replace('@VERSION@', version).replace('@SOURCE_LINE@', line).replace('@REVISION@', revision)


def stage_legal(out, version, revision, repo):
    """Copy the licence texts and write SOURCE.txt into <stage>/legal."""
    source = legal_root()
    legal = out / 'legal'
    if legal.exists():
        shutil.rmtree(legal)
    legal.mkdir(parents=True)
    for name in LEGAL_FILES:
        shutil.copyfile(source / name, legal / name)
    shutil.copytree(source / LEGAL_FOLDER, legal / LEGAL_FOLDER)
    (legal / 'SOURCE.txt').write_bytes(source_text(version, revision, repo).encode('ascii'))


def stage_folder(dotnet, out, args, plugin_dir=None):
    out = outside_checkout(out)
    for key in (args.root_key, args.signing_key):
        if not KEY_SHAPE.match(key):
            fail('Keys must be minisign public keys (RW...). Never pass a private key.')
    work = out / '_work'
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True)
    launcher_out = out / 'launcher'
    if launcher_out.exists():
        shutil.rmtree(launcher_out)
    traces = build_launcher(dotnet, work / 'launcher', launcher_out, args)
    if plugin_dir:
        shutil.copytree(plugin_dir, out / 'plugin', dirs_exist_ok=True)
    else:
        build_plugin(dotnet, work / 'plugin', out / 'plugin', args.hooks, getattr(args, 'references', '') or None)
    try:
        loader_inputs.fetch(out / 'loader', args.loader_cache or None)
        loader_inputs.check_notice()
    except loader_inputs.Refused as error:
        fail('loader files: ' + str(error))
    release = {'schema': 1, 'version': args.version, 'saveSchema': save_schema(), 'sourceRevision': source_revision()}
    (out / 'release.json').write_text(json.dumps(release, indent=1) + '\n', encoding='ascii')
    shutil.copy2(INSTALLER / 'NOTICE.txt', out / 'NOTICE.txt')
    stage_legal(out, args.version, release['sourceRevision'], args.repo)
    shutil.rmtree(work)
    # Rules that need no loader: nothing from Hearthstone's client, no stray program next to the launcher.
    tests = Path(os.environ.get('SOLOSTONE_TOOL_DLL', ''))
    if tests.is_file():
        probe = out / '_app'
        shutil.copytree(launcher_out, probe)
        (probe / 'BepInEx' / 'plugins').mkdir(parents=True)
        for name in ('SoloStone.Plugin.dll', 'SabberStoneCore.dll'):
            shutil.copy2(out / 'plugin' / name, probe / 'BepInEx' / 'plugins' / name)
        try:
            print(run([dotnet, str(tests), 'tool', 'check-app', str(probe)]).strip())
        finally:
            shutil.rmtree(probe)
    return {'out': str(out), 'release': release, 'hookTraces': traces}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--out', required=True)
    parser.add_argument('--dotnet', default=DEFAULT_DOTNET)
    parser.add_argument('--version', default='0.1.0')
    parser.add_argument('--hooks', action='store_true', help='live-test build only; never for a release')
    parser.add_argument('--root-key', default='')
    parser.add_argument('--signing-key', default='')
    parser.add_argument('--repo', default='')
    parser.add_argument('--seq-floor', type=int, default=0)
    parser.add_argument('--tested-file', default='')
    parser.add_argument('--loader-cache', default='', help='folder that keeps downloaded loader archives between stages')
    parser.add_argument('--plugin-dir', default='', help='reuse an earlier plugin build instead of building one')
    parser.add_argument('--references', default='', help='folder with game/ and bepinex/ (your client\'s Managed files, BepInEx.dll and 0Harmony.dll); '
                        'default: references/ next to plugin/')
    args = parser.parse_args()
    if not Path(args.dotnet).exists():
        fail('dotnet not found at ' + args.dotnet)
    print(json.dumps(stage_folder(args.dotnet, args.out, args, args.plugin_dir or None), indent=1))


if __name__ == '__main__':
    main()
