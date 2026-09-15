# 0112 — El perk que se activa se ve

Estado: **Aceptada** (15 sep 2026). Deriva de `docs/analisis/perks-auditoria-diseno.md` y de la decisión
del revisor de dirigir el catálogo hacia perks de **habilidad** (PD-6, `docs/analisis/perks-catalogo-unificado.md`).
Añade un tipo de evento a la secuencia pública de `/Sim` (RT-013), así que es una decisión de arquitectura,
no un ajuste.

## Problema

La auditoría de diseño midió que **41 de los 61 perks del catálogo son imperceptibles o casi** durante el
partido. El motivo no es solo de diseño: aunque un perk cambie el resultado de una jugada, **la activación
no salía del motor**. Llegaba al informe de después del partido como un recuento agregado, y en el campo no
había nada que la señalara. Un perk que no se puede **atribuir** a una jugada concreta es, para quien mira,
indistinguible de la suerte.

Eso hace daño en dos sitios a la vez:

- **En el juego**: la parte del bucle que el jugador construye entre partidos —la build— no se ve cuando se
  cobra. El partido parece el mismo con perks y sin ellos.
- **En el desarrollo**: no se puede juzgar si un perk nuevo «se nota» sin volcar el CSV de `/Balance`.

Con el catálogo virando hacia habilidades —cosas que **pasan**, no cuotas que se suman— el problema pasa de
molesto a bloqueante: una habilidad que nadie ve es peor que una cuota, porque además promete espectáculo.

## Decisión

**El motor emite un evento `PERK_TRIGGERED` cada vez que un perk se cobra**, con el id del perk en el
detalle y el portador como actor, y la pantalla de Partido lo pinta como un cartel de **un segundo** sobre
la cabeza de ese jugador.

Tres condiciones, y las tres son requisito:

1. **Es un evento de presentación, no una jugada.** Se emite con `publish: false`: no lo ve el motor de
   perks, así que no puede disparar otro perk ni entrar en un bucle. No lo narra el log de RF-121 (sería
   una línea por activación sin nada que contar) y el cargador lo **rechaza** como disparador de un perk
   en `/data` (`PerkLoader.ParseTrigger`).
2. **No cambia el partido.** Añadir el evento no mueve un solo tick, y el sobre `MATCH_START` … `MATCH_END`
   se respeta: un perk colgado de `MATCH_END` **no** emite su aviso, porque el final tiene que seguir
   siendo el último evento de la secuencia.
3. **`/Game` no decide nada** (RT-014). `MatchFlashView`, en `/Sim`, convierte los eventos en filas
   `(fotograma, ficha, equipo, id de perk, nombre)`; el campo solo elige tipografía, color y desvanecido.
   El fotograma sale de `MatchTrace.FrameOfTick`, no de una cuenta propia de la interfaz.

La duración es **15 fotogramas = 1 s a 15 ticks/s** (RT-020), con el último tercio desvaneciéndose. Dos
avisos simultáneos del mismo jugador se apilan hacia arriba en vez de pisarse.

## Consecuencias

- El aviso es **la columna vertebral del rediseño del catálogo**: cada perk de la tanda 1 y de la tanda 2
  que caiga después se ve el día que se escribe, sin trabajo de interfaz adicional.
- Es **el sitio donde se colgará el efecto visual** de cada habilidad (la ADR no lo decide todavía): el
  evento ya lleva el id del perk, así que un balón de fuego para `Obús` es un caso del render, no un
  cambio de motor.
- Abre una deuda pequeña y conocida: el nombre se resuelve con `Name.Es`, como el resto de las vistas de
  `/Game` (`GameData.Language` es hoy la constante `"es"`). El día que el idioma sea de verdad una opción,
  se cambia en todas a la vez, no solo aquí.
- Un perk que se cobra **muchas** veces por partido llenará el campo de carteles. Es información, no ruido:
  si molesta, lo que hay que revisar es el perk, no el aviso.

## Alternativas descartadas

- **Dejarlo en el informe de después.** Es lo que había. No resuelve la atribución, que es el problema.
- **Que `/Game` dedujera la activación de los eventos que ya existían** (un `TACKLE` con un detalle raro,
  una lesión que no ocurre). Rompe RT-014 y además es imposible para los perks que solo cambian una
  probabilidad.
- **Una línea más en el log de texto.** El log es prosa localizada y se lee después; el problema es que no
  se ve **mientras pasa**.
