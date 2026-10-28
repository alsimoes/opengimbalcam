"""List the OSC messages and Lua scripts of a TouchOSC layout (.tosc = zlib-compressed XML).

usage: tosc_dump.py "OBSBOT Sample-UDP.tosc" [--scripts]

OBSBOT Center ships two samples in data/ctrl/touchosc/. Paths are built from CONSTANT
partials and the control's own PROPERTY partials (tag, parent.tag, name).
"""
import sys
import xml.etree.ElementTree as ET
import zlib

path = sys.argv[1]
root = ET.fromstring(zlib.decompress(open(path, 'rb').read())).find('node')


def props(n):
    return {p.findtext('key'): p.findtext('value') for p in n.findall('./properties/property')}


seen = set()


def walk(n, parent):
    pr = props(n)
    env = {'name': pr.get('name'), 'tag': pr.get('tag'),
           'parent.tag': (parent or {}).get('tag'), 'parent.name': (parent or {}).get('name')}
    for osc in n.findall('./messages/osc'):
        addr = ''.join((p.findtext('value') or '') if p.findtext('type') == 'CONSTANT'
                       else str(env.get(p.findtext('value'), '?' + p.findtext('value')))
                       for p in osc.findall('./path/partial'))
        args = []
        for a in osc.findall('./arguments/partial'):
            t, c, v = a.findtext('type'), a.findtext('conversion'), a.findtext('value')
            args.append(f'{c[0]}={v}' if t == 'CONSTANT' else f"{c[0]}:{v}[{a.findtext('scaleMin')}..{a.findtext('scaleMax')}]")
        trig = ','.join(f"{t.findtext('var')}{t.findtext('condition')}" for t in osc.findall('./triggers/trigger'))
        key = (addr, tuple(args), osc.findtext('send'))
        if key in seen:
            continue
        seen.add(key)
        direction = ('S' if osc.findtext('send') == '1' else '-') + ('R' if osc.findtext('receive') == '1' else '-')
        print(f"{addr:52} {' '.join(args):22} {direction} {trig:10} [{pr.get('name')}]")
    if '--scripts' in sys.argv and pr.get('script'):
        print(f"--- script de {pr.get('name')}:\n{pr['script']}\n---")
    for c in n.findall('./children/node'):
        walk(c, pr)


walk(root, None)
