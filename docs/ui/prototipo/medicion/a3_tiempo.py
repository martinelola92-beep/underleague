import json, sys, collections as C
sys.path.insert(0, sys.argv[1]); from director import *
W = 15
def pct(v, p): v = sorted(v); return v[min(len(v)-1, int(p*len(v)))] if v else float('nan')
for path in sys.argv[2:]:
    n = 0; st = {1: C.Counter(), 4: C.Counter(), 16: C.Counter()}; real = {1: 0.0, 4: 0.0, 16: 0.0}; shown_n = {1: 0, 4: 0, 16: 0}
    gaps_any = []; gaps_n3 = []; burst5 = 0; maxmarks = []; marks_hist = C.Counter(); stack = C.Counter(); mk_total = mk_abs = 0
    mk_ours = 0; rep5 = 0; quiet_frac = []; stamps_overlap = 0; per_min = []; mk_per_min = []; perk_ids = C.Counter(); matches_perks = []
    for line in open(path):
        m = json.loads(line); n += 1; ev = m["ev"]; T = m["ticks"]
        mos = moments(ev, W); mks = marks(ev, mos, W)
        for sp in (1, 4, 16):
            s, shown = timeline(mos, sp); st[sp].update(s); shown_n[sp] += len(shown)
            pauses = sum(PAUSE_SECONDS[lv] for (_, lv, _, p) in shown if p)
            real[sp] += T / 15 / sp + pauses
            if sp == 1:
                ts = sorted(t for (t, *_ ) in shown)
                gaps_any += [(b - a) / 15 for a, b in zip(ts, ts[1:])]
                t3 = sorted(t for (t, lv, _, _) in shown if lv >= 3)
                gaps_n3 += [(b - a) / 15 for a, b in zip([0] + t3, t3 + [T])]
                burst5 += sum(1 for i in range(len(ts) - 2) if ts[i + 2] - ts[i] <= 75)
                # sellos N1/N2 que se pisan entre sí
                st_ = sorted((t, t + d) for (t, lv, d, p) in shown if lv <= 2)
                stamps_overlap += sum(1 for (a, b), (c, d) in zip(st_, st_[1:]) if c < b)
                busy = set()
                for (t, lv, d, p) in shown:
                    busy.update(range(t, t + (d if d else 0)))
                quiet_frac.append(1 - len(busy & set(range(T))) / T)
                per_min.append(len(shown) / (T / 15 / 60 + pauses / 60))
        # marcas de perk (solo x1)
        vis = [(t, p, tm, pk) for (t, p, tm, pk, ab) in mks if not ab]
        mk_total += len(mks); mk_abs += len(mks) - len(vis); mk_ours += sum(1 for v in vis if v[2] == 0)
        mk_per_min.append(len(vis) / (T / 15 / 60))
        active = C.Counter()
        for (t, p, tm, pk) in vis:
            for k in range(t, t + 15): active[k] += 1
        mx = max(active.values()) if active else 0; maxmarks.append(mx)
        for k in range(T): marks_hist[active.get(k, 0)] += 1
        last = {}
        for (t, p, tm, pk) in vis:
            if (p, pk) in last and t - last[(p, pk)] <= 75: rep5 += 1
            last[(p, pk)] = t
            perk_ids[pk] += 1
        per_player = C.Counter((p, t // 15) for (t, p, tm, pk) in vis)
        stack.update(v for v in per_player.values())
    print(f"=== {path.split('/')[-1]} · {n} partidos · ventana de agrupamiento {W/15:.1f} s")
    for sp in (1, 4, 16):
        print(f"  x{sp:<2d} presentaciones {shown_n[sp]/n:5.2f}/partido · tiempo real {real[sp]/n:5.1f} s · " + ", ".join(f"{k} {v/n:.2f}" for k, v in sorted(st[sp].items())))
    print(f"  x1 presentaciones por minuto real: p10 {pct(per_min,.1):.1f} · mediana {pct(per_min,.5):.1f} · p90 {pct(per_min,.9):.1f}")
    print(f"  x1 distancia entre presentaciones (cualquier nivel): p10 {pct(gaps_any,.1):.1f}s · mediana {pct(gaps_any,.5):.1f}s · p90 {pct(gaps_any,.9):.1f}s")
    print(f"  x1 silencio entre voces altas (N3/N4, incluye inicio y final): p10 {pct(gaps_n3,.1):.1f}s · mediana {pct(gaps_n3,.5):.1f}s · p90 {pct(gaps_n3,.9):.1f}s")
    print(f"  x1 ráfagas (3 presentaciones en ≤5 s): {burst5/n:.2f}/partido · sellos que se pisan: {stamps_overlap/n:.2f}/partido")
    print(f"  x1 fracción del partido sin ninguna presentación de voz: mediana {pct(quiet_frac,.5):.2f}")
    print(f"  marcas de perk: {mk_total/n:.2f}/partido, absorbidas en un momento {mk_abs/n:.2f}, visibles {(mk_total-mk_abs)/n:.2f} (nuestras {mk_ours/n:.2f}) · por minuto de partido mediana {pct(mk_per_min,.5):.1f} p90 {pct(mk_per_min,.9):.1f}")
    tot = sum(marks_hist.values())
    print("  marcas simultáneas (fracción de ticks): " + " ".join(f"{k}:{v/tot:.3f}" for k, v in sorted(marks_hist.items())) + f" · máximo por partido p50 {pct(maxmarks,.5)} p90 {pct(maxmarks,.9)} máx {max(maxmarks)}")
    print(f"  repetición (mismo perk y jugador en ≤5 s): {rep5/n:.2f}/partido · apilamiento en el mismo segundo por jugador: {dict(sorted(stack.items()))}")
    top = perk_ids.most_common(8); tv = sum(perk_ids.values())
    print("  perks que más marcas producen: " + ", ".join(f"{k} {v/tv:.0%}" for k, v in top) + f" · los 8 primeros = {sum(v for _,v in top)/tv:.0%} de {len(perk_ids)} perks distintos")
