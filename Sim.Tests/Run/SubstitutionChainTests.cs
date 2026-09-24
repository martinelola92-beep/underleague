using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// BA-B: la cadena de sustituciones. Un suplente que ya entró tiene que poder volver a salir si se lesiona
/// él, y eso lo daban por imposible **dos** sitios: <c>Substitutions.Pending</c>, que solo miraba el once
/// inicial y por tanto no abría ninguna ventana, y la validación de <c>Simulator</c>, que rechazaba la
/// sustitución aunque llegara.
///
/// <para>Costaba una run entera: el partido se quedaba esperando una decisión que el jugador no podía
/// tomar, y con guardado ironman (RT-061) la única salida era cerrar el juego. Se veía sobre todo en un
/// jefe, que es donde hay más bajas por partido.</para>
/// </summary>
public sealed class SubstitutionChainTests
{
    [Fact]
    public void ASubstituteWhoGetsHurt_OpensAnotherSubstitutionWindow()
    {
        var catalog = TestData.LoadCatalog();

        // Un emparejamiento muy desigual produce bajas de sobra en el equipo débil, que es lo que hace
        // falta para encadenar. Se barren semillas porque la cadena depende de que el propio suplente se
        // lesione, que es el caso raro y justo el que estaba roto.
        //
        // El barrido son 400 y no 60: con 60 el test pasaba en 16x6 y se puso rojo al pasar a 16x7 (ADR
        // 0109) sin que el mecanismo cambiara, solo porque +17 % de superficie hace más raro que el propio
        // suplente se lesione. Un test que falla porque el campo crece es un test mal dimensionado, no una
        // regresión. Sale por la primera semilla que encuentra el caso, así que el coste real es bajo.
        // 400 -> 1500 semillas (BI-E, 24 sep 2026): lo que este test necesita es que un SUPLENTE YA
        // ENTRADO se lesione, y con las lesiones en 0,65-0,70 por partido tras el Gameplay AI Foundations
        // Pass ese encadenamiento se ha hecho más raro. La afirmación —que la ventana se vuelve a ofrecer
        // sobre quien ya entró, que es lo que BA-B arregla— no cambia; lo que cambia es cuánto hay que
        // buscar para encontrar el caso. El propio test grita si no lo encuentra.
        for (ulong seed = 1; seed <= 1500; seed++)
        {
            var setup = TestMatches.Build(catalog, seed, homeQuality: 15, awayQuality: 95);
            var result = Simulator.Run(setup, seed, catalog, new SimConfig(CollectLog: false));

            int chained = 0;
            for (int step = 0; step < 4; step++)
            {
                var point = SubstitutionPoints.Pending(setup, result, team: 0, catalog);
                if (point is null || point.Candidates.Count == 0)
                {
                    break;
                }

                bool wasSubstitute = false;
                for (int i = 0; i < setup.Home.Substitutions.Count; i++)
                {
                    wasSubstitute |= setup.Home.Substitutions[i].InPlayerId == point.OutPlayerId;
                }

                var next = new List<Substitution>(setup.Home.Substitutions)
                {
                    new(point.Tick, point.OutPlayerId, point.Candidates[0].Id),
                };
                setup = setup with { Home = setup.Home with { Substitutions = next } };
                result = Simulator.Run(setup, seed, catalog, new SimConfig(CollectLog: false));

                if (wasSubstitute)
                {
                    // Es el caso de BA-B: el que sale ya había entrado. Antes ni se ofrecía la ventana
                    // (Pending devolvía null) ni se aceptaba la decisión (Simulator lanzaba).
                    Assert.True(result.Report.Ticks > 0);
                    return;
                }

                chained++;
            }

            Assert.True(chained <= 4);
        }

        Assert.Fail("en mil quinientas semillas no se ha encadenado ninguna sustitución sobre un suplente ya entrado: "
            + "el emparejamiento del test no produce el caso que BA-B arregla");
    }
}
