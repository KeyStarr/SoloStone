#!/usr/bin/env python3
"""Scan current tracked text, reporting locations and kinds without exposing values."""
import argparse
import ipaddress
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote

IPV4 = r"(?<![\w.])(?:[0-9]{1,3}\.){3}[0-9]{1,3}(?![\w.])"
DOCUMENTATION = tuple(ipaddress.ip_network(value) for value in
                      ('192.0.2.0/24', '198.51.100.0/24', '203.0.113.0/24'))
PRIVATE = tuple(ipaddress.ip_network(value) for value in
                ('10.' + '0.0.0/8', '172.' + '16.0.0/12', '192.168.' + '0.0/16'))
# Names fixed by the platform that are not personal, so they are never reported: Proton's default Windows user (steamuser) and
# the Windows Public profile. The two project test-account entries are spelled in pieces on purpose.
WINDOWS_USERS = {'hsnativeui' + 'probe', 'hsoffline' + 'probe', 'steamuser', 'public', '<lab-user>'}
TOKENS = {
    'discord-token': r"(?<![\w-])(?:mfa\.[\w-]{80,}|[\w-]{23,28}\.[\w-]{6}\.[\w-]{27,})(?![\w-])",
    'discord-webhook': r"https://(?:(?:canary|ptb)\.)?discord(?:app)?\.com/api/(?:v[0-9]+/)?webhooks/[0-9]+/[A-Za-z0-9_-]+",
    'private-key': r"-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----",
    'github-token': r"(?<![\w])(?:ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})",
    'openai-token': r"(?<![\w])sk-[A-Za-z0-9_-]{20,}",
    'aws-access-key': r"(?<![\w])AKIA[A-Z0-9]{16}(?![\w])",
}


def documentation_address(address):
    return any(address in network for network in DOCUMENTATION)


def scan_text(path, source, denylist=()):
    """Yield (line, kind). Never return matched contents, including private literals."""
    denylist = tuple(value.casefold() for value in denylist)
    for number, line in enumerate(source.splitlines(), 1):
        kinds = set()
        decoded = unquote(line)
        # Source strings and JSON commonly escape Windows separators.
        normalized = re.sub(r"C:\\\\Users\\\\", lambda match: "C:" + chr(92) + "Users" + chr(92), decoded, flags=re.I)
        for match in re.finditer(IPV4, line):
            try:
                address = ipaddress.ip_address(match[0])
            except ValueError:
                continue
            if documentation_address(address):
                continue
            # This is the plugin's own network canary (plugin/OfflineProbe.cs tries to connect to it and to 1.1.1.1 to confirm the game has
            # no outbound network) and docs describe it. It is a generic router address, not a real host, so it is allowed only in those
            # places and flagged everywhere else.
            canary = str(address) == '192.168.' + '1.1' and (
                path.endswith('/plugin/OfflineProbe.cs') or path == 'plugin/OfflineProbe.cs'
                or '/docs/' in '/' + path)
            if any(address in network for network in PRIVATE) and not canary:
                kinds.add('private-ipv4')
        for match in re.finditer(r"(?<![\w])[A-Za-z0-9_.-]{1,64}@((?:[0-9]{1,3}\.){3}[0-9]{1,3})(?![\w.])", line):
            try:
                if not documentation_address(ipaddress.ip_address(match[1])):
                    kinds.add('ssh-ip-target')
            except ValueError:
                continue
        user = r"[\w.-]+"
        boundary = r"(?![\w.-])"
        for match in re.finditer(r"/Users/(" + user + ')' + boundary, decoded, re.I):
            # Wine's virtual Windows profile uses the same fixed account allowlist.
            wine = decoded[max(0, match.start() - 7):match.start()].casefold() == 'drive_c'
            if not wine or match[1].casefold() not in WINDOWS_USERS:
                kinds.add('windows-home' if wine else 'mac-home')
        if re.search(r"-Users-" + user + r"-", decoded, re.I):
            kinds.add('mac-home')
        for match in re.finditer(r"/home/" + '(' + user + ')' + boundary, decoded):
            # Default Linux users of SteamOS (deck) and Proton (steamuser) are not personal either.
            if match[1] not in ('deck', 'steamuser'):
                kinds.add('linux-home')
        for match in re.finditer(r"C:\\Users\\([A-Za-z0-9_][A-Za-z0-9_. -]*|<lab-user>)", normalized, re.I):
            if match[1].casefold() not in WINDOWS_USERS:
                kinds.add('windows-home')
        if ('/private/tmp/' + 'claude-') in line:
            kinds.add('session-scratch')
        for kind, pattern in TOKENS.items():
            if re.search(pattern, line):
                kinds.add(kind)
        if any(value in line.casefold() for value in denylist):
            kinds.add('private-literal')
        for kind in sorted(kinds):
            yield number, kind


def decode_text(data):
    """Decode BOM text first; tolerate damaged UTF-8, but exclude binary payloads."""
    if data.startswith((b'\xff\xfe', b'\xfe\xff')):
        source = data.decode('utf-16', errors='replace')
    else:
        source = data.decode('utf-8-sig', errors='replace')
    if '\0' in source:
        return None
    nontext = sum((ord(char) < 32 and char not in '\t\n\r\f') or char == '\ufffd' for char in source)
    if nontext > max(3, len(source) * 0.1):
        return None
    return source


def scan_tree(root, denylist=()):
    tracked = subprocess.check_output(['git', '-C', str(root), 'ls-files', '-z']).split(b'\0')
    for raw in tracked:
        if not raw:
            continue
        path = raw.decode('utf-8')
        file = root / path
        if not file.exists():  # tracked deletion
            continue
        source = decode_text(file.read_bytes())
        if source is None:
            continue
        for number, kind in scan_text(path, source, denylist):
            yield path, number, kind


def read_denylist(explicit=None):
    """Keep this scanner independent of private remote tooling."""
    path = explicit or Path(os.environ.get('HS_LAB_CONFIG', '~/.config/solostone/lab.json')).expanduser()
    try:
        value = json.loads(path.read_text(encoding='utf-8-sig'))
    except FileNotFoundError:
        if explicit is not None:
            raise
        return []
    if explicit is None:
        if not isinstance(value, dict):
            raise ValueError('Lab config must be a JSON object')
        value = value.get('privacy_denylist', [])
    if not isinstance(value, list) or any(not isinstance(v, str) or not v for v in value):
        raise ValueError('privacy_denylist must be a list of nonempty strings')
    return value


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, help='Git worktree to scan (default: this checkout)')
    parser.add_argument('--denylist-file', type=Path, help='Explicit JSON list of private literals')
    args = parser.parse_args(argv)
    try:
        root = args.root or Path(subprocess.check_output(
            ['git', '-C', str(Path(__file__).resolve().parent), 'rev-parse', '--show-toplevel'], text=True).strip())
        denylist = read_denylist(args.denylist_file)
        hits = list(scan_tree(root, denylist))
        for path, number, kind in hits:
            print(f'{path}:{number} {kind}')
        return int(bool(hits))
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print(f'Privacy scan failed: {error}', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
