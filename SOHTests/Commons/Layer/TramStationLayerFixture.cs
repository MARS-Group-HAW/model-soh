using System;
using Mars.Interfaces.Data;
using SOHModel.Tram.Model;
using SOHModel.Tram.Route;
using SOHModel.Tram.Station;

namespace SOHTests.Commons.Layer
{
    /// <summary>
    /// Fixture für die Tram-Route-Layer (Straßenbahn), aufgebaut wie der TrainRouteLayerFixture.
    /// Initialisiert Station- und Routenlayer aus Ressourcen.
    /// </summary>
    public class TramRouteLayerFixture : IDisposable
    {
        public TramRouteLayerFixture()
        {
            TramStationLayer = new TramStationLayerFixture().TramStationLayer;

            // TramRouteLayer anlegen und mit StationLayer verdrahten
            var tramRouteLayer = new TramRouteLayer(TramStationLayer);

            tramRouteLayer.InitLayer(
                new LayerInitData
                {
                    LayerInitConfig = { File = ResourcesConstants.TramT1LineCsv } // <== an deine Ressource anpassen
                },
                (_, _) => { },   // on register callback (wie bei Train)
                (_, _) => { }    // on deregister callback (wie bei Train)
            );

            TramRouteLayer = tramRouteLayer;
        }

        public TramStationLayer TramStationLayer { get; private set; }

        // Falls du ein ITramRouteLayer-Interface definiert hast, nutze das hier:
        public ITramRouteLayer TramRouteLayer { get; private set; }
        // Andernfalls ersetze den obigen Typ durch 'TramRouteLayer'.

        public void Dispose()
        {
            TramStationLayer?.Dispose();
            TramStationLayer = null;
            TramRouteLayer = null;
        }
    }

    /// <summary>
    /// Fixture für den Tram-Station-Layer. Lädt die Haltestellen aus Ressourcen.
    /// </summary>
    public class TramStationLayerFixture : IDisposable
    {
        public TramStationLayerFixture()
        {
            TramStationLayer = new TramStationLayer();

            // Datei auf deine Tram-Haltestellen-Ressource setzen (analog zu TrainStationsU1).
            TramStationLayer.InitLayer(
                new LayerInitData
                {
                    LayerInitConfig = { File = ResourcesConstants.TramStationsT1 }  
                }
            );
        }

        public TramStationLayer TramStationLayer { get; }

        public void Dispose()
        {
            TramStationLayer?.Dispose();
        }
    }
}
