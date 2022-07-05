using Horizon.Plugin.UYA.Messages;
using Server.Medius.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Horizon.Plugin.UYA
{
    public static class Downloader
    {
        private static Dictionary<int, DownloaderState> _states = new Dictionary<int, DownloaderState>();

        class DownloaderState
        {
            public int Id { get; set; }
            public List<Payload> Payloads { get; set; }
            public int TotalSize { get; set; }
            public Func<ClientObject, int, Task> OnFinished { get; set; }

            public DownloaderState(int id, IEnumerable<Payload> payloads)
            {
                Id = id;
                Payloads = payloads.ToList();
                TotalSize = payloads.Sum(x => x.Data.Length);
            }
        }

        public static Task OnDataDownloadResponse(ClientObject client, DataDownloadResponseMessage response)
        {
            return onDataDownloadResponse(client, response.Id, response.BytesReceived);
        }

        public static Task InitiateDataDownload(ClientObject client, int id, IEnumerable<Payload> payloads, Func<ClientObject, int, Task> onFinishedCallback = null)
        {
            if (_states.TryGetValue(client.AccountId, out var state))
                throw new InvalidOperationException($"InitiateDataDownload triggered for {client.AccountId} with already existing state. {state}");

            // add state
            state = new DownloaderState(id, payloads)
            {
                OnFinished = onFinishedCallback
            };

            _states[client.AccountId] = state;

            // begin
            return onDataDownloadResponse(client, id, 0);
        }

        public static Task OnPlayerLoggedOut(ClientObject client)
        {
            if (_states.ContainsKey(client.AccountId))
                _states.Remove(client.AccountId);

            return Task.CompletedTask;
        }

        private static Task onDataDownloadResponse(ClientObject client, int id, int bytesReceived)
        {
            if (!_states.TryGetValue(client.AccountId, out var state))
                throw new InvalidOperationException($"onDataDownloadResponse triggered for {client.AccountId} with no active state. {id}");

            if (state.Id != id)
                throw new InvalidOperationException($"onDataDownloadResponse triggered for {client.AccountId} with active state id {state.Id} with id {id}");

            // find next segment
            int segIdx = 0;

            foreach (var payload in state.Payloads)
            {
                if (bytesReceived < payload.Data.Length)
                    break;

                bytesReceived -= payload.Data.Length;
                segIdx++;
            }

            // if not finished
            if (segIdx < state.Payloads.Count)
            {
                Payload payload = state.Payloads[segIdx];

                // grab data block
                var msgData = payload.Data.Skip(bytesReceived).Take(DataDownloadRequestMessage.MAX_DATA_SIZE).ToArray();

                client.Queue(new DataDownloadRequestMessage()
                {
                    Id = state.Id,
                    Data = msgData,
                    TargetAddress = (uint)(payload.Address + bytesReceived),
                    TotalSize = state.TotalSize,
                    DataOffset = bytesReceived
                });
            }
            else
            {
                // remove from active
                _states.Remove(client.AccountId);

                // invoke finish callback
                if (state.OnFinished != null)
                    return state.OnFinished(client, id);
            }

            return Task.CompletedTask;
        }
    }
}
