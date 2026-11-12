#!/usr/bin/env python3
"""Build and run every public SoloStone test suite with one command (Python 3.10+, standard library only).

    python3 tests/run.py                      build and run everything
    python3 tests/run.py --list               list the suites and what each one covers
    python3 tests/run.py --suite cards        run one suite (repeat the flag for more)
    python3 tests/run.py --no-build --artifacts DIR   run again without building (needs the DIR of an earlier run)
    python3 tests/run.py --client DIR         also run the suites that need your own Hearthstone client
    python3 tests/run.py --ci                 what GitHub runs: only the suites that need no client, and a skip fails

Needs the .NET SDK that global.json names (put it on PATH, or set DOTNET, or pass --dotnet).

Two tiers (docs/TESTING.md):
  ci      every suite builds from this repository alone and needs no Hearthstone file. GitHub runs these.
  client  a suite marked needs_client loads your own client's libraries. --client DIR copies them (the files named in
          CLIENT_FILES, from the Hearthstone folder or its Managed folder) into references/game, which git ignores.
          Without them, or without the data the suite lists in needs_local, the suite is SKIPPED with a printed
          reason, and the summary counts every skip.

Builds are Release builds with -p:EngineSource=true: the rules engine is built from engine/, never from a prebuilt
DLL. Build output goes to a temporary folder (or --artifacts) and nothing is written inside the repository except
the usual obj/ folders of the projects.

Exit code: 0 every suite passed (or was a skip that is allowed), 1 at least one failed or, with --ci, any suite was
skipped, 2 the tool could not start (no SDK, unknown suite, bad --client folder, ...).
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
TAIL_LINES = 60
GAME_REFERENCES = ROOT / 'references' / 'game'
# The player's own libraries that the plugin and the client-tier suites reference (docs/BUILDING.md, step 1).
CLIENT_FILES = ('Assembly-CSharp.dll', 'blizzard.bgsclient.dll', 'blizzard.bgssdk.dll', 'Blizzard.T5.Core.dll',
                'Blizzard.T5.JobQueue.dll', 'Blizzard.T5.MiniJSON.dll', 'Blizzard.T5.ServiceLocator.dll', 'UnityEngine.dll',
                'UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll', 'UnityEngine.InputLegacyModule.dll',
                'UnityEngine.PhysicsModule.dll', 'UnityEngine.ScreenCaptureModule.dll')
# Where the Managed folder sits under a Hearthstone folder: Windows, then the macOS app bundle.
MANAGED_UNDER = (('Hearthstone_Data', 'Managed'), ('Managed',), ('Hearthstone.app', 'Contents', 'Resources', 'Data', 'Managed'),
                 ('Contents', 'Resources', 'Data', 'Managed'))


class Usage(Exception):
    pass


class Suite:
    """One named group of checks. `kind` picks the steps: dotnet project, HsSim command or Python unittest folder."""

    def __init__(self, name, kind, what, project=None, args=(), props=(), commands=(), needs_engine=False, folder=None,
                 artifacts=None, unix_only=False, needs_client=False, needs_local=(), prebuild=()):
        self.name, self.kind, self.what = name, kind, what
        self.project = project          # dotnet: csproj, relative to the repository root
        self.args = list(args)          # dotnet: arguments for the built program
        self.props = list(props)        # dotnet: extra -p: build properties
        self.commands = list(commands)  # hssim: argument lists for HsSim.dll
        self.needs_engine = needs_engine  # dotnet: build property SabberStoneCorePath = the engine DLL
        self.folder = folder            # python: unittest discover folder
        self.artifacts = artifacts      # dotnet: own artifacts sub-folder (default: the shared one)
        self.unix_only = unix_only      # skipped, with a note, on Windows
        self.needs_client = needs_client  # client tier: needs references/game (see --client); skipped without it
        self.needs_local = list(needs_local)  # client tier: files built locally (never committed), relative to the root
        self.prebuild = list(prebuild)  # dotnet: projects to build first into the shared artifacts (the suite loads their output)

    @property
    def tier(self):
        return 'client' if self.needs_client else 'ci'

    def skip_reason(self):
        """Why this suite cannot run on this machine, or None. A reason is always printed and always counted."""
        if self.unix_only and os.name == 'nt':
            return 'its tests use unix paths and tools, so they do not run on Windows'
        if self.needs_client:
            missing = [name for name in CLIENT_FILES if not (GAME_REFERENCES / name).is_file()]
            if missing:
                return ('needs your own Hearthstone client: references/game lacks ' + ', '.join(missing[:3])
                        + (' and %d more' % (len(missing) - 3) if len(missing) > 3 else '') + ' (run with --client DIR)')
            absent = [path for path in self.needs_local if not (ROOT / path).exists()]
            if absent:
                return 'needs data built locally from your client, not found: ' + ', '.join(absent)
        return None

    @property
    def assembly(self):
        return Path(self.project).stem


GOLDENS = ('classic-source', 'modern', 'core-reprints', 'core-2026', 'offline-standard-pools-v1')

SUITES = [
    Suite('cards', 'dotnet', 'card catalog and pool counts, rarity rules, and card scenarios on the engine (damage, auras, deathrattles, secrets)', 'tests/cards/CardsTests.csproj'),
    Suite('fair-packs', 'dotnet', 'pack opening: fair rolls, pity rules and the frozen pack results', 'tests/fairpacks/FairPackTests.csproj'),
    Suite('ladder-rules', 'dotnet', 'ranked ladder rules (stars, floors, rewards) and a simulated climb through a synthetic opponent world',
          'tests/ladder-rules/LadderRulesTests.csproj'),
    Suite('recipes', 'dotnet', 'the shipped shop deck recipes validate and follow the recipe rules', 'tests/recipes/RecipeTests.csproj'),
    Suite('npc-world', 'dotnet', 'opponent decks, skills, personalities, matchmaking and the opponent population',
          'tests/npc-world/NpcWorldTests.csproj'),
    Suite('ai', 'dotnet', 'opponent AI fairness: it may not see hidden information', 'tests/ai/AiTests.csproj'),
    Suite('ai-golden', 'dotnet', 'opponent AI decisions on recorded positions (lethal, replies, planning, mulligan) and the three golden decks',
          'tests/golden/GoldenTests.csproj'),
    Suite('parallel-groups', 'dotnet', 'the card lists behind each offline card pool, and the group proof checks', 'tests/parallel-groups/ParallelGroupsTests.csproj'),
    Suite('tier-presentation', 'dotnet', 'the rank tier reward screen steps (with stand-in game classes)', 'tests/tier-presentation/TierPresentationTests.csproj'),
    Suite('fire-fx-scene', 'dotnet', 'the fire effect scene helper', 'tests/fire-fx-scene/FireFxSceneTests.csproj'),
    Suite('dormant', 'dotnet', 'engine rules for Dormant cards', 'tests/dormant-proof/DormantProofTests.csproj', needs_engine=True),
    Suite('kindred', 'dotnet', 'engine rules for Kindred cards', 'tests/kindred-proof/KindredProofTests.csproj', needs_engine=True),
    Suite('reborn', 'dotnet', 'engine rules for Reborn cards', 'tests/reborn-proof/RebornProofTests.csproj', needs_engine=True),
    Suite('goldens', 'hssim', 'recorded games replay to the same result on the current engine (5 golden files)',
          commands=[['goldens', 'check', f'tests/goldens/{name}.json'] for name in GOLDENS]),
    Suite('fuzz', 'hssim', '200 random games: no crash, same result twice, no broken invariant',
          commands=[['fuzz', '--games', '200', '--seed', '1']]),
    Suite('launcher', 'dotnet', 'the launcher and installer logic (some checks skip without a Hearthstone client)',
          'installer/SoloStone.Launcher.Tests/SoloStone.Launcher.Tests.csproj', artifacts='launcher'),
    Suite('installer-python', 'python', 'the installer build tools (stage and loader checks; not on Windows)', folder='installer',
          unix_only=True),
]
# The client tier: suites that compile or load your own client's libraries (references/game; BepInEx in references/bepinex,
# docs/BUILDING.md step 1). The data they read is in the repository, except what is built locally from a client (needs_local).
BEPINEX = ('references/bepinex/BepInEx.dll', 'references/bepinex/0Harmony.dll')
# Card and shop data exported from your own client with maintainer tools (docs/TESTING.md, "Data built locally"). Never committed.
CARDDB = 'data/carddb/37.0.0.254477/cards.json.gz'
SUITES += [
    Suite('native-bindings', 'dotnet', 'the plugin\'s bindings: every client method it patches exists in your client libraries',
          'tests/native-bindings/NativeBindingTests.csproj', needs_client=True, needs_local=BEPINEX),
    Suite('native-match', 'dotnet', 'the native match: plugin match logic on recorded games, and the recipe editor of the built plugin',
          'tests/native-match/NativeMatchTests.csproj', needs_client=True, prebuild=['plugin/OfflineProbe.csproj'],
          needs_local=BEPINEX + (CARDDB, 'data/fx/empty-playeffect-37.0.0.254477.tsv')),
    Suite('transport-invariants', 'dotnet', 'engine-to-client transport: invariants over recorded and random games (long)',
          'tests/transport-invariants/TransportInvariants.csproj', args=['--strict'], needs_client=True, needs_local=(CARDDB,)),
]
BY_NAME = {suite.name: suite for suite in SUITES}


def say(text=''):
    print(text, flush=True)


def find_dotnet(given):
    found = given or os.environ.get('DOTNET') or shutil.which('dotnet')
    if not found:
        raise Usage('no .NET SDK found: put `dotnet` on PATH, set DOTNET, or pass --dotnet (global.json names the version)')
    return str(found)


def find_managed(given):
    """The folder that holds the client's libraries: GIVEN itself, or the usual place under a Hearthstone folder."""
    base = Path(given).expanduser()
    for parts in ((),) + MANAGED_UNDER:
        folder = base.joinpath(*parts)
        if (folder / 'Assembly-CSharp.dll').is_file():
            return folder
    raise Usage(f'--client {given}: no Assembly-CSharp.dll there or under Hearthstone_Data/Managed (point it at the '
                'Hearthstone folder or its Managed folder)')


def collect_client(given):
    """Copy the player's own libraries into references/game, which git ignores. Returns the Managed folder used."""
    managed = find_managed(given)
    lacking = [name for name in CLIENT_FILES if not (managed / name).is_file()]
    if lacking:
        raise Usage(f'{managed} lacks: ' + ', '.join(lacking))
    GAME_REFERENCES.mkdir(parents=True, exist_ok=True)
    for name in CLIENT_FILES:
        target = GAME_REFERENCES / name
        target.unlink(missing_ok=True)  # a client file is often read-only, and so is an earlier copy of it
        shutil.copyfile(managed / name, target)
    return managed


def tail(path, count=TAIL_LINES):
    lines = Path(path).read_text(errors='replace').splitlines()
    return '\n'.join(lines[-count:])


class Runner:
    def __init__(self, dotnet, artifacts, build, verbose):
        self.dotnet, self.artifacts, self.build, self.verbose = dotnet, Path(artifacts), build, verbose
        self.logs = self.artifacts / 'logs'
        self.logs.mkdir(parents=True, exist_ok=True)
        self.engine_built = None  # None not tried, else True or False
        self.env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
                        PYTHONDONTWRITEBYTECODE='1')

    def shared(self):
        return self.artifacts / 'build'

    def engine_dll(self):
        return self.shared() / 'bin' / 'SabberStoneCore' / 'release_net10.0' / 'SabberStoneCore.dll'

    def hssim_dll(self):
        return self.shared() / 'bin' / 'HsSim' / 'release' / 'HsSim.dll'

    def step(self, log, command, label):
        """Run one command from the repository root, appending its output to `log`. Returns True on exit 0."""
        with open(log, 'ab') as handle:
            handle.write(f'$ {" ".join(str(part) for part in command)}\n'.encode())
            handle.flush()
            if self.verbose:
                result = subprocess.run([str(part) for part in command], cwd=ROOT, env=self.env, stdout=subprocess.PIPE,
                                        stderr=subprocess.STDOUT)
                handle.write(result.stdout)
                sys.stdout.buffer.write(result.stdout)
                sys.stdout.flush()
            else:
                result = subprocess.run([str(part) for part in command], cwd=ROOT, env=self.env, stdout=handle,
                                        stderr=subprocess.STDOUT)
            handle.write(f'[{label} exit {result.returncode}]\n'.encode())
        return result.returncode == 0

    def build_project(self, log, project, artifacts, props=()):
        command = [self.dotnet, 'build', project, '-c', 'Release', '--artifacts-path', artifacts, '-p:EngineSource=true',
                   '-p:NuGetAudit=false', '--nologo', '-v', 'quiet', '-m:1', *props]
        return self.step(log, command, 'build')

    def ensure_hssim(self, log):
        """HsSim builds the engine too; the engine DLL it leaves is the one the Dormant, Kindred and Reborn suites test."""
        if self.engine_built is None:
            self.engine_built = (not self.build) or self.build_project(log, 'sim/HsSim/HsSim.csproj', self.shared())
        return self.engine_built and self.hssim_dll().is_file() and self.engine_dll().is_file()

    def run(self, suite):
        log = self.logs / (suite.name + '.log')
        log.write_bytes(b'')
        started = time.monotonic()
        try:
            ok = getattr(self, 'run_' + suite.kind)(suite, log)
        except OSError as error:
            log.write_bytes(log.read_bytes() + f'could not start: {error}\n'.encode())
            ok = False
        return ok, time.monotonic() - started, log

    def run_dotnet(self, suite, log):
        artifacts = self.artifacts / suite.artifacts if suite.artifacts else self.shared()
        props = list(suite.props)
        if suite.needs_engine:
            if not self.ensure_hssim(log):
                return False
            props.append(f'-p:SabberStoneCorePath={self.engine_dll()}')
        if self.build:
            for project in suite.prebuild:
                if not self.build_project(log, project, artifacts):
                    return False
            if not self.build_project(log, suite.project, artifacts, props):
                return False
        program = artifacts / 'bin' / suite.assembly / 'release' / (suite.assembly + '.dll')
        if not program.is_file():
            Path(log).write_bytes(Path(log).read_bytes() + f'missing build output {program}\n'.encode())
            return False
        return self.step(log, [self.dotnet, program, *suite.args], 'run')

    def run_hssim(self, suite, log):
        if not self.ensure_hssim(log):
            return False
        ok = True
        for command in suite.commands:
            ok = self.step(log, [self.dotnet, self.hssim_dll(), *command], 'run') and ok
        return ok

    def run_python(self, suite, log):
        return self.step(log, [sys.executable, '-I', '-B', '-m', 'unittest', 'discover', '-s', suite.folder, '-p', 'test_*.py'], 'run')


def parse(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--list', action='store_true', help='list the suites and exit')
    parser.add_argument('--suite', action='append', default=[], metavar='NAME', help='run only this suite (repeatable)')
    parser.add_argument('--no-build', action='store_true', help='skip the builds; needs --artifacts of an earlier run')
    parser.add_argument('--artifacts', type=Path, help='keep build output and logs here (default: a temporary folder, removed after)')
    parser.add_argument('--dotnet', help='path of the dotnet program (default: DOTNET, then PATH)')
    parser.add_argument('--client', metavar='DIR', help='your Hearthstone folder (or its Managed folder): copy its libraries into '
                        'references/game so the client-tier suites can run')
    parser.add_argument('--ci', action='store_true', help='what GitHub runs: only the suites that need no client; any skip is a failure')
    parser.add_argument('-v', '--verbose', action='store_true', help='show each suite output while it runs')
    return parser.parse_args(argv)


def main(argv=None):
    args = parse(argv)
    if args.list:
        width = max(len(suite.name) for suite in SUITES)
        for suite in SUITES:
            say(f'{suite.name.ljust(width)}  [{suite.tier}]  {suite.what}')
        return 0
    try:
        unknown = [name for name in args.suite if name not in BY_NAME]
        if unknown:
            raise Usage('unknown suite: ' + ', '.join(unknown) + ' (see --list)')
        if args.ci and args.client:
            raise Usage('--ci runs only the suites that need no client, so it cannot be combined with --client')
        chosen = [BY_NAME[name] for name in args.suite] if args.suite else SUITES
        if args.ci:
            if any(suite.needs_client for suite in chosen) and args.suite:
                raise Usage('--ci cannot run a client-tier suite: ' + ', '.join(suite.name for suite in chosen if suite.needs_client))
            chosen = [suite for suite in chosen if not suite.needs_client]
        managed = find_managed(args.client) if args.client else None
        if args.no_build and not args.artifacts:
            raise Usage('--no-build needs --artifacts, the folder of an earlier run')
        dotnet = find_dotnet(args.dotnet) if any(suite.kind != 'python' for suite in chosen) else None
    except Usage as error:
        print('run.py: ' + str(error), file=sys.stderr)
        return 2
    if managed:
        try:
            collect_client(managed)
        except (OSError, Usage) as error:
            print(f'run.py: could not copy the client libraries: {error}', file=sys.stderr)
            return 2
        say(f'run.py: copied {len(CLIENT_FILES)} client libraries from {managed} to references/game')
    temporary = None
    if args.artifacts:
        artifacts = args.artifacts.resolve()
        if args.no_build and not artifacts.is_dir():
            print(f'run.py: {artifacts} does not exist', file=sys.stderr)
            return 2
    else:
        temporary = tempfile.mkdtemp(prefix='solostone-tests-')
        artifacts = Path(temporary)
    results = []
    try:
        runner = Runner(dotnet, artifacts, not args.no_build, args.verbose)
        say(f'run.py: {len(chosen)} suites, artifacts in {artifacts}')
        for suite in chosen:
            say(f'--- {suite.name}')
            reason = suite.skip_reason()
            if reason:
                results.append((suite.name, None, 0.0, None))
                say(f'SKIP {suite.name}: {reason}')
                continue
            ok, seconds, log = runner.run(suite)
            results.append((suite.name, ok, seconds, log))
            say(f'{"PASS" if ok else "FAIL"} {suite.name} ({seconds:.0f}s)')
            if not ok and not args.verbose:
                say(tail(log))
        say()
        width = max(len(name) for name, *_ in results)
        for name, ok, seconds, log in results:
            label = 'SKIP' if ok is None else 'PASS' if ok else 'FAIL'
            say(f'{label}  {name.ljust(width)}  {seconds:6.1f}s' + (f'  log: {log}' if ok is False else ''))
        failed = [name for name, ok, *_ in results if ok is False]
        skipped = [name for name, ok, *_ in results if ok is None]
        say()
        say(f'{len(results) - len(failed) - len(skipped)} of {len(results)} suites passed, {len(skipped)} skipped'
            + (f' ({", ".join(skipped)})' if skipped else '')
            + (f'; failed: {", ".join(failed)}' if failed else ''))
        if failed and temporary:
            say('(logs are removed with the temporary folder; use --artifacts DIR to keep them)')
        if skipped and args.ci:
            say(f'run.py --ci: {len(skipped)} skipped, and in CI a skip is a failure: {", ".join(skipped)}')
        return 1 if failed or (skipped and args.ci) else 0
    finally:
        if temporary:
            shutil.rmtree(temporary, ignore_errors=True)


if __name__ == '__main__':
    sys.exit(main())
