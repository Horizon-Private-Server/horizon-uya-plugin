using RT.Models;
using Server.Common;
using Server.Common.Stream;
using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA
{
    public static class Patch
    {
        class PatchSetup
        {
            public enum PatchHookType
            {
                NONE,
                JUMP
            }

            public int AppId { get; set; }
            public (uint, string) UnpatchPayload { get; set; }
            public (uint, string)[] Payloads { get; set; }
            public uint HookAddress { get; set; }
            public PatchHookType HookType { get; set; }

            public uint GetHookValue(uint targetAddress)
            {
                if (HookType == PatchHookType.NONE)
                    return 0;

                uint value = targetAddress / 4;
                return value | 0x08000000;
            }

            public byte[] ComputeHash()
            {
                var bytes = new List<byte>();

                foreach (var payload in Payloads)
                    bytes.AddRange(File.ReadAllBytes(payload.Item2));

                var hash = System.Security.Cryptography.SHA256.Create();
                return hash.ComputeHash(bytes.ToArray());
            }

            public byte[] ComputeHash(IEnumerable<byte[]> payloads)
            {
                var bytes = new List<byte>();

                foreach (var payload in payloads)
                    bytes.AddRange(payload);

                var hash = System.Security.Cryptography.SHA256.Create();
                return hash.ComputeHash(bytes.ToArray());
            }

            public bool IsMatch(ClientObject client)
            {
                return client.ApplicationId == this.AppId;
            }
        }

        static readonly PatchSetup[] PatchSetups = new PatchSetup[]
        {
            // PAL
            new PatchSetup()
            {
                AppId = 10683,
                HookAddress = 0x00139E94, 
                HookType = PatchSetup.PatchHookType.JUMP,
                UnpatchPayload = (0x000CE000, Path.Combine(Plugin.WorkingDirectory, "bin/patch/unpatch-10683.bin")),
                Payloads = new (uint, string)[]
                {
                    (0x000E0000, Path.Combine(Plugin.WorkingDirectory, "bin/patch/patch-10683.bin")),
                    (0x000C8000, Path.Combine(Plugin.WorkingDirectory,  "bin/exceptiondisplay.bin"))
                }
            },
            // NTSC
            new PatchSetup()
            {
                AppId = 10684,
                HookAddress = 0x00139E94,
                HookType = PatchSetup.PatchHookType.JUMP,
                UnpatchPayload = (0x000CE000, Path.Combine(Plugin.WorkingDirectory, "bin/patch/unpatch-10684.bin")),
                Payloads = new (uint, string)[]
                {
                    (0x000E0000, Path.Combine(Plugin.WorkingDirectory, "bin/patch/patch-10684.bin")),
                    (0x000C8000, Path.Combine(Plugin.WorkingDirectory,  "bin/exceptiondisplay.bin"))
                }
            },
        };

        public static Task QueryForPatch(ClientObject client)
        {
            var patch = PatchSetups.FirstOrDefault(x => x.IsMatch(client));
            if (patch == null)
                return Task.CompletedTask;

            var patchHash = patch.ComputeHash();

            client.Queue(new RT_MSG_SERVER_CHEAT_QUERY()
            {
                Address = 0x000DFFE0,
                Length = 0x20,
                QueryType = RT.Common.CheatQueryType.DME_SERVER_CHEAT_QUERY_RAW_MEMORY,
                SequenceId = 101
            });

            // setup task that will auto send patch if the client doesn't respond in a period of time
            Task.Delay(5000).ContinueWith(r =>
            {
                var playerInfo = Player.GetPlayerExtraInfo(client.AccountId);
                if (client.IsConnected && playerInfo != null)
                {
                    if (playerInfo.PatchHash == null || !patchHash.SequenceEqual(playerInfo.PatchHash))
                    {
                        _ = Apply(client, patch);
                    }
                }
            });

            return Task.CompletedTask;
        }

        public static Task QueryForPatchResponse(ClientObject client, RT_MSG_SERVER_CHEAT_QUERY response)
        {
            var patch = PatchSetups.FirstOrDefault(x => x.IsMatch(client));
            if (patch == null)
                return Task.CompletedTask;

            var playerInfo = Player.GetPlayerExtraInfo(client.AccountId);
            var patchHash = patch.ComputeHash();
            if (client.IsConnected && playerInfo != null)
            {
                playerInfo.PatchHash = response.Data;
                if (playerInfo.PatchHash == null || !patchHash.SequenceEqual(playerInfo.PatchHash))
                {
                    _ = Apply(client, patch);
                }
            }

            return Task.CompletedTask;
        }

        public static Task SendPatch(ClientObject client)
        {
            var patch = PatchSetups.FirstOrDefault(x => x.IsMatch(client));
            if (patch == null)
                return Task.CompletedTask;

            _ = Apply(client, patch);

            return Task.CompletedTask;
        }

        private static async Task Apply(ClientObject client, PatchSetup setup)
        {
            try
            {
                var hasHook = setup.HookType != PatchSetup.PatchHookType.NONE && setup.HookAddress > 0;
                var playerInfo = Player.GetPlayerExtraInfo(client.AccountId);

                // reset hook first
                if (hasHook && setup.HookType == PatchSetup.PatchHookType.JUMP)
                    client.Queue(RT_MSG_SERVER_MEMORY_POKE.FromPayload(setup.HookAddress, BitConverter.GetBytes(0x03E00008)));

                // send unpatch payload
                if (setup.UnpatchPayload.Item1 > 0 && File.Exists(setup.UnpatchPayload.Item2))
                {
                    var bytes = File.ReadAllBytes(setup.UnpatchPayload.Item2);
                    var pokeMsgs = RT_MSG_SERVER_MEMORY_POKE.FromPayload(setup.UnpatchPayload.Item1, bytes);

                    foreach (var pokeMsg in pokeMsgs)
                    {
                        // send
                        client.Queue(pokeMsg);

                        // wait 25 ms after each poke
                        await Task.Delay(25);
                    }

                    // send hook
                    if (hasHook)
                    {
                        var hookMsgs = RT_MSG_SERVER_MEMORY_POKE.FromPayload(setup.HookAddress, BitConverter.GetBytes(setup.GetHookValue(setup.UnpatchPayload.Item1)));
                        client.Queue(hookMsgs);
                    }

                    // wait a bit
                    await Task.Delay(500);
                }

                // construct payloads
                var payloads = setup.Payloads.Select(x =>
                {
                    return new Payload(x.Item1, File.ReadAllBytes(x.Item2));
                });

                // compute patch hash
                var hash = setup.ComputeHash(payloads.Select(x => x.Data));

                // add extra payloads
                payloads = payloads.Union(new Payload[]
                {
                    // patch config
                    new Payload(0x000E0008, (await Player.GetPatchConfig(client)).Serialize()),
                    // hash
                    new Payload(0x000DFFE0, hash),
                    // hook
                    new Payload(0x000E0008, (await Player.GetPatchConfig(client)).Serialize()),
                });

                // update saved player hash
                playerInfo.PatchHash = hash;

                // send payloads as data download
                await Downloader.InitiateDataDownload(client, 101, payloads, (_client, _id) =>
                {
                    if (hasHook)
                    {
                        var hookMsgs = RT_MSG_SERVER_MEMORY_POKE.FromPayload(setup.HookAddress, BitConverter.GetBytes(setup.GetHookValue(setup.Payloads[0].Item1)));
                        _client.Queue(hookMsgs);
                    }

                    return Task.CompletedTask;
                });

            }
            catch (Exception ex)
            {
                Plugin.Host.Log(DotNetty.Common.Internal.Logging.InternalLogLevel.ERROR, ex);
            }
        }

    }


    public enum PatchModuleEntryType : int
    {
        DISABLED,
        RUN_ONCE_GAME,
        RUN_ALWAYS
    }

    public class PatchModuleEntry
    {
        public PatchModuleEntryType Type { get; set; }
        public uint GameEntrypoint { get; set; }
        public uint LobbyEntrypoint { get; set; }
        public uint LoadEntrypoint { get; set; }

        public byte[] Serialize()
        {
            byte[] output = new byte[16];
            using (var ms = new MemoryStream(output, true))
            {
                using (var writer = new MessageWriter(ms))
                {
                    writer.Write(Type);
                    writer.Write(GameEntrypoint);
                    writer.Write(LobbyEntrypoint);
                    writer.Write(LoadEntrypoint);
                }
            }

            return output;
        }

        public void Deserialize(MessageReader reader)
        {
            Type = reader.Read<PatchModuleEntryType>();
            GameEntrypoint = reader.ReadUInt32();
            LobbyEntrypoint = reader.ReadUInt32();
            LoadEntrypoint = reader.ReadUInt32();
        }
    }
}
