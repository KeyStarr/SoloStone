"""The card drift file (facts/drift/<meta>.json) is well formed and agrees with the card file's overlay header.

It lists base rows that differ from the 37.0 client export (allowlist) or are built from it (clientRows). CI has no client export,
so this checks only what the two files say about each other; the comparison with the export is private tooling
(tools/card_drift.py). Run through tests/run.py (suite facts-drift). Schema: facts/README.md.
"""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
META = 'violet-hold-2026-09'
CLASSES = {'rework', 'script', 'balance-held', 'tags-held', 'text-layout', 'export-quirk', 'absent'}
FIELD = re.compile(r'^(absent|dbf|name|text|tag:\d+|ref:\d+)$')


def field_key(field):
    kind, _, number = field.partition(':')
    return ('absent', 'dbf', 'name', 'text', 'tag', 'ref').index(kind), int(number) if number else 0


class DriftFileTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.facts = json.loads((ROOT / 'facts' / (META + '.json')).read_text(encoding='utf-8'))
        cls.drift = json.loads((ROOT / 'facts' / 'drift' / (META + '.json')).read_text(encoding='utf-8'))
        cls.non_snapshot_overlay_ids = {row['id'] for row in cls.facts['overlay']['cards'] if not row.get('snapshot', False)}
        cls.base_ids = {row['id'] for row in cls.facts['cards']} - cls.non_snapshot_overlay_ids

    def test_header_matches_the_card_file(self):
        overlay = self.facts['overlay']
        self.assertEqual(self.drift['schemaVersion'], 1)
        self.assertEqual(self.drift['meta'], self.facts['meta'])
        self.assertEqual(self.drift['clientBuild'], overlay['sourceBuild'])
        self.assertEqual(self.drift['exportSha256'], overlay['exportSha256'])
        self.assertEqual(set(self.drift), {'schemaVersion', 'meta', 'clientBuild', 'exportSha256', 'clientRows', 'allowlist'})

    def test_client_rows(self):
        rows = self.drift['clientRows']
        self.assertEqual(rows, sorted(set(rows)), 'sorted and unique')
        self.assertFalse(set(rows) - self.base_ids, 'every client row is a base row of the card file')
        self.assertFalse(set(rows) & self.non_snapshot_overlay_ids, 'no client row is a non-snapshot overlay id')

    def test_allowlist(self):
        entries = self.drift['allowlist']
        ids = [entry['id'] for entry in entries]
        self.assertEqual(ids, sorted(set(ids)), 'sorted by id, unique')
        self.assertFalse(set(ids) - self.base_ids, 'every allowlisted id is a base row of the card file')
        self.assertFalse(set(ids) & self.non_snapshot_overlay_ids, 'no allowlisted id is a non-snapshot overlay id')
        self.assertFalse(set(ids) & set(self.drift['clientRows']), 'no id is both a client row and allowlisted')
        for entry in entries:
            self.assertEqual(set(entry), {'id', 'class', 'fields', 'reason'}, entry['id'])
            self.assertIn(entry['class'], CLASSES, entry['id'])
            self.assertTrue(isinstance(entry['reason'], str) and entry['reason'].strip(), entry['id'] + ': reason')
            fields = entry['fields']
            self.assertTrue(fields and all(isinstance(f, str) and FIELD.match(f) for f in fields), entry['id'] + ': fields')
            self.assertEqual(fields, sorted(set(fields), key=field_key), entry['id'] + ': fields sorted and unique')
            self.assertEqual('absent' in fields, fields == ['absent'], entry['id'] + ': absent stands alone')
            self.assertEqual(entry['class'] == 'absent', fields == ['absent'], entry['id'] + ': class absent iff field absent')


if __name__ == '__main__':
    unittest.main()
