"""`tests/run.py --client DIR` copies the client's libraries into references/game. It must never lose a copy that is
already there: not when DIR is references/game itself, a link to it, or a folder whose files link into it.

Run through tests/run.py (suite run-tool). Every test works in a temporary folder; the real references/game is never
touched.
"""
import importlib.util
import os
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FILES = ('Assembly-CSharp.dll', 'UnityEngine.dll')


def load_run():
    spec = importlib.util.spec_from_file_location('solostone_run_tool', ROOT / 'tests' / 'run.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class CollectClient(unittest.TestCase):
    def setUp(self):
        self.run = load_run()
        self.temp = tempfile.TemporaryDirectory()
        base = Path(self.temp.name)
        self.references = base / 'references' / 'game'
        self.client = base / 'Hearthstone' / 'Managed'
        self.client.mkdir(parents=True)
        for name in FILES:
            (self.client / name).write_bytes(b'client ' + name.encode())
        self.run.GAME_REFERENCES = self.references
        self.run.CLIENT_FILES = FILES

    def tearDown(self):
        self.temp.cleanup()

    def contents(self):
        return {name: (self.references / name).read_bytes() for name in FILES if (self.references / name).is_file()}

    def collect_once(self):
        self.run.collect_client(str(self.client))
        self.assertEqual(self.contents(), {name: b'client ' + name.encode() for name in FILES})

    def test_copies_the_client_libraries(self):
        self.collect_once()

    def test_a_second_collect_replaces_the_copies(self):
        self.collect_once()
        (self.client / 'UnityEngine.dll').write_bytes(b'newer')
        self.run.collect_client(str(self.client))
        self.assertEqual((self.references / 'UnityEngine.dll').read_bytes(), b'newer')
        self.assertEqual(sorted(path.name for path in self.references.iterdir()), sorted(FILES))

    def link(self, target, link, folder=False):
        try:
            os.symlink(target, link, target_is_directory=folder)
        except OSError as error:  # Windows without the symlink privilege
            self.skipTest('cannot make a symbolic link here: ' + str(error))

    def refused_and_kept(self, given):
        before = self.contents()
        with self.assertRaises(self.run.Usage):
            self.run.collect_client(str(given))
        self.assertEqual(self.contents(), before)
        self.assertEqual(len(before), len(FILES))

    def test_references_game_itself_is_refused_and_nothing_is_lost(self):
        self.collect_once()
        self.refused_and_kept(self.references)

    def test_a_link_to_references_game_is_refused(self):
        self.collect_once()
        link = Path(self.temp.name) / 'link-to-references'
        self.link(self.references, link, folder=True)
        self.refused_and_kept(link)

    def test_a_folder_whose_files_link_into_references_game_is_refused(self):
        self.collect_once()
        linked = Path(self.temp.name) / 'linked-files'
        linked.mkdir()
        for name in FILES:
            self.link(self.references / name, linked / name)
        self.refused_and_kept(linked)

    def test_a_folder_of_hard_links_to_references_game_is_refused(self):
        self.collect_once()
        linked = Path(self.temp.name) / 'hard-linked-files'
        linked.mkdir()
        for name in FILES:
            os.link(self.references / name, linked / name)
        self.refused_and_kept(linked)

    def test_references_game_that_only_links_to_the_client_is_replaced_by_copies(self):
        self.references.mkdir(parents=True)
        for name in FILES:
            self.link(self.client / name, self.references / name)
        self.collect_once()
        self.assertFalse(any((self.references / name).is_symlink() for name in FILES))
        self.assertTrue(all((self.client / name).is_file() for name in FILES))

    def test_a_read_only_copy_refused_once_like_windows_is_made_writable_and_replaced(self):
        self.collect_once()
        (self.client / 'UnityEngine.dll').write_bytes(b'newer')
        replace = Path.replace
        refused = []

        def windows_like(path, target):
            if Path(target).name == 'UnityEngine.dll' and not refused:
                refused.append(target)
                raise PermissionError('read-only')
            return replace(path, target)

        Path.replace = windows_like
        try:
            self.run.collect_client(str(self.client))
        finally:
            Path.replace = replace
        self.assertEqual(len(refused), 1)
        self.assertEqual((self.references / 'UnityEngine.dll').read_bytes(), b'newer')
        self.assertFalse((self.references / 'UnityEngine.dll.partial').exists())

    def test_a_failed_copy_keeps_the_earlier_copy(self):
        self.collect_once()
        copy = self.run.shutil.copyfile

        def failing(source, target):
            if Path(source).name == 'UnityEngine.dll':
                raise OSError('disk full')
            return copy(source, target)

        self.run.shutil.copyfile = failing
        try:
            with self.assertRaises(OSError):
                self.run.collect_client(str(self.client))
        finally:
            self.run.shutil.copyfile = copy
        self.assertEqual((self.references / 'UnityEngine.dll').read_bytes(), b'client UnityEngine.dll')


if __name__ == '__main__':
    unittest.main()
