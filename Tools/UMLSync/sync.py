"""Safe first-pass draw.io synchronizer: adds missing types and inheritance edges.
Does not rewrite pre-existing manually drawn nodes or parse full C# semantics.
Requires Python 3.9+; no third-party packages.
"""
import argparse
import html
import json
import re
import sys
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = Path(__file__).with_name('config.json')
TYPE_RE = re.compile(r'\b(?:public|internal|protected|private|abstract|sealed|static|partial|readonly|ref|new|\s)*\b(class|interface|struct|enum|record)\s+(\w+)(?:\s*<[^>{}]+>)?\s*(?::\s*([^\{]+))?\s*\{', re.MULTILINE)
COMMENT_RE = re.compile(r'/\*.*?\*/|//[^\n]*', re.DOTALL)
TAG_RE = re.compile(r'<[^>]+>')

def get_types(paths):
    types = {}
    for spec in paths:
        base = Path(spec['path'])
        if not base.is_absolute(): base = ROOT / base
        if not base.is_dir():
            print(f'[WARN] Missing source directory: {base}', file=sys.stderr)
            continue
        files = base.rglob('*.cs') if spec.get('recursive', False) else base.glob('*.cs')
        for file in sorted(files):
            src = COMMENT_RE.sub('', file.read_text(encoding='utf-8-sig'))
            for match in TYPE_RE.finditer(src):
                kind, name, parents = match.groups()
                # Prototype supports simple type names and inheritance only.
                parent_names = [re.split(r'[<\s]', p.strip())[0].split('.')[-1] for p in (parents or '').split(',') if p.strip()]
                if name in types:
                    print(f'[WARN] Duplicate simple type name {name}; first wins ({file})', file=sys.stderr)
                    continue
                types[name] = (kind, parent_names, str(file))
    return types

def get_name(cell):
    val = html.unescape(cell.get('value', ''))
    val = TAG_RE.sub('\n', val)
    lines = [s.strip() for s in val.splitlines() if s.strip()]
    for line in reversed(lines):
        if re.fullmatch(r'[A-Za-z_]\w*', line): return line
    return None

def sync_one(item, dry_run=False):
    target = ROOT / item['output']
    if not target.exists():
        raise FileNotFoundError(f'Create draw.io file first: {target}')
    tree = ET.parse(target)
    graph = tree.find('.//mxGraphModel/root')
    if graph is None: raise ValueError(f'Unsupported draw.io format: {target}')
    types = get_types(item['sources'])
    existing = {}
    for cell in graph.findall('mxCell'):
        if cell.get('vertex') != '1': continue
        if not ('swimlane' in cell.get('style','') or cell.get('uml-sync') == '1'): continue
        name = get_name(cell)
        if name: existing.setdefault(name, cell.get('id'))
    additions = 0
    for name, (kind, _, _) in sorted(types.items()):
        if name in existing: continue
        node_id = 'umlsync-' + uuid.uuid5(uuid.NAMESPACE_URL, item['output'] + ':' + name).hex[:16]
        cell = ET.SubElement(graph, 'mxCell', {
            'id': node_id, 'value': f'<<{kind}>>\n{name}',
            'style': 'rounded=1;whiteSpace=wrap;html=0;fillColor=#dae8fc;strokeColor=#6c8ebf;',
            'vertex': '1', 'parent': '1', 'uml-sync': '1'
        })
        ET.SubElement(cell, 'mxGeometry', {
            'x': str(2000 + (additions % 3) * 260), 'y': str(200 + (additions // 3) * 120),
            'width': '220', 'height': '80', 'as': 'geometry'
        })
        existing[name] = node_id
        additions += 1
    # Only add inheritance/implementation connections for types inside selected paths.
    # Do not recreate deleted/manual edges or connect outside the selected type set.
    known_edges = {(c.get('source'), c.get('target')) for c in graph.findall('mxCell') if c.get('edge') == '1'}
    edges = 0
    for name, (_, parents, _) in sorted(types.items()):
        for parent in parents:
            if parent not in types: continue
            pair = (existing[name], existing[parent])
            if pair in known_edges: continue
            edge_id = 'umlsync-edge-' + uuid.uuid5(uuid.NAMESPACE_URL, item['output'] + ':' + name + '->' + parent).hex[:16]
            edge = ET.SubElement(graph, 'mxCell', {
                'id': edge_id, 'style': 'endArrow=block;endFill=0;html=1;',
                'edge': '1', 'parent': '1', 'source': pair[0], 'target': pair[1], 'uml-sync': '1'
            })
            ET.SubElement(edge, 'mxGeometry', {'relative': '1', 'as': 'geometry'})
            known_edges.add(pair)
            edges += 1
    print(f'{item["output"]}: {len(types)} types, +{additions} nodes, +{edges} edges')
    if not dry_run and (additions or edges):
        tree.write(target, encoding='utf-8', xml_declaration=True)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()
    for item in json.loads(CONFIG.read_text(encoding='utf-8'))['diagrams']:
        sync_one(item, args.dry_run)
