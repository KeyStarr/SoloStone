#!/usr/bin/env python3
"""The loader files SoloStone ships next to the game, from the recorded list installer/loader-inputs.json.

Doorstop, BepInEx and the Mono class libraries (BepInEx\\unstripped_corlib) come only from archives named in that list,
each pinned by SHA-256, and every shipped file is pinned too. Nothing is taken from a lab machine.

    python3 installer/loader_inputs.py fetch --out DIR [--cache DIR]   download, check, assemble DIR (winhttp.dll, BepInEx\\...)
    python3 installer/loader_inputs.py verify DIR                      DIR holds exactly the listed files with the listed hashes
    python3 installer/loader_inputs.py check-notice                    NOTICE.txt names every component's name, version and licence link

Downloaded archives are data: they are hashed and unpacked member by member with fixed names, never run.
"""
import argparse
import hashlib
import io
import json
import re
import shutil
import sys
import urllib.request
import zipfile
from pathlib import Path

sys.dont_write_bytecode = True

INSTALLER = Path(__file__).resolve().parent
LIST = INSTALLER / 'loader-inputs.json'
NOTICE = INSTALLER / 'NOTICE.txt'


HEX64 = re.compile('[0-9a-f]{64}')


class Refused(Exception):
    pass


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def download(url, expected, cache):
    """Bytes from the cache if the hash fits, else from the URL; refused if the hash differs."""
    name = url.rsplit('/', 1)[-1]
    cached = Path(cache) / (expected + '-' + name) if cache else None
    if cached and cached.is_file():
        stored = cached.read_bytes()
        if sha256(stored) == expected:
            return stored
    request = urllib.request.Request(url, headers={'User-Agent': 'SoloStone-build'})
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            data = response.read()
    except OSError as error:
        raise Refused('%s: download failed (%s)' % (url, error))
    if sha256(data) != expected:
        raise Refused('%s: hash %s, listed %s' % (url, sha256(data), expected))
    if cached:
        cached.parent.mkdir(parents=True, exist_ok=True)
        cached.write_bytes(data)
    return data


def load(path=LIST):
    data = json.loads(Path(path).read_text(encoding='utf-8'))
    if data.get('schema') != 1:
        raise Refused('loader-inputs.json: unknown schema')
    sources, files = data['sources'], data['files']
    seen = set()
    seen_lower = set()
    for entry in files:
        path_ = entry['path']
        if path_ in seen or path_.startswith('/') or '..' in path_.split('/') or '\\' in path_:
            raise Refused('bad or repeated path: ' + path_)
        seen.add(path_)
        if entry['source'] not in sources:
            raise Refused('%s names an unknown source %s' % (path_, entry['source']))
        if sources[entry['source']].get('raw') and not entry.get('url', '').startswith('https://'):
            raise Refused('%s is a raw file without an https url' % path_)
        if path_.lower() in seen_lower:
            raise Refused('paths differing only by case: ' + path_)
        seen_lower.add(path_.lower())
        if not HEX64.fullmatch(entry['sha256']):
            raise Refused('%s has no SHA-256' % path_)
    for name, source in sources.items():
        if source.get('raw'):
            if source.get('url') or source.get('archiveSha256'):
                raise Refused('%s is a raw source: urls belong to its files' % name)
        elif not HEX64.fullmatch(source.get('archiveSha256', '')) or not source.get('url'):
            raise Refused('%s has no archive url or SHA-256' % name)
        if not source.get('components'):
            raise Refused('%s lacks components' % name)
        for part in source['components']:
            for key in ('name', 'version', 'licence', 'licenceUrl'):
                if not part.get(key):
                    raise Refused('%s lists a component without %s' % (name, key))
    return data


def fetch(out, cache=None, data=None):
    data = data or load()
    out = Path(out)
    if out.exists():
        if not (out / '.solostone-loader').is_file():
            raise Refused('refusing to replace an unmarked folder: ' + str(out))
        shutil.rmtree(out)
    out.mkdir(parents=True)
    (out / '.solostone-loader').write_text('made by loader_inputs.py\n', encoding='ascii')
    archives = {}
    for entry in data['files']:
        source = data['sources'][entry['source']]
        if source.get('raw'):
            content = download(entry['url'], entry['sha256'], cache)
        else:
            if entry['source'] not in archives:
                try:
                    archives[entry['source']] = zipfile.ZipFile(io.BytesIO(download(source['url'], source['archiveSha256'], cache)))
                except zipfile.BadZipFile:
                    raise Refused('%s is not a zip archive' % source['url'])
            try:
                info = archives[entry['source']].getinfo(entry['member'])
            except KeyError:
                raise Refused('%s has no member %s' % (source['url'], entry['member']))
            if info.file_size > 64 * 1024 * 1024:
                raise Refused(entry['member'] + ' is too large')
            content = archives[entry['source']].read(info)
            if sha256(content) != entry['sha256']:
                raise Refused('%s from %s: hash %s, listed %s' % (entry['member'], entry['source'], sha256(content), entry['sha256']))
        target = out.joinpath(*entry['path'].split('/'))
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)
    verify(out, data)
    return out


def verify(folder, data=None):
    """Exactly the listed files, byte for byte; no extra file anywhere under the loader root."""
    data = data or load()
    folder = Path(folder)
    want = {e['path']: e['sha256'] for e in data['files']}
    have = {}
    for item in sorted(folder.rglob('*')):
        if item.is_symlink():
            raise Refused('link in the loader folder: ' + str(item))
        if item.is_file() and item.name != '.solostone-loader':
            have[item.relative_to(folder).as_posix()] = sha256(item.read_bytes())
    problems = ['missing: ' + p for p in want if p not in have]
    problems += ['unlisted: ' + p for p in have if p not in want]
    problems += ['hash differs: ' + p for p in want if p in have and have[p] != want[p]]
    if problems:
        raise Refused('; '.join(problems))
    return len(have)


def check_notice(data=None):
    data = data or load()
    text = NOTICE.read_text(encoding='utf-8')
    problems = []
    for key, source in data['sources'].items():
        for part in source['components']:
            for needed in (part['name'] + ' ' + part['version'], part['licence'].split()[0].rstrip(',.'), part['licenceUrl']):
                if needed not in text:
                    problems.append('NOTICE.txt lacks "%s" (%s)' % (needed, key))
    if problems:
        raise Refused('; '.join(problems))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest='command', required=True)
    f = sub.add_parser('fetch')
    f.add_argument('--out', required=True)
    f.add_argument('--cache')
    f.add_argument('--list', default=str(LIST), help='another list, e.g. a candidate under test')
    v = sub.add_parser('verify')
    v.add_argument('folder')
    sub.add_parser('check-notice')
    args = parser.parse_args()
    try:
        if args.command == 'fetch':
            data = load(args.list)
            print('loader ready: %d files in %s' % (verify(fetch(args.out, args.cache, data), data), args.out))
        elif args.command == 'verify':
            print('loader ok: %d files' % verify(args.folder))
        else:
            check_notice()
            print('NOTICE.txt covers every source')
    except Refused as error:
        print('LOADER REFUSED: ' + str(error), file=sys.stderr)
        sys.exit(1)


if __name__ == '__main__':
    main()
