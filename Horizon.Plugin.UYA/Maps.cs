using Horizon.Plugin.UYA.Messages;
using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA
{
    public static class Maps
    {
        static readonly string MapVersionPath = Path.Combine(Plugin.WorkingDirectory, "bin/cmaps version.txt");
        static readonly string[] MapModules = new string[]
        {
            Path.Combine(Plugin.WorkingDirectory, "bin/usbhdfsd.irx"),
            Path.Combine(Plugin.WorkingDirectory, "bin/usbserv.irx"),
            Path.Combine(Plugin.WorkingDirectory, "bin/usbd.irx"),
        };

        static readonly CustomMap[] CustomMaps = new CustomMap[]
        {
            new CustomMap(CustomMapId.CMAP_ID_MARAXUS_PRISON, "Maraxus Prison", "maraxus", 40),
        };

        public static CustomMap FindCustomMapById(CustomMapId id)
        {
            return CustomMaps.FirstOrDefault(x => x.MapId == id);
        }

        public static Task SendMapModules(ClientObject client, uint module1Addr, uint module2Addr)
        {
            var payloads = new Payload[]
            {
                new Payload(module1Addr, File.ReadAllBytes(MapModules[0])),
                new Payload(module2Addr, File.ReadAllBytes(MapModules[1])),
                new Payload(0x000AA000, File.ReadAllBytes(MapModules[2])),
            };
            
            return Downloader.InitiateDataDownload(client, 102, payloads, (_client, _id) =>
            {
                client.Queue(new MapModulesResponseMessage()
                {
                    CustomMapsVersion = int.Parse(File.ReadAllText(MapVersionPath)),
                    Module1Size = payloads[0].Data.Length,
                    Module2Size = payloads[1].Data.Length,
                });

                // try and send current map override to client if possible
                return Game.SendMapOverride(client);
            });
        }

        public static Task SendMapVersion(ClientObject client)
        {
            client.Queue(new MapModulesResponseMessage()
            {
                CustomMapsVersion = int.Parse(File.ReadAllText(MapVersionPath)),
                Module1Size = File.ReadAllBytes(MapModules[0]).Length,
                Module2Size = File.ReadAllBytes(MapModules[1]).Length,
            });

            return Task.CompletedTask;
        }

        public static Task SendMapOverride(ClientObject client, CustomMap map)
        {
            client.Queue(new SetMapOverrideRequestMessage()
            {
                MapId = (byte)(map?.LoadingMapId ?? 0),
                MapFilename = map?.MapFilename ?? "",
                MapName = map?.MapName ?? ""
            });

            return Task.CompletedTask;
        }
    }


    public enum MapId : byte
    {
        BATTLEDOME = 41,
        CATACROM = 42,
        SARATHOS = 44,
        DARK_CATHEDRAL = 45,
        SHAAR = 46,
        VALIX = 47,
        MINING_FACILITY = 48,
        TORVAL = 50,
        TEMPUS = 51,
        MARAXUS = 53,
        GHOST_STATION = 54
    }

    public enum CustomMapId : byte
    {
        // custom map ids
        CMAP_ID_MARAXUS_PRISON = 1,
    }

    public class CustomMap
    {
        public CustomMapId MapId { get; private set; }
        public string MapName { get; private set; }
        public string MapFilename { get; private set; }
        public int LoadingMapId { get; private set; }
        public virtual CustomModeId? ModeId { get; private set; }

        public CustomMap(CustomMapId id, string name, string filename, int loadingMapId, CustomModeId? modeId = null)
        {
            MapId = id;
            MapName = name;
            MapFilename = filename;
            LoadingMapId = loadingMapId;
            ModeId = modeId;
        }
    }
}
