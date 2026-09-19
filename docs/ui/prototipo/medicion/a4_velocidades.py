"""C7: comparar políticas de presentación por velocidad sobre los partidos de la fase A.
Tiempos en segundos REALES de pantalla. Las decisiones se cuentan aparte (dependen del jugador)."""
import json, sys, collections as C
sys.path.insert(0, sys.argv[1]); from director import moments
W = 15
# política: por nivel -> (se muestra?, segundos reales, congela?)
POL = {
 "x1":            {"speed":1,  1:(True,1.0,False), 2:(True,1.5,False), 3:(True,3.0,True),  4:(True,4.0,True)},
 "x4 actual":     {"speed":4,  1:(True,1.0,False), 2:(True,1.5,False), 3:(True,3.0,True),  4:(True,4.0,True)},
 "x4-A comprimido":{"speed":4, 1:(False,0,False),  2:(True,0.8,False), 3:(True,1.2,True),  4:(True,2.0,True)},
 "x4-B residual": {"speed":4,  1:(False,0,False),  2:(False,0,False),  3:(False,0,False), 4:(True,1.5,True)},
 "x16":           {"speed":16, 1:(False,0,False),  2:(False,0,False),  3:(False,0,False), 4:(True,1.0,True)},
}
def pct(v,p): v=sorted(v); return v[min(len(v)-1,int(p*len(v)))]
for path in sys.argv[2:]:
    ms=[json.loads(l) for l in open(path)]
    print(f"== {path.split('/')[-1]} · {len(ms)} partidos (decisiones del jugador aparte: pausan siempre, duración = la del jugador)")
    print(f"{'política':18s} {'real s':>7s} {'congelado':>10s} {'% cong.':>8s} {'present./min':>13s} {'N3 vistos':>10s} {'goles vistos':>12s}")
    for name,pol in POL.items():
        real=[];frozen=[];dens=[];n3=0;goals=0;dec=0
        for m in ms:
            play=m["ticks"]/15/pol["speed"]; fr=0; shown=0
            for mo in moments(m["ev"],W):
                lv=mo["level"]; show,dur,freeze=pol[lv]
                if mo["decision"]: dec+=1
                if not show: continue
                shown+=1
                if lv==3: n3+=1
                if any(e[0]=="Goal" for e in mo["ev"]): goals+=1
                if freeze and (mo["pause"] or lv==4): fr+=dur
            t=play+fr; real.append(t); frozen.append(fr); dens.append(shown/(t/60))
        n=len(ms)
        print(f"{name:18s} {sum(real)/n:7.1f} {sum(frozen)/n:10.1f} {100*sum(frozen)/sum(real):7.0f}% {pct(dens,.5):6.1f} (p90 {pct(dens,.9):4.1f}) {n3/n:10.2f} {goals/n:12.2f}")
    print(f"decisiones del jugador por partido: {dec/n:.2f}")
