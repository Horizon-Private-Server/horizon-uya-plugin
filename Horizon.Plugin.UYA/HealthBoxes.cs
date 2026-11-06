using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Horizon.Plugin.UYA.Messages;
using RT.Common;
using Server.Medius.Models;
using GameModel = Server.Medius.Models.Game;

namespace Horizon.Plugin.UYA
{
	// A reservation is never reassigned merely because an acknowledgement is slow:
	// the winner may already have healed. TCP retries repeat the same transaction.
	internal static class HealthBoxes
	{
		private sealed class Box
		{
			public uint LastToken;
			public long AvailableAt;
			public bool RespawnSent;
			public ClientObject Winner;
			public HealthBoxMessage Grant;
			public long SentAt;
		}
		private sealed class Round
		{
			public uint Session;
			public bool Enabled;
			public readonly Dictionary<uint, Box> Boxes = new();
			public readonly Dictionary<ClientObject, uint> Clients = new();
		}
		private static readonly Dictionary<GameModel, Round> rounds = new();
		private static readonly object gate = new();
		private static uint nextId;
		private static long now => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
		private static uint newId() { nextId = (nextId + 1) & 0x3fffffff; return nextId == 0 ? ++nextId : nextId; }
		private static bool active(GameModel game, ClientObject client) => client != null && client.IsConnected && client.CurrentGame == game;

		internal static void clear(GameModel game)
		{
			lock (gate) rounds.Remove(game);
		}

		internal static void tick()
		{
			lock (gate)
			{
				foreach (var item in rounds.ToArray())
				{
					var game = item.Key;
					if (game.WorldStatus != MediusWorldStatus.WorldActive) { rounds.Remove(game); continue; }
					foreach (var entry in item.Value.Boxes)
					{
						var box = entry.Value;
						if (box.LastToken != 0 && !box.RespawnSent && now >= box.AvailableAt) {
							box.RespawnSent = true;
							foreach (var peer in item.Value.Clients.Keys.Where(c => active(game, c)))
								peer.Queue(new HealthBoxMessage { Action = HealthBoxAction.Respawn, Session = item.Value.Session,
									BoxUid = entry.Key, Token = box.LastToken });
						}
						if (box.Winner == null) continue;
						if (!active(game, box.Winner)) { box.Winner = null; box.Grant = null; continue; }
						if (now - box.SentAt >= 1000) { box.Winner.Queue(box.Grant); box.SentAt = now; }
					}
				}
			}
		}

		internal static void handle(ClientObject client, HealthBoxMessage message)
		{
			var game = client.CurrentGame;
			if (game == null || !active(game, client) || game.WorldStatus != MediusWorldStatus.WorldActive) return;
			lock (gate)
			{
				if (!rounds.TryGetValue(game, out var round))
				{
					if (message.Action != HealthBoxAction.Hello) return;
					rounds.Add(game, round = new Round { Session = newId() });
				}
				if (message.Action == HealthBoxAction.Hello)
				{
					round.Clients[client] = message.Token;
					// Do not switch a mixed-version world away from native pickup authority.
					if (game.Clients.Any(c => active(game, c.Client) && !round.Clients.ContainsKey(c.Client))) return;
					round.Enabled = true;
					foreach (var peer in round.Clients.Where(p => active(game, p.Key)))
						peer.Key.Queue(new HealthBoxMessage { Action = HealthBoxAction.Ready, Session = round.Session, Token = peer.Value });
					foreach (var entry in round.Boxes.Where(e => e.Value.LastToken != 0))
						client.Queue(new HealthBoxMessage { Action = now < entry.Value.AvailableAt ? HealthBoxAction.Consume : HealthBoxAction.Respawn,
							Session = round.Session, BoxUid = entry.Key, Token = entry.Value.LastToken });
					return;
				}
				if (!round.Enabled || message.Session != round.Session || !round.Clients.ContainsKey(client)) return;
				if (message.Action == HealthBoxAction.Request)
				{
					// The hero UID identifies its owning DME client, including split-screen heroes.
					if (((message.BoxUid >> 24) & 0xf) != 7 || !client.DmeClientId.HasValue || (message.PlayerUid >> 28) != (uint)client.DmeClientId.Value
						|| message.RespawnMs < 0 || message.RespawnMs > 3600000) return;
					if (!round.Boxes.TryGetValue(message.BoxUid, out var box))
					{
						if (round.Boxes.Count >= 256) return;
						round.Boxes.Add(message.BoxUid, box = new Box());
					}
					if (message.PreviousToken != box.LastToken || now < box.AvailableAt)
					{
						client.Queue(new HealthBoxMessage { Action = now < box.AvailableAt ? HealthBoxAction.Consume : HealthBoxAction.CatchUp,
							Session = round.Session, BoxUid = message.BoxUid, Token = box.LastToken });
						return;
					}
					if (box.Winner != null || round.Boxes.Values.Any(b => b.Winner == client)) return;
					box.Winner = client;
					box.Grant = new HealthBoxMessage { Action = HealthBoxAction.Grant, Session = round.Session,
						BoxUid = message.BoxUid, PlayerUid = message.PlayerUid, Token = newId(),
						PreviousToken = box.LastToken, RespawnMs = message.RespawnMs };
					box.SentAt = now;
					client.Queue(box.Grant);
					return;
				}
				if (message.Action != HealthBoxAction.Applied && message.Action != HealthBoxAction.Failed) return;
				if (!round.Boxes.TryGetValue(message.BoxUid, out var reserved) || reserved.Winner != client
					|| reserved.Grant == null || reserved.Grant.Token != message.Token || reserved.Grant.PlayerUid != message.PlayerUid) return;
				if (message.Action == HealthBoxAction.Applied)
				{
					reserved.LastToken = message.Token;
					reserved.RespawnSent = false;
					reserved.AvailableAt = reserved.Grant.RespawnMs == 0 ? long.MaxValue : now + reserved.Grant.RespawnMs;
					foreach (var peer in round.Clients.Keys.Where(c => active(game, c)))
						peer.Queue(new HealthBoxMessage { Action = HealthBoxAction.Consume, Session = round.Session,
							BoxUid = message.BoxUid, Token = message.Token });
				}
				reserved.Winner = null;
				reserved.Grant = null;
			}
		}
	}
}
