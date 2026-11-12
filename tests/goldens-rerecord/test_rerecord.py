"""`HsSim goldens rerecord`: replays a golden file and re-records only the games that no longer match.

Run through tests/run.py (suite goldens-rerecord), which builds HsSim and sets HSSIM_DLL and DOTNET.
"""
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'tests' / 'goldens' / 'core-2026.json'
DLL = os.environ.get('HSSIM_DLL')
DOTNET = os.environ.get('DOTNET_PATH') or shutil.which('dotnet')
ENGINE_SHA = re.compile(r'"engineSha":"[0-9a-f]+"')


def game_ids(text):
    return re.findall(r'"id":"([^"]+)"', text)


def game_chunks(text):
    """The file split into the header and one chunk per game, to compare games one by one."""
    return re.split(r'(?=\{"replaySchema")', text)


@unittest.skipUnless(DLL and DOTNET, 'run through tests/run.py --suite goldens-rerecord')
class RerecordTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = SOURCE.read_text()
        cls.chunks = game_chunks(cls.original)
        cls.ids = game_ids(cls.original)
        assert len(cls.ids) >= 3

    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.path = Path(folder.name) / 'golden.json'
        self.path.write_text(self.original)

    def rerecord(self, *extra):
        # Run from the repository root: the pool snapshots are found at tests/goldens/pools.
        result = subprocess.run([DOTNET, DLL, 'goldens', 'rerecord', str(self.path), *extra], cwd=ROOT,
                                capture_output=True, text=True, timeout=1200)
        return result

    def games(self):
        return game_chunks(ENGINE_SHA.sub('"engineSha":"x"', self.path.read_text()))

    def expected_games(self):
        return game_chunks(ENGINE_SHA.sub('"engineSha":"x"', self.original))

    def corrupt(self, index, old, new):
        """Replace `old` by `new` inside game `index` (1-based chunk) of the copy."""
        chunks = game_chunks(self.path.read_text())
        self.assertEqual(chunks[index].count(old), 1, old)
        chunks[index] = chunks[index].replace(old, new)
        self.path.write_text(''.join(chunks))

    def history_sha(self, index):
        return re.search(r'"historySha":"([0-9a-f]+)"', self.chunks[index]).group(1)

    def test_no_change_leaves_the_file_byte_identical(self):
        result = self.rerecord()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(f'0 of {len(self.ids)} games re-recorded', result.stdout)
        self.assertEqual(self.path.read_text(), self.original)

    def test_restores_only_the_corrupted_games(self):
        # Game 1: wrong history hash. Game 3: one action changed, so its replay diverges.
        self.corrupt(1, self.history_sha(1), '0' * 64)
        first_action = re.search(r'"actions":\[(\d+)', self.chunks[3]).group(1)
        self.corrupt(3, f'"actions":[{first_action}', f'"actions":[{int(first_action) + 1}')
        broken = self.path.read_text()
        result = self.rerecord()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('2 of %d games re-recorded' % len(self.ids), result.stdout)
        self.assertIn(f'changed {self.ids[0]}: winner', result.stdout)
        self.assertIn(f'changed {self.ids[2]}: winner', result.stdout)
        self.assertNotEqual(self.path.read_text(), broken)
        # The same case on the same pool gives the same game back; every other game is untouched.
        self.assertEqual(self.games(), self.expected_games())
        # The engine hash is updated because a record changed.
        self.assertRegex(self.path.read_text(), r'"engineSha":"[0-9a-f]{64}"')

    def test_only_limits_the_games_looked_at(self):
        self.corrupt(1, self.history_sha(1), '0' * 64)
        self.corrupt(2, self.history_sha(2), '0' * 64)
        result = self.rerecord('--only', self.ids[1])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('1 of 1 games re-recorded', result.stdout)
        games = self.games()
        self.assertEqual(games[2], self.expected_games()[2])
        self.assertIn('0' * 64, games[1])  # game 1 was out of scope and stays broken

    def test_dry_run_writes_nothing(self):
        self.corrupt(1, self.history_sha(1), '0' * 64)
        broken = self.path.read_text()
        result = self.rerecord('--dry-run')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('1 of %d games re-recorded' % len(self.ids), result.stdout)
        self.assertIn('dry run', result.stdout)
        self.assertEqual(self.path.read_text(), broken)

    def test_usage_errors_exit_2(self):
        for extra in (['--only'], ['--bogus'], ['--only', 'no-such-game']):
            result = self.rerecord(*extra)
            self.assertEqual(result.returncode, 2, (extra, result.stderr))
        self.assertEqual(self.path.read_text(), self.original)

    def test_a_game_that_cannot_be_rebuilt_fails_and_writes_nothing(self):
        self.corrupt(1, '"player":"core-2026-1"', '"player":"no-such-deck"')
        # The id contains the deck name, so the edit above only changes the player field.
        broken = self.path.read_text()
        result = self.rerecord()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn('cannot re-record', result.stderr)
        self.assertEqual(self.path.read_text(), broken)


if __name__ == '__main__':
    unittest.main()
