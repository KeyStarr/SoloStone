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
  ci      every suite builds from this repository alone and needs no Hearthstone file. GitHub runs these. A few have a
          check that reads your client's own library: it runs when references/game is set up (see --client) and is
          skipped, with a printed reason, when it is not.
  client  a suite marked needs_client loads your own client's libraries. --client DIR copies them (the files named in
          CLIENT_FILES, from the Hearthstone folder or its Managed folder) into references/game, which git ignores.
          A suite that lists needs_local reads data built from your client (git ignores it), such as the card export
          in data/carddb/. Without the libraries or that data, the suite is SKIPPED with a printed reason, and the
          summary counts every skip.

Builds are Release builds with -p:EngineSource=true: the rules engine is built from engine/, never from a prebuilt
DLL. Build output goes to a temporary folder (or --artifacts) and nothing is written inside the repository except
the usual obj/ folders of the projects, for the client suite transport-invariants sim/out/, and, for fx-harmony with a client
set up, plugin/bin/ and engine/SabberStoneCore/bin and obj (git ignores all of them).
With --no-build, fx-harmony uses whatever plugin/bin holds. With a client set up it fails, rather than skips, its pinned check
when references/bepinex is missing (the plugin cannot be built) or, with --no-build, plugin/bin holds no built plugin.

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
PLUGIN_REFERENCES = ('references/bepinex/BepInEx.dll', 'references/bepinex/0Harmony.dll')
MANAGED_UNDER = (('Hearthstone_Data', 'Managed'), ('Managed',), ('Hearthstone.app', 'Contents', 'Resources', 'Data', 'Managed'),
                 ('Contents', 'Resources', 'Data', 'Managed'))


class Usage(Exception):
    pass


class Suite:
    """One named group of checks. `kind` picks the steps: dotnet project, HsSim command or Python unittest folder."""

    def __init__(self, name, kind, what, project=None, args=(), props=(), commands=(), needs_engine=False, folder=None,
                 artifacts=None, unix_only=False, needs_client=False, needs_local=(), prebuild=(), prebuild_in_tree_with_client=()):
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
        # mono: projects to build first into their own bin/ (the suite reads plugin/bin/...), only when a client is set up
        self.prebuild_in_tree_with_client = list(prebuild_in_tree_with_client)

    @property
    def tier(self):
        return 'client' if self.needs_client or self.needs_local else 'ci'

    def skip_reason(self):
        """Why this suite cannot run on this machine, or None. A reason is always printed and always counted."""
        if self.unix_only and os.name == 'nt':
            return 'its tests use unix paths and tools, so they do not run on Windows'
        if self.kind == 'mono' and os.name != 'nt' and not shutil.which('mono'):
            return 'needs mono on PATH (the client runs on Mono, and this suite patches under it)'
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
    Suite('fair-packs', 'dotnet', 'pack opening: fair rolls, pity rules and the frozen pack results', 'tests/fair-packs/FairPackTests.csproj'),
    Suite('ladder-rules', 'dotnet', 'ranked ladder rules (stars, floors, rewards) and a simulated climb through a synthetic opponent world',
          'tests/ladder-rules/LadderRulesTests.csproj'),
    Suite('recipes', 'dotnet', 'the shipped shop deck recipes validate and follow the recipe rules', 'tests/recipes/RecipeTests.csproj'),
    Suite('npc-world', 'dotnet', 'opponent decks, skills, personalities, matchmaking and the opponent population',
          'tests/npc-world/NpcWorldTests.csproj'),
    Suite('ai', 'dotnet', 'opponent AI fairness: it may not see hidden information', 'tests/ai/AiTests.csproj'),
    Suite('ai-golden', 'dotnet', 'opponent AI decisions on recorded positions (lethal, replies, planning, mulligan) and the three golden decks',
          'tests/ai-golden/GoldenTests.csproj'),
    Suite('parallel-groups', 'dotnet', 'the card lists behind each offline card pool, and the group proof checks', 'tests/parallel-groups/ParallelGroupsTests.csproj'),
    Suite('tier-presentation', 'dotnet', 'the rank tier reward screen steps (with stand-in game classes)', 'tests/tier-presentation/TierPresentationTests.csproj'),
    Suite('fire-fx-scene', 'dotnet', 'the fire effect scene helper', 'tests/fire-fx-scene/FireFxSceneTests.csproj'),
    Suite('dormant', 'dotnet', 'engine rules for Dormant cards', 'tests/dormant/DormantProofTests.csproj', needs_engine=True),
    Suite('kindred', 'dotnet', 'engine rules for Kindred cards', 'tests/kindred/KindredProofTests.csproj', needs_engine=True),
    Suite('reborn', 'dotnet', 'engine rules for Reborn cards', 'tests/reborn/RebornProofTests.csproj', needs_engine=True),
    Suite('engine-proof', 'dotnet', 'card-by-card engine proofs: effects, resolution, snapshots and definitions, and the engine rule proofs '
          '(Imbue, Dormant, Rewind, History, Location, Decision, snapshots, pools), canonical hashes, and the planner on a recorded turn; the full run, about 5 minutes',
          'tests/engine-proof/EngineProof.csproj', args=['--full']),
    Suite('standard-native', 'dotnet', 'native Standard-format flow checks that need no client: quest definitions, playability policy, loaners, '
          'definition audit, input trace binding', 'tests/standard-native/StandardNativeTests.csproj'),
    Suite('goldens', 'hssim', 'recorded games replay to the same result on the current engine (5 golden files)',
          commands=[['goldens', 'check', f'tests/goldens/{name}.json'] for name in GOLDENS]),
    Suite('fuzz', 'hssim', '200 random games: no crash, same result twice, no broken invariant',
          commands=[['fuzz', '--games', '200', '--seed', '1']]),
    Suite('launcher', 'dotnet', 'the launcher and installer logic (some checks skip without a Hearthstone client)',
          'installer/SoloStone.Launcher.Tests/SoloStone.Launcher.Tests.csproj', artifacts='launcher'),
    Suite('installer-python', 'python', 'the installer build tools (stage and loader checks; not on Windows)', folder='installer',
          unix_only=True),
]
# Logic suites. They build from this repository alone (stand-ins for the few game types they touch, tests/shared and
# tests/*/GameStandIns.cs), so GitHub runs them. Three checks read the client's own library and skip without it.
SUITES += [
    Suite('deck-ui', 'dotnet', 'deck layout geometry, button chrome and handheld rules of the native screens',
          'tests/deck-ui/DeckUiTests.csproj'),
    Suite('projection', 'dotnet', 'how menu and match state is projected onto the native screens (and the zero-tag equivalence table)',
          'tests/projection/ProjectionTests.csproj'),
    Suite('ladder', 'dotnet', 'the ranked ladder profile code: results, rewards, milestones, ledger, placement and casual mode',
          'tests/ladder/LadderTests.csproj'),
    Suite('recipe-picker', 'dotnet', 'the recipe picker and recipe guide of the native shop (one check reads the client library, if set up)',
          'tests/recipe-picker/RecipePickerTests.csproj'),
    Suite('progression', 'dotnet', 'profile, rewards, quests, achievements, ranks and settings logic behind the native screens',
          'tests/progression/ProgressionTests.csproj'),
    Suite('progression-extended', 'dotnet', 'the older progression fixtures: migrations, full premium fixtures, Reward V3, Death Knight, hero drafts, test loaners, NPC records',
          'tests/progression/ProgressionTests.csproj', args=['--extended']),
    Suite('progression-standard-batches', 'dotnet', 'the Standard test loaners and batch manifests against the client card export (classes, DBF ids, set admission)',
          'tests/progression/ProgressionTests.csproj', args=['--standard-batches-only'],
          needs_local=['data/carddb/37.0.0.254477/cards.json.gz', 'data/carddb/37.0.0.254477/set-overrides.json']),
    Suite('shop', 'dotnet', 'the native shop: catalogue, purchases, deck tiles and art maths (one check reads the client library, if set up)',
          'tests/shop/ShopTests.csproj'),
    Suite('fx-harmony', 'mono', 'the effect patches applied by the real Harmony library under Mono (the sub-spell pin needs a client)',
          'tests/fx-harmony/FxHarmonyTests.csproj', prebuild_in_tree_with_client=['plugin/OfflineProbe.csproj']),
]
# The client tier: suites that compile or load your own client's libraries (references/game; BepInEx in references/bepinex,
# docs/BUILDING.md step 1). engine-proof and standard-native build a second time with -p:ClientTransport=true, which adds the
# client's libraries and the native match, and each client half runs only what needs them. The data they read is in the
# repository, except what is built locally from a client (needs_local).
# Card and shop data exported from your own client with maintainer tools (docs/TESTING.md, "Data built locally"). Never committed.
CARDDB = 'data/carddb/37.0.0.254477/cards.json.gz'
SUITES += [
    Suite('engine-proof-client', 'dotnet', 'engine proofs that compare the engine with your client (card definitions file, hero powers, '
          'independent pool derivation) and the native transport halves of engine proofs',
          'tests/engine-proof/EngineProof.csproj', props=['-p:ClientTransport=true'], artifacts='engine-proof-client', needs_client=True,
          needs_local=(CARDDB, 'data/carddb/37.0.0.254477/set-overrides.json', 'engine/SabberStoneCore/resources/Data/CardDefs.xml', 'engine/SabberStoneCore/resources/Data/ClientOverlay.xml')),
    Suite('standard-native-client', 'dotnet', 'native match flow checks on Standard-format games: packets, Trade, live snapshots, location projection',
          'tests/standard-native/StandardNativeTests.csproj', props=['-p:ClientTransport=true'], artifacts='standard-native-client',
          needs_client=True),
    Suite('native-bindings', 'dotnet', 'the plugin\'s bindings: every client method it patches exists in your client libraries',
          'tests/native-bindings/NativeBindingTests.csproj', needs_client=True, needs_local=PLUGIN_REFERENCES),
    Suite('native-match', 'dotnet', 'the native match: plugin match logic on recorded games, and the recipe editor of the built plugin',
          'tests/native-match/NativeMatchTests.csproj', needs_client=True, prebuild=['plugin/OfflineProbe.csproj'],
          needs_local=PLUGIN_REFERENCES + (CARDDB, 'data/fx/empty-playeffect-37.0.0.254477.tsv')),
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
        self.env = dict(os.environ, SOLOSTONE_ROOT=str(ROOT), DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
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

    def prebuild_with_client(self, log, suite):
        """With a client set up, build what the suite's client-only check reads. False (a failure) when that cannot be done:
        a green run must not hide a skipped check from someone who set up a client to run it."""
        def note(text):
            Path(log).write_bytes(Path(log).read_bytes() + (text + '\n').encode())
        if not all((GAME_REFERENCES / name).is_file() for name in CLIENT_FILES):
            note('no client set up (references/game): the pinned sub-spell check skips')
            return True
        absent = [path for path in PLUGIN_REFERENCES if not (ROOT / path).is_file()]
        if absent:
            note('references/game is set up, but these are missing: ' + ', '.join(absent) + ', so the plugin cannot be built and the pinned '
                 'sub-spell check would skip (docs/BUILDING.md step 1)')
            return False
        return all(self.step(log, [self.dotnet, 'build', project, '-c', 'Release', '-p:EngineSource=true', '-p:NuGetAudit=false',
                                   '--nologo', '-v', 'quiet', '-m:1'], 'build') for project in suite.prebuild_in_tree_with_client)

    def run_mono(self, suite, log):
        """A .NET Framework program (the client's runtime): built like a dotnet suite, run under mono (directly on Windows)."""
        artifacts = self.shared()
        if self.build:
            if suite.prebuild_in_tree_with_client and not self.prebuild_with_client(log, suite):
                return False
            if not self.build_project(log, suite.project, artifacts):
                return False
        elif suite.prebuild_in_tree_with_client and all((GAME_REFERENCES / name).is_file() for name in CLIENT_FILES) \
                and not (ROOT / 'plugin/bin/Release/net48/HSNativeUIProbe.dll').is_file():
            Path(log).write_bytes(Path(log).read_bytes() + b'references/game is set up but plugin/bin holds no built plugin (--no-build): '
                                  b'the pinned sub-spell check would skip\n')
            return False
        program = artifacts / 'bin' / suite.assembly / 'release' / (suite.assembly + '.exe')
        if not program.is_file():
            Path(log).write_bytes(Path(log).read_bytes() + f'missing build output {program}\n'.encode())
            return False
        return self.step(log, ([] if os.name == 'nt' else [shutil.which('mono')]) + [program, *suite.args], 'run')

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
            if any(suite.tier == 'client' for suite in chosen) and args.suite:
                raise Usage('--ci cannot run a client-tier suite: ' + ', '.join(suite.name for suite in chosen if suite.tier == 'client'))
            chosen = [suite for suite in chosen if suite.tier == 'ci']
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
