import json, sys, collections as C
for path in sys.argv[1:]:
    n=0; cnt=C.Counter(); det=C.Counter(); ticks=0; us=C.Counter()
    for line in open(path):
        m=json.loads(line); n+=1; ticks+=m["ticks"]
        for t,tick,team,a,tg,op,ph,d in m["ev"]:
            cnt[t]+=1
            if t in ("Foul","Card","Injury","Death","Substitution","MatchEnd","Goal","Tackle","Shot","ConsumableUsed","MobStart","RefereeLeaves","Save","ShotBlocked","AerialDuel","DribbleWon","DribbleLost"):
                det[(t,d.split(":")[0] if t!="Death" else d.split(":")[0])]+=1
            if t in ("Goal","Injury","Death","Card","Foul","PerkTriggered","ConsumableUsed") and team==0: us[t]+=1
    print(f"== {path.split('/')[-1]}: {n} partidos · duración media {ticks/n/15:.1f} s a 1x")
    for t,c in cnt.most_common(): print(f"  {t:18s} {c/n:7.2f}/partido   (nuestro equipo: {us[t]/n:.2f})" if t in us else f"  {t:18s} {c/n:7.2f}/partido")
    print("  -- detalles")
    for (t,d),c in sorted(det.items()): print(f"  {t:14s} {d:16s} {c/n:7.3f}")
