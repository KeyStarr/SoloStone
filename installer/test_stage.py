"""Checks for stage.py and the build files it stages: unittest, no network, no dotnet. Run from the repository root:
    python3 -I installer/test_stage.py
A build from a clean checkout must need nothing outside it.
"""
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import stage  # noqa: E402

PRODUCT = Path(__file__).resolve().parent.parent
LEGAL = ('NOTICE', 'LICENSE', 'LICENSE-ADDITIONAL-PERMISSION.md')


def git(folder, *args):
    environment = {'GIT_CONFIG_GLOBAL': '/dev/null', 'GIT_CONFIG_SYSTEM': '/dev/null', 'PATH': '/usr/bin:/bin:/usr/local/bin:/opt/homebrew/bin'}
    return subprocess.run(['git', '-C', str(folder), *args], capture_output=True, text=True, check=True, env=environment).stdout.strip()


def write_legal(folder):
    for name in LEGAL:
        (folder / name).write_text(name + ' text\n')
    (folder / 'LICENSES').mkdir()
    (folder / 'LICENSES' / 'AGPL-3.0-or-later.txt').write_text('agpl\n')


class Folders(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self.tmp.name).resolve()
        self.addCleanup(self.tmp.cleanup)

    def failing(self, call, *args):
        with self.assertRaises(SystemExit):
            with mock.patch('sys.stderr'):
                call(*args)


class OutputFolder(Folders):
    def test_checkout_is_the_git_toplevel_not_the_folder_above_the_product(self):
        """The product is the repository root; CHECKOUT = PRODUCT.parent made every folder beside it 'inside'."""
        git(self.dir, 'init', '-q')
        (self.dir / 'installer').mkdir()
        with mock.patch.object(stage, 'PRODUCT', self.dir):
            self.assertEqual(stage.git_toplevel(), self.dir)
        (self.dir / 'sub' / 'product').mkdir(parents=True)  # the product inside a larger work tree
        with mock.patch.object(stage, 'PRODUCT', self.dir / 'sub' / 'product'):
            self.assertEqual(stage.git_toplevel(), self.dir)
        with mock.patch.object(stage, 'PRODUCT', self.dir / 'not-git-xyz'), mock.patch('subprocess.run', side_effect=lambda *a, **k: subprocess.CompletedProcess(a, 128, '', '')):
            self.assertEqual(stage.git_toplevel(), self.dir / 'not-git-xyz')

    def test_output_beside_an_export_is_fine_and_inside_or_around_it_is_refused(self):
        export = self.dir / 'work' / 'solostone'
        export.mkdir(parents=True)
        with mock.patch.object(stage, 'CHECKOUT', export):
            self.assertEqual(stage.outside_checkout(self.dir / 'work' / 'stage'), self.dir / 'work' / 'stage')
            self.assertEqual(stage.outside_checkout(self.dir / 'elsewhere'), self.dir / 'elsewhere')
            self.failing(stage.outside_checkout, export)
            self.failing(stage.outside_checkout, export / 'installer' / 'out')   # inside the repository
            self.failing(stage.outside_checkout, self.dir / 'work')              # would contain it
            self.failing(stage.outside_checkout, self.dir)

    def test_dotnet_default_prefers_the_environment_then_the_path(self):
        source = (PRODUCT / 'installer' / 'stage.py').read_text(encoding='utf-8')
        self.assertIn("os.environ.get('DOTNET') or", source)
        self.assertIn("shutil.which('dotnet')", source)


class LegalFiles(Folders):
    def layout(self):
        product = self.dir / 'product'
        (product / 'installer').mkdir(parents=True)
        write_legal(product)
        return product

    def test_licence_files_come_from_the_repository_root(self):
        product = self.layout()
        with mock.patch.object(stage, 'PRODUCT', product):
            self.assertEqual(stage.legal_root(), product)

    def test_missing_licence_files_stop_the_stage(self):
        product = self.layout()
        (product / 'LICENSE').unlink()
        with mock.patch.object(stage, 'PRODUCT', product):
            self.failing(stage.legal_root)

    def test_stage_legal_copies_the_texts_and_writes_the_source_pointer(self):
        product = self.layout()
        out = self.dir / 'stage'
        out.mkdir()
        with mock.patch.object(stage, 'PRODUCT', product):
            stage.stage_legal(out, '0.1.0-beta.1', 'abc123', 'owner/name')
        legal = out / 'legal'
        for name in LEGAL:
            self.assertEqual((legal / name).read_text(), name + ' text\n')
        self.assertTrue((legal / 'LICENSES' / 'AGPL-3.0-or-later.txt').is_file())
        source = (legal / 'SOURCE.txt').read_text()
        self.assertIn('0.1.0-beta.1', source)
        self.assertIn('https://github.com/owner/name', source)
        self.assertIn('abc123', source)
        self.assertNotIn('@', source)
        self.assertNotIn('{{', source)

    def test_without_a_repository_the_source_pointer_says_so_plainly(self):
        text = stage.source_text('1.0.0', 'abc', '')
        self.assertIn('not set', text)
        self.assertNotIn('github.com', text)
        self.assertNotIn('@', text)

    def test_the_required_set_is_the_same_in_stage_py_build_ps1_and_the_launcher(self):
        """stage.py makes it, build.ps1 copies and checks it, UpdateEngine.cs (check-release) requires it. They must name the same files."""
        script = (PRODUCT / 'installer' / 'build.ps1').read_text(encoding='utf-8')
        engine = (PRODUCT / 'installer' / 'SoloStone.Launcher' / 'UpdateEngine.cs').read_text(encoding='utf-8')
        listed = re.search(r'\$licenceFiles=@\((.*?)\)', script).group(1)
        in_script = {item.strip("'").replace('\\', '/') for item in listed.split(',')}
        in_engine = set(re.findall(r'"([A-Za-z0-9./_-]+)"', re.search(r'LicenceFiles=\{(.*?)\};', engine, re.S).group(1)))
        self.assertEqual(in_script, in_engine)
        self.assertTrue({'NOTICE', 'LICENSE', 'LICENSE-ADDITIONAL-PERMISSION.md', 'SOURCE.txt'} <= in_engine)
        self.assertTrue(any(name.startswith('LICENSES/') for name in in_engine))
        base = PRODUCT
        for name in in_engine - {'SOURCE.txt'}:
            self.assertTrue((base / name).is_file(), f'{name} is required in the download but is not in {base}')


class NothingOutside(unittest.TestCase):
    """The build files must not reach for anything an export lacks or a plain SDK machine does not have."""

    def test_the_engine_build_runs_no_program(self):
        project = (PRODUCT / 'engine' / 'SabberStoneCore' / 'SabberStoneCore.csproj').read_text(encoding='utf-8')
        self.assertNotIn('<Exec', project)
        self.assertNotIn('python3 -c', project)
        self.assertIn('RoslynCodeTaskFactory', project)

    def test_the_sdk_is_pinned(self):
        pin = (PRODUCT / 'global.json').read_text(encoding='utf-8')
        self.assertRegex(pin, r'"version":\s*"10\.0\.\d+"')
        self.assertIn('"rollForward": "disable"', pin)

    def test_stage_and_build_files_do_not_climb_out_of_the_product_folder(self):
        for name in ('installer/stage.py', 'installer/build.ps1', 'installer/test-all.sh'):
            text = (PRODUCT / name).read_text(encoding='utf-8')
            self.assertNotIn('PRODUCT.parent', text, name)
            self.assertNotIn('hearthstone-native-ui-prototype', text, name)
            self.assertNotIn('python3 -c', text, name)
            self.assertNotIn('hs-prototype-tools', text, name)
            self.assertNotIn('/usr/bin/python3', text, name)

    def test_the_plugin_builds_the_engine_from_source_by_default(self):
        project = (PRODUCT / 'plugin' / 'OfflineProbe.csproj').read_text(encoding='utf-8')
        self.assertRegex(project, r"<EngineSource Condition=\"'\$\(EngineSource\)' == '' and '\$\(StandardProof\)' != 'true' and '\$\(SabberStoneCorePath\)' == ''\">true</EngineSource>")
        # Tracked files only: a maintainer's ignored private/ checkout has projects that do name the vendor DLL.
        projects = [PRODUCT / name for name in git(PRODUCT, 'ls-files', '--', '*.csproj').splitlines()]
        self.assertIn(PRODUCT / 'plugin' / 'OfflineProbe.csproj', projects)
        for csproj in projects:
            self.assertNotIn('vendor/SabberStoneCore.dll', csproj.read_text(encoding='utf-8'), str(csproj))


    def test_release_builds_carry_no_build_machine_path(self):
        # 2026-11-09: a real 0.1.0 build carried /Users/<owner>/... (the PDB path) in SoloStone.exe and the plugin.
        flags = stage.no_machine_paths()
        self.assertIn('-p:DebugType=none', flags)
        self.assertIn('-p:PathMap=%s=/_/' % PRODUCT.resolve(), flags)
        calls = []
        with mock.patch.object(stage, 'run', side_effect=lambda command, **kw: calls.append(command) or ''), \
                mock.patch.object(stage, 'fail', side_effect=AssertionError):
            try:
                stage.build_plugin('dotnet', Path(tempfile.mkdtemp()), Path(tempfile.mkdtemp()))
            except AssertionError:
                pass  # no plugin file comes out of a mocked build
        self.assertTrue(set(flags) <= set(calls[0]), calls[0])


if __name__ == '__main__':
    unittest.main()
