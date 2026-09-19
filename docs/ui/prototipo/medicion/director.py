"""Simulación en papel del director de presentación con la gramática baseline de la ronda 2.
No es código del juego: solo cuenta qué pasaría. Duraciones en ticks (15/s)."""
import json, collections as C

DUR = {1: 15, 2: 22, 3: 45}          # N1 ~1 s, N2 ~1,5 s, N3 ~3 s (baseline)
STALE = 22                           # caducidad baseline: 1,5 s esperando -> residuo
PAUSE_SECONDS = {3: 3.0, 4: 4.0}     # lo que dura la presentación que congela (supuesto, para el tiempo real)

def classify(e):
    t, tick, team, a, tg, op, ph, d = e
    base, cancelled = d.split(":")[0], d.endswith(":cancelled")
    if t == "PerkTriggered": return ("mark", 1, False)
    if t == "Foul": return ("stamp", 1, False)
    if t == "ConsumableUsed": return ("stamp", 1, False)
    if t == "Injury" and cancelled: return ("stamp", 1, False)
    if t == "Injury" and base == "minor": return ("stamp", 2, False)
    if t == "Card" and base == "yellow": return ("stamp", 2, False)
    if t == "Card": return ("panel", 3, False)                       # roja: viñeta, sin pausa
    if t == "Goal": return ("panel", 3, True)                        # gol: viñeta y pausa breve
    if t == "Injury": return ("panel", 3, team == 0)                 # grave: pausa si hay decisión (nuestra)
    if t == "MobStart": return ("band", 3, False)
    if t == "RefereeLeaves": return ("panel", 3, False)
    if t == "Death": return ("scene", 4, True)
    if t == "MatchEnd": return ("scene", 4, True)
    if t == "Substitution": return ("residue", 0, False)
    return None

def people(e): return {x for x in (e[3], e[4], e[5]) if x >= 0}
CHAIN = {("Goal", "MatchEnd"), ("MobStart", "RefereeLeaves"), ("Death", "MatchEnd"), ("Injury", "MatchEnd"), ("Card", "MatchEnd")}

def moments(ev, W):
    """Agrupa sucesos de nivel >= 1 (no marcas) y sustituciones en momentos."""
    out = []
    for e in ev:
        c = classify(e)
        if c is None or c[0] == "mark" or e[0] == "MatchStart": continue
        cur = out[-1] if out else None
        joins = cur is not None and e[1] - cur["last"] <= W and (
            (people(e) & cur["people"] and cur["team_of"].get(e[0], e[2]) is not None)
            or any((t0, e[0]) in CHAIN for t0 in cur["types"]))
        if joins:
            cur["ev"].append(e); cur["last"] = e[1]; cur["people"] |= people(e); cur["types"].add(e[0])
            cur["level"] = max(cur["level"], c[1]); cur["pause"] |= c[2]
            if c[1] >= cur["lead_level"]: cur["lead"], cur["lead_level"], cur["kind"] = e, c[1], c[0]
        else:
            out.append({"t": e[1], "last": e[1], "ev": [e], "people": people(e), "types": {e[0]}, "team_of": {},
                        "level": c[1], "pause": c[2], "lead": e, "lead_level": c[1], "kind": c[0]})
    for mo in out:
        mo["decision"] = any(x[0] == "Substitution" and x[2] == 0 for x in mo["ev"])
        mo["pause"] |= mo["decision"]
        mo["level"] = max(mo["level"], 1) if mo["level"] == 0 else mo["level"]
        mo["ours"] = mo["lead"][2] == 0
    return out

def marks(ev, mos, W):
    """Marcas de perk; las del mismo jugador que coinciden con un momento se absorben como subtítulo."""
    res = []
    for e in ev:
        if e[0] != "PerkTriggered": continue
        absorbed = any(abs(e[1] - mo["t"]) <= W and e[3] in mo["people"] for mo in mos)
        res.append((e[1], e[3], e[2], e[7], absorbed))
    return res

def timeline(mos, speed):
    """Un solo canal de voz alta (N3/N4) con cola y caducidad; sellos N1/N2 en su propio canal.
    speed: 1, 4, 16. Degradación baseline: x4 baja un nivel salvo N4; x16 solo N4 (comprimido)."""
    scale = speed  # 1 s real = 15*speed ticks de partido
    stats = C.Counter(); voice_busy_until = -1; queue = []; shown = []
    for mo in sorted(mos, key=lambda m: m["t"]):
        lvl = mo["level"]
        if speed == 4 and lvl < 4: lvl -= 1
        if speed == 16 and lvl < 4: lvl = 0
        if lvl <= 0: stats["solo_residuo"] += 1; continue
        pause = mo["pause"] and (speed == 1 or lvl == 4)
        if lvl <= 2:
            stats[f"sello_N{lvl}"] += 1; shown.append((mo["t"], lvl, DUR[lvl] * scale, False)); continue
        # voz alta
        if pause:
            stats[f"N{lvl}_con_pausa"] += 1; shown.append((mo["t"], lvl, 0, True)); voice_busy_until = mo["t"]; continue
        if mo["t"] < voice_busy_until:
            wait = voice_busy_until - mo["t"]
            if wait > STALE * scale: stats["caduca_a_residuo"] += 1; continue
            stats["espera_en_cola"] += 1
        stats[f"N{lvl}_sin_pausa"] += 1
        start = max(mo["t"], voice_busy_until); voice_busy_until = start + DUR[3] * scale
        shown.append((start, lvl, DUR[3] * scale, False))
    return stats, shown
