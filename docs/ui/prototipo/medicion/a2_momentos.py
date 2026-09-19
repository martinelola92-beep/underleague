import json, sys, collections as C
sys.path.insert(0, sys.argv[1]); from director import *
paths = sys.argv[2:]
for W in (0, 8, 15, 30):
    for path in paths:
        n = 0; nsuc = 0; nmom = 0; lv = C.Counter(); lv_ours = C.Counter(); fused = 0; pauses = C.Counter(); dec = 0
        for line in open(path):
            m = json.loads(line); n += 1
            mos = moments(m["ev"], W)
            for mo in mos:
                nmom += 1; nsuc += len([x for x in mo["ev"] if x[0] != "Substitution"])
                lv[mo["level"]] += 1; lv_ours[mo["level"]] += mo["ours"]
                if len({x[0] for x in mo["ev"]}) > 1: fused += 1
                if mo["pause"]: pauses[mo["lead"][0]] += 1
                dec += mo["decision"]
        print(f"W={W:2d} ({W/15:.2f}s) {path.split('/')[-1]:12s} sucesos-voz {nsuc/n:5.2f} -> momentos {nmom/n:5.2f}/partido | fusionados {fused/n:.2f} | "
              + " ".join(f"N{k}={lv[k]/n:.2f}(nuestros {lv_ours[k]/n:.2f})" for k in sorted(lv))
              + f" | pausas {sum(pauses.values())/n:.2f} {dict((k, round(v/n,2)) for k,v in pauses.items())} | decisiones {dec/n:.2f}")
