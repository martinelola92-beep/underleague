"""
Genera las cinco referencias neutras de la puerta de fase 1 (ADR 0104, cierra CAT-E).

La regla vive aquí y no en la cabeza de nadie: si el catálogo de perks o la tabla de
valores cambian, se vuelve a ejecutar y las referencias se recalculan solas.

    python3 tools/build-neutral-reference.py            # imprime la seleccion, no toca nada
    python3 tools/build-neutral-reference.py --write     # la aplica a las cinco referencias

Por defecto SOLO IMPRIME. Hasta el 19 sep 2026 este docstring decia que escribia
las builds y el codigo no escribia nada (BB-S): al borrar un perk que estaba en
las cinco referencias, la herramienta imprimio la seleccion nueva y los ficheros
se quedaron con el perk borrado dentro.

Escribir esta detras de --write a proposito, no por comodidad: estas cinco builds
son la LINEA BASE contra la que miden las 43 puertas, asi que reescribirlas mueve
el baseline de todo el balance. Eso se hace mirando, no de pasada.
"""

import json, glob, os, statistics

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
D = lambda *p: os.path.join(ROOT, *p)

PERKS = 14          # los mismos que lleva cualquier build (RF-023: dos slots por titular)
MAX_PER_FAMILY = 2  # para que ninguna línea acumule cuota sobre el mismo canal

vals = json.load(open(D('data', 'economy', 'perk-values.json'), encoding='utf-8'))['values']
perks = {json.load(open(f, encoding='utf-8'))['id']: json.load(open(f, encoding='utf-8'))
         for f in glob.glob(D('data', 'perks', '*.json'))}
racials = {json.load(open(f, encoding='utf-8'))['ability'] for f in glob.glob(D('data', 'races', '*.json'))}


def eligible():
    """Lo que puede formar parte de una referencia NEUTRA, y por qué se descarta el resto."""
    out = []
    for pid, value in vals.items():
        p = perks.get(pid)
        if not p or pid in racials:
            continue
        if p.get('race') or p.get('positionOnly') or p.get('tagsRequired'):
            continue                      # bloqueado: en un portador cualquiera se desperdicia
        if p.get('lethal'):
            continue                      # la letalidad es una elección de build, no el estado neutro
        if p.get('requires'):
            continue                      # maestro: exige dos perks de su línea, que es coherencia
        if p.get('kind') == 'ruleBreaker':
            continue                      # un rompe-reglas no es "lo normal"
        if 'linked(' in (p.get('condition') or ''):
            continue                      # depende de la COLOCACIÓN, que es justo lo que la puerta mide
        out.append((pid, value, p.get('family')))
    return out


def select():
    pool = eligible()
    median = statistics.median([v for _, v, _ in pool])
    pool.sort(key=lambda t: (abs(t[1] - median), t[0]))   # desempate por id: determinista (RT-041)
    chosen, per_family = [], {}
    for pid, value, family in pool:
        if family and per_family.get(family, 0) >= MAX_PER_FAMILY:
            continue
        chosen.append((pid, value, family))
        if family:
            per_family[family] = per_family.get(family, 0) + 1
        if len(chosen) == PERKS:
            break
    if len(chosen) < PERKS:
        raise SystemExit(f'solo {len(chosen)} perks elegibles de {PERKS}: revisa los filtros')
    return median, chosen


def apply(chosen):
    """Reescribe la lista de perks de las cinco referencias, conservando todo lo demas."""
    ids = [pid for pid, _, _ in chosen]
    written = []
    for path in sorted(glob.glob(D('data', 'balance', 'builds', '*_neutral.json'))):
        build = json.load(open(path, encoding='utf-8'))
        slots = [entry['slot'] for entry in build['perks']]
        if len(slots) != len(ids):
            raise SystemExit(
                f'{os.path.basename(path)} tiene {len(slots)} huecos y la seleccion trae {len(ids)}: '
                'no se toca nada hasta que cuadren')
        build['perks'] = [{'slot': slot, 'perk': pid} for slot, pid in zip(slots, ids)]
        with open(path, 'w', encoding='utf-8') as handle:
            handle.write(json.dumps(build, ensure_ascii=False, indent=2) + '\n')
        written.append(os.path.basename(path))
    return written


if __name__ == '__main__':
    import sys

    median, chosen = select()
    print(f'mediana del catálogo elegible: {median} · suma de los {PERKS}: {sum(v for _, v, _ in chosen)}')
    for pid, value, family in chosen:
        print(f'  {pid:24} {value:5}  {family}')

    if '--write' in sys.argv:
        for name in apply(chosen):
            print(f'  escrita {name}')
        print('Recuerda: has movido la linea base de las 43 puertas. Ejecutalas.')
    else:
        print('(solo impreso; --write para aplicarlo a las cinco referencias)')
