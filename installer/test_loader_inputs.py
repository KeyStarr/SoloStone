"""Checks for loader_inputs.py: unittest, no network. Run from hearthstone-native-ui-prototype/:
    /usr/bin/python3 -I installer/test_loader_inputs.py
"""
import hashlib
import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import loader_inputs as li  # noqa: E402


def digest(data):
    return hashlib.sha256(data).hexdigest()


class FakeList(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self.tmp.name)
        self.archive = self.dir / 'a.zip'
        with zipfile.ZipFile(self.archive, 'w') as z:
            z.writestr('x64/one.dll', b'one')
            z.writestr('two.dll', b'two')
        self.data = {'schema': 1, 'sources': {'s': {'url': self.archive.as_uri(), 'archiveSha256': digest(self.archive.read_bytes()),
                     'components': [{'name': 'N', 'version': '1', 'licence': 'MIT', 'licenceUrl': 'u'}]}},
                     'files': [{'path': 'one.dll', 'source': 's', 'member': 'x64/one.dll', 'sha256': digest(b'one')},
                               {'path': 'BepInEx/two.dll', 'source': 's', 'member': 'two.dll', 'sha256': digest(b'two')}]}
        self.list = self.dir / 'list.json'
        self.list.write_text(json.dumps(self.data))

    def tearDown(self):
        self.tmp.cleanup()

    def test_fetch_and_verify(self):
        out = self.dir / 'out'
        li.fetch(out, self.dir / 'cache', li.load(self.list))
        self.assertEqual(li.verify(out, li.load(self.list)), 2)
        self.assertEqual((out / 'BepInEx' / 'two.dll').read_bytes(), b'two')

    def test_archive_hash_refused(self):
        self.data['sources']['s']['archiveSha256'] = '0' * 64
        with self.assertRaises(li.Refused):
            li.fetch(self.dir / 'out', None, li.load(self._write()))

    def test_file_hash_refused(self):
        self.data['files'][0]['sha256'] = digest(b'other')
        with self.assertRaises(li.Refused):
            li.fetch(self.dir / 'out', None, li.load(self._write()))

    def test_extra_missing_and_changed_files_refused(self):
        out = self.dir / 'out'
        li.fetch(out, None, li.load(self.list))
        data = li.load(self.list)
        (out / 'extra.dll').write_bytes(b'x')
        with self.assertRaisesRegex(li.Refused, 'unlisted'):
            li.verify(out, data)
        (out / 'extra.dll').unlink()
        (out / 'one.dll').write_bytes(b'changed')
        with self.assertRaisesRegex(li.Refused, 'hash differs'):
            li.verify(out, data)
        (out / 'one.dll').unlink()
        with self.assertRaisesRegex(li.Refused, 'missing'):
            li.verify(out, data)

    def test_unmarked_folder_not_replaced(self):
        out = self.dir / 'out'
        out.mkdir()
        with self.assertRaises(li.Refused):
            li.fetch(out, None, li.load(self.list))

    def test_raw_file_source(self):
        raw = self.dir / 'raw.dll'
        raw.write_bytes(b'raw')
        self.data['sources']['r'] = {'raw': True, 'components': self.data['sources']['s']['components']}
        self.data['files'].append({'path': 'BepInEx/raw.dll', 'source': 'r', 'url': raw.as_uri(), 'sha256': digest(b'raw')})
        with self.assertRaisesRegex(li.Refused, 'raw file without an https url'):
            li.load(self._write())
        self.data['files'][-1]['url'] = 'https://example.invalid/raw.dll'
        self.assertEqual(li.load(self._write())['files'][-1]['source'], 'r')

    def test_poisoned_cache_entry_ignored(self):
        cache = self.dir / 'cache'
        cache.mkdir()
        (cache / (self.data['sources']['s']['archiveSha256'] + '-a.zip')).write_bytes(b'not the archive')
        out = self.dir / 'out'
        li.fetch(out, cache, li.load(self.list))  # falls through to the real file, which the hash accepts
        self.assertEqual(li.verify(out, li.load(self.list)), 2)

    def test_missing_member_and_bad_archive_refused(self):
        self.data['files'][0]['member'] = 'nope.dll'
        with self.assertRaisesRegex(li.Refused, 'no member'):
            li.fetch(self.dir / 'out', None, li.load(self._write()))
        bad = self.dir / 'bad.zip'
        bad.write_bytes(b'plain')
        self.data['sources']['s'].update(url=bad.as_uri(), archiveSha256=digest(b'plain'))
        with self.assertRaisesRegex(li.Refused, 'not a zip'):
            li.fetch(self.dir / 'out2', None, li.load(self._write()))

    def test_symlink_in_loader_folder_refused(self):
        out = self.dir / 'out'
        li.fetch(out, None, li.load(self.list))
        (out / 'link.dll').symlink_to(out / 'one.dll')
        with self.assertRaisesRegex(li.Refused, 'link'):
            li.verify(out, li.load(self.list))

    def test_bad_hash_and_case_clash_refused(self):
        self.data['files'][0]['sha256'] = '+' + 'a' * 63
        with self.assertRaises(li.Refused):
            li.load(self._write())
        self.data['files'][0]['sha256'] = digest(b'one')
        self.data['files'][1]['path'] = 'ONE.dll'
        with self.assertRaisesRegex(li.Refused, 'case'):
            li.load(self._write())

    def test_path_escape_refused(self):
        self.data['files'][0]['path'] = '../escape.dll'
        with self.assertRaises(li.Refused):
            li.load(self._write())

    def _write(self):
        self.list.write_text(json.dumps(self.data))
        return self.list


class NoticeCheck(unittest.TestCase):
    def test_missing_entry_and_wrong_licence_refused(self):
        data = li.load()
        li.check_notice(data)
        data['sources']['bepinex']['components'].append({'name': 'Made Up', 'version': '1', 'licence': 'MIT', 'licenceUrl': 'https://x'})
        with self.assertRaisesRegex(li.Refused, 'Made Up'):
            li.check_notice(data)
        data = li.load()
        data['sources']['doorstop']['components'][0]['licence'] = 'Zlib'
        with self.assertRaisesRegex(li.Refused, 'Zlib'):
            li.check_notice(data)


class RecordedList(unittest.TestCase):
    def test_list_is_complete_and_noticed(self):
        data = li.load()
        paths = [e['path'] for e in data['files']]
        self.assertIn('winhttp.dll', paths)
        self.assertIn('BepInEx/unstripped_corlib/mscorlib.dll', paths)
        self.assertIn('BepInEx/core/BepInEx.Preloader.dll', paths)
        li.check_notice(data)


if __name__ == '__main__':
    unittest.main()
