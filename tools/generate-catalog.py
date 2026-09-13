"""
Genera docs/catalogo-perks-y-objetos.md a partir de /data.

Las descripciones de los perks NO se escriben aquí (RT-035): salen de
Sim.Perks.DescriptionGenerator a través de `Balance --describe es`.

    dotnet run --project Balance -c Release -- --describe es > /tmp/describe-es.txt
    python3 tools/generate-catalog.py /tmp/describe-es.txt

Todo lo demás (rarezas, arquetipos, atributos, familias, letalidad) se lee de los
JSON de /data, que son la fuente de verdad (RF-065, RT-031).
"""

import json, glob, os, re, io, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, 'data')
DESC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'out', 'describe-es.txt')
OUT  = os.path.join(ROOT, 'docs', 'catalogo-perks-y-objetos.md')

# --- descripciones generadas por Sim.Perks.DescriptionGenerator (RT-035) ---
desc = {}
for line in open(DESC, encoding='utf-8').read().splitlines()[1:]:
    if not line.strip() or line.startswith('catálogo:') or line.startswith('  '):
        continue
    parts = re.split(r'\s{2,}', line.rstrip())
    if len(parts) >= 5 and re.fullmatch(r'[a-z0-9_]+', parts[0]):
        desc[parts[0]] = parts[4].strip()

perks = [json.load(open(f, encoding='utf-8')) for f in sorted(glob.glob(f'{DATA}/perks/*.json'))]
items = [json.load(open(f, encoding='utf-8')) for f in sorted(glob.glob(f'{DATA}/items/*.json'))]
cons  = [json.load(open(f, encoding='utf-8')) for f in sorted(glob.glob(f'{DATA}/consumables/*.json'))]
races = [json.load(open(f, encoding='utf-8')) for f in sorted(glob.glob(f'{DATA}/races/*.json'))]
equip = json.load(open(f'{DATA}/equipment/equipment.json', encoding='utf-8'))

racial = {r['ability']: r for r in races}
race_es = {r['id']: r['name']['es'] for r in races}

RAR = {'common': 'Común', 'uncommon': 'Poco común', 'rare': 'Raro', 'legendary': 'Legendario'}
KIND = {'filler': 'relleno', 'conditional': 'condicional', 'ruleBreaker': 'rompe-reglas'}
FAM = {'wall': 'La Muralla', 'craft': 'El Toque', 'aim': 'La Puntería', 'butchery': 'La Carnicería'}
ARCH = {'normal': 'normal', 'cursed': 'maldito', 'fragile': 'frágil', 'restricted': 'restringido'}
ATTR = {'strength': 'fuerza', 'speed': 'velocidad', 'technique': 'técnica', 'stamina': 'resistencia', 'leash': 'correa'}
MASTERS = {'blood_tithe', 'first_touch_school', 'granite_line', 'killing_range'}

def req(p):
    bits = []
    if p.get('race'):
        bits.append(f"solo {race_es.get(p['race'], p['race'])}")
    for t in p.get('tagsRequired', []):
        if t not in (p.get('race') or ''):
            bits.append(f'etiqueta `{t}`')
    if p.get('positionOnly'):
        bits.append('solo portero' if p['positionOnly'] == 'Goalkeeper' else f"solo {p['positionOnly']}")
    return ', '.join(bits) or '—'

def perk_row(p):
    mark = ' ☠' if p.get('lethal') else (' ★' if p['id'] in MASTERS else '')
    return f"| **{p['name']['es']}**{mark} | `{p['id']}` | {RAR.get(p.get('rarity'),'—')} | {p.get('minAct')} | {KIND.get(p.get('kind'),'—')} | {req(p)} | {desc.get(p['id'],'')} |"

HEAD = '| Nombre | id | Rareza | Acto | Tipo | Requisitos | Qué hace |\n|---|---|---|---|---|---|---|'

o = io.StringIO()
w = o.write

w("""# Catálogo de perks, objetos y consumibles

> Generado desde `/data` con `tools/generate-catalog.py` el 13 sep 2026. Las descripciones de los perks son las que produce
> `Sim.Perks.DescriptionGenerator` (RT-035, `dotnet run --project Balance -c Release -- --describe es`):
> no hay texto de efecto escrito a mano. Este documento es **derivado**; la fuente de verdad son los
> ficheros JSON de `/data` (RF-065, RT-031).

## Cómo se leen los números

Un perk no suma puntos porcentuales: **multiplica la cuota** del canal (`odds = p/(1−p)`, ADR 0050 P1).
Por eso el texto dice «multiplica por 2 sus opciones de robar el balón» y no «+15 %». Los valores legales
son `±15, ±30, ±50, ±100, ±200, ±300, ±500` en `/data`, que se leen como `k = 1 + n/100` y su inverso
exacto. El techo lo compra la rareza (ADR 0058): común ×2 · poco común ×3 · raro ×4 · legendario ×6.

Marcas: **☠** perk letal (puede matar; ADR 0046 y 0048) · **★** maestro de línea (ADR 0051: exige dos
perks de su línea y cierra otra para el resto de la run).

""")

# --- Resumen ---
tot = len(perks)
w('## Resumen\n\n')
w('| Categoría | Cantidad |\n|---|---|\n')
w(f'| Perks (total en `/data/perks`) | {tot} |\n')
w(f'| — habilidades raciales (automáticas, no ocupan slot) | {len(racial)} |\n')
w(f'| — perks obtenibles | {tot - len(racial)} |\n')
w(f"| — letales | {sum(1 for p in perks if p.get('lethal'))} |\n")
w(f'| Objetos de equipamiento | {len(items)} |\n')
w(f'| Consumibles | {len(cons)} |\n\n')

by_kind = {}
for p in perks:
    by_kind[p['kind']] = by_kind.get(p['kind'], 0) + 1
w('Distribución RF-069 (objetivo 60/30/10 ± 8): ')
w(' · '.join(f"{KIND[k]} {v} ({100*v/tot:.1f} %)".replace('.', ',') for k, v in
             sorted(by_kind.items(), key=lambda kv: -kv[1])) + '\n\n')

# --- Habilidades raciales ---
w('## Habilidades raciales\n\nUna por raza (RF-031b, ADR 0026). Se asignan solas a toda la plantilla de esa raza, son gratis, irrenunciables y **no ocupan slot de perk**.\n\n')
w(HEAD + '\n')
for p in perks:
    if p['id'] in racial:
        w(perk_row(p) + '\n')
w('\n')

# --- Líneas de build ---
w('## Perks por línea de build (ADR 0051)\n\nCuatro líneas con un maestro cada una. El maestro exige llevar ya dos perks de su línea y **cierra la línea opuesta** para el resto de la run.\n\n')
for fam in ('butchery', 'craft', 'wall', 'aim'):
    grp = [p for p in perks if p.get('family') == fam and p['id'] not in racial]
    grp.sort(key=lambda p: (p['id'] not in MASTERS, ['common','uncommon','rare','legendary'].index(p['rarity']), p['id']))
    w(f'### {FAM[fam]} (`{fam}`) — {len(grp)} perks\n\n')
    w(HEAD + '\n')
    for p in grp:
        w(perk_row(p) + '\n')
    w('\n')

# --- Sin línea ---
rest = [p for p in perks if not p.get('family') and p['id'] not in racial]
rest.sort(key=lambda p: (['common','uncommon','rare','legendary'].index(p['rarity']), p['id']))
w(f'## Perks sin línea — {len(rest)}\n\nNo cuentan para ningún maestro ni cierran nada.\n\n')
w(HEAD + '\n')
for p in rest:
    w(perk_row(p) + '\n')
w('\n')

# --- Letales ---
leth = [p for p in perks if p.get('lethal')]
leth.sort(key=lambda p: -p['lethalChance'])
w("""## Perks letales ☠

Los únicos perks que pueden matar (ADR 0046 vía 2, ADR 0048). Los cuatro cubren canales distintos, cada
activación marca a **un solo rival** —el que peor lo tiene— y todos se cobran **en la jugada**, nunca en el
saque (regla del cargador).

`lethalChance` es la probabilidad **base** de la tirada, en base 10.000 (900 = 9 %). Sobre ella actúan
multiplicativamente las tres cosas que el jugador decide antes de confirmar la alineación (RF-012c,
`data/sim/tuning.json`):

- **en qué estado alinea**: sano ×1, tocado ×8, lesión grave ×25;
- **a quién alinea**: fuerza del portador contra aguante de la víctima (`relativeFactor` 6);
- **dónde lo coloca**: distancia de emparejamiento entre portador y víctima.

La tirada final está acotada a `maxChance` 8.000 (80 %): al tope, un tocado marcado no vuelve. Por eso el
9.000 de *La segunda herida* no es «90 % de muertes»: es el letal de **menor conversión** de los cuatro,
porque exige una segunda lesión sobre el mismo jugador y el marcador tiene que ir empatado o por debajo.

""")
w('| Nombre | id | Rareza | Acto | Canal / disparador | `lethalChance` base | Qué hace |\n|---|---|---|---|---|---|---|\n')
for p in leth:
    w(f"| **{p['name']['es']}** | `{p['id']}` | {RAR[p['rarity']]} | {p['minAct']} | {p['trigger']} | {p['lethalChance']} ({p['lethalChance']/100:.1f} %".replace('.', ',') + f") | {desc.get(p['id'],'')} |\n")
w('\n')

# --- Objetos ---
w("""## Objetos de equipamiento

Un objeto **sube atributos y nada más** (ADR 0036). La rareza fija **cuántos** atributos toca
(común 1 · poco común 2 · raro 3 · legendario 4), la magnitud base es siempre **±10**, y el arquetipo
cambia la magnitud o el riesgo:

| Arquetipo | Qué cambia |
|---|---|
| normal | sin contrapartida |
| maldito | ×2 la magnitud, en lo que sube **y** en lo que baja (±20) |
| frágil | magnitud normal, pero se rompe con `breakChancePercent` por partido; cuesta el 55 % y aparece el doble de veces en la tienda |
| restringido | sin rareza, 3 atributos a magnitud normal, **solo para una raza**; abre una build que esa raza no puede permitirse por su sesgo racial |

Valor marginal medido por atributo (milésimas de punto de tasa de victoria por cada +20 repartidos entre los diez jugadores, fase 1b, ADR 0038): """)
w(' · '.join(f"{ATTR[k]} {v}" for k, v in equip['marginalValuePerAttribute'].items()) + '. Es la tabla con la que se calcula el precio en vez de medirlo.\n\n')

def bonus_str(d):
    return ', '.join(f"{'+' if v > 0 else ''}{v} {ATTR[k]}" for k, v in d['attributeBonus'].items())

def note(d):
    doc = (d.get('_doc') or '').strip()
    if doc:
        doc = doc.replace('\n', ' ')
        return doc[:300].rstrip() + ('…' if len(doc) > 300 else '')
    return '—'

for arch, title, extra in (
    ('normal', 'Normales', ''),
    ('cursed', 'Malditos', 'Suben el doble y bajan el doble. Excelentes en el jugador correcto y ruinosos en el resto: la decisión es a quién se lo pones.'),
    ('fragile', 'Frágiles', 'Baratos y frecuentes, pero se rompen.'),
    ('restricted', 'Restringidos por raza', 'Sin rareza y sin contrapartida: la restricción **es** el coste. Cada uno abre la build que el sesgo racial le niega a esa raza.'),
):
    grp = [d for d in items if d['archetype'] == arch]
    grp.sort(key=lambda d: (d.get('race') or '', ['common','uncommon','rare','legendary'].index(d['rarity']) if d.get('rarity') else 0, d['id']))
    w(f'### {title} — {len(grp)} objetos\n\n')
    if extra:
        w(extra + '\n\n')
    if arch == 'fragile':
        w('| Nombre | id | Rareza | Acto | Atributos | Rotura/partido |\n|---|---|---|---|---|---|\n')
        for d in grp:
            w(f"| **{d['name']['es']}** | `{d['id']}` | {RAR[d['rarity']]} | {d['minAct']} | {bonus_str(d)} | {d['breakChancePercent']} % |\n")
    elif arch == 'restricted':
        w('| Nombre | id | Raza | Acto | Atributos | Qué abre |\n|---|---|---|---|---|---|\n')
        for d in grp:
            w(f"| **{d['name']['es']}** | `{d['id']}` | {race_es.get(d['race'], d['race'])} | {d['minAct']} | {bonus_str(d)} | {note(d)} |\n")
    else:
        w('| Nombre | id | Rareza | Acto | Atributos | Nota |\n|---|---|---|---|---|---|\n')
        for d in grp:
            w(f"| **{d['name']['es']}** | `{d['id']}` | {RAR[d['rarity']]} | {d['minAct']} | {bonus_str(d)} | {note(d)} |\n")
    w('\n')

# --- Consumibles ---
w("""## Consumibles

Un solo partido, se gastan al usarlos. El efecto **no tiene portador**: lo usa el entrenador y alcanza a
**todos los jugadores de su equipo que estén en el campo** en ese instante, hasta el final del partido
(`EffectEngine.ResolveConsumables`, RF-080..082).

""")
w('| Nombre | id | Rareza | Familia | Efecto |\n|---|---|---|---|---|\n')
PROB = {'injure': 'lesionar', 'foul': 'hacer falta', 'shotOnTarget': 'tirar a puerta', 'tackleEvasion': 'resistir entradas'}
FAMC = {'medical': 'médica', 'dirty': 'sucia', 'supernatural': 'sobrenatural', 'tactical': 'táctica'}
for d in sorted(cons, key=lambda d: d['id']):
    eff = []
    for e in d['effects']:
        if e['type'] == 'modifyAttribute':
            eff.append(f"{'+' if e['value'] > 0 else ''}{e['value']} {ATTR[e['attribute']]}")
        else:
            v = e['value']
            k = 1 + abs(v) / 100
            verbo = 'multiplica' if v > 0 else 'divide'
            eff.append(f"{verbo} por {str(k).replace('.', ',').rstrip(',0') if k % 1 else int(k)} sus opciones de {PROB.get(e['probability'], e['probability'])}")
    warn = ' ⚠' if d['id'] == 'field_bandage' else ''
    w(f"| **{d['name']['es']}** | `{d['id']}` | {RAR[d['rarity']]} | {FAMC.get(d['family'], d['family'])} | {'; '.join(eff)}{warn} |\n")
w('\n')

w("""⚠ **Posible error de datos en `field_bandage`.** Usa el canal `injure`, que en el motor es la
probabilidad de que el *entrante* lesione (`MatchEngine`: `Odds(tackler, Injure)` contra
`Odds(victim, Injury)`). Aplicado a todo el equipo propio, un vendaje reduce a la mitad la capacidad
de **lesionar al rival**, no la de **lesionarse**, que sería el canal `injury`. Anotado en
`docs/pendientes.md` (CAT-A).

## Cómo regenerar este documento

```bash
dotnet run --project Balance -c Release -- --describe es > out/describe-es.txt   # RT-035
python3 tools/generate-catalog.py out/describe-es.txt
```

El resto sale de leer `/data/perks`, `/data/items`, `/data/consumables`, `/data/races` y
`/data/equipment/equipment.json`.
""")

open(OUT, 'w', encoding='utf-8').write(o.getvalue())
print('escrito', OUT, len(o.getvalue()), 'bytes')
