#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using OpenRA.Server;
using OpenRA.Traits;
using Valve.Sockets;

namespace OpenRA.Network
{
	public enum ConnectionState
	{
		PreConnecting,
		NotConnected,
		Connecting,
		Connected,
	}

	public interface IConnection : IDisposable
	{
		int LocalClientId { get; }
		void StartGame();
		void Send(int frame, IEnumerable<Order> orders);
		void SendImmediate(IEnumerable<Order> orders);
		void SendSync(int frame, int syncHash, ulong defeatState);
		void Receive(OrderManager orderManager);
	}

	public sealed class EchoConnection : IConnection
	{
		const int LocalClientId = 1;
		readonly Queue<(int Frame, int SyncHash, ulong DefeatState)> sync = new();
		readonly Queue<(int Frame, OrderPacket Orders)> orders = new();
		readonly Queue<OrderPacket> immediateOrders = new();
		bool disposed;

		int IConnection.LocalClientId => LocalClientId;

		void IConnection.StartGame()
		{
			// Inject an empty frame to fill the gap we are making by projecting forward orders
			orders.Enqueue((0, new OrderPacket(Array.Empty<Order>())));
		}

		void IConnection.Send(int frame, IEnumerable<Order> o)
		{
			orders.Enqueue((frame, new OrderPacket(o)));
		}

		void IConnection.SendImmediate(IEnumerable<Order> o)
		{
			immediateOrders.Enqueue(new OrderPacket(o));
		}

		void IConnection.SendSync(int frame, int syncHash, ulong defeatState)
		{
			sync.Enqueue((frame, syncHash, defeatState));
		}

		void IConnection.Receive(OrderManager orderManager)
		{
			while (immediateOrders.TryDequeue(out var i))
			{
				orderManager.ReceiveImmediateOrders(LocalClientId, i);

				// An immediate order may trigger a chain of actions that disposes the OrderManager and connection.
				// Bail out to avoid potential problems from acting on disposed objects.
				if (disposed)
					break;
			}

			// Project orders forward to the next frame
			while (orders.TryDequeue(out var o))
				orderManager.ReceiveOrders(LocalClientId, (o.Frame + 1, o.Orders));

			while (sync.TryDequeue(out var s))
				orderManager.ReceiveSync(s);
		}

		void IDisposable.Dispose()
		{
			disposed = true;
		}
	}

	public sealed class NetworkConnection : IConnection
	{
		public readonly ConnectionTarget Target;
		internal ReplayRecorder Recorder { get; private set; }
		readonly Queue<(int Frame, int SyncHash, ulong DefeatState)> sentSync = new();
		readonly Queue<(int Frame, int SyncHash, ulong DefeatState)> queuedSyncPackets = new();

		readonly Queue<(int Frame, OrderPacket Orders)> sentOrders = new();
		readonly Queue<OrderPacket> sentImmediateOrders = new();
		readonly ConcurrentQueue<(int FromClient, byte[] Data)> receivedPackets = new();
		public Valve.Sockets.NetworkingSockets networkingSockets;
		readonly Valve.Sockets.NetworkingUtils utils;
		private List<uint> connections = new List<uint>();
		private List<Valve.Sockets.Address> addresses = new List<Valve.Sockets.Address>();
		public uint connection;
		private bool hasConnection = false;
		private Valve.Sockets.StatusCallback status;
		volatile ConnectionState connectionState = ConnectionState.Connecting;
		volatile int clientId;
		bool disposed;

		public NetworkConnection(ConnectionTarget target)
		{
			Valve.Sockets.Library.Initialize();
			networkingSockets = new Valve.Sockets.NetworkingSockets();
			utils = new Valve.Sockets.NetworkingUtils();

			status = (ref Valve.Sockets.StatusInfo info) => {
				switch (info.connectionInfo.state) {
					case Valve.Sockets.ConnectionState.None:
						break;

					case Valve.Sockets.ConnectionState.Connected:
						if (!hasConnection)
						{
							hasConnection = true;
							Console.WriteLine("Client connected to server - ID: " + info.connection);
							// Copy endpoint here to have it even after getting disconnected.
							var connectionInfo = new Valve.Sockets.ConnectionInfo();
							networkingSockets.GetConnectionInfo(info.connection, ref connectionInfo);
							var ipAddress = new IPAddress(connectionInfo.address.ip);
							EndPoint = new IPEndPoint(ipAddress, connectionInfo.address.port);
							connection = info.connection;

							new Thread(NetworkConnectionReceive)
							{
								Name = $"{GetType().Name} (receive from {EndPoint})",
								IsBackground = true
							}.Start();
						}
						else
						{
							networkingSockets.CloseConnection(info.connection);
						}
						break;

					case Valve.Sockets.ConnectionState.ClosedByPeer:
					case Valve.Sockets.ConnectionState.ProblemDetectedLocally:
						networkingSockets.CloseConnection(info.connection);
						connectionState = ConnectionState.NotConnected;
						Console.WriteLine("Client disconnected from server");
						break;
				}
			};

			utils.SetStatusCallback(status);
			DebugCallback debug = (type, message) => {
				Console.WriteLine("Debug - Type: " + type + ", Message: " + message);
			};
			utils.SetDebugCallback(DebugType.Everything, debug);
			Target = target;
			NetworkConnectionConnect();
		}

		public ConnectionState ConnectionState => connectionState;

		public IPEndPoint EndPoint { get; private set; }

		public string ErrorMessage { get; private set; }

		void NetworkConnectionConnect()
		{
			// TODO: Support multiple endpoints
			var atLeastOneEndpoint = false;
			foreach (var endpoint in Target.GetConnectEndPoints())
			{
				atLeastOneEndpoint = true;
				try
				{
					Valve.Sockets.Address address = new Valve.Sockets.Address();

					address.SetAddress(endpoint.Address.ToString(), (ushort)endpoint.Port);

					uint connection = networkingSockets.Connect(ref address);
					addresses.Add(address);

					connections.Add(connection);
				}
				catch (Exception ex)
				{
					ErrorMessage = "Failed to connect";
					Log.Write("client", $"Failed to connect to {endpoint}: {ex.Message}");
				}

				break;
			}

			if (!atLeastOneEndpoint)
			{
				ErrorMessage = "Failed to resolve address";
				connectionState = ConnectionState.NotConnected;
			}

			new Thread(PumpCallbacksUntilConnected)
			{
				Name = $"{GetType().Name} (run callbacks)",
				IsBackground = true
			}.Start();
		}

		void PumpCallbacksUntilConnected()
		{
			while (!hasConnection)
			{
				networkingSockets.RunCallbacks();

				Thread.Sleep(21);
			}
		}

		void NetworkConnectionReceive()
		{
			try
			{
				bool isHandshake = true;
				const int maxMessages = 20;

				Valve.Sockets.NetworkingMessage[] netMessages = new Valve.Sockets.NetworkingMessage[maxMessages];

				while (true) {
					Thread.Sleep(21);
					networkingSockets.RunCallbacks();
					int netMessagesCount = networkingSockets.ReceiveMessagesOnConnection(connection, netMessages, maxMessages);
					if (netMessagesCount > 0) {
						for (int i = 0; i < netMessagesCount; i++) {
							ref Valve.Sockets.NetworkingMessage netMessage = ref netMessages[i];
							if (netMessage.length > 0) {
								var bytes = new Byte[netMessage.length];
								Marshal.Copy(netMessage.data, bytes, 0, netMessage.length);
								var stream = new MemoryStream(bytes);

								if (isHandshake)
								{
									var handshakeProtocol = stream.ReadInt32();

									if (handshakeProtocol != ProtocolVersion.Handshake)
										throw new InvalidOperationException($"Handshake protocol version mismatch. Server={handshakeProtocol} Client={ProtocolVersion.Handshake}");

									clientId = stream.ReadInt32();
									connectionState = ConnectionState.Connected;

									isHandshake = false;
								}
								else
								{

									var len = stream.ReadInt32();
									var client = stream.ReadInt32();
									var buf = stream.ReadBytes(len);
									if (len == 0)
										throw new NotImplementedException();
									receivedPackets.Enqueue((client, buf));
								}
							}
							netMessage.Destroy();
						}
					}
				}
			}
			catch (Exception ex)
			{
				ErrorMessage = "Connection failed";
				Log.Write("client", $"Connection to {EndPoint} failed: {ex.Message}");
			}
			finally
			{
				connectionState = ConnectionState.NotConnected;
			}
		}

		int IConnection.LocalClientId => clientId;

		void IConnection.StartGame() { }

		void IConnection.Send(int frame, IEnumerable<Order> orders)
		{
			var o = new OrderPacket(orders);
			sentOrders.Enqueue((frame, o));
			Send(o.Serialize(frame));
		}

		void IConnection.SendImmediate(IEnumerable<Order> orders)
		{
			var o = new OrderPacket(orders);
			sentImmediateOrders.Enqueue(o);
			Send(o.Serialize(0));
		}

		void IConnection.SendSync(int frame, int syncHash, ulong defeatState)
		{
			// Send sync packets together with the next set of orders.
			// This was originally explained as reducing network bandwidth
			// (TCP overhead?), but the original discussions have been lost to time.
			// Add the sync packets to the send queue before adding them to the local sync queue in the Send() method.
			// Otherwise the client will process the local sync queue before sending the packet.
			queuedSyncPackets.Enqueue((frame, syncHash, defeatState));
		}

		void Send(byte[] packet)
		{
			try
			{
				var ms = new MemoryStream();
				ms.Write(packet.Length);
				ms.Write(packet);

				foreach (var s in queuedSyncPackets)
				{
					var q = OrderIO.SerializeSync(s);

					ms.Write(q.Length);
					ms.Write(q);

					sentSync.Enqueue(s);
				}

				queuedSyncPackets.Clear();
				var bytes = ms.ToArray();
				networkingSockets.SendMessageToConnection(connection, bytes, Valve.Sockets.SendFlags.Reliable);
			}
			catch (ObjectDisposedException) { /* ditto */ }
			catch (InvalidOperationException) { /* ditto */ }
			catch (IOException) { /* ditto */ }
			catch (Exception) {  }
		}

		void IConnection.Receive(OrderManager orderManager)
		{
			// Locally generated orders
			while (sentImmediateOrders.TryDequeue(out var i))
			{
				orderManager.ReceiveImmediateOrders(clientId, i);
				Recorder?.Receive(clientId, i.Serialize(0));

				// An immediate order may trigger a chain of actions that disposes the OrderManager and connection.
				// Bail out to avoid potential problems from acting on disposed objects.
				if (disposed)
					return;
			}

			while (sentSync.TryDequeue(out var s))
			{
				orderManager.ReceiveSync(s);
				Recorder?.Receive(clientId, OrderIO.SerializeSync(s));
			}

			// Orders from other players
			while (receivedPackets.TryDequeue(out var p))
			{
				if (OrderIO.TryParseDisconnect(p, out var disconnect))
				{
					orderManager.ReceiveDisconnect(disconnect.ClientId, disconnect.Frame);
					Recorder?.Receive(p.FromClient, p.Data);
				}
				else if (OrderIO.TryParseSync(p.Data, out var sync))
				{
					orderManager.ReceiveSync(sync);
					Recorder?.Receive(p.FromClient, p.Data);
				}
				else if (OrderIO.TryParseTickScale(p, out var scale))
					orderManager.ReceiveTickScale(scale);
				else if (OrderIO.TryParsePingRequest(p, out var timestamp))
				{
					// Note that processing this here, rather than in NetworkConnectionReceive,
					// so that poor world tick performance can be reflected in the latency measurement
					Send(OrderIO.SerializePingResponse(timestamp, (byte)orderManager.OrderQueueLength));
				}
				else if (OrderIO.TryParseAck(p, out var ackFrame, out var ackCount))
				{
					if (ackCount > sentOrders.Count)
						throw new InvalidOperationException($"Received Ack for {ackCount} > {sentOrders.Count} frames.");

					// The Acknowledgement packet is a placeholder that tells us to process the first packet in our
					// local sent buffer and the frame at which it should be applied. This is an optimization to avoid having
					// to send the (much larger than 5 byte) packet back to us over the network.
					OrderPacket packet;
					if (ackCount != 1)
					{
						var orders = Enumerable.Range(0, ackCount)
							.Select(i => sentOrders.Dequeue().Orders);
						packet = OrderPacket.Combine(orders);
					}
					else
						packet = sentOrders.Dequeue().Orders;

					orderManager.ReceiveOrders(clientId, (ackFrame, packet));
					Recorder?.Receive(clientId, packet.Serialize(ackFrame));
				}
				else if (OrderIO.TryParseOrderPacket(p.Data, out var orders))
				{
					if (orders.Frame == 0)
						orderManager.ReceiveImmediateOrders(p.FromClient, orders.Orders);
					else
						orderManager.ReceiveOrders(p.FromClient, orders);

					Recorder?.Receive(p.FromClient, p.Data);
				}
				else
					throw new InvalidDataException($"Received unknown packet from client {p.FromClient} with length {p.Data.Length}");

				// An immediate order may trigger a chain of actions that disposes the OrderManager and connection.
				// Bail out to avoid potential problems from acting on disposed objects.
				if (disposed)
					return;
			}
		}

		public void StartRecording(Func<string> chooseFilename)
		{
			// If we have a previous recording then save/dispose it and start a new one.
			Recorder?.Dispose();
			Recorder = new ReplayRecorder(chooseFilename);
		}

		void IDisposable.Dispose()
		{
			if (disposed)
				return;

			disposed = true;

			Recorder?.Dispose();
		}
	}
}
