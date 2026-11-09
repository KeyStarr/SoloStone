#!/usr/bin/env python3
"""Checks for package_linux.py's font rename: python3 -I installer/test_package_linux.py"""
import struct
import sys
import unittest
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import package_linux as pl  # noqa: E402


def checksum(block):
    block += b'\0' * (-len(block) % 4)
    return sum(struct.unpack('>%dI' % (len(block) // 4), block)) & 0xFFFFFFFF


def font(tables):
    """A TrueType file from {tag: bytes}, tables in tag order, checksums left zero: rename_font recomputes them."""
    count = len(tables)
    data = struct.pack('>IHHHH', 0x00010000, count, 0, 0, 0)
    offset = 12 + 16 * count
    body = b''
    for tag in sorted(tables):
        data += struct.pack('>4sIII', tag, 0, offset + len(body), len(tables[tag]))
        body += tables[tag] + b'\0' * (-len(tables[tag]) % 4)
    return data + body


def name_table(records):
    header = struct.pack('>HHH', 0, len(records), 6 + 12 * len(records))
    strings = b''
    for platform, encoding, language, name_id, text in records:
        header += struct.pack('>HHHHHH', platform, encoding, language, name_id, len(text), len(strings))
        strings += text
    return header + strings


def tables_of(data):
    count = struct.unpack_from('>H', data, 4)[0]
    found = {}
    for index in range(count):
        tag, check, offset, length = struct.unpack_from('>4sIII', data, 12 + 16 * index)
        found[tag] = (check, data[offset:offset + length])
    return found


def names_of(table):
    _, count, storage = struct.unpack_from('>HHH', table, 0)
    found = {}
    for index in range(count):
        platform, _, _, name_id, length, offset = struct.unpack_from('>HHHHHH', table, 6 + 12 * index)
        text = table[storage + offset:storage + offset + length]
        found[(platform, name_id)] = text.decode('utf-16-be' if platform == 3 else 'mac_roman')
    return found


class RenameFont(unittest.TestCase):
    def setUp(self):
        records = []
        for platform, encoding, language, codec in ((1, 0, 0, 'mac_roman'), (3, 1, 1033, 'utf-16-be')):
            for name_id, text in ((0, 'Copyright Example'), (1, 'Original Sans'), (2, 'Regular'), (4, 'Original Sans'), (6, 'OriginalSans'), (7, 'Original is a trademark')):
                records.append((platform, encoding, language, name_id, text.encode(codec)))
        self.glyphs = bytes(range(37))
        self.source = font({b'head': bytes(54), b'name': name_table(records), b'glyf': self.glyphs})
        self.renamed = pl.rename_font(self.source, {1: 'New', 4: 'New', 6: 'New-Regular'}, {7})

    def test_names_replaced_dropped_and_kept(self):
        names = names_of(tables_of(self.renamed)[b'name'][1])
        for platform in (1, 3):
            self.assertEqual(names[(platform, 1)], 'New')
            self.assertEqual(names[(platform, 4)], 'New')
            self.assertEqual(names[(platform, 6)], 'New-Regular')
            self.assertEqual(names[(platform, 0)], 'Copyright Example')
            self.assertEqual(names[(platform, 2)], 'Regular')
            self.assertNotIn((platform, 7), names)

    def test_other_tables_keep_their_bytes(self):
        self.assertEqual(tables_of(self.renamed)[b'glyf'][1], self.glyphs)

    def test_checksums(self):
        for tag, (check, block) in tables_of(self.renamed).items():
            if tag == b'head':
                block = block[:8] + b'\0\0\0\0' + block[12:]
            self.assertEqual(check, checksum(block), tag)
        self.assertEqual(checksum(self.renamed), 0xB1B0AFBA)

    def test_refuses_what_it_cannot_read(self):
        with self.assertRaises(SystemExit):
            pl.rename_font(b'OTTO' + self.source[4:], {}, set())

    def test_shipped_names_avoid_the_reserved_ones(self):
        for text in pl.FONT_NAMES.values():
            for reserved in ('Liberation', 'Arimo', 'Tinos', 'Cousine'):
                self.assertNotIn(reserved.lower(), text.lower())
        self.assertEqual(pl.FONT_NAMES[1], 'LegacyRuntime')


if __name__ == '__main__':
    unittest.main()
