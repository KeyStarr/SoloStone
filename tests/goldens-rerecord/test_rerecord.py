"""`HsSim goldens rerecord`: replays a golden file and re-records only the games that no longer match.

Run through tests/run.py (suite goldens-rerecord), which builds HsSim and sets HSSIM_DLL and DOTNET_PATH.
"""
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DLL = os.environ.get('HSSIM_DLL')
DOTNET = os.environ.get('DOTNET_PATH') or shutil.which('dotnet')


def game_ids(text):
    return re.findall(r'"id":"([^"]+)"', text)


def legacy_form(text, history='a' * 64):
    """The same file in the single-line form written before: an engine hash, and a history hash instead of the decisions
    hash in every game."""
    one_line = text.replace('[\n\n{', '[{').replace('},\n\n{', '},{').replace('}\n\n]}', '}]}')
    one_line = re.sub(r'"decisionsSha":"[0-9a-f]+"', '"historySha":"' + history + '"', one_line)
    return one_line.replace('"games":[', '"engineSha":"' + 'b' * 64 + '","games":[', 1)


def game_chunks(text):
    """The file split into the header and one chunk per game, to compare games one by one.
    A game starts with its replay schema (schema 3) or straight with its id (schema 2)."""
    return re.split(r'(?=\{"(?:replaySchema|id)")', text)


class RerecordTests:
    """The checks, for one golden file (`golden`). The two subclasses below run them on a schema 3 and a schema 2 file."""
    golden = None

    @classmethod
    def setUpClass(cls):
        cls.original = (ROOT / 'tests' / 'goldens' / cls.golden).read_text()
        cls.chunks = game_chunks(cls.original)
        cls.ids = game_ids(cls.original)
        assert len(cls.ids) >= 3
        # Run from a scratch folder holding only the pool snapshots, so the traces rerecord dumps stay out of the repository.
        cls.work = tempfile.TemporaryDirectory()
        shutil.copytree(ROOT / 'tests' / 'goldens' / 'pools', Path(cls.work.name) / 'tests' / 'goldens' / 'pools')

    @classmethod
    def tearDownClass(cls):
        cls.work.cleanup()

    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.path = Path(folder.name) / 'golden.json'
        self.path.write_text(self.original)

    def rerecord(self, *extra):
        # The pool snapshots are found at tests/goldens/pools under the working folder.
        result = subprocess.run([DOTNET, DLL, 'goldens', 'rerecord', str(self.path), *extra], cwd=self.work.name,
                                capture_output=True, text=True, timeout=1200)
        return result

    def games(self):
        return game_chunks(self.path.read_text())

    def expected_games(self):
        return game_chunks(self.original)

    def corrupt(self, index, old, new):
        """Replace `old` by `new` inside game `index` (1-based chunk) of the copy."""
        chunks = game_chunks(self.path.read_text())
        self.assertEqual(chunks[index].count(old), 1, old)
        chunks[index] = chunks[index].replace(old, new)
        self.path.write_text(''.join(chunks))

    def decisions_sha(self, index):
        return re.search(r'"decisionsSha":"([0-9a-f]+)"', self.chunks[index]).group(1)

    def test_no_change_leaves_the_file_byte_identical(self):
        result = self.rerecord()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(f'0 re-recorded (0 shape only, 0 state, 0 behaviour), {len(self.ids)} unchanged, all {len(self.ids)} match', result.stdout)
        self.assertEqual(self.path.read_text(), self.original)

    def test_restores_only_the_corrupted_games(self):
        # Game 1: wrong decisions hash (shape only). Game 3: one action changed, so its replay diverges (behaviour).
        self.corrupt(1, self.decisions_sha(1), '0' * 64)
        first_action = re.search(r'"actions":\[(\d+)', self.chunks[3]).group(1)
        self.corrupt(3, f'"actions":[{first_action}', f'"actions":[{int(first_action) + 1}')
        broken = self.path.read_text()
        result = self.rerecord('--allow-behaviour-change')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('2 re-recorded (1 shape only, 0 state, 1 behaviour), %d unchanged, all %d match' % (len(self.ids) - 2, len(self.ids)), result.stdout)
        self.assertIn(f'changed {self.ids[0]}: shape only (decisions moved), 0 actions changed', result.stdout)
        self.assertIn(f'changed {self.ids[2]}: BEHAVIOUR', result.stdout)
        self.assertIn(f'WARNING {self.ids[2]}', result.stderr)
        self.assertNotEqual(self.path.read_text(), broken)
        # The same case on the same pool gives the same game back; every other game is untouched.
        self.assertEqual(self.games(), self.expected_games())
        # Written one game per line, with no engine or history hash.
        self.assertEqual(self.path.read_text(), self.original)
        self.assertNotIn('engineSha', self.path.read_text())
        self.assertNotIn('historySha', self.path.read_text())

    def test_the_file_is_one_game_per_line(self):
        lines = self.original.split('\n')
        self.assertTrue(lines[0].endswith('"games":['), lines[0][:200])
        self.assertEqual(lines[-2:], [']}', ''])
        games = [line for line in lines[1:-2] if line]
        self.assertEqual(len(games), len(self.ids))
        self.assertTrue(all(line.startswith('{"') for line in games))
        self.assertTrue(all(line == '' for line in lines[1:-2:2]), 'a blank line between games')
        self.assertNotIn('engineSha', self.original)
        self.assertNotIn('historySha', self.original)
        self.assertEqual(self.original.count('"decisionsSha":'), len(self.ids))

    def test_changes_to_neighbouring_games_merge_without_a_conflict(self):
        # Two branches that re-record games 1 and 2 merge cleanly: the blank line between them keeps git's hunks apart.
        folder = Path(self.path.parent)
        base, ours, theirs = folder / 'base.json', folder / 'ours.json', folder / 'theirs.json'
        base.write_text(self.original)
        ours.write_text(self.original.replace(self.decisions_sha(1), '0' * 64, 1))
        theirs.write_text(self.original.replace(self.decisions_sha(2), '1' * 64, 1))
        merged = subprocess.run(['git', 'merge-file', '-p', str(ours), str(base), str(theirs)], capture_output=True, text=True)
        self.assertEqual(merged.returncode, 0, merged.stdout[-500:])
        self.assertIn('0' * 64, merged.stdout)
        self.assertIn('1' * 64, merged.stdout)

    def test_a_file_in_the_earlier_single_line_form_is_converted(self):
        self.path.write_text(legacy_form(self.original))
        dry = self.rerecord('--dry-run')
        self.assertEqual(dry.returncode, 0, dry.stderr)
        self.assertIn('converted to one game per line (dry run, not written)', dry.stdout)
        self.assertEqual(self.path.read_text(), legacy_form(self.original))
        result = self.rerecord()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('converted to one game per line', result.stdout)
        self.assertIn(f'0 re-recorded (0 shape only, 0 state, 0 behaviour), {len(self.ids)} unchanged', result.stdout)
        self.assertEqual(self.path.read_text(), self.original)

    def test_a_stale_history_hash_in_an_earlier_form_file_is_dropped_not_compared(self):
        # A branch made before this change carries history hashes from its own engine; they are not compared any more.
        self.path.write_text(legacy_form(self.original, history='0' * 64))
        result = self.rerecord()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('converted to one game per line', result.stdout)
        self.assertIn(f'0 re-recorded (0 shape only, 0 state, 0 behaviour), {len(self.ids)} unchanged', result.stdout)
        self.assertEqual(self.path.read_text(), self.original)

    def test_an_earlier_form_file_with_a_state_change_is_refused_without_the_flag(self):
        self.path.write_text(legacy_form(self.original.replace(self.state_sha(1), '1' * 64, 1)))
        broken = self.path.read_text()
        result = self.rerecord()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn(f'changed {self.ids[0]}: STATE', result.stdout)
        self.assertEqual(self.path.read_text(), broken)

    def test_an_earlier_form_file_cannot_be_converted_in_part(self):
        self.path.write_text(legacy_form(self.original))
        result = self.rerecord('--only', self.ids[0])
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn('convert the whole file first', result.stderr)

    def test_check_refuses_an_earlier_form_file(self):
        self.path.write_text(legacy_form(self.original))
        result = subprocess.run([DOTNET, DLL, 'goldens', 'check', str(self.path)], cwd=self.work.name, capture_output=True, text=True, timeout=1200)
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn('single-line form written before', result.stderr)

    def test_only_limits_the_games_looked_at(self):
        self.corrupt(1, self.decisions_sha(1), '0' * 64)
        self.corrupt(2, self.decisions_sha(2), '0' * 64)
        result = self.rerecord('--only', self.ids[1])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('1 re-recorded (1 shape only, 0 state, 0 behaviour), 0 unchanged, all 1 in scope match', result.stdout)
        games = self.games()
        self.assertEqual(games[2], self.expected_games()[2])
        self.assertIn('0' * 64, games[1])  # game 1 was out of scope and stays broken

    def test_dry_run_writes_nothing(self):
        self.corrupt(1, self.decisions_sha(1), '0' * 64)
        broken = self.path.read_text()
        result = self.rerecord('--dry-run')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('1 re-recorded (1 shape only, 0 state, 0 behaviour), %d unchanged' % (len(self.ids) - 1), result.stdout)
        self.assertIn('shape only', result.stdout)
        self.assertIn('dry run', result.stdout)
        self.assertEqual(self.path.read_text(), broken)

    def test_usage_errors_exit_2(self):
        for extra in (['--only'], ['--bogus'], ['--allow-behaviour'], ['--only', 'no-such-game']):
            result = self.rerecord(*extra)
            self.assertEqual(result.returncode, 2, (extra, result.stderr))
        self.assertEqual(self.path.read_text(), self.original)

    def state_sha(self, index):
        return re.search(r'"stateSha":"([0-9a-f]+)"', self.chunks[index]).group(1)

    def test_a_history_only_change_needs_no_flag(self):
        self.corrupt(1, self.decisions_sha(1), '0' * 64)
        self.corrupt(2, self.decisions_sha(2), '1' * 64)
        result = self.rerecord()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.count('shape only (decisions moved)'), 2, result.stdout)
        self.assertNotIn('STATE', result.stdout)
        self.assertNotIn('BEHAVIOUR', result.stdout)
        self.assertIn('2 re-recorded (2 shape only, 0 state, 0 behaviour)', result.stdout)
        self.assertEqual(self.games(), self.expected_games())

    def state_changed(self):
        """Game 1 differs only in its final state hash: what a stats/tags change that offers no new option looks like."""
        self.corrupt(1, self.state_sha(1), '1' * 64)
        return self.path.read_text()

    def test_a_state_only_change_is_refused_without_the_flag(self):
        broken = self.state_changed()
        result = self.rerecord()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn('--allow-state-change', result.stderr)
        self.assertIn(f'changed {self.ids[0]}: STATE', result.stdout)
        self.assertNotIn('shape only (', result.stdout)
        self.assertIn('nothing written', result.stdout)
        self.assertEqual(self.path.read_text(), broken)

    def test_a_state_only_change_is_still_refused_in_a_dry_run_without_the_flag(self):
        broken = self.state_changed()
        result = self.rerecord('--dry-run')
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn(f'changed {self.ids[0]}: STATE', result.stdout)
        self.assertEqual(self.path.read_text(), broken)

    def test_a_state_only_change_is_accepted_with_allow_state_change(self):
        broken = self.state_changed()
        dry = self.rerecord('--dry-run', '--allow-state-change')
        self.assertEqual(dry.returncode, 0, dry.stderr)
        self.assertIn(f'changed {self.ids[0]}: STATE', dry.stdout)
        self.assertEqual(self.path.read_text(), broken)
        result = self.rerecord('--allow-state-change')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(f'WARNING {self.ids[0]}: STATE', result.stderr)
        self.assertIn('1 re-recorded (0 shape only, 1 state, 0 behaviour)', result.stdout)
        self.assertEqual(self.games(), self.expected_games())

    def test_a_state_only_change_is_accepted_with_allow_behaviour_change(self):
        self.state_changed()
        result = self.rerecord('--allow-behaviour-change')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(f'WARNING {self.ids[0]}: STATE', result.stderr)
        self.assertIn('1 re-recorded (0 shape only, 1 state, 0 behaviour)', result.stdout)
        self.assertEqual(self.games(), self.expected_games())

    def test_allow_state_change_does_not_allow_behaviour_changes(self):
        first = re.search(r'"actions":\[(\d+)', self.chunks[1]).group(1)
        self.corrupt(1, f'"actions":[{first}', f'"actions":[{int(first) + 1}')
        broken = self.path.read_text()
        result = self.rerecord('--allow-state-change')
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn('--allow-behaviour-change', result.stderr)
        self.assertEqual(self.path.read_text(), broken)

    def behaviour_changed(self, old, new, shown):
        """A recorded game that differs in play (a deck or card edit, or a rules change, would look like this)."""
        self.corrupt(1, old, new)
        broken = self.path.read_text()
        refused = self.rerecord()
        self.assertEqual(refused.returncode, 1, refused.stderr)
        self.assertIn('--allow-behaviour-change', refused.stderr)
        self.assertIn(f'changed {self.ids[0]}: BEHAVIOUR', refused.stdout)
        self.assertIn(shown, refused.stdout)
        self.assertEqual(self.path.read_text(), broken)
        dry = self.rerecord('--dry-run', '--allow-behaviour-change')
        self.assertEqual(dry.returncode, 0, dry.stderr)
        self.assertEqual(self.path.read_text(), broken)
        allowed = self.rerecord('--allow-behaviour-change')
        self.assertEqual(allowed.returncode, 0, allowed.stderr)
        self.assertIn(f'WARNING {self.ids[0]}: BEHAVIOUR', allowed.stderr)
        self.assertEqual(self.games(), self.expected_games())

    def test_changed_actions_are_refused_without_the_flag(self):
        first = re.search(r'"actions":\[(\d+)', self.chunks[1]).group(1)
        self.behaviour_changed(f'"actions":[{first}', f'"actions":[{int(first) + 1}', '1 changed, first at step 0')

    def test_changed_legal_moves_are_refused_without_the_flag(self):
        sha = re.search(r'"legalShas":\["([0-9a-f]+)"', self.chunks[1]).group(1)
        self.behaviour_changed(f'"legalShas":["{sha}"', '"legalShas":["' + '2' * 64 + '"', 'legal moves differ from step 0')

    def test_a_mix_of_fixable_and_unbuildable_games_writes_nothing(self):
        self.corrupt(1, self.decisions_sha(1), '0' * 64)  # fixable
        player = re.search(r'"player":"([^"]+)"', self.chunks[2]).group(1)
        self.corrupt(2, f'"player":"{player}"', '"player":"no-such-deck"')  # not buildable
        broken = self.path.read_text()
        result = self.rerecord()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn('nothing written', result.stdout)
        self.assertEqual(self.path.read_text(), broken)

    def test_a_file_not_in_record_form_is_not_rewritten(self):
        import json
        self.corrupt(1, self.decisions_sha(1), '0' * 64)
        self.path.write_text(json.dumps(json.loads(self.path.read_text()), indent=1))
        broken = self.path.read_text()
        result = self.rerecord()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn('not in the form', result.stderr)
        self.assertEqual(self.path.read_text(), broken)

    def test_a_game_that_cannot_be_rebuilt_fails_and_writes_nothing(self):
        player = re.search(r'"player":"([^"]+)"', self.chunks[1]).group(1)
        self.corrupt(1, f'"player":"{player}"', '"player":"no-such-deck"')
        # The id contains the deck name, so the edit above only changes the player field.
        broken = self.path.read_text()
        result = self.rerecord()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn('cannot re-record', result.stderr)
        self.assertEqual(self.path.read_text(), broken)

    def test_a_rebuilt_case_with_another_identity_fails_and_writes_nothing(self):
        # The seed is part of the id, so a record whose seed was edited no longer matches the case rebuilt from it.
        seed = re.search(r'"seed":(\d+)', self.chunks[1]).group(1)
        self.corrupt(1, f'"seed":{seed},', f'"seed":{int(seed) + 100},')
        broken = self.path.read_text()
        result = self.rerecord()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn('differs from the record', result.stderr)
        self.assertEqual(self.path.read_text(), broken)


@unittest.skipUnless(DLL and DOTNET, 'run through tests/run.py --suite goldens-rerecord')
class Schema3Tests(RerecordTests, unittest.TestCase):
    golden = 'core-2026.json'


@unittest.skipUnless(DLL and DOTNET, 'run through tests/run.py --suite goldens-rerecord')
class Schema2Tests(RerecordTests, unittest.TestCase):
    golden = 'core-reprints.json'


if __name__ == '__main__':
    unittest.main()
