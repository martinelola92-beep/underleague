# Piloto: OpenCode como ejecutor

Pregunta: ¿delegar la ejecución en OpenCode (modelos gratuitos) reduce el consumo de Claude sin subir las
vueltas de corrección frente a `fast-worker`? Skill: `opencode-worker`. Decisión tras 3–5 encargos reales:
extender a `/Sim`, quedarse en `/data`/tests/docs, o abandonar.

## Preparación (19 sep 2026)

- OpenCode 1.18.31 en WSL (`~/.npm-global/bin/opencode`), sin credenciales: solo modelos gratuitos de Zen.
- `AGENTS.md` tiene precedencia sobre `CLAUDE.md` para OpenCode — CONFIRMED (fichero de prueba con ambos).
- `opencode.json`: las reglas `deny` de bash y `external_directory` se aplican sin interfaz — CONFIRMED.
- El proceso puede no salir tras escribir el informe — CONFIRMED; el script espera la línea `NOTAS:`.
- Cinco ejecuciones en paralelo se colgaron sin salida; dos funcionan — causa no aislada (LIKELY: límite de
  concurrencia del plan gratuito).
- Prueba de humo en el repo: encargo con una tentación fuera de alcance (`README.md`); el ejecutor la rechazó
  citando `AGENTS.md`. 16 s de principio a fin.

## Registro de encargos

| Fecha | Encargo | Ámbito | Modelo | Tiempo | Veredicto del script | Vueltas | Defectos hallados en revisión | Comentario |
|---|---|---|---|---|---|---|---|---|
