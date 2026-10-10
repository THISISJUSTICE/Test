"""Synchronize C# type declarations with monochrome three-compartment draw.io UML.
Python 3.9+, standard library only. Back up your .drawio before first use.
Explicit source directories or individual .cs files; existing hand-made edges are preserved.
This is a lightweight C# parser, not Roslyn: complex declarations may need manual review.
"""
import argparse
import hashlib
import copy
import html
import heapq
import math
import json
import re
import sys
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = Path(__file__).with_name('config.json')
TYPE_RE = re.compile(r'\b(class|interface|struct|enum|record)\s+(?:class\s+|struct\s+)?([A-Za-z_]\w*)(?:\s*<[^>{}]+>)?\s*(?::\s*([^\{]+?))?\s*\{', re.S)
TAG_RE = re.compile(r'<[^>]*>')
VIS = {'public': '+', 'internal': '+', 'protected': '+', 'private': '+'}


def clean_source(source):
    # Preserve offsets and braces while masking comments and string/char literals.
    pattern = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
    return pattern.sub(lambda m: ''.join('\n' if c == '\n' else ' ' for c in m.group()), source)


def matching_brace(src, start):
    depth = 0
    for i in range(start, len(src)):
        if src[i] == '{': depth += 1
        elif src[i] == '}':
            depth -= 1
            if depth == 0: return i
    return len(src)


def split_top_level(text, delimiter=','):
    result, last = [], 0
    angles = parens = brackets = 0
    for i, ch in enumerate(text):
        if ch == '<': angles += 1
        elif ch == '>': angles = max(0, angles - 1)
        elif ch == '(': parens += 1
        elif ch == ')': parens = max(0, parens - 1)
        elif ch == '[': brackets += 1
        elif ch == ']': brackets = max(0, brackets - 1)
        elif ch == delimiter and not (angles or parens or brackets):
            result.append(text[last:i].strip()); last = i + 1
    result.append(text[last:].strip())
    return [x for x in result if x]


def direct_declarations(source, masked, start, end):
    # Read declarations at class-body depth 0, skipping method/property bodies.
    i = start
    while i < end:
        while i < end and masked[i].isspace(): i += 1
        if i >= end: break
        begin = i
        parens = brackets = angles = 0
        while i < end:
            c = masked[i]
            if c == '(': parens += 1
            elif c == ')': parens = max(0, parens - 1)
            elif c == '[': brackets += 1
            elif c == ']': brackets = max(0, brackets - 1)
            elif c == '<': angles += 1
            elif c == '>': angles = max(0, angles - 1)
            if not (parens or brackets or angles) and c in '{;': break
            i += 1
        header = source[begin:i].strip()
        if i >= end: break
        if masked[i] == '{':
            close = matching_brace(masked, i)
            body = source[i + 1:close]
            yield header, body, True
            i = close + 1
            # property initializer / expression-bodied members after a block
            while i < end and masked[i].isspace(): i += 1
            if i < end and masked[i] == ';': i += 1
        else:
            yield header, '', False
            i += 1


def parse_members(source, masked, begin, end, kind, type_name):
    members, methods = [], []
    if kind == 'enum':
        for val in split_top_level(source[begin:end]):
            name = re.match(r'\s*(\w+)', val)
            if name: members.append('+ ' + name.group(1))
        return members, methods
    for header, body, has_body in direct_declarations(source, masked, begin, end):
        h = re.sub(r'\[[^\]]*\]', ' ', header).strip()
        h = re.sub(r'\s+', ' ', h)
        if not h or re.search(r'\b(class|interface|struct|enum|record)\b', h): continue
        # Static/const declarations are intentionally excluded from the diagram.
        if re.search(r'\b(static|const)\b', h): continue
        tokens = re.findall(r'\b(public|private|protected|internal)\b', h)
        visibility = tokens[0] if tokens else ('public' if kind == 'interface' else 'private')
        mark = VIS[visibility]
        h = re.sub(r'\b(public|private|protected|internal|static|virtual|override|abstract|sealed|readonly|const|partial|new|async|extern|required|unsafe|volatile)\b', '', h)
        h = re.sub(r'\s+', ' ', h).strip()
        if '(' in h and ')' in h:
            if visibility not in ('public', 'internal') and kind != 'interface': continue
            match = re.search(r'([\w.<>]+)\s*\((.*)\)', h)
            if not match: continue
            name, args = match.groups()
            before = h[:match.start(1)].strip()
            ret = before.split()[-1] if before else ''
            params = []
            for arg in split_top_level(args):
                arg = arg.split('=')[0].strip()
                arg = re.sub(r'\b(ref|out|in|params|this|scoped)\b', '', arg).strip()
                pieces = arg.split()
                if len(pieces) >= 2: params.append(' '.join(pieces[:-1]))
                elif pieces: params.append(pieces[0])
            if name == type_name:  # Constructors are not UML methods.
                continue
            signature = f'+ {name}({", ".join(params)})'
            if ret and name != type_name: signature += f' : {ret}'
            methods.append(signature)
        else:
            # Auto/full property: body has get/set/init; expression-bodied property also supported.
            is_property = has_body and bool(re.search(r'\b(get|set|init)\b', body))
            if '=>' in h: h = h.split('=>')[0].strip()
            if '=' in h: h = h.split('=')[0].strip()
            match = re.match(r'(.+?)\s+([A-Za-z_]\w*)$', h)
            if match:
                typ, name = match.groups()
                members.append(f'+ {name} : {typ}')
            elif is_property:
                print(f'[WARN] Could not parse property: {header[:70]}', file=sys.stderr)
    return list(dict.fromkeys(members)), list(dict.fromkeys(methods))


def get_types(paths):
    types = {}
    for spec in paths:
        base = Path(spec['path'])
        if not base.is_absolute(): base = ROOT / base
        if base.is_file():
            if base.suffix.lower() != '.cs':
                print(f'[WARN] Source file is not C#: {base}', file=sys.stderr)
                continue
            files = [base]  # recursive applies only to directories.
        elif base.is_dir():
            files = base.rglob('*.cs') if spec.get('recursive', False) else base.glob('*.cs')
        else:
            print(f'[WARN] Missing source path: {base}', file=sys.stderr)
            continue
        for file in sorted(files):
            source = file.read_text(encoding='utf-8-sig')
            masked = clean_source(source)
            for match in TYPE_RE.finditer(masked):
                kind, name, parents = match.groups()
                # Inspect modifiers immediately preceding the declaration, not a prior member.
                line_start = masked.rfind('\n', 0, match.start()) + 1
                prefix = masked[line_start:match.start()]
                if re.search(r'\bstatic\b', prefix): continue
                # Avoid nested types in this prototype.
                # Nested class definitions require namespace/type-aware parsing.
                opening = match.end() - 1
                closing = matching_brace(masked, opening)
                parent_names = [re.split(r'[<\s]', p.strip())[0].split('.')[-1] for p in split_top_level(parents or '')]
                members, methods = parse_members(source, masked, opening + 1, closing, kind, name)
                if name in types:
                    print(f'[WARN] Duplicate type {name} in {file}; first wins', file=sys.stderr); continue
                types[name] = {'kind':kind, 'parents':parent_names, 'members':members, 'methods':methods,
                               'mono': 'MonoBehaviour' in parent_names, 'file':str(file)}
    suppress_inherited_members(types)
    return types


def suppress_inherited_members(types):
    """Hide members already declared on any parsed ancestor, including overrides/new.

    Fields/properties are matched by name, methods by name and parameter types.
    An overload with a different parameter list remains visible. Only ancestors
    included in this diagram's parsed sources can be inspected.
    """
    original_members = {name: tuple(info['members']) for name, info in types.items()}
    original_methods = {name: tuple(info['methods']) for name, info in types.items()}

    def member_key(member):
        # '+ PropertyName : Type' -- type may differ for a hiding declaration.
        return member.removeprefix('+ ').split(' : ', 1)[0].strip()

    def method_key(method):
        # '+ Method(Type, Type) : ReturnType' -- return type is not an overload key.
        return method.removeprefix('+ ').split(' : ', 1)[0].strip()

    def inherited_keys(name, visited):
        members, methods = set(), set()
        for parent in types[name]['parents']:
            if parent not in types or parent in visited:
                continue
            members.update(member_key(m) for m in original_members[parent])
            methods.update(method_key(m) for m in original_methods[parent])
            ancestor_members, ancestor_methods = inherited_keys(parent, visited | {parent})
            members.update(ancestor_members)
            methods.update(ancestor_methods)
        return members, methods

    for name, info in types.items():
        inherited_members, inherited_methods = inherited_keys(name, {name})
        info['members'] = [m for m in info['members'] if member_key(m) not in inherited_members]
        info['methods'] = [m for m in info['methods'] if method_key(m) not in inherited_methods]


def get_name(cell):
    val = html.unescape(cell.get('value', ''))
    val = re.sub(r'</(?:div|p)>|<br\s*/?>', '\n', val, flags=re.I)
    val = TAG_RE.sub('', val)
    lines = [x.strip() for x in val.splitlines() if x.strip()]
    for line in reversed(lines):
        if re.fullmatch(r'[A-Za-z_]\w*', line): return line
    return None


def title(info, name):
    stereotype = 'MonoBehaviour' if info['mono'] else (info['kind'] if info['kind'] != 'class' else None)
    return (f'<div>&lt;&lt;{stereotype}&gt;&gt;</div>' if stereotype else '') + '<h4 style="margin:0">' + html.escape(name) + '</h4>'


def lines_html(lines):
    return ''.join('<div style="height:20px;line-height:20px;margin:0;padding:0;white-space:nowrap">' + html.escape(x) + '</div>' for x in lines) if lines else ''


def geom(cell): return cell.find('mxGeometry')


def create_cell(graph, cell_id, value, style, parent, x, y, w, h):
    cell = ET.SubElement(graph, 'mxCell', {'id':cell_id, 'value':value, 'style':style,
                                           'vertex':'1', 'parent':parent, 'uml-sync':'1'})
    ET.SubElement(cell, 'mxGeometry', {'x':str(x),'y':str(y),'width':str(w),'height':str(h),'as':'geometry'})
    return cell


def set_section(graph, parent, existing, suffix, content, y, height, width):
    if existing is None:
        existing = create_cell(graph, parent.get('id') + '-' + suffix, content,
            'text;fontSize=14;strokeColor=#000000;fillColor=none;align=left;verticalAlign=middle;spacingTop=4;spacingBottom=4;spacingLeft=6;spacingRight=6;whiteSpace=wrap;overflow=hidden;html=1;',
            parent.get('id'), 0, y, width, height)
    else:
        existing.set('value', content)
        existing.set('style', 'text;fontSize=14;strokeColor=#000000;fillColor=none;align=left;verticalAlign=middle;spacingTop=4;spacingBottom=4;spacingLeft=6;spacingRight=6;whiteSpace=wrap;overflow=hidden;html=1;')
        g = geom(existing)
        if g is not None:
            g.set('x', '0'); g.set('y', str(y)); g.set('width', str(width)); g.set('height', str(height))
    return existing


def managed(cell):
    return cell.get('uml-sync') == '1' or (cell.get('id') or '').startswith(('umlsync-', 'umlsync-edge-'))


def remove_cell_and_children(graph, cell_id):
    """Remove an owned node, its children and edges incident to it."""
    ids = {cell_id}
    changed = True
    while changed:
        changed = False
        for c in graph.findall('mxCell'):
            if c.get('parent') in ids and c.get('id') not in ids:
                ids.add(c.get('id')); changed = True
    for c in list(graph.findall('mxCell')):
        if c.get('id') in ids or (c.get('edge') == '1' and (c.get('source') in ids or c.get('target') in ids)):
            graph.remove(c)


def clean_auto_edge(edge):
    """Avoid retaining stale fixed connection points or waypoints."""
    style = 'edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;dashed=1;endArrow=block;endFill=0;strokeColor=#000000;'
    edge.set('style', style)
    g = edge.find('mxGeometry')
    if g is not None:
        for child in list(g): g.remove(child)
        for attr in ('x','y','width','height'): g.attrib.pop(attr, None)
        g.set('relative','1')


def normalize_managed_node(cell):
    """Migrate old prototype rounded colored boxes to 3-section UML."""
    if 'swimlane' not in cell.get('style',''):
        cell.set('style', 'swimlane;fontSize=16;fontStyle=1;align=center;verticalAlign=middle;childLayout=stackLayout;horizontal=1;startSize=46;horizontalStack=0;resizeParent=1;resizeParentMax=0;resizeLast=0;collapsible=0;html=1;fillColor=none;swimlaneFillColor=none;strokeColor=#000000;')
        g = geom(cell)
        if g is not None and float(g.get('width','0')) < 300: g.set('width','320')


def estimate_width(name, info):
    """Estimate glyph advances at draw.io's default 12px font (not character count)."""
    import unicodedata
    texts = [name] + info['members'] + info['methods']
    label = 'MonoBehaviour' if info['mono'] else (info['kind'] if info['kind'] != 'class' else '')
    if label: texts.append('«' + label + '»')
    def pixels(text):
        width = 0.0
        for c in text:
            if unicodedata.east_asian_width(c) in ('F', 'W'): width += 12.0
            elif c in 'il.,:;!|\'` ': width += 3.4
            elif c in 'mwMW@%': width += 9.8
            elif c in '()[]{}<>': width += 4.5
            elif c.isupper(): width += 7.7
            elif c.isdigit(): width += 6.7
            else: width += 6.1
        return width
    # Preserve glyph-based sizing with a small 4% correction for body text.
    body_width = max((pixels(t) * (14 / 12) * 1.04 for t in info['members'] + info['methods']), default=0)
    # Heading 4 is bold and may render wider than the 12px baseline estimate.
    # Give titles their own font scaling and additional horizontal insets.
    title_width = max(pixels(name) * (16 / 12) * 1.18,
                      pixels('«' + label + '»') * (16 / 12) if label else 0)
    return max(72, int(math.ceil(max(body_width + 24, title_width + 36))))


def segment_intersects_rect(x1,y1,x2,y2,rx,ry,rw,rh):
    """Liang-Barsky segment/axis-aligned rectangle intersection."""
    dx=x2-x1; dy=y2-y1; lo=0.0; hi=1.0
    for p,q in ((-dx,x1-rx),(dx,rx+rw-x1),(-dy,y1-ry),(dy,ry+rh-y1)):
        if abs(p)<1e-9:
            if q<0: return False
        else:
            t=q/p
            if p<0: lo=max(lo,t)
            else: hi=min(hi,t)
            if lo>hi: return False
    return True


def auto_layout(graph, existing, types, desired, gap=65):
    """Deterministic relationship-centric layout; all synced nodes are movable."""
    names = sorted(types)
    adjacency = {n: set() for n in names}
    for a, b in desired:
        if a in adjacency and b in adjacency:
            adjacency[a].add(b); adjacency[b].add(a)
    # Local density reserves more routing corridors around high-degree vertices.
    degree = {n: len(adjacency[n]) for n in names}
    boxes = {}
    for name in names:
        g = geom(existing[name])
        boxes[name] = (float(g.get('width', 180)), float(g.get('height', 60)))
    # Cluster connected components. Within a cluster place high-degree hubs first,
    # then put neighbors around their connected already-placed vertices.
    remaining = set(names)
    components = []
    while remaining:
        start = max(remaining, key=lambda n: (len(adjacency[n]), n))
        stack, component = [start], set()
        while stack:
            n = stack.pop()
            if n in component: continue
            component.add(n)
            stack.extend(adjacency[n] - component)
        remaining -= component
        components.append(component)
    # Unconnected types are positioned as a compact header row above the graph,
    # rather than as distant single-node components on its right.
    isolated = sorted(n for n in names if not adjacency[n])
    components = [c for c in components if len(c) > 1]
    components.sort(key=lambda c: (-len(c), sorted(c)))
    locations = {}
    offset_x = 60.0
    for comp in components:
        placed = {}
        pending = set(comp)
        hub = max(pending, key=lambda n: (len(adjacency[n] & comp), n))
        placed[hub] = (0.0, 0.0)
        pending.remove(hub)
        while pending:
            name = max(pending, key=lambda n: (len(adjacency[n] & placed.keys()), len(adjacency[n]), n))
            neighbors = [n for n in adjacency[name] if n in placed]
            if neighbors:
                cx = sum(placed[n][0] + boxes[n][0] / 2 for n in neighbors) / len(neighbors)
                cy = sum(placed[n][1] + boxes[n][1] / 2 for n in neighbors) / len(neighbors)
            else:
                cx = cy = 0.0
            w, h = boxes[name]
            candidates = []
            # Cardinal and diagonal rings around the connected classes, rather than rows.
            for radius in range(1, 22):
                for dx, dy in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,1),(1,-1),(-1,-1)):
                    # Space is proportional to nearby edge density and the retry gap.
                    local_gap = gap + 8 * min(8, degree[name])
                    x = round((cx + dx * radius * (w / 2 + 75 + local_gap) - w / 2) / 10) * 10
                    y = round((cy + dy * radius * (h / 2 + 45 + local_gap) - h / 2) / 10) * 10
                    if any(x < px + pw + max(gap, local_gap, gap + 8 * min(8, degree[other])) and
                           x + w + max(gap, local_gap, gap + 8 * min(8, degree[other])) > px and
                           y < py + ph + max(gap, local_gap, gap + 8 * min(8, degree[other])) and
                           y + h + max(gap, local_gap, gap + 8 * min(8, degree[other])) > py
                           for other, (px, py) in placed.items()
                           for pw, ph in [boxes[other]]):
                        continue
                    length = sum(abs(x+w/2-(placed[n][0]+boxes[n][0]/2)) +
                                 abs(y+h/2-(placed[n][1]+boxes[n][1]/2)) for n in neighbors)
                    # Leaf nodes should remain next to their sole neighbor. Avoid
                    # placements that make a one-edge class unnecessarily remote.
                    # Also discourage routes whose center-to-center line crosses
                    # the footprint of an already placed, unrelated class.
                    obstruction = 0.0
                    for neighbor in neighbors:
                        sx = x + w/2; sy = y + h/2
                        ex = placed[neighbor][0] + boxes[neighbor][0]/2
                        ey = placed[neighbor][1] + boxes[neighbor][1]/2
                        for other, (px,py) in placed.items():
                            if other == neighbor: continue
                            pw,ph = boxes[other]
                            # Bounding-box overlap is a cheap pre-filter, then
                            # test the center segment against the rectangle.
                            if max(sx,ex) < px-15 or min(sx,ex) > px+pw+15 or max(sy,ey) < py-15 or min(sy,ey) > py+ph+15:
                                continue
                            if segment_intersects_rect(sx,sy,ex,ey,px-15,py-15,pw+30,ph+30):
                                obstruction += 300.0
                    spread = abs(x-cx) + abs(y-cy)
                    candidates.append((length + spread*.08 + obstruction, radius, y, x))
                if len(candidates) >= 8: break
            if not candidates:
                raise RuntimeError('Cannot place UML node ' + name)
            _, _, y, x = min(candidates)
            placed[name] = (x, y)
            pending.remove(name)
        min_x = min(x for x,y in placed.values())
        min_y = min(y for x,y in placed.values())
        max_x = max(x+boxes[n][0] for n,(x,y) in placed.items())
        for n, (x,y) in placed.items():
            locations[n] = (x - min_x + offset_x, y - min_y + 60)
        offset_x += max_x - min_x + 220
    if isolated:
        if locations:
            left = min(x for x, y in locations.values())
            right = max(x + boxes[n][0] for n, (x, y) in locations.items())
            top = min(y for x, y in locations.values())
        else:
            left, right, top = 60.0, 60.0, 60.0
        # Fill rows within the connected graph's width. No edge can enter the
        # isolated header band, so it cannot overlap routed relationships.
        row_limit = max(500.0, right - left)
        rows = []
        row, row_width, row_height = [], 0.0, 0.0
        spacing = 35.0
        for n in isolated:
            w, h = boxes[n]
            if row and row_width + spacing + w > row_limit:
                rows.append((row, row_width, row_height))
                row, row_width, row_height = [], 0.0, 0.0
            row.append(n)
            row_width += (spacing if len(row) > 1 else 0) + w
            row_height = max(row_height, h)
        if row: rows.append((row, row_width, row_height))
        header_height = sum(r[2] for r in rows) + 35.0 * max(0, len(rows)-1)
        # Shift connected graph down, preserving its existing edge-free geometry.
        shift = header_height + 115.0 if locations else 0.0
        locations = {n: (x, y + shift) for n, (x, y) in locations.items()}
        y = top
        for row, row_width, row_height in rows:
            x = left + max(0.0, (row_limit - row_width) / 2)
            for n in row:
                locations[n] = (x, y)
                x += boxes[n][0] + spacing
            y += row_height + 35.0
    for name, (x,y) in locations.items():
        g = geom(existing[name])
        g.set('x', str(round(x))); g.set('y', str(round(y)))
    return locations


def route_edges(graph, existing, desired, edges_by_pair, grid=10, clearance=35):
    """Orthogonal obstacle-aware routing with distinct perimeter ports and reserved lanes.

    Port stubs are always normal to the node border, preventing sideways arrowheads.
    Parallel tracks must stay apart; perpendicular crossings are allowed.
    """
    from collections import defaultdict
    rects = {n: tuple(float(geom(c).get(k, 0)) for k in ('x','y','width','height'))
             for n,c in existing.items()}
    if not rects or not edges_by_pair:
        return 0, []
    margin = 200
    bounds = (math.floor((min(r[0] for r in rects.values())-margin)/grid),
              math.floor((min(r[1] for r in rects.values())-margin)/grid),
              math.ceil((max(r[0]+r[2] for r in rects.values())+margin)/grid),
              math.ceil((max(r[1]+r[3] for r in rects.values())+margin)/grid))
    blocked = set()
    for x,y,w,h in rects.values():
        for gx in range(math.ceil((x-clearance)/grid), math.floor((x+w+clearance)/grid)+1):
            for gy in range(math.ceil((y-clearance)/grid), math.floor((y+h+clearance)/grid)+1):
                blocked.add((gx,gy))

    # Assign an independent perimeter slot to each incident edge. Slots are not
    # shared, including when several edges point at the same class.
    degree = defaultdict(int)
    for a,b in edges_by_pair:
        degree[a]+=1; degree[b]+=1
    # Reserve short/leaf connections first. They have fewer reasonable routing
    # alternatives; long multi-hub edges can use the remaining corridors.
    pairs = sorted(edges_by_pair, key=lambda p: (
        min(degree[p[0]], degree[p[1]]) != 1,
        abs(rects[p[0]][0]+rects[p[0]][2]/2-rects[p[1]][0]-rects[p[1]][2]/2) +
        abs(rects[p[0]][1]+rects[p[0]][3]/2-rects[p[1]][1]-rects[p[1]][3]/2), p))
    incidents = defaultdict(list)
    for a,b in pairs:
        for node,other in ((a,b),(b,a)):
            x,y,w,h=rects[node]; ox,oy,ow,oh=rects[other]
            cx,cy=x+w/2,y+h/2
            tx,ty=ox+ow/2,oy+oh/2
            dx,dy=tx-cx,ty-cy
            # Select the border facing the other class, not the least crowded
            # border regardless of direction. The secondary facing side is
            # available when the direct side has too many incident arrows.
            if abs(dx)>=abs(dy):
                primary='right' if dx>=0 else 'left'
                secondary='bottom' if dy>=0 else 'top'
            else:
                primary='bottom' if dy>=0 else 'top'
                secondary='right' if dx>=0 else 'left'
            incidents[node].append(((a,b),primary,secondary,dx,dy))
    assignments = {}
    for node,entries in incidents.items():
        x,y,w,h=rects[node]
        groups=defaultdict(list)
        # Prioritize edges with the clearest directional preference, leaving
        # neighboring perpendicular faces as overflow when ports get crowded.
        entries.sort(key=lambda e: (-max(abs(e[3]),abs(e[4]))/(min(abs(e[3]),abs(e[4]))+1),e[0]))
        for pair,primary,secondary,dx,dy in entries:
            def side_cost(side):
                length = h if side in ('left','right') else w
                count=len(groups[side])+1
                # 24px is the desired port-to-port separation. A congested
                # facing edge can spill to its adjacent edge, never to the
                # opposite (backward-facing) side.
                overcrowd=max(0.0, count*24.0-max(1.0,length-20.0))
                facing_penalty=0.0 if side==primary else 32.0
                return facing_penalty + overcrowd*3.0 + count*4.0
            side=min((primary,secondary),key=side_cost)
            groups[side].append((pair,dx,dy))
        for side,group in groups.items():
            length=h if side in ('left','right') else w
            # Sort ports by their other endpoint's projected position. This
            # prevents neighboring arrows from crossing near their origin.
            group.sort(key=lambda entry: (
                (rects[entry[0][1] if entry[0][0]==node else entry[0][0]][1] +
                 rects[entry[0][1] if entry[0][0]==node else entry[0][0]][3]/2)
                if side in ('left','right') else
                (rects[entry[0][1] if entry[0][0]==node else entry[0][0]][0] +
                 rects[entry[0][1] if entry[0][0]==node else entry[0][0]][2]/2),entry[0]))
            for i,(pair,_,_) in enumerate(group):
                frac=(i+1)/(len(group)+1)
                offset=length*frac
                if side in ('left','right'):
                    tangent=round((y+offset)/grid)*grid
                    tangent=min(y+h-10,max(y+10,tangent))
                    frac=(tangent-y)/h
                    if side=='left':attach=(x,tangent);direction=(-1,0);rel=(0,frac)
                    else:attach=(x+w,tangent);direction=(1,0);rel=(1,frac)
                else:
                    tangent=round((x+offset)/grid)*grid
                    tangent=min(x+w-10,max(x+10,tangent))
                    frac=(tangent-x)/w
                    if side=='top':attach=(tangent,y);direction=(0,-1);rel=(frac,0)
                    else:attach=(tangent,y+h);direction=(0,1);rel=(frac,1)
                assignments[(pair,node)]=(attach,direction,rel)

    occupied_h=set(); occupied_v=set()
    dirs=((1,0),(-1,0),(0,1),(0,-1))
    def cells_of(a,b):
        if a[1]==b[1]:
            return ('h',[(a[1],i) for i in range(min(a[0],b[0]),max(a[0],b[0]))])
        return ('v',[(a[0],i) for i in range(min(a[1],b[1]),max(a[1],b[1]))])
    def conflict(a,b,radius):
        axis,cells=cells_of(a,b)
        # A perpendicular intersection at a point is legal; parallel proximity is not.
        if axis=='h':
            return any((yy,xx) in occupied_h for y,x in cells for yy in range(y-radius,y+radius+1) for xx in (x,))
        return any((xx,y) in occupied_v for x,y in cells for xx in range(x-radius,x+radius+1))
    def reserve(a,b):
        axis,cells=cells_of(a,b)
        (occupied_h if axis=='h' else occupied_v).update(cells)
    def to_grid(p): return (round(p[0]/grid),round(p[1]/grid))
    def stub(attach,direction):
        # A stub guarantees the last segment approaches the border perpendicularly.
        return to_grid((attach[0]+direction[0]*50,attach[1]+direction[1]*50))
    def search(start,goal,start_dir,end_dir,radius):
        # Constrain the first and final path segment to be perpendicular to the box.
        first = (start[0]+start_dir[0],start[1]+start_dir[1])
        before_goal=(goal[0]+end_dir[0],goal[1]+end_dir[1])
        if first in blocked or before_goal in blocked or first==start:
            return None
        if conflict(start,first,radius) or conflict(before_goal,goal,radius):
            return None
        def heuristic(x,y): return (abs(x-before_goal[0])+abs(y-before_goal[1]))*10
        def crossing_cost(a,b):
            # Orthogonal crossings are legal, but avoid them where a comparable
            # non-crossing route exists. Do not penalize merely touching an
            # adjacent grid cell along the same line (handled by conflict).
            if a[1] == b[1]:
                yy=a[1]; xx=min(a[0],b[0])
                return 55 if any((xx+k, yy) in occupied_v for k in (0,1)) else 0
            xx=a[0]; yy=min(a[1],b[1])
            return 55 if any((yy+k, xx) in occupied_h for k in (0,1)) else 0
        start_state=(first[0],first[1],dirs.index(start_dir))
        # Optimize route length first, with a strong penalty for each bend.
        # Tie-break equal scores by fewer bends, then shorter actual distance.
        BEND_PENALTY = 100  # grid-cost units: equivalent to 100 px at grid=10
        queue=[(heuristic(*first),0,0,0,0,start_state)]
        distances={start_state:(0,0,0)};prev={};count=1;expanded=0
        while queue and expanded<35000:
            _,cost,bends,steps,_,state=heapq.heappop(queue)
            if (cost,bends,steps)!=distances.get(state):continue
            expanded+=1
            x,y,d=state
            if (x,y)==before_goal:
                path=[(x,y)]
                while state in prev:
                    state=prev[state];path.append((state[0],state[1]))
                path.reverse()
                return [start]+path+[goal]
            for nd,(dx,dy) in enumerate(dirs):
                nx,ny=x+dx,y+dy
                if not(bounds[0]<=nx<=bounds[2] and bounds[1]<=ny<=bounds[3]):continue
                if (nx,ny) in blocked or conflict((x,y),(nx,ny),radius):continue
                # Avoid immediate reversal and favor straighter paths.
                if nd == (d ^ 1 if d < 2 else 5 - d):
                    continue
                bent = int(nd != d)
                new_bends = bends + bent
                new_steps = steps + 1
                new = cost + 10 + (BEND_PENALTY if bent else 0) + crossing_cost((x,y),(nx,ny))
                nxt=(nx,ny,nd)
                score=(new,new_bends,new_steps)
                if score>=distances.get(nxt,(float('inf'),)*3):continue
                distances[nxt]=score;prev[nxt]=state
                heapq.heappush(queue,(new+heuristic(nx,ny),new,new_bends,new_steps,count,nxt));count+=1
        return None

    routed=0
    excessive_detours = []
    for pair in pairs:
        a,b=pair
        sa,sd,srel=assignments[(pair,a)]
        ta,td,trel=assignments[(pair,b)]
        start=stub(sa,sd);goal=stub(ta,td)
        # The two stubs themselves must be kept clear of previously routed edges.
        result=None; chosen_radius=None
        for radius in (4,3,2):  # 40/30/20px minimum parallel clearance
            if conflict(to_grid(sa),start,radius) or conflict(goal,to_grid(ta),radius):
                continue
            result=search(start,goal,sd,td,radius)
            if result is not None:
                chosen_radius=radius;break
        if result is None:
            raise RoutingFailure(f'No clear route for {a} -> {b}')
        full=[to_grid(sa)]+result+[to_grid(ta)]
        route_length = sum((abs(v[0]-u[0])+abs(v[1]-u[1]))*grid for u,v in zip(full,full[1:]))
        direct_length = abs(sa[0]-ta[0]) + abs(sa[1]-ta[1])
        if direct_length > 100 and route_length > max(direct_length * 3.5, direct_length + 450):
            excessive_detours.append((a,b,round(route_length/direct_length,1)))
        # Reserve complete routes, including approach segments, not only the middle.
        for u,v in zip(full,full[1:]):
            if u!=v:reserve(u,v)
        turns=[result[0]]
        for i in range(1,len(result)-1):
            p,q,r=result[i-1],result[i],result[i+1]
            if (q[0]-p[0],q[1]-p[1])!=(r[0]-q[0],r[1]-q[1]):
                turns.append(q)
        turns.append(result[-1])
        # Explicit exact-coordinate stubs preserve the approach direction at the
        # attachment. Rounded grid endpoints are NOT attachment coordinates.
        start_stub=(sa[0]+sd[0]*50, sa[1]+sd[1]*50)
        end_stub=(ta[0]+td[0]*50, ta[1]+td[1]*50)
        waypoints=[start_stub]
        waypoints += [(gx*grid,gy*grid) for gx,gy in turns]
        waypoints.append(end_stub)
        # Drop repeated points, keeping precise port-adjacent stubs.
        waypoints=[v for i,v in enumerate(waypoints) if i==0 or v!=waypoints[i-1]]
        # Remove collinear waypoints, but retain both precise outer stubs to
        # guarantee a perpendicular entry/exit at the class boundary.
        if len(waypoints)>4:
            compact=[waypoints[0]]
            for i in range(1,len(waypoints)-1):
                prev_pt=compact[-1]; cur=waypoints[i]; nxt=waypoints[i+1]
                collinear=((abs(prev_pt[0]-cur[0])<1e-6 and abs(cur[0]-nxt[0])<1e-6)
                           or (abs(prev_pt[1]-cur[1])<1e-6 and abs(cur[1]-nxt[1])<1e-6))
                if i not in (1,len(waypoints)-2) and collinear:
                    continue
                compact.append(cur)
            compact.append(waypoints[-1])
            waypoints=compact
        edge=edges_by_pair[pair]
        inheritance=desired[pair]=='inheritance'
        style=('dashed=1;dashPattern=6 4;endArrow=block;endFill=0;' if inheritance
               else 'dashed=0;endArrow=classic;endFill=1;')
        edge.set('style', 'edgeStyle=none;rounded=0;html=1;strokeColor=#000000;'
                 +style+f'exitX={srel[0]:.6f};exitY={srel[1]:.6f};exitDx=0;exitDy=0;exitPerimeter=0;'
                 +f'entryX={trel[0]:.6f};entryY={trel[1]:.6f};entryDx=0;entryDy=0;entryPerimeter=0;')
        geometry=edge.find('mxGeometry')
        for child in list(geometry):geometry.remove(child)
        points=ET.SubElement(geometry,'Array',{'as':'points'})
        for px,py in waypoints:
            ET.SubElement(points,'mxPoint',{'x':str(round(px,3)),'y':str(round(py,3))})
        routed+=1
    return routed, excessive_detours

class RoutingFailure(RuntimeError):
    pass

def remove_legacy_demo_nodes(graph, types):
    """Remove only the two known colored placeholder cards shipped with Test.
    Never remove a real, user-authored UML node or a current source type.
    """
    removed = 0
    for c in list(graph.findall('mxCell')):
        if c.get('vertex') != '1' or c.get('parent') != '1': continue
        if c.get('id') not in ('2', '3'): continue
        if c.get('value') not in ('GolemCore', 'GolemData'): continue
        if 'rounded=1' not in c.get('style', '') or 'fillColor=#' not in c.get('style',''): continue
        if c.get('value') in types: continue
        remove_cell_and_children(graph, c.get('id'))
        removed += 1
    return removed


def sync_one(item, dry_run=False):
    target = ROOT / item['output']
    if target.exists():
        tree = ET.parse(target)
    else:
        # A separate draw.io document is created for each configured output.
        mxfile = ET.Element('mxfile', {'host': 'app.diagrams.net', 'type': 'device'})
        diagram = ET.SubElement(mxfile, 'diagram', {'id': 'umlsync-page', 'name': 'Page-1'})
        model = ET.SubElement(diagram, 'mxGraphModel', {
            'dx': '1200', 'dy': '800', 'grid': '1', 'gridSize': '10',
            'guides': '1', 'tooltips': '1', 'connect': '1', 'arrows': '1',
            'fold': '1', 'page': '1', 'pageScale': '1', 'pageWidth': '1600',
            'pageHeight': '1200', 'math': '0', 'shadow': '0'})
        root = ET.SubElement(model, 'root')
        ET.SubElement(root, 'mxCell', {'id': '0'})
        ET.SubElement(root, 'mxCell', {'id': '1', 'parent': '0'})
        tree = ET.ElementTree(mxfile)
        print(f'[INFO] Creating missing diagram: {item["output"]}', flush=True)
    graph = tree.find('.//mxGraphModel/root')
    if graph is None: raise ValueError(f'Unsupported draw.io format: {target}')
    types = get_types(item['sources'])
    print(f'[INFO] Parsed {len(types)} C# types; building diagram...', flush=True)
    by_id = {c.get('id'): c for c in graph.findall('mxCell')}
    existing = {}
    removed = remove_legacy_demo_nodes(graph, types)
    # Only purge elements that this tool owns. Manually authored UML remains intact.
    for c in list(graph.findall('mxCell')):
        if c.get('vertex') != '1' or not managed(c): continue
        if c.get('parent') != '1': continue
        name = get_name(c)
        if name not in types:
            remove_cell_and_children(graph, c.get('id')); removed += 1
    for c in graph.findall('mxCell'):
        if c.get('vertex') != '1' or c.get('parent') != '1': continue
        if 'swimlane' not in c.get('style', '') and not managed(c): continue
        name = get_name(c)
        if name:
            # Prefer existing hand-authored nodes if duplicates are present.
            if name not in existing or (managed(existing[name]) and not managed(c)):
                existing[name] = c
    # Remove duplicate automatically generated nodes if a manually authored one exists.
    for c in list(graph.findall('mxCell')):
        if c.get('vertex') != '1' or c.get('parent') != '1' or not managed(c): continue
        name = get_name(c)
        if name in existing and existing[name] is not c:
            remove_cell_and_children(graph, c.get('id')); removed += 1
    for c in existing.values():
        if managed(c): normalize_managed_node(c)
    # Existing manually drawn UML nodes are not relocated.
    additions = updates = 0
    for name, info in sorted(types.items()):
        cell = existing.get(name)
        is_new = cell is None
        if is_new:
            id_ = 'umlsync-' + uuid.uuid5(uuid.NAMESPACE_URL, item['output'] + ':' + name).hex[:16]
            cell = create_cell(graph, id_, title(info,name),
                'swimlane;fontSize=16;fontStyle=1;align=center;verticalAlign=middle;childLayout=stackLayout;horizontal=1;startSize=46;horizontalStack=0;resizeParent=0;resizeLast=0;collapsible=0;html=1;fillColor=none;swimlaneFillColor=none;strokeColor=#000000;',
                '1', 0, 0, 180, 40)
            existing[name] = cell
            additions += 1
        else:
            if cell.get('value') != title(info,name):
                cell.set('value', title(info,name)); updates += 1
        if not managed(cell):
            # Do not destructively overwrite a manually authored multi-cell diagram.
            continue
        normalize_managed_node(cell)
        cell.set('style', 'swimlane;fontSize=16;fontStyle=1;align=center;verticalAlign=middle;childLayout=stackLayout;horizontal=1;startSize=46;horizontalStack=0;resizeParent=0;resizeLast=0;collapsible=0;html=1;fillColor=none;swimlaneFillColor=none;strokeColor=#000000;')
        children = [c for c in graph.findall('mxCell') if c.get('parent') == cell.get('id') and c.get('vertex') == '1']
        children.sort(key=lambda c: float(geom(c).get('y','0')) if geom(c) is not None else 0)
        old_sections = [(c.get('id'), c.get('value'), c.get('style'), dict(geom(c).attrib) if geom(c) is not None else {}) for c in children]
        width = estimate_width(name, info)
        member_h = 20 * len(info['members']) + 16 if info['members'] else 0
        method_h = 20 * len(info['methods']) + 16 if info['methods'] else 0
        sections = []
        if info['members']: sections.append(('members', lines_html(info['members']), member_h))
        if info['methods']: sections.append(('methods', lines_html(info['methods']), method_h))
        if not sections: sections.append(('empty', '', 22))
        # Remove stale/empty child cells, so empty member sections consume zero height.
        for child in children:
            graph.remove(child)
        y = 46
        for suffix, content, height in sections:
            set_section(graph, cell, None, suffix, content, y, height, width)
            y += height
        g = geom(cell)
        if g is not None:
            before = (g.get('width'), g.get('height'))
            g.set('width', str(width)); g.set('height', str(y))
            if not is_new and before != (str(width),str(y)): updates += 1
        new_sections = [(c.get('id'), c.get('value'), c.get('style'), dict(geom(c).attrib) if geom(c) is not None else {}) for c in graph.findall('mxCell') if c.get('parent') == cell.get('id') and c.get('vertex') == '1']
        if not is_new and old_sections != new_sections: updates += 1
    # Repack all generated nodes based on final (variable) widths and heights.
    # Existing handmade nodes remain at their original positions.
    # Relationship edges: inheritance and member-type references within selected folders only.
    # Edges attach to the top-level swimlane vertex, never to its text sections.
    desired = {}
    for name, info in sorted(types.items()):
        for parent in info['parents']:
            if parent in types and parent != name:
                desired[(name, parent)] = 'inheritance'
        for member in info['members']:
            # Member format: + fieldName : TypeName; resolve simple/generic type identifiers.
            member_type = member.partition(' : ')[2]
            for referenced in re.findall(r'\b[A-Za-z_]\w*\b', member_type):
                if referenced in types and referenced != name and (name, referenced) not in desired:
                    desired[(name, referenced)] = 'reference'
    by_node_id = {cell.get('id'): name for name, cell in existing.items()}
    # Preserve manually created edges; reconcile only tool-managed ones.
    manual_pairs = {(by_node_id.get(e.get('source')), by_node_id.get(e.get('target')))
                    for e in graph.findall('mxCell') if e.get('edge') == '1' and not managed(e)}
    managed_edges = {}
    removed_edges = 0
    for edge in list(graph.findall('mxCell')):
        if edge.get('edge') != '1' or not managed(edge): continue
        pair = (by_node_id.get(edge.get('source')), by_node_id.get(edge.get('target')))
        if pair not in desired or pair in manual_pairs or pair in managed_edges:
            graph.remove(edge); removed += 1; removed_edges += 1
        else:
            managed_edges[pair] = edge
    # Recompute ALL positions from relationships, including existing managed nodes.
    edges = 0
    edge_style = ('edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;'
                  'jettySize=auto;html=1;endArrow=classic;endFill=1;strokeColor=#000000;'
                  'exitPerimeter=1;entryPerimeter=1;')
    edges_by_pair = {}
    for (source_name, target_name), relationship in sorted(desired.items()):
        if (source_name, target_name) in manual_pairs: continue
        edge = managed_edges.get((source_name, target_name))
        if edge is None:
            edge_id = 'umlsync-edge-' + uuid.uuid5(uuid.NAMESPACE_URL,
                item['output'] + ':' + source_name + '->' + target_name).hex[:16]
            edge = ET.SubElement(graph, 'mxCell', {'id':edge_id, 'edge':'1',
                'parent':'1', 'uml-sync':'1'})
            ET.SubElement(edge, 'mxGeometry', {'relative':'1', 'as':'geometry'})
            edges += 1
        edge.set('source', existing[source_name].get('id'))
        edge.set('target', existing[target_name].get('id'))
        edge.set('style', edge_style)
        geometry = edge.find('mxGeometry')
        if geometry is None: geometry = ET.SubElement(edge, 'mxGeometry')
        for child in list(geometry): geometry.remove(child)
        for attr in ('x','y','width','height'): geometry.attrib.pop(attr, None)
        geometry.set('relative','1'); geometry.set('as','geometry')
        edges_by_pair[(source_name,target_name)] = edge
    # The graph is only written after a complete successful routing pass.
    # Re-layout on failure, gradually opening corridors; no invalid partial XML is saved.
    attempts = (85, 120, 165, 220, 300)
    routed = 0
    for attempt, gap in enumerate(attempts, 1):
        auto_layout(graph, existing, types, desired, gap=gap)
        try:
            routed, detours = route_edges(graph, existing, desired, edges_by_pair)
        except RoutingFailure as exc:
            print(f'[WARN] Layout {attempt}/{len(attempts)} (gap={gap}px): {exc}; expanding spacing...', flush=True)
            continue
        if detours and attempt < len(attempts):
            print(f'[WARN] Layout {attempt}/{len(attempts)}: {len(detours)} excessive detours; expanding spacing...', flush=True)
            continue
        if detours:
            print(f'[WARN] {len(detours)} long routes remain: {detours[:4]}', flush=True)
        break
    else:
        raise RoutingFailure(f'Unable to route after {len(attempts)} spacing attempts; last gap={attempts[-1]}px')
    print(f'{item["output"]}: {len(types)} types, +{additions} nodes, ~{updates} updates, +{edges} edges, {routed} routed, -{removed} obsolete elements')
    if not dry_run:
        target.parent.mkdir(parents=True, exist_ok=True)
        tree.write(target,encoding='utf-8',xml_declaration=True)

def diagram_references(items):
    """Find direct cross-diagram type dependencies, without transitive traversal.

    A relationship is resolved locally when both endpoints are in this diagram.
    Shared types do not, by their mere presence, link all containing diagrams.
    The same relationship rules as the draw.io renderer are used: base types
    and types named by visible fields/properties (not method signatures).
    """
    diagram_types = {item['output']: get_types(item['sources']) for item in items}
    owners = {}
    for path, types in diagram_types.items():
        for name in types:
            owners.setdefault(name, set()).add(path)

    references = {}
    for path, types in diagram_types.items():
        linked = set()
        for name, info in types.items():
            dependencies = set(info['parents'])
            for member in info['members']:
                member_type = member.partition(' : ')[2]
                dependencies.update(re.findall(r'\b[A-Za-z_]\w*\b', member_type))
            for dependency in dependencies:
                if dependency == name or dependency in types:
                    continue  # Already represented locally; do not follow into other diagrams.
                linked.update(owners.get(dependency, ()) - {path})
        references[path] = sorted(linked, key=str.casefold)
    return references


def sync_all(config, dry_run=False):
    """Synchronize every configured output, then prune unlisted .drawio files.

    Cleanup recursively removes unconfigured .drawio files under Docs only.
    Other file types and directories outside Docs are never deleted.
    """
    items = config.get('diagrams', [])
    if not isinstance(items, list):
        raise ValueError('config.json: diagrams must be a list')
    targets = set()
    for item in items:
        if not isinstance(item, dict) or not item.get('output') or not isinstance(item.get('sources'), list):
            raise ValueError('Each diagram needs output and sources')
        target = (ROOT / item['output']).resolve()
        if not target.is_relative_to(ROOT.resolve()):
            raise ValueError(f'Output must be inside project root: {target}')
        if target.suffix.lower() != '.drawio':
            raise ValueError(f'Output must end with .drawio: {target}')
        if target in targets:
            raise ValueError(f'Duplicate diagram output: {target}')
        targets.add(target)

    # Resolve cross-diagram links from direct relationships, not shared-type transitivity.
    references = diagram_references(items)

    # Generate everything first; do not delete old diagrams on a failed sync.
    for item in items:
        sync_one(item, dry_run)

    # Prune every unconfigured .drawio under Docs, including nested folders.
    # Restrict deletion to Docs; other project directories are not touched.
    docs_dir = ROOT / 'Docs'
    manifest_path = docs_dir / 'diagrams.json'
    if docs_dir.exists():
        for candidate in sorted(docs_dir.rglob('*')):
            if (candidate.is_file() and candidate.suffix.lower() == '.drawio'
                    and candidate.resolve() not in targets):
                print(f'[CLEAN] {"Would remove" if dry_run else "Removing"} unconfigured diagram: {candidate.relative_to(ROOT)}', flush=True)
                if not dry_run:
                    candidate.unlink()

    # Build the gallery manifest from configured outputs, not from stale files.
    # During dry-run, targets may not exist yet; still show the intended list.
    diagram_paths = sorted(
        (target.relative_to(ROOT).as_posix() for target in targets
         if target.is_relative_to(docs_dir.resolve())),
        key=str.casefold
    )
    if not dry_run:
        missing = [path for path in diagram_paths if not (ROOT / path).is_file()]
        if missing:
            raise FileNotFoundError(f'Configured diagrams missing after sync: {missing}')
    # Content hashes change only when each diagram's bytes change.
    # The browser can cache unchanged diagrams while refreshing edited ones.
    versions = {
        path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest()[:16]
        for path in diagram_paths
    } if not dry_run else {}
    # Group diagrams by directory below Docs; files directly under Docs are ungrouped.
    groups = {}
    for path in diagram_paths:
        relative_parent = (Path(path).parent.relative_to('Docs')).as_posix()
        group = '' if relative_parent == '.' else relative_parent
        groups.setdefault(group, []).append(path)
    manifest_content = json.dumps({'diagrams': diagram_paths, 'versions': versions, 'groups': groups, 'references': references}, ensure_ascii=False, indent=2) + '\n'
    if dry_run:
        print(f'[DRY-RUN] Would update {manifest_path.relative_to(ROOT)} with {len(diagram_paths)} diagrams', flush=True)
    else:
        docs_dir.mkdir(parents=True, exist_ok=True)
        if not manifest_path.exists() or manifest_path.read_text(encoding='utf-8') != manifest_content:
            manifest_path.write_text(manifest_content, encoding='utf-8')
        print(f'[INFO] Updated {manifest_path.relative_to(ROOT)}: {len(diagram_paths)} diagrams', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()
    sync_all(json.loads(CONFIG.read_text(encoding='utf-8')), args.dry_run)
