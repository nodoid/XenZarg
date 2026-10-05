#!/usr/bin/env python3
"""Checks every field in stores/LISTING.md against its store's character limit."""
import os, re, sys

path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'stores', 'LISTING.md')
text = open(path).read()
ok = True
for m in re.finditer(r'\*\*([^*]+)\*\* \((\d+) max(?: each)?(?:, (\d+) characters each)?\)\n```\n(.*?)\n```', text, re.S):
    field, limit, per_line, body = m.group(1), int(m.group(2)), m.group(3), m.group(4)
    if per_line or field in ('Product features',):
        each = int(per_line) if per_line else limit
        lines = body.split('\n')
        bad = [l for l in lines if len(l) > each]
        status = 'OK' if not bad and (not per_line or len(lines) <= limit) else 'TOO LONG'
        print(f'{status:8} {field}: {len(lines)} lines, longest {max(map(len, lines))}/{each}')
    else:
        status = 'OK' if len(body) <= limit else 'TOO LONG'
        print(f'{status:8} {field}: {len(body)}/{limit}')
    ok &= status == 'OK'
sys.exit(0 if ok else 1)
