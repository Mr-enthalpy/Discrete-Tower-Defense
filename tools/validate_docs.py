"""Validate local documentation links and immutable baseline sources (stdlib only)."""
from pathlib import Path
import hashlib
import re
import sys
from urllib.parse import unquote


def main():
    root = Path(__file__).resolve().parents[1]
    failures = []
    documents = list(root.rglob('*.md'))
    count = 0
    for path in documents:
        text = path.read_text(encoding='utf-8')
        # Links in fenced code are examples, not navigation.
        visible = re.sub(r'```.*?```', '', text, flags=re.S)
        for destination in re.findall(r'(?<!!)\[[^\]\n]+\]\(([^\n)]+)\)', visible):
            destination = destination.strip().strip('<>')
            if re.match(r'^[a-zA-Z][\w+.-]*:', destination) or destination.startswith('#'):
                continue
            local = unquote(destination.split('#', 1)[0])
            if not local:
                continue
            count += 1
            if not (path.parent / local).resolve().exists():
                failures.append(f'{path.relative_to(root)}: broken link {destination}')
    baseline = root / 'docs/baseline/v1.0'
    manifest = (baseline / 'README.md').read_text(encoding='utf-8')
    for label, extension in [('PDF', 'pdf'), ('DOCX', 'docx')]:
        expected = re.search(rf'SHA256 {label}\s+([a-f0-9]{{64}})', manifest)
        source = baseline / f'Discrete_Tower_Defense_Design_Baseline_v1.{extension}'
        actual = hashlib.sha256(source.read_bytes()).hexdigest()
        if not expected or actual != expected[1]:
            failures.append(f'{label}: source hash mismatch')
    chapters = sorted(baseline.glob('[0-9][0-9]-*.md'))
    if [p.name[:2] for p in chapters] != [f'{i:02}' for i in range(12)]:
        failures.append('baseline: expected exactly chapters 00 through 11')
    table_count = sum(len(re.findall(r'^\| ---', p.read_text(encoding='utf-8'), re.M)) for p in chapters)
    if table_count != 18:
        failures.append(f'baseline: expected 18 tables, got {table_count}')
    if failures:
        print('\n'.join(failures))
        return 1
    print(f'OK: {len(documents)} Markdown files, {count} local links, 12 chapters, 18 tables, 2 source hashes.')
    print('Documentation checks only; no game implementation, rule tests or runtime performance verified.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
