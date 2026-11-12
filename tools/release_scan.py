#!/usr/bin/env python3
"""Release and contribution scanner. CI runs it on every pull request (.github/workflows/scan.yml), the maintainer's local
commit and push hooks run it with the private denylist, and the maintainer runs it on each release build. Publish nothing if it reports.

    release_scan.py --source TREE [--binaries BUILD_DIR] [--client-reference CLIENT_DIR]

Exits 0 when clean, 1 on any hit, 2 on a usage or config error. Hits print as `path:LOCATION rule`, never the matched
private text. LOCATION is a line number in text, `@0xOFFSET` in a binary, 0 for a whole file.

Source tree (the repository, or a clean copy of it) fails on Blizzard files (CardDefs.xml and ClientOverlay.xml by name and hash,
game DLLs, asset bundles), media outside the allowlist, decompiled code and IL, encoded blobs, oversized or unreadable
files, every privacy_scan.py rule, lab paths, e-mail addresses outside the allowlist, and banned phrases (free cards,
private server, donations, payment links) in player-facing text. Card IDs, names, text and per-meta files pass.

Binaries (DLLs, installer payload, zips) fail on the same file rules plus private info and lab paths in their strings,
test-hook traces (installer/stage.py markers), and embedded managed resources named or hashed like a Blizzard file.
Nothing is executed: strings, .NET metadata and zip members are only read.

Anything private (lab hosts, owner identity, word lists, extra hashes, the allowed e-mails) lives in the private lab
config (HS_LAB_CONFIG, default ~/.config/solostone/lab.json): `privacy_denylist` as for privacy_scan.py, and an optional
`release_scan` object with `blizzard_sha256`, `blizzard_names`, `lab_path_patterns`, `binary_markers`, `phrases`
([{"rule", "regex", "weak"}]), `allowed_emails`, `media_allow`, `large_allow` and `waivers` ([{"path", "rule"}] globs; an optional "text" also needs
that text on the hit's own source line and no other hit of the rule there; `scanner.waiver_hits[i]` is the set of hits
waiver i absorbed).

Public rules (--public-rules, tools/release_scan.public.json) hold what is not private: the one public identity
(`allowed_emails`), the reviewed waivers and size allowances of the repository, the scanner's own planted phrases and
`forbidden_paths` (globs that may never be tracked). They merge with the private config, so CI can run them alone with
--allow-no-private-config and a local run adds the denylist.

Git modes, for commit and push hooks: `--staged` scans the blobs in the index, `--commits RANGE`
scans the messages, identities and blobs of the commits `git rev-list RANGE` lists (blobs as each commit introduced
them, so a later commit cannot hide an earlier hit), `--message FILE` scans one commit message. `--require-identity
'Name <email>'` makes any other author or committer a hit; without it only private literals in identities are hits. The git
modes are strict: public waivers that name no rule, or one of the privacy, path, Blizzard, lab-path, IL, decompiler, game-dll,
asset-bundle, forbidden-path or git rules, are ignored; content-hiding rules (encoded-blob, byte-array, opaque-file, unscannable,
archive, binary-in-source, payment-link) accept only a public waiver on one exact path with a `count`; public allowed_emails,
large_allow, media_allow and card_data are ignored; a waiver with no count covers one hit. `--strict` gives --source the same rules (CI). `--trust-public-rules` (CI, with the rules taken from the base branch) re-enables
only the public allowed_emails, large_allow, media_allow and card_data; every other strict rule stays. A waiver's `count` N
always caps it at N hits per file content (per blob, so a line that moves between commits is not a second hit; for a waiver
with a glob path the count is per matching file) and must be a positive integer. A content-hiding public waiver must carry
`sha256` (the blob) or `line_sha256` (the source line of the hit, exact text without its line break): it stops covering the
file when that content changes. A hit reported for a wrapped multi-line encoding stands for the whole run, so a `line_sha256`
never covers it: such a waiver needs the `sha256` of the blob.
"""
import argparse
import bz2
import fnmatch
import gzip
import hashlib
import io
import lzma
import json
import os
from pathlib import Path
import re
import shlex
import struct
import subprocess
import sys
import tarfile
import zipfile
import zlib

sys.dont_write_bytecode = True
TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import privacy_scan  # noqa: E402

PRODUCT = TOOLS.parent
MAX_BYTES = 2 * 1024 * 1024
MAX_READ = 512 * 1024 * 1024
MAX_ZIP_DEPTH = 3
MAX_ZIP_MEMBERS = 50000
MAX_INFLATED = 2 * 1024 ** 3  # all unpacked bytes of one run
MAX_RATIO = 200
MIN_HASHED = 64  # an empty file is not 'identical to a client file'
BLIZZARD_NAMES = {'carddefs.xml', 'clientoverlay.xml'}
GAME_FILE = re.compile(r'^(?:assembly-csharp[\w.-]*|unityengine[\w.-]*|unityplayer|blizzard[\w.-]*|hearthstone[\w.-]*|'
                       r'bnetlib[\w.-]*|battle\.net[\w.-]*|unity\.[\w.-]+|pegasus[\w.-]*)\.(?:dll|exe|so|dylib|pdb|mdb)$', re.I)
CLIENT_PATH = re.compile(r'(?:^|[/!#\\])[^/!#\\]*_Data[/\\](?:Managed|Plugins|StreamingAssets)[/\\]', re.I)
BUNDLE_EXT = {'.unity3d', '.assetbundle', '.assets', '.ress', '.blp', '.mpq', '.casc'}
BUNDLE_NAME = re.compile(r'^(?:globalgamemanagers[\w.]*|sharedassets\d+[\w.]*|level\d+|mainData|\.build\.info|\.product\.db)$', re.I)
MEDIA_EXT = {'.png', '.jpg', '.jpeg', '.gif', '.bmp', '.tga', '.psd', '.webp', '.tif', '.tiff', '.dds', '.ico', '.icns',
             '.svg', '.exr', '.hdr', '.ttf', '.otf', '.woff', '.woff2', '.eot', '.fnt', '.wav', '.mp3', '.ogg', '.flac',
             '.m4a', '.aac', '.wem', '.bnk', '.fsb', '.opus', '.wma', '.aiff', '.mid', '.mp4', '.webm', '.avi', '.mov',
             '.mkv', '.bik', '.flv', '.wmv'}
EXECUTABLE_EXT = {'.dll', '.exe', '.so', '.dylib', '.pdb', '.mdb', '.o', '.a', '.lib', '.msi', '.sys'}
ARCHIVE_EXT = {'.zip', '.gz', '.tgz', '.bz2', '.xz', '.7z', '.rar', '.tar', '.jar', '.nupkg', '.cab', '.pak'}
PROSE_EXT = {'.md', '.txt', '.html', '.htm', '.rst', '.iss', '.cmd', '.bat', '.rtf'}
PROSE_NAMES = {'readme', 'license', 'notice', 'copying', 'authors', 'changelog', 'contributing'}
CODE_LANG = {'.cs': 'cs', '.py': 'py', '.ps1': 'ps1', '.psm1': 'ps1', '.sh': 'sh', '.js': 'js', '.ts': 'js'}
DATA_EXT = {'.json', '.xml', '.csv', '.yml', '.yaml', '.toml'}


def W(*parts):
    """Spell a private-looking literal in pieces, so this file never trips its own rules."""
    return ''.join(parts)


LAB_PATH = re.compile('|'.join((W(r'battle', r'\.?net[\\/]+hearth', r'stone'), W(r'\b[A-Za-z]:[\\/]+battle', r'\.?net\b'),
                                W(r'hs-native-ui-', r'prototype'), W(r'HSOffline', r'Probe'),
                                W(r'Users[\\/]+HSNative', r'UIProbe'))), re.I)
IL_LABEL = re.compile(r'^\s*IL_[0-9A-Fa-f]{4}:\s')
IL_DIRECTIVE = re.compile(r'^\s*\.(?:method\b.*\bcil\s+managed|maxstack\s+\d+|locals\s+init\b|assembly\s+extern\b|'
                          r'custom\s+instance\s+void\b)|\bcil\s+managed\b')
DECOMPILED = re.compile(r'Decompiled\s+(?:with|by)\b|ICSharpCode\.Decompiler|^\s*(?://|/\*|\*|#).*\b(?:ILSpy|dnSpy|dotPeek)\b|'
                        r'//\s*(?:Token|RVA):\s*0x[0-9A-Fa-f]+|\[Token\(Token\s*=\s*"0x|' + W('Il2Cpp', 'Dumper'))
# Wrapped encodings: one line is short, a run of lines is not (base64 -w76, xxd -p, xxd -i, decimal arrays).
B64_LINE = re.compile(r'^(?![0-9A-Fa-f]+$)[A-Za-z0-9+/]{40,}={0,2}$')  # not a plain hex line (hash lists)
HEX_LINE = re.compile(r'^(?:[0-9A-Fa-f]{2}[ :]?){16,}$')
HEXBYTE_LINE = re.compile(r'^[\s{(\[]*(?:0x[0-9A-Fa-f]{1,2}\s*,?\s*)+[\s})\]]*;?$')
ESCAPED_LINE = re.compile(r'^[\s(b]*(?:\\x[0-9A-Fa-f]{2})+\s*\)?$')
XXD_LINE = re.compile(r'^[0-9a-f]{8}:\s(?:[0-9a-f]{2,4}\s?){4,}')
ESCAPED_RUN = re.compile(r'(?:\\x[0-9A-Fa-f]{2}){64,}')
DECBYTE_LINE = re.compile(r'^[\s{(\[]*(?:\d{1,3}\s*,\s*)+\d{0,3}[\s})\]]*;?$')
BASE64_RUN = re.compile(r'[A-Za-z0-9+/]{300,}={0,2}')
HEX_RUN = re.compile(r'[0-9A-Fa-f]{256,}')
DATA_URI = re.compile(r'data:[\w/+.-]+;base64,', re.I)
BYTE_ARRAY = re.compile(r'(?:0x[0-9A-Fa-f]{1,2}\s*,\s*){32,}')
EMAIL = re.compile(r'(?<![\w.%+-])[\w.%+-]+@([A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,})(?![\w-])')
NOT_EMAIL_TLD = {'dll', 'exe', 'so', 'dylib', 'sys', 'cs', 'py', 'js', 'png'}
SAFE_EMAIL_DOMAINS = {'example.com', 'example.org', 'example.net', 'users.noreply.github.com', 'noreply.github.com'}
PAYMENT = re.compile(r'(?:patreon\.com|ko-fi\.com|buymeacoffee\.com|paypal\.(?:me|com)|github\.com/sponsors|opencollective\.com|'
                     r'liberapay\.com|venmo\.com|cash\.app|gofundme\.com|boosty\.to|buy\.stripe\.com|flattr\.com|'
                     r'(?:bitcoin|ethereum|monero|litecoin):[A-Za-z0-9])', re.I)
# (rule, regex, weak). A weak phrase is allowed where it is card text or flavor text; a strong one never is.
PHRASES = [
    ('phrase-free-cards', r'\bfree\s+cards?\b', False),
    ('phrase-private-server', r'\bprivate\s+servers?\b', False),
    ('phrase-donation', r'\bdonat(?:e|es|ed|ing|ion|ions)\b', True),
    ('phrase-all-cards', r'\ball\s+(?:the\s+)?cards\b', True),
    ('phrase-earn-every-card', r'\bearn\s+every\s+card\b', True),
    ('phrase-full-standard-meta', r'\bfull\s+standard\s+meta\b', True),
]
# In code, only `card.Text =` and a JSON-style "Text": count as card text. A UI control's `Text =` or `x.Text =` is not.
CARD_TEXT = re.compile(r'\bcard\w*\.Text\s*=|"Text"\s*:', re.I)
CARD_DATA = ['facts/**', 'engine/*/resources/Data/**', 'data/**']
CARDDEFS_ROOT = re.compile(rb'\A(?:\xef\xbb\xbf)?\s*(?:<\?xml[^>]*\?>\s*)?(?:<!--.*?-->\s*)*<CardDefs\b', re.S)


class ConfigError(Exception):
    pass


class ParseError(Exception):
    pass


def glob_match(path, patterns):
    return any(fnmatch.fnmatchcase(path, pattern.replace('**', '*')) for pattern in patterns)


# In the git modes a waiver from the public rules never covers these: a rule file in the checkout must not be able to
# excuse a private literal, a Blizzard file, a forbidden path or a foreign identity.
UNWAIVABLE_PUBLIC = re.compile(r'^(?:privacy-|path-|commit-|binary-|blizzard-|git-|lab-path$|il-listing$|decompiler-banner$|game-dll$|'
                               r'asset-bundle$|forbidden-path$|gitlink$|symlink$)')
# These hide content from the scan, so a public waiver for them must be narrow: one exact path (no glob) and a stated count.
NARROW_PUBLIC = re.compile(r'^(?:unscannable$|archive$|encoded-blob$|byte-array$|opaque-file$|binary-in-source$|payment-link$)')
# Keys of the public rules that loosen the scan. In the git modes only the private config may set them (and the rules file
# itself is hash-pinned by the maintainer's local hooks), so a merge cannot widen what the hooks let through.
STRICT_IGNORED_PUBLIC = ('media_allow', 'large_allow', 'card_data', 'allowed_emails')
LIST_KEYS = ('blizzard_sha256', 'blizzard_names', 'lab_path_patterns', 'binary_markers', 'allowed_emails', 'media_allow',
             'large_allow', 'phrases', 'waivers', 'forbidden_paths', 'card_data')


def load_public_rules(path):
    """The public rules file: one JSON object with the keys of the private `release_scan` object. Mandatory once named."""
    try:
        value = json.loads(Path(path).expanduser().read_text(encoding='utf-8-sig'))
    except (OSError, ValueError) as error:
        raise ConfigError(f'Unreadable public rules {path}: {error}')
    if not isinstance(value, dict):
        raise ConfigError('The public rules must be a JSON object')
    for key in LIST_KEYS:
        if not isinstance(value.get(key, []), list):
            raise ConfigError(f'public rules {key} must be a list')
    return value


def load_config(path, allow_missing, public_rules=None, strict=False):
    """Returns (denylist, extras). The private denylist is mandatory unless the caller opts out. `public_rules` is merged
    into the extras: every list is the public entries followed by the private ones."""
    denylist, extras = load_private_config(path, allow_missing)
    if public_rules is not None:
        public = load_public_rules(public_rules)
        public['waivers'] = [dict(w, _public=True) for w in public.get('waivers', []) if isinstance(w, dict)]
        extras = dict(extras)
        for key in LIST_KEYS:
            if strict and key in STRICT_IGNORED_PUBLIC:
                continue
            if key in public or key in extras:
                extras[key] = list(public.get(key, [])) + list(extras.get(key, []))
    return denylist, extras


def load_private_config(path, allow_missing):
    explicit = path is not None
    path = Path(path or os.environ.get('HS_LAB_CONFIG', '~/.config/solostone/lab.json')).expanduser()
    try:
        value = json.loads(path.read_text(encoding='utf-8-sig'))
    except FileNotFoundError:
        if explicit or not allow_missing:
            raise ConfigError(f'No private config at {path}; pass --config or --allow-no-private-config')
        return [], {}
    except (OSError, ValueError) as error:
        raise ConfigError(f'Unreadable private config {path}: {error}')
    if not isinstance(value, dict):
        raise ConfigError('The private config must be a JSON object')
    denylist = value.get('privacy_denylist', [])
    extras = value.get('release_scan', {})
    if not isinstance(denylist, list) or any(not isinstance(v, str) or not v for v in denylist):
        raise ConfigError('privacy_denylist must be a list of nonempty strings')
    if not isinstance(extras, dict):
        raise ConfigError('release_scan must be an object')
    for key in LIST_KEYS:
        if not isinstance(extras.get(key, []), list):
            raise ConfigError(f'release_scan.{key} must be a list')
    if not denylist and not allow_missing:
        raise ConfigError('privacy_denylist is empty: owner identity and lab hosts would go unchecked; '
                          'fill it in or pass --allow-no-private-config')
    return denylist, extras


def stage_markers():
    """The launcher and plugin hook markers installer/stage.py already checks builds against."""
    import importlib.util
    spec = importlib.util.spec_from_file_location('release_scan_stage', PRODUCT / 'installer' / 'stage.py')
    try:
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return list(getattr(module, 'HOOK_MARKERS', [])), list(getattr(module, 'PLUGIN_MARKERS', []))
    except (OSError, ImportError, AttributeError, SyntaxError):
        return [], []


MAX_LOCAL_INPUT_BYTES = 512 * 1024 * 1024
MAX_LOCAL_INPUT_FILES = 2000


def _sha256_file(path):
    digest = hashlib.sha256()
    with open(path, 'rb') as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b''):
            digest.update(chunk)
    return digest.hexdigest()


def private_blizzard_hashes():
    """Hashes of the Blizzard-derived files this checkout still carries, so renamed copies are caught too: files with a
    Blizzard name that are tracked, untracked or ignored (a symlink into private/local-inputs counts, and is resolved),
    plus every file under private/local-inputs/ when that folder exists."""
    found, candidates = set(), []
    for flags in ([], ['-o', '--exclude-standard'], ['-o', '-i', '--exclude-standard']):
        try:
            names = subprocess.check_output(['git', '-C', str(PRODUCT), 'ls-files', '-z'] + flags, text=True,
                                            stderr=subprocess.DEVNULL, errors='surrogateescape')
        except (OSError, subprocess.SubprocessError):
            continue
        candidates += [PRODUCT / name for name in names.split('\0') if name and Path(name).name.casefold() in BLIZZARD_NAMES]
    inputs = PRODUCT / 'private' / 'local-inputs'
    if inputs.is_dir():
        candidates += [p for p in sorted(inputs.rglob('*'))[:MAX_LOCAL_INPUT_FILES * 4]]
    taken = 0
    for path in dict.fromkeys(candidates):
        try:
            real = path.resolve()
            if not real.is_file() or real.stat().st_size > MAX_LOCAL_INPUT_BYTES:
                continue
            found.add(_sha256_file(real))
        except OSError:
            continue
        taken += 1
        if taken >= MAX_LOCAL_INPUT_FILES:
            break
    return found


# ---- managed resources: read the .NET metadata tables without loading the assembly ----

def parse_managed_resources(data):
    """Yield (name, bytes) for each resource embedded in a .NET assembly; nothing if `data` is not one."""
    if data[:2] != b'MZ':
        return
    try:
        yield from _managed_resources(data)
    except (struct.error, IndexError, ValueError, KeyError, OverflowError, UnicodeDecodeError) as error:
        raise ParseError(str(error))


_CODED = {  # name: (tag bits, target tables); None = a tag with no table
    'TypeDefOrRef': (2, (0x02, 0x01, 0x1B)),
    'HasConstant': (2, (0x04, 0x08, 0x17)),
    'HasCustomAttribute': (5, (0x06, 0x04, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x00, 0x0E, 0x17, 0x14, 0x11, 0x1A, 0x1B, 0x20,
                               0x23, 0x26, 0x27, 0x28, 0x2A, 0x2C, 0x2B)),
    'HasFieldMarshal': (1, (0x04, 0x08)),
    'HasDeclSecurity': (2, (0x02, 0x06, 0x20)),
    'MemberRefParent': (3, (0x02, 0x01, 0x1A, 0x06, 0x1B)),
    'HasSemantics': (1, (0x14, 0x17)),
    'MethodDefOrRef': (1, (0x06, 0x0A)),
    'MemberForwarded': (1, (0x04, 0x06)),
    'Implementation': (2, (0x26, 0x23, 0x27)),
    'CustomAttributeType': (3, (None, None, 0x06, 0x0A, None)),
    'ResolutionScope': (2, (0x00, 0x1A, 0x23, 0x01)),
    'TypeOrMethodDef': (1, (0x02, 0x06)),
}
# Column kinds: 1/2/4 fixed bytes, S/G/B heap index, ('T', table) table index, ('C', name) coded index.
_TABLES = {
    0x00: (2, 'S', 'G', 'G', 'G'), 0x01: (('C', 'ResolutionScope'), 'S', 'S'),
    0x02: (4, 'S', 'S', ('C', 'TypeDefOrRef'), ('T', 0x04), ('T', 0x06)), 0x03: (('T', 0x04),),
    0x04: (2, 'S', 'B'), 0x05: (('T', 0x06),), 0x06: (4, 2, 2, 'S', 'B', ('T', 0x08)), 0x07: (('T', 0x08),),
    0x08: (2, 2, 'S'), 0x09: (('T', 0x02), ('C', 'TypeDefOrRef')), 0x0A: (('C', 'MemberRefParent'), 'S', 'B'),
    0x0B: (1, 1, ('C', 'HasConstant'), 'B'), 0x0C: (('C', 'HasCustomAttribute'), ('C', 'CustomAttributeType'), 'B'),
    0x0D: (('C', 'HasFieldMarshal'), 'B'), 0x0E: (2, ('C', 'HasDeclSecurity'), 'B'), 0x0F: (2, 4, ('T', 0x02)),
    0x10: (4, ('T', 0x04)), 0x11: ('B',), 0x12: (('T', 0x02), ('T', 0x14)), 0x13: (('T', 0x14),),
    0x14: (2, 'S', ('C', 'TypeDefOrRef')), 0x15: (('T', 0x02), ('T', 0x17)), 0x16: (('T', 0x17),), 0x17: (2, 'S', 'B'),
    0x18: (2, ('T', 0x06), ('C', 'HasSemantics')), 0x19: (('T', 0x02), ('C', 'MethodDefOrRef'), ('C', 'MethodDefOrRef')),
    0x1A: ('S',), 0x1B: ('B',), 0x1C: (2, ('C', 'MemberForwarded'), 'S', ('T', 0x1A)), 0x1D: (4, ('T', 0x04)),
    0x1E: (4, 4), 0x1F: (4,), 0x20: (4, 2, 2, 2, 2, 4, 'B', 'S', 'S'), 0x21: (4,), 0x22: (4, 4, 4),
    0x23: (2, 2, 2, 2, 4, 'B', 'S', 'S', 'B'), 0x24: (4, ('T', 0x23)), 0x25: (4, 4, 4, ('T', 0x23)),
    0x26: (4, 'S', 'B'), 0x27: (4, 4, 'S', 'S', ('C', 'Implementation')), 0x28: (4, 4, 'S', ('C', 'Implementation')),
}


def _managed_resources(data):
    u16 = lambda at: struct.unpack_from('<H', data, at)[0]
    u32 = lambda at: struct.unpack_from('<I', data, at)[0]
    if len(data) < 0x40:
        return
    pe = u32(0x3C)
    if data[pe:pe + 4] != b'PE\0\0':  # not a PE file at all: nothing to read, and no managed resources
        return
    sections_count, optional_size = u16(pe + 6), u16(pe + 20)
    optional = pe + 24
    directories = optional + (96 if u16(optional) == 0x10B else 112)
    clr_rva, clr_size = u32(directories + 14 * 8), u32(directories + 14 * 8 + 4)
    if not clr_rva or not clr_size:
        return
    sections = [struct.unpack_from('<4I', data, optional + optional_size + 40 * i + 8) for i in range(sections_count)]

    def offset(rva):
        for virtual_size, virtual_address, raw_size, raw_pointer in sections:
            if virtual_address <= rva < virtual_address + max(virtual_size, raw_size):
                return raw_pointer + rva - virtual_address
        raise ValueError('RVA outside every section')

    clr = offset(clr_rva)
    metadata = offset(u32(clr + 8))
    resources_rva = u32(clr + 24)
    if data[metadata:metadata + 4] != b'BSJB':
        raise ValueError('no metadata root')
    at = metadata + 16 + u32(metadata + 12)
    streams = {}
    header = at + 4
    for _ in range(u16(at + 2)):
        start, size = struct.unpack_from('<II', data, header)
        end = data.index(b'\0', header + 8)
        streams[data[header + 8:end].decode('ascii')] = (metadata + start, size)
        header += 8 + ((end - header - 8 + 4) & ~3)
    table_start, _ = streams.get('#~') or streams['#-']
    strings_start, strings_size = streams['#Strings']
    heap = data[table_start + 6]
    valid = struct.unpack_from('<Q', data, table_start + 8)[0]
    at = table_start + 24
    rows = {}
    for table in range(64):
        if valid >> table & 1:
            rows[table] = u32(at)
            at += 4

    def heap_size(bit):
        return 4 if heap & bit else 2

    def coded_size(name):
        bits, targets = _CODED[name]
        limit = 1 << (16 - bits)
        return 4 if any(rows.get(t, 0) >= limit for t in targets if t is not None) else 2

    def width(column):
        if isinstance(column, int):
            return column
        if column == 'S':
            return heap_size(1)
        if column == 'G':
            return heap_size(2)
        if column == 'B':
            return heap_size(4)
        return (4 if rows.get(column[1], 0) >= 65536 else 2) if column[0] == 'T' else coded_size(column[1])

    resource_table = 0x28
    for table in sorted(rows):
        if table >= resource_table:
            break
        if table not in _TABLES:
            raise ValueError('unknown metadata table %#x' % table)
        at += rows[table] * sum(width(c) for c in _TABLES[table])
    columns = [width(c) for c in _TABLES[resource_table]]
    resources = offset(resources_rva) if resources_rva else None
    for _ in range(rows.get(resource_table, 0)):
        data_offset = int.from_bytes(data[at:at + 4], 'little')
        name_at = int.from_bytes(data[at + 8:at + 8 + columns[2]], 'little')
        implementation = int.from_bytes(data[at + 8 + columns[2]:at + sum(columns)], 'little')
        at += sum(columns)
        if implementation or resources is None:  # linked in a file or another assembly, not embedded here
            continue
        name_start = strings_start + name_at
        if name_at >= strings_size:
            raise ValueError('resource name outside the string heap')
        name = data[name_start:data.index(b'\0', name_start)].decode('utf-8')
        size = u32(resources + data_offset)
        yield name, bytes(data[resources + data_offset + 4:resources + data_offset + 4 + size])


# ---- code string extraction, so phrases are only judged where a player could read them ----

def _unescape(text):
    text = re.sub(r'\\u([0-9a-fA-F]{4})|\\x([0-9a-fA-F]{2})', lambda m: chr(int(m[1] or m[2], 16)), text)
    return re.sub(r'\\[nrt]|`[nrt]', ' ', text)


HEREDOC = re.compile(r'<<-?\s*[\'"]?([A-Za-z_]\w*)[\'"]?')
ECHO = re.compile(r'^\s*(?:echo|printf|Write-Host|Write-Output|Write-Warning|Write-Error)\s+([^"\'\s-].*)$', re.I)


def code_strings(lines, lang):
    """Yield (line number, text of the string literals on the line, skip) for C#, Python, PowerShell, shell or JS source.
    Comments are skipped. Verbatim, raw, triple-quoted, here-strings, template literals and shell heredocs are followed
    across lines, escapes are decoded, and literals joined by `+` (also across a line break) read as one string."""
    block = False
    state = None  # None, or the terminator of a string that carries over to the next line
    heredoc = None
    carry = ''  # literal text before a trailing `+`
    sep = ''  # a line break inside a string reads as a space
    skip = 0  # leading characters of the yielded text that were already judged on the previous line
    for number, line in enumerate(lines, 1):
        if heredoc:
            if line.strip() == heredoc:
                heredoc = None
            else:
                yield number, line, 0
            continue
        i, n = 0, len(line)
        groups = []
        skip = 0
        last_end = None  # where the previous literal on this line ended

        def emit(text, start, end, decode=True):
            nonlocal last_end, carry, skip, sep
            text = _unescape(text) if decode else text
            gap = line[last_end:start].rstrip('@$fFbBrRuU') if last_end is not None else ''
            if last_end is not None and (gap.strip() == '+' or lang == 'py' and not gap.strip()):
                groups[-1] += text
            elif last_end is None and carry and line[:start].rstrip('@$fFbBrRuU').strip() in ('', '+'):
                groups.append(carry + sep + text)
                skip = len(carry) + len(sep)
            else:
                groups.append(text)
            last_end = end
            carry = ''

        while i < n:
            if block:
                end = line.find('*/', i)
                if end < 0:
                    break
                block, i = False, end + 2
                continue
            if state:
                begin = i
                if state == 'verbatim':
                    end = i
                    while end < n and not (line[end] == '"' and line[end + 1:end + 2] != '"'):
                        end += 2 if line[end] == '"' else 1
                    closed = end < n
                else:
                    end = line.find(state, i)
                    closed = end >= 0
                    end = end if closed else n
                emit(line[begin:end], begin, end if not closed else end + 1, decode=False)
                if closed:
                    i = end + (1 if state == 'verbatim' else len(state))
                    state = None
                else:
                    i = n
                continue
            c = line[i]
            two = line[i:i + 2]
            if lang in ('cs', 'js') and two == '//' or lang in ('py', 'ps1', 'sh') and c == '#':
                break
            if lang in ('cs', 'js') and two == '/*':
                block, i = True, i + 2
                continue
            if lang == 'cs' and line[i:i + 3] == '"""':
                state, i = '"""', i + 3
            elif lang == 'cs' and (two == '@"' or line[i:i + 3] in ('$@"', '@$"')):
                state, i = 'verbatim', line.index('"', i) + 1
            elif lang == 'py' and line[i:i + 3] in ('"""', "'''"):
                state, i = line[i:i + 3], i + 3
            elif lang == 'js' and c == '`':
                state, i = '`', i + 1
            elif lang == 'ps1' and two in ('@"', "@'"):
                state, i = two[1] + '@', i + 2
            elif c == '"' or (c == "'" and lang != 'cs'):
                j = i + 1
                while j < n and line[j] != c:
                    j += 2 if line[j] == '\\' and lang != 'ps1' else 1
                emit(line[i + 1:j], i, j + 1)
                i = j + 1
            elif c == "'" and lang == 'cs':  # char literal, may hold a quote
                j = i + 1
                while j < n and line[j] != "'":
                    j += 2 if line[j] == '\\' else 1
                i = j + 1
            else:
                i += 1
        if lang == 'sh':
            found = HEREDOC.search(line)
            if found:
                heredoc = found[1]
        if not groups and lang in ('sh', 'ps1'):
            echo = ECHO.match(line)
            if echo:
                groups.append(echo[1])
        if state and not groups:
            groups.append(line)
        tail = line[last_end:].strip() if last_end is not None else None
        continues = bool(state) or tail == '+' or lang == 'py' and tail == ''
        sep = ' ' if state else ''
        carry = groups[-1][-200:] if groups and continues else ''  # the tail is enough to judge a phrase split across lines
        if groups:
            yield number, '\0'.join(groups), skip  # a concatenation "all " + n + " cards" is not the phrase


def file_kind(name):
    ext = Path(name).suffix.casefold()
    return 'code' if ext in CODE_LANG else 'prose'


def _decor(line):
    """A source line with quotes, commas, `+`, brackets and a trailing comment removed, so wrapped literals compare as data."""
    line = re.sub(r'\s+//.*$', '', line.strip())
    line = re.sub(r'^(?:[bfBF@$]+(?=["\'`]))?[\s"\'`+(,\[{]*', '', line)
    return re.sub(r'[\s"\'`+),;\]}]*$', '', line)


def wrapped_encodings(lines, data_ok):
    """Yield (line number, rule) when consecutive lines add up to an encoded blob. Hex, escaped and decimal byte arrays,
    base64 and xxd output look harmless line by line. Checked once per run, at the line that crosses the threshold."""
    kinds = [('encoded-blob', B64_LINE, lambda l: len(l), 600),
             ('encoded-blob', HEX_LINE, lambda l: len(l), 1024),
             ('encoded-blob', XXD_LINE, lambda l: 32, 512),
             ('byte-array', HEXBYTE_LINE, lambda l: l.count('0x'), 32),
             ('byte-array', ESCAPED_LINE, lambda l: l.count('\\x'), 64)]
    if data_ok:
        # Dense lines only: a CSV or a pretty-printed JSON list of small numbers is data, not a blob.
        kinds.append(('byte-array', DECBYTE_LINE, lambda l: len(re.findall(r'\d+', l)) if len(re.findall(r'\d+', l)) >= 8 and all(
            int(v) < 256 for v in re.findall(r'\d+', l)) else 0, 128))
    totals = [0] * len(kinds)
    for number, raw in enumerate(lines, 1):
        line = _decor(raw)
        for index, (rule, regex, size, limit) in enumerate(kinds):
            if regex.match(line) and size(line):
                before = totals[index]
                totals[index] += size(line)
                if before < limit <= totals[index]:
                    yield number, rule
            else:
                totals[index] = 0


TRAILER = re.compile(r'^(?:Signed-off-by|Co-authored-by|Reviewed-by|Acked-by|Tested-by|Reported-by|Suggested-by|Helped-by|Cc|Author):', re.I)
# ^ credit lines may name anyone; a squash merge message lists `Author:` lines


class ObjectReader:
    """Blobs from `git cat-file --batch`, one process for a whole scan."""
    def __init__(self, repo):
        self.process = subprocess.Popen(['git', '-C', str(repo), 'cat-file', '--batch'], stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)

    def get(self, sha):
        """(size, bytes), (size, None) for a blob over MAX_READ, or (None, None) when the object is missing."""
        self.process.stdin.write(sha.encode() + b'\n')
        self.process.stdin.flush()
        header = self.process.stdout.readline().split()
        if len(header) != 3:
            return None, None
        size = int(header[2])
        if header[1] != b'blob':
            self.process.stdout.read(size + 1)
            return None, None
        if size > MAX_READ:
            left = size + 1
            while left:
                chunk = self.process.stdout.read(min(left, 1 << 20))
                if not chunk:
                    break
                left -= len(chunk)
            return size, None
        return size, self.process.stdout.read(size + 1)[:-1]

    def close(self):
        self.process.stdin.close()
        self.process.stdout.close()
        self.process.wait()


def raw_entries(repo, args):
    """(path, mode, sha) of each file a `git diff-index` / `diff-tree --raw -z` run adds or changes; deletions are skipped.
    A combined merge diff (`-c`) lists only the paths the merge itself changed against every parent."""
    out = subprocess.check_output(['git', '-C', str(repo)] + args, stderr=subprocess.DEVNULL)
    parts, entries, i = out.split(b'\0'), [], 0
    while i < len(parts):
        head = parts[i]
        if not head.startswith(b':'):
            i += 1
            continue
        parents = len(head) - len(head.lstrip(b':'))
        fields = head[parents:].decode().split()
        mode, sha = fields[parents], fields[2 * parents + 1]
        path = parts[i + 1].decode('utf-8', 'surrogateescape')
        i += 2
        if mode != '000000' and sha.strip('0'):
            entries.append((path, mode, sha))
    return entries


def commit_entries(repo, sha):
    """What a commit adds or changes against its first parent (a root commit: against nothing). For a merge that is everything
    the merged-in side brings, so outside work merged locally is scanned in full."""
    parents = subprocess.check_output(['git', '-C', str(repo), 'rev-list', '--parents', '-n', '1', sha], text=True,
                                      stderr=subprocess.DEVNULL).split()[1:]
    args = ['diff-tree', '-r', '--no-renames', '--no-commit-id', '--raw', '-z']
    return raw_entries(repo, args + ([parents[0], sha] if parents else ['--root', sha]))


def staged_entries(repo):
    head = 'HEAD' if subprocess.call(['git', '-C', str(repo), 'rev-parse', '--verify', '-q', 'HEAD'],
                                     stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0 else '4b825dc642cb6eb9a060e54bf8d69288fbee4904'
    return raw_entries(repo, ['diff-index', '--cached', '-r', '--no-renames', '--raw', '-z', head])


def parse_person(text):
    found = re.fullmatch(r'(.*?)\s*<([^<>]*)>', text.strip(), re.S)
    if not found:
        raise ConfigError(f'Not a "Name <email>" identity: {text!r}')
    return found[1], found[2]


class Scanner:
    def __init__(self, denylist=(), extras=None, media_allow=(), large_allow=(), max_bytes=MAX_BYTES,
                 hashes=(), client_reference=None, hook_markers=None, plugin_markers=None, strict=False):
        extras = extras or {}
        self.denylist = tuple(denylist)
        self.names = BLIZZARD_NAMES | {n.casefold() for n in extras.get('blizzard_names', [])}
        self.media_allow = ['docs/community/branding/**'] + list(media_allow) + list(extras.get('media_allow', []))
        self.large_allow = ['facts/**'] + list(large_allow) + list(extras.get('large_allow', []))
        self.card_data = CARD_DATA + list(extras.get('card_data', []))
        self.forbidden_paths = list(extras.get('forbidden_paths', []))
        self.max_bytes = max_bytes
        self.hashes = set(hashes) | {h.casefold() for h in extras.get('blizzard_sha256', [])}
        self.client_by_size = {}
        self.client_hashes = {}
        try:
            self.lab_path = re.compile('|'.join([LAB_PATH.pattern] + list(extras.get('lab_path_patterns', []))), re.I)
            self.phrases = [(rule, re.compile(rx, re.I), weak) for rule, rx, weak in PHRASES]
            self.phrases += [(p['rule'], re.compile(p['regex'], re.I), bool(p.get('weak'))) for p in extras.get('phrases', [])]
        except (re.error, KeyError, TypeError) as error:
            raise ConfigError(f'Bad pattern in release_scan: {error}')
        self.allowed_emails = {e.casefold() for e in extras.get('allowed_emails', [])}
        self.waivers = extras.get('waivers', [])
        for w in self.waivers:
            if not isinstance(w, dict) or not isinstance(w.get('path'), str):
                raise ConfigError('every waiver needs a path')
            if 'count' in w and (not isinstance(w['count'], int) or isinstance(w['count'], bool) or w['count'] < 1):
                raise ConfigError(f'waiver for {w["path"]}: count must be a positive integer')
        self.waiver_hits = [set() for _ in self.waivers]
        self.waiver_paths = [{} for _ in self.waivers]  # per waiver: (file, blob sha) -> the hits it absorbed there
        self.strict = strict  # the git modes and CI: see UNWAIVABLE_PUBLIC
        self.current_sha = None  # sha256 of the blob being scanned, for waivers bound to content
        self.wrapped_hits = set()  # (path, line, rule) reported for a multi-line run of an encoding
        self.current_text = (None, [])  # (path, lines) of the text being scanned, for waivers that name their text
        self.hook_markers, self.plugin_markers = (hook_markers, plugin_markers) if hook_markers is not None else stage_markers()
        extra_markers = [m.encode() for m in extras.get('binary_markers', [])]
        self.plugin_markers += extra_markers
        self.hook_markers += extra_markers
        self.hits = []
        self.waived = 0
        self.notes = []
        self.private_paths = set()
        self.inflated = 0
        if client_reference:
            self.index_client(Path(client_reference))

    def index_client(self, folder):
        """Size-indexed, hashed only on a size match, as the launcher's payload check does."""
        if not (folder / 'Hearthstone.exe').is_file() or not (folder / 'Hearthstone_Data' / 'Managed' / 'Assembly-CSharp.dll').is_file():
            raise ConfigError(f'The client reference folder is not a Hearthstone client: {folder}')
        for current, _, files in os.walk(folder):
            for file in files:
                path = Path(current, file)
                if path.is_file() and not path.is_symlink() and path.stat().st_size >= MIN_HASHED:
                    self.client_by_size.setdefault(path.stat().st_size, []).append(path)

    def waived_by(self, path, loc, rule):
        """Index of the first waiver that covers this hit, or None. A waiver with "text" only covers a hit whose own
        source line holds that text, so the same phrase elsewhere in the file, or in another file, still fails."""
        for index, w in enumerate(self.waivers):
            if self.strict and w.get('_public') and (UNWAIVABLE_PUBLIC.match(rule) or w.get('rule', '*') == '*' or NARROW_PUBLIC.match(rule)
                                                     and (re.search(r'[*?\[]', w['path']) or not isinstance(w.get('count'), int))):
                continue  # and a public waiver must name its rule: a wildcard excuses nothing in the git modes
            if self.strict and rule.startswith('privacy-') and w.get('text') is None:
                continue  # a privacy hit is only ever waived for one stated text on its own line
            if self.strict and w.get('_public') and NARROW_PUBLIC.match(rule) and 'sha256' not in w and 'line_sha256' not in w:
                continue  # content-hiding rules: the waiver must be bound to the content it excuses
            if not (fnmatch.fnmatchcase(path, w['path'].replace('**', '*')) and w.get('rule', '*') in ('*', rule)):
                continue
            text = w.get('text')
            if text is not None and not self.text_covers(text, path, loc, rule):
                continue
            if not self.content_matches(w, path, loc, rule):
                continue
            if self.exhausted(index, w, path, (path, loc, rule)):
                continue
            return index
        return None

    def content_matches(self, w, path, loc, rule=None):
        """`sha256` binds a waiver to the whole blob, `line_sha256` to the one source line the hit is on (the line after
        git's splitting, without its line break). A waiver bound to content stops covering a file once that content changes."""
        if 'sha256' in w and w['sha256'] != self.current_sha:
            return False
        if 'line_sha256' in w:
            if (path, loc, rule) in self.wrapped_hits:
                return False  # the hit stands for a whole wrapped run, not for this one line: it needs a `sha256` of the blob
            current, lines = self.current_text
            if current != path or not isinstance(loc, int) or not 0 < loc <= len(lines):
                return False
            return hashlib.sha256(lines[loc - 1].encode('utf-8')).hexdigest() == w['line_sha256']
        return True

    def exhausted(self, index, w, path, hit):
        """A waiver with `count` N covers at most N hits in one file; any further hit is unwaived. Without a count, the git
        modes allow one (an old private waiver may still span many hits in the export and binary scans)."""
        limit = w.get('count') if isinstance(w.get('count'), int) and w['count'] >= 1 else (1 if self.strict else None)
        if limit is None:
            return False
        taken = self.waiver_paths[index].setdefault((path, self.current_sha), set())  # per file content: a moved line is not a second hit
        return hit not in taken and len(taken) >= limit

    def text_covers(self, text, path, loc, rule):
        """The hit's own line holds `text`, and with every occurrence of `text` blanked the rule no longer fires there
        (nor across the line break either side), so the waiver cannot absorb a second, different hit on the same line.
        Only phrase rules and the e-mail rule can be judged this way; a text waiver for any other rule never applies."""
        current, lines = self.current_text
        if current != path or not isinstance(loc, int) or not 0 < loc <= len(lines) or text not in lines[loc - 1]:
            return False
        blanked = lines[loc - 1].replace(text, ' ' * len(text))
        if rule == 'email-address':
            return not self.bad_email(blanked)
        if rule.startswith('privacy-'):
            return not any(kind == rule[len('privacy-'):] for _, kind in privacy_scan.scan_text(path, blanked, self.denylist))
        regexes = [regex for name, regex, _ in self.phrases if name == rule]
        if not regexes:
            return False
        before = lines[loc - 2] + ' ' if loc > 1 else ''
        after = ' ' + lines[loc] if loc < len(lines) else ''
        view = before + blanked + after
        start, end = len(before), len(before) + len(blanked)
        return not any(m.end() > start and m.start() < end for regex in regexes for m in regex.finditer(view))

    def hit(self, path, loc, rule):
        index = self.waived_by(path, loc, rule)
        if index is not None:
            self.waiver_hits[index].add((path, loc, rule))
            self.waiver_paths[index].setdefault((path, self.current_sha), set()).add((path, loc, rule))
            self.waived += 1
            return
        self.hits.append((path, loc, rule))

    def label(self, path, show_private=False):
        """The path as printed. A path carrying a private literal is replaced by a short hash of itself."""
        if not show_private:
            for private in self.private_paths:
                if path.startswith(private):
                    return '[private-path:%s]%s' % (hashlib.sha1(private.encode()).hexdigest()[:10], path[len(private):])
        return path

    # -- file-level rules, shared by source and binaries --

    def name_rules(self, rel, name):
        low, ext = name.casefold(), Path(name).suffix.casefold()
        if low in self.names:
            self.hit(rel, 0, 'blizzard-name')
        if GAME_FILE.match(low) or CLIENT_PATH.search(rel):
            self.hit(rel, 0, 'game-dll')
        if ext in BUNDLE_EXT or BUNDLE_NAME.match(name):
            self.hit(rel, 0, 'asset-bundle')
        if ext in MEDIA_EXT and not glob_match(rel, self.media_allow):
            self.hit(rel, 0, 'media')
        if low == 'funding.yml':
            self.hit(rel, 0, 'payment-link')
        if glob_match(rel, self.forbidden_paths):
            self.hit(rel, 0, 'forbidden-path')
        self.path_privacy(rel)

    def path_privacy(self, rel):
        for _, kind in privacy_scan.scan_text('', rel, self.denylist):
            self.private_paths.add(rel)
            self.hit(rel, 0, 'path-' + kind)

    def blizzard_bytes(self, data):
        """{'hash'} for a file identical to a known Blizzard file, {'content'} for a CardDefs document, or both."""
        found = set()
        if len(data) < MIN_HASHED:
            return found
        head = data[:4096]
        if head[:2] in (b'\xff\xfe', b'\xfe\xff'):
            head = head.decode('utf-16', errors='replace').encode('utf-8')
        if CARDDEFS_ROOT.search(head):
            found.add('content')
        candidates = self.client_by_size.get(len(data), ())
        if self.hashes or candidates:
            digest = hashlib.sha256(data).hexdigest()
            if digest in self.hashes:
                found.add('hash')
            for path in candidates:
                if path not in self.client_hashes:
                    self.client_hashes[path] = hashlib.sha256(path.read_bytes()).hexdigest()
                if self.client_hashes[path] == digest:
                    found.add('hash')
        return found

    def hash_rule(self, rel, data):
        for found in self.blizzard_bytes(data):
            self.hit(rel, 0, 'blizzard-' + found)

    # -- text --

    def bad_email(self, line):
        for match in EMAIL.finditer(line):
            domain, address = match[1].casefold(), match[0].casefold()
            if domain.rsplit('.', 1)[-1] in NOT_EMAIL_TLD or re.search(r'://[^\s"\'<>]*$', line[:match.start()]):
                continue  # P/Invoke names like Foo@kernel32.dll, and URL userinfo
            if address not in self.allowed_emails and domain not in SAFE_EMAIL_DOMAINS and address.split('@')[0] != 'noreply':
                return True
        return False

    def scan_text(self, rel, text, name, embedded=False):
        """`embedded`: a resource inside a binary. Its data is card data for the weak phrases; strong ones still apply."""
        kind = file_kind(name)
        lines = text.splitlines()
        self.current_text = (rel, lines)
        for number, kind_name in privacy_scan.scan_text(rel, text, self.denylist):
            self.hit(rel, number, 'privacy-' + kind_name)
        card_data = embedded or glob_match(rel, self.card_data)
        for number, line in enumerate(lines, 1):
            if IL_LABEL.search(line) or IL_DIRECTIVE.search(line):
                self.hit(rel, number, 'il-listing')
            if DECOMPILED.search(line):
                self.hit(rel, number, 'decompiler-banner')
            blob = BASE64_RUN.search(line) if len(line) >= 300 else None
            if (blob and len(set(blob[0])) >= 20) or HEX_RUN.search(line) or DATA_URI.search(line):
                self.hit(rel, number, 'encoded-blob')
            if BYTE_ARRAY.search(line) or ESCAPED_RUN.search(line):
                self.hit(rel, number, 'byte-array')
            if self.lab_path.search(line):
                self.hit(rel, number, 'lab-path')
            if PAYMENT.search(line):
                self.hit(rel, number, 'payment-link')
            if self.bad_email(line):
                self.hit(rel, number, 'email-address')
        for number, rule in wrapped_encodings(lines, not glob_match(rel, ['facts/**'])):
            self.wrapped_hits.add((rel, number, rule))  # one line stands for a whole run: only a blob hash can bind such a waiver
            self.hit(rel, number, rule)
        if kind == 'code':
            judged = code_strings(lines, CODE_LANG[Path(name).suffix.casefold()])
        else:
            judged = ((number, line, 0) for number, line in enumerate(lines, 1))
        if kind != 'code':
            for number, (a, b) in enumerate(zip(lines, lines[1:]), 1):
                for rule, regex, weak in self.phrases:
                    if not (weak and card_data) and any(m.start() < len(a) < m.end() for m in regex.finditer(a + ' ' + b)):
                        self.hit(rel, number, rule)
        for number, line, skip in judged:
            for rule, regex, weak in self.phrases:
                if any(m.end() > skip for m in regex.finditer(line)) and not (
                        weak and (card_data or (kind == 'code' and CARD_TEXT.search(lines[number - 1])))):
                    self.hit(rel, number, rule)

    # -- source tree --

    def walk(self, root):
        for current, dirs, files in os.walk(root, followlinks=False):
            dirs[:] = sorted(d for d in dirs if d != '.git')
            for entry in sorted(dirs + files):
                full = Path(current, entry)
                if full.is_symlink():
                    link = full.relative_to(root).as_posix()
                    self.hit(link, 0, 'symlink')
                    self.path_privacy(link)
            for file in sorted(files):
                full = Path(current, file)
                if not full.is_symlink():
                    yield full.relative_to(root).as_posix(), full

    def scan_source(self, root, git=True):
        for rel, full in self.walk(root):
            try:
                self.scan_source_file(rel, full)
            except OSError:
                self.path_privacy(rel)
                self.hit(rel, 0, 'unscannable')
        if git:
            self.git_identity(root)
            self.git_history(root)

    def scan_source_file(self, rel, full):
        self.scan_source_data(rel, full.stat().st_size, full.read_bytes)

    def scan_source_data(self, rel, size, read):
        """One source file by its path, size and a reader for its bytes (a file on disk or a blob from the object store)."""
        self.current_sha = None  # until the bytes are read, nothing can be waived by content
        name = Path(rel).name
        ext = Path(rel).suffix.casefold()
        self.name_rules(rel, name)
        if ext in ARCHIVE_EXT:
            self.hit(rel, 0, 'archive')
        if size > self.max_bytes and not glob_match(rel, self.large_allow):
            self.hit(rel, 0, 'oversized')
        if size > MAX_READ:
            self.hit(rel, 0, 'unscannable')
            return
        data = read()
        self.current_sha = hashlib.sha256(data).hexdigest()
        self.hash_rule(rel, data)
        text = privacy_scan.decode_text(data)
        if text is None:
            if ext in EXECUTABLE_EXT:
                self.hit(rel, 0, 'binary-in-source')
            elif ext not in ARCHIVE_EXT and ext not in MEDIA_EXT and ext not in BUNDLE_EXT:
                self.hit(rel, 0, 'opaque-file')
            self.scan_strings(rel, data)  # allowlisted art can still carry metadata text
            return
        self.scan_text(rel, text, name)

    def git(self, root, *args):
        return subprocess.check_output(['git', '-C', str(root)] + list(args), errors='replace', stderr=subprocess.DEVNULL)

    def git_identity(self, root):
        if not (Path(root) / '.git').exists():
            return
        try:
            names = self.git(root, 'log', '--all', '--format=%an <%ae>%n%cn <%ce>')
            messages = self.git(root, 'log', '--all', '--format=%B%x00')
            tags = self.git(root, 'for-each-ref', 'refs/tags', '--format=%(taggername) <%(taggeremail)>%n%(contents)%x00')
        except (OSError, subprocess.SubprocessError):
            self.hit('.git', 0, 'git-unreadable')
            return
        for number, kind in privacy_scan.scan_text('.git', names + messages + tags, self.denylist):
            self.hit('.git', number, 'git-privacy-' + kind)
        if not self.allowed_emails:
            self.hit('.git', 0, 'git-identity-unset')  # release_scan.allowed_emails names the one public identity
            return
        for number, line in enumerate((names + tags).splitlines(), 1):
            for match in EMAIL.finditer(line):
                if match[0].casefold() not in self.allowed_emails:
                    self.hit('.git', number, 'git-identity')
        for number, line in enumerate(messages.splitlines(), 1):
            if self.bad_email(line):
                self.hit('.git', number, 'git-identity')

    def git_history(self, root):
        """Every blob ever committed, deleted ones included: the public history must be as clean as the tree."""
        if not (Path(root) / '.git').exists():
            return
        try:
            objects = self.git(root, 'rev-list', '--objects', '--all').splitlines()
            wanted = {line.split(' ', 1)[0]: (line.split(' ', 1) + [''])[1] for line in objects}
            process = subprocess.Popen(['git', '-C', str(root), 'cat-file', '--batch'], stdin=subprocess.PIPE,
                                       stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
            process.stdin.write(''.join(sha + '\n' for sha in wanted).encode())
            process.stdin.close()
            try:
                for sha in wanted:
                    header = process.stdout.readline().split()
                    if len(header) != 3:
                        continue
                    body = process.stdout.read(int(header[2]) + 1)[:-1]
                    if header[1] != b'blob':
                        continue
                    path = wanted[sha] or sha
                    self.scan_blob(f'.git:{path}@{sha[:8]}', Path(path).name, body if len(body) <= MAX_READ else None, 0)
            finally:
                process.stdout.close()
                process.wait()
        except (OSError, subprocess.SubprocessError, ValueError):
            self.hit('.git', 0, 'git-unreadable')

    # -- git objects: the index and commit ranges, read from the object store, never from the working tree --

    def scan_git_entries(self, repo, entries):
        """`entries`: (path, mode, blob sha). A nested repo (gitlink) or a symlink is a hit; a file is scanned as source."""
        reader = ObjectReader(repo)
        seen = set()
        try:
            for path, mode, sha in entries:
                if (path, sha) in seen:
                    continue
                seen.add((path, sha))
                if mode in ('160000', '120000'):
                    self.name_rules(path, Path(path).name)
                    self.hit(path, 0, 'gitlink' if mode == '160000' else 'symlink')
                    continue
                size, data = reader.get(sha)
                if size is None:
                    self.path_privacy(path)
                    self.hit(path, 0, 'unscannable')
                    continue
                self.scan_source_data(path, size, lambda data=data: data)
        finally:
            reader.close()

    def scan_staged(self, repo):
        self.scan_git_entries(repo, staged_entries(repo))

    def scan_commits(self, repo, rev_range, identity=None):
        """Messages and identities of the commits `rev_range` names (a `git rev-list` expression), then each commit's own
        change set, so a hit an older commit introduced still fails after a newer commit removes it."""
        shas = self.git(repo, 'rev-list', '--reverse', *shlex.split(rev_range)).split()
        entries = []
        for sha in shas:
            an, ae, cn, ce, message = self.git(repo, 'show', '-s', '--format=%an%x00%ae%x00%cn%x00%ce%x00%B', sha).split('\0', 4)
            self.scan_commit_text('commit:' + sha[:8], message, [(an, ae), (cn, ce)], identity)
            entries += commit_entries(repo, sha)
        self.scan_git_entries(repo, entries)
        return len(shas)

    def scan_message(self, text, people, identity=None):
        """A commit message that is about to be committed. In the editor flow git's own `#` lines are not part of it."""
        marker = '# ------------------------ >8 ------------------------'
        text = text.split(marker)[0]
        if 'Please enter the commit message' in text:
            text = '\n'.join(l for l in text.splitlines() if not l.startswith('#'))
        self.scan_commit_text('message', text, people, identity)

    def scan_commit_text(self, label, message, people, identity=None):
        """Private literals, lab paths, payment links and strong banned phrases in a message and its identities. With an
        `identity` (name, email), every other person and any foreign e-mail address in the message is a hit too."""
        text = ''.join(f'{name} <{email}>\n' for name, email in people) + message
        for number, kind in privacy_scan.scan_text(label, text, self.denylist):
            self.hit(label, number, 'commit-privacy-' + kind)
        for number, line in enumerate(text.splitlines(), 1):
            if self.lab_path.search(line):
                self.hit(label, number, 'commit-lab-path')
            if PAYMENT.search(line):
                self.hit(label, number, 'commit-payment-link')
            for rule, regex, weak in self.phrases:
                if not weak and regex.search(line):
                    self.hit(label, number, 'commit-' + rule)
            if identity and number > len(people) and self.bad_email(line) and not TRAILER.match(line):
                self.hit(label, number, 'commit-email-address')
        if identity:
            for person in people:
                if (person[0], person[1].casefold()) != (identity[0], identity[1].casefold()):
                    self.hit(label, 0, 'commit-identity')

    # -- binaries --

    def scan_binaries(self, root):
        for rel, full in self.walk(root):
            try:
                self.scan_blob(rel, full.name, full.read_bytes() if full.stat().st_size <= MAX_READ else None, 0)
            except OSError:
                self.path_privacy(rel)
                self.hit(rel, 0, 'unscannable')

    def unpack(self, rel, name, data, depth):
        """Scan what a container holds: zip (also one appended to an exe), gzip, bzip2, xz and tar."""
        def member(label, member_name, body, **kw):
            self.scan_blob(label, member_name, body, depth + 1, **kw)

        def inflate(read):
            """Read a stream in chunks against the run-wide budget; None when it exceeds it."""
            out = bytearray()
            while True:
                chunk = read(1 << 20)
                if not chunk:
                    return bytes(out)
                out += chunk
                self.inflated += len(chunk)
                if self.inflated > MAX_INFLATED or len(out) > MAX_READ:
                    return None

        try:
            if zipfile.is_zipfile(io.BytesIO(data)):
                archive = zipfile.ZipFile(io.BytesIO(data))
                infos = [i for i in archive.infolist() if not i.is_dir()]
                if len(infos) > MAX_ZIP_MEMBERS:
                    raise ValueError('too many members')
                for info in infos:
                    label = f'{rel}!{info.filename}'
                    if info.file_size > 64 * 1024 * 1024 and info.file_size > MAX_RATIO * max(info.compress_size, 1):
                        self.hit(label, 0, 'unscannable')
                        continue
                    try:
                        with archive.open(info) as stream:
                            body = inflate(stream.read)
                    except (RuntimeError, NotImplementedError, zipfile.BadZipFile, zlib.error, EOFError, OSError, ValueError):
                        body = None
                    member(label, Path(info.filename).name, body)
                return
            for magic, opener in ((b'\x1f\x8b', lambda f: gzip.GzipFile(fileobj=f)), (b'BZh', bz2.BZ2File),
                                  (b'\xfd7zXZ\0', lzma.LZMAFile)):
                if data.startswith(magic):
                    try:
                        body = inflate(opener(io.BytesIO(data)).read)
                    except (OSError, EOFError, zlib.error, lzma.LZMAError, ValueError):
                        body = None
                    inner = name.rsplit('.', 1)[0] if '.' in name else name
                    member(f'{rel}!{inner}', inner, body)
                    return
            if data[:6] in (b"7z\xbc\xaf'\x1c", b'Rar!\x1a\x07') or data[:4] == b'MSCF':
                raise ValueError('unreadable archive format')
            if len(data) > 512 and tarfile.is_tarfile(io.BytesIO(data)):
                with tarfile.open(fileobj=io.BytesIO(data)) as archive:
                    for info in archive:
                        if info.isfile():
                            label = f'{rel}!{info.name}'
                            body = inflate(archive.extractfile(info).read) if info.size <= MAX_READ else None
                            member(label, Path(info.name).name, body)
        except (zipfile.BadZipFile, tarfile.TarError, ValueError, OSError, RuntimeError, EOFError):
            self.hit(rel, 0, 'unscannable')

    def scan_blob(self, rel, name, data, depth, embedded=False):
        outer = self.current_sha
        self.current_sha = hashlib.sha256(data).hexdigest() if data is not None else None
        try:
            self._scan_blob(rel, name, data, depth, embedded)
        finally:
            self.current_sha = outer  # members and resources are scanned with their own sha; the container gets its own back

    def _scan_blob(self, rel, name, data, depth, embedded):
        own = self.current_sha
        self.name_rules(rel, name)
        if data is None or depth > MAX_ZIP_DEPTH:
            self.hit(rel, 0, 'unscannable')
            return
        self.hash_rule(rel, data)
        text = privacy_scan.decode_text(data)
        if text is not None and data[:2] != b'MZ':
            self.scan_text(rel, text, name, embedded=embedded)
            return
        if b'Inno Setup Setup Data' in data:
            self.notes.append((rel, 'Inno Setup payload is compressed and was not read; scan the app folder and update zip it came from'))
        self.unpack(rel, name, data, depth)
        self.current_sha = own  # unpack scanned the members: what follows judges this blob again
        self.scan_binary(rel, data, depth)

    def scan_strings(self, rel, data):
        """Privacy, lab paths and e-mail in the printable strings of a non-text file (ASCII and UTF-16), plus any
        non-ASCII private literal searched for as raw UTF-8 and UTF-16 bytes."""
        strings = [(m.start(), m[0].decode('ascii')) for m in re.finditer(rb'[\x20-\x7e\t]{5,}', data)]
        strings += [(m.start(), m[0].decode('utf-16-le')) for m in re.finditer(rb'(?:[\x20-\x7e]\x00){5,}', data)]
        strings.sort()
        for number, kind in privacy_scan.scan_text(rel, '\n'.join(text for _, text in strings), self.denylist):
            self.hit(rel, '@0x%x' % strings[number - 1][0], 'binary-' + kind)
        for offset, text in strings:
            if self.lab_path.search(text):
                self.hit(rel, '@0x%x' % offset, 'binary-lab-path')
            if self.bad_email(text):
                self.hit(rel, '@0x%x' % offset, 'binary-email-address')
        for literal in self.denylist:
            if literal.isascii():
                continue
            for variant in {literal, literal.casefold(), literal.upper(), literal.title()}:
                for encoding in ('utf-8', 'utf-16-le'):
                    at = data.find(variant.encode(encoding))
                    if at >= 0:
                        self.hit(rel, '@0x%x' % at, 'binary-private-literal')

    def scan_binary(self, rel, data, depth=0):
        self.scan_strings(rel, data)
        low = Path(rel.replace('!', '/').replace('#', '/')).name.casefold()
        markers = []
        if low.startswith('solostone') and low.endswith('.exe'):
            markers = self.hook_markers
        if '/plugins/' in '/' + rel.replace('\\', '/').casefold() or low.startswith(('hsnativeuiprobe', 'solostone')) and low.endswith('.dll'):
            markers = markers + self.plugin_markers
        for marker in markers:
            for needle in (marker, marker.decode('ascii', 'ignore').encode('utf-16-le')):
                at = data.find(needle)
                if at >= 0 and needle:
                    self.hit(rel, '@0x%x' % at, 'binary-test-hook')
                    break
        try:
            for resource, body in parse_managed_resources(data):
                lowered = resource.casefold()
                if any(lowered == n or lowered.endswith(('.' + n, '/' + n, '\\' + n)) for n in self.names):
                    self.hit(rel, 0, 'blizzard-resource-name')
                if Path(lowered).suffix in BUNDLE_EXT:
                    self.hit(rel, 0, 'asset-bundle')
                elif Path(lowered).suffix in MEDIA_EXT:
                    self.hit(rel, 0, 'media')
                for found in self.blizzard_bytes(body):
                    self.hit(rel, 0, 'blizzard-resource-' + found)
                clean = re.sub(r'^costura\.|\.compressed$', '', Path(resource.replace('\\', '/')).name, flags=re.I)
                self.scan_blob(f'{rel}#{resource}', clean, body, depth + 1, embedded=True)
                if resource.casefold().endswith('.compressed'):  # Costura.Fody: raw deflate
                    try:
                        inflated = zlib.decompressobj(-15).decompress(body, MAX_READ)
                    except zlib.error:
                        inflated = None
                    self.scan_blob(f'{rel}#{resource}!inflated', clean, inflated, depth + 1, embedded=True)
        except ParseError:
            self.hit(rel, 0, 'metadata-unreadable')


def run(source, binaries, config, allow_no_config=False, client_reference=None, media_allow=(), large_allow=(),
        max_bytes=MAX_BYTES, hashes=None, public_rules=None, skip_git=False, strict=False, trust_public=False):
    denylist, extras = load_config(config, allow_no_config, public_rules, strict and not trust_public)
    for folder in (source, binaries):
        if folder is not None and not Path(folder).is_dir():
            raise ConfigError(f'Not a folder: {folder}')
    scanner = Scanner(denylist, extras, media_allow, large_allow, max_bytes,
                      private_blizzard_hashes() if hashes is None else hashes, client_reference, strict=strict)
    if binaries is not None and not (scanner.hook_markers or scanner.plugin_markers):
        raise ConfigError('the launcher and plugin markers of installer/stage.py are unavailable: a binary scan without them checks nothing')
    if source is not None:
        scanner.scan_source(Path(source), git=not skip_git)
    if binaries is not None:
        scanner.scan_binaries(Path(binaries))
    return scanner


def printable(text):
    """A line of output that cannot start a new line or a workflow command: control characters, line separators and a leading
    `::` (GitHub Actions workflow commands) are escaped, so a file named after a command prints as plain text."""
    text = re.sub('[\x00-\x1f\x7f-\x9f\u2028\u2029]', lambda m: '\\x%02x' % ord(m[0]) if ord(m[0]) < 256 else '\\u%04x' % ord(m[0]), str(text))
    return '\\x3a' + text[1:] if text.startswith('::') else text


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--source', type=Path, help='Source tree to scan (the repository, or a clean copy of it)')
    parser.add_argument('--binaries', type=Path, help='Folder of built binaries and payload to scan')
    parser.add_argument('--config', type=Path, help='Private config (default: HS_LAB_CONFIG or ~/.config/solostone/lab.json)')
    parser.add_argument('--client-reference', type=Path, help='A Hearthstone client folder: identical files are Blizzard files')
    parser.add_argument('--media-allow', action='append', default=[], help='Extra glob where media is allowed')
    parser.add_argument('--large-allow', action='append', default=[], help='Extra glob where big files are allowed')
    parser.add_argument('--max-bytes', type=int, default=MAX_BYTES, help='Largest file outside --large-allow')
    parser.add_argument('--allow-no-private-config', action='store_true', help='Run without the private denylist (weaker)')
    parser.add_argument('--public-rules', type=Path, help='Public rules JSON (tools/release_scan.public.json), merged with the private config')
    parser.add_argument('--skip-git', action='store_true', help='With --source: skip the git identity and history checks')
    parser.add_argument('--repo', type=Path, default=Path('.'), help='Repository for --staged, --commits and --message')
    parser.add_argument('--staged', action='store_true', help='Scan the blobs in the index of --repo')
    parser.add_argument('--commits', metavar='RANGE', help='Scan the commits `git rev-list RANGE` lists: messages, identities, blobs')
    parser.add_argument('--message', type=Path, help='Scan this commit message file')
    parser.add_argument('--ident', action='append', default=[], metavar='"Name <email>"', help='With --message: an author or committer')
    parser.add_argument('--strict', action='store_true', help='Apply the git-mode strictness to --source/--binaries too (CI)')
    parser.add_argument('--trust-public-rules', action='store_true',
                        help='CI: honour the public allowed_emails, large_allow, media_allow and card_data in strict mode (take the '
                             'rules from the base branch, not from the change); every other strict rule stays')
    parser.add_argument('--require-identity', metavar='"Name <email>"', help='With --commits or --message: any other identity is a hit')
    parser.add_argument('--blizzard-file', action='append', default=[], type=Path, help='A local Blizzard file: an identical file is a hit')
    parser.add_argument('--show-private-paths', action='store_true', help='Print paths that carry a private literal (default: a hash)')
    args = parser.parse_args(argv)
    if not (args.source or args.binaries or args.staged or args.commits or args.message):
        parser.error('give --source, --binaries, --staged, --commits or --message')
    try:
        hashes = private_blizzard_hashes()
        for file in args.blizzard_file:
            if file.is_file():
                hashes.add(hashlib.sha256(file.read_bytes()).hexdigest())
        strict = bool(args.staged or args.commits or args.message or args.strict)
        scanner = run(args.source, args.binaries, args.config, args.allow_no_private_config, args.client_reference,
                      args.media_allow, args.large_allow, args.max_bytes, hashes, args.public_rules, args.skip_git, strict,
                      args.trust_public_rules)
        identity = parse_person(args.require_identity) if args.require_identity else None
        if args.staged:
            scanner.scan_staged(args.repo)
        if args.commits:
            scanner.scan_commits(args.repo, args.commits, identity)
        if args.message:
            scanner.scan_message(args.message.read_text(encoding='utf-8', errors='replace'), [parse_person(i) for i in args.ident],
                                 identity)
    except (ConfigError, OSError, ValueError, subprocess.SubprocessError) as error:
        print(printable(f'Release scan failed: {error}'), file=sys.stderr)
        return 2
    except Exception as error:  # a crash must not look like "hits found" (exit 1)
        print(printable(f'Release scan crashed: {type(error).__name__}: {error}'), file=sys.stderr)
        return 2
    hits = set(scanner.hits)
    for path, loc, rule in sorted(hits, key=lambda h: (h[0], str(h[1]).zfill(12), h[2])):
        print(printable(f'{scanner.label(path, args.show_private_paths)}:{loc} {rule}'))
    for path, note in scanner.notes:
        print(printable(f'note: {scanner.label(path, args.show_private_paths)}: {note}'), file=sys.stderr)
    counts = {}
    for _, _, rule in hits:
        counts[rule] = counts.get(rule, 0) + 1
    summary = ', '.join(f'{rule}={count}' for rule, count in sorted(counts.items())) or 'clean'
    waived = f' ({scanner.waived} waived)' if scanner.waived else ''
    print(f'release scan: {len(hits)} hits{waived}: {summary}', file=sys.stderr)
    return int(bool(hits))


if __name__ == '__main__':
    sys.exit(main())
