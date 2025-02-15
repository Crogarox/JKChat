﻿using System;
using System.Collections.Generic;
using System.Linq;

using JKChat.Core.Models;

namespace JKChat.Core.Services {
	public class GameClientsService : IGameClientsService {
		private Dictionary<JKClient.NetAddress, GameClient> clients = new Dictionary<JKClient.NetAddress, GameClient>(new JKClient.NetAddressComparer());

		public int ActiveClients => clients.Count(kv => kv.Value.Status != ConnectionStatus.Disconnected);

		public int UnreadMessages => clients.Sum(kv => kv.Value.UnreadMessages);

		public GameClient GetOrStartClient(JKClient.ServerInfo serverInfo) {
			var address = serverInfo.Address;
			if (clients.ContainsKey(address)) {
				return clients[address];
			} else {
				return (clients[address] = new GameClient(serverInfo));
			}
		}

		public IEnumerable<JKClient.NetAddress> AddressesWithStatus(ConnectionStatus status, bool without = false) {
			if (clients.Count <= 0) {
				return null;
			}
			Func<ConnectionStatus, bool> condition;
			if (!without) {
				condition = (s) => s == status;
			} else {
				condition = (s) => s != status;
			}
			return clients.Where(kv => condition(kv.Value.Status)).Select(kv => kv.Key);
		}

		public void DisconnectFromAll() {
			foreach (var client in clients) {
				client.Value.Disconnect(true);
			}
		}

		public void ShutdownAll() {
			foreach (var client in clients) {
				client.Value.Shutdown();
			}
			clients.Clear();
		}
	}
}
