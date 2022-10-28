#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Net;
using OpenRA.Server;
using Valve.Sockets;

namespace OpenRA.Network
{
    public class EndPoint {
        Address address;
        public EndPoint(Address address) {
			this.address = address;
        }

		public String Address => address.GetIP();

		public int Port => address.port;
    }

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
		readonly Queue<(int Frame, int SyncHash, ulong DefeatState)> sync = new Queue<(int, int, ulong)>();
		readonly Queue<(int Frame, OrderPacket Orders)> orders = new Queue<(int, OrderPacket)>();
		readonly Queue<OrderPacket> immediateOrders = new Queue<OrderPacket>();
		bool disposed;

		int IConnection.LocalClientId => LocalClientId;

		void IConnection.StartGame()
		{
			// Inject an empty frame to fill the gap we are making by projecting forward orders
			orders.Enqueue((0, new OrderPacket(Array.Empty<Order>())));
		}

		void IConnection.Send(int frame, IEnumerable<Order> o)
		{
			orders.Enqueue((frame, new OrderPacket(o.ToArray())));
		}

		void IConnection.SendImmediate(IEnumerable<Order> o)
		{
			immediateOrders.Enqueue(new OrderPacket(o.ToArray()));
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
		readonly Queue<(int Frame, int SyncHash, ulong DefeatState)> sentSync = new Queue<(int, int, ulong)>();
		readonly Queue<(int Frame, int SyncHash, ulong DefeatState)> queuedSyncPackets = new Queue<(int, int, ulong)>();

		readonly Queue<(int Frame, OrderPacket Orders)> sentOrders = new Queue<(int, OrderPacket)>();
		readonly Queue<OrderPacket> sentImmediateOrders = new Queue<OrderPacket>();
		readonly ConcurrentQueue<(int FromClient, byte[] Data)> receivedPackets = new ConcurrentQueue<(int, byte[])>();
		NetworkingSockets client;
		Queue<uint> connections;
		EndPoint endpoint;

		const int maxNetMessages = 20;
		NetworkingMessage[] netMessages;

		volatile ConnectionState connectionState = ConnectionState.Connecting;
		volatile int clientId;
		bool disposed;
		string errorMessage;

		public NetworkConnection(ConnectionTarget target)
		{
			client = new NetworkingSockets();
			netMessages = new NetworkingMessage[maxNetMessages];
			Target = target;
			new Thread(NetworkConnectionConnect)
			{
				Name = $"{GetType().Name} (connect to {target})",
				IsBackground = true
			}.Start();
		}

		void NetworkConnectionConnect()
		{
			var queue = new BlockingCollection<uint>();

			var atLeastOneEndpoint = false;

			foreach (var endpoint in Target.GetConnectEndPoints())
			{
				atLeastOneEndpoint = true;
				new Thread(() =>
				{
					try
					{
						uint c = 0;
						Address address = new Address();
						address.SetAddress(endpoint.Address.ToString(), ((ushort)endpoint.Port));

						c = client.Connect(ref address);

						try
						{
							queue.Add(c);
						}
						catch (InvalidOperationException)
						{
							// Another connection was faster, close this one.
							client.CloseConnection(c);
						}
					}
					catch (Exception ex)
					{
						errorMessage = "Failed to connect";
						Log.Write("client", $"Failed to connect to {endpoint}: {ex.Message}");
					}
				})
				{
					Name = $"{GetType().Name} (connect to {endpoint})",
					IsBackground = true
				}.Start();
			}

			uint conn = 0;

			if (!atLeastOneEndpoint)
			{
				errorMessage = "Failed to resolve address";
				connectionState = ConnectionState.NotConnected;
			}

			// Wait up to 5s for a successful connection. This should hopefully be enough because such high latency makes the game unplayable anyway.
			else if (queue.TryTake(out conn, 5000))
			{
				// Copy endpoint here to have it even after getting disconnected.
				Address address = new Address();
				client.GetListenSocketAddress(conn, ref address);
				endpoint = new EndPoint(address);
				connections.Append(conn);

				new Thread(NetworkConnectionReceive)
				{
					Name = $"{GetType().Name} (receive from {address.GetIP()})",
					IsBackground = true
				}.Start(conn);
			}
			else
			{
				connectionState = ConnectionState.NotConnected;
			}

			// Close all unneeded connections in the queue and make sure new ones are closed on the connect thread.
			queue.CompleteAdding();
			foreach (var c in queue)
				client.CloseConnection(c);
		}

		void NetworkConnectionReceive(object connectionObject)
		{
			uint connection = (uint)connectionObject;
			while (true) {
				client.RunCallbacks();
				int netMessagesCount = client.ReceiveMessagesOnConnection(connection, netMessages, maxNetMessages);

				if (netMessagesCount > 0) {
					for (int i = 0; i < netMessagesCount; i++) {
						ref NetworkingMessage netMessage = ref netMessages[i];

						Console.WriteLine("Message received from server - Channel ID: " + netMessage.channel + ", Data length: " + netMessage.length);

						IntPtr data = netMessage.data;
						unsafe {
							try {
								var stream = new UnmanagedMemoryStream((byte*)data.ToPointer(), netMessage.length);

								var handshakeProtocol = stream.ReadInt32();

								if (handshakeProtocol != ProtocolVersion.Handshake)
									throw new InvalidOperationException($"Handshake protocol version mismatch. Server={handshakeProtocol} Client={ProtocolVersion.Handshake}");

								clientId = stream.ReadInt32();
								connectionState = ConnectionState.Connected;

								while (stream.CanRead)
								{
									var len = stream.ReadInt32();
									var client = stream.ReadInt32();
									var buf = stream.ReadBytes(len);
									if (len == 0)
										throw new NotImplementedException();
									receivedPackets.Enqueue((client, buf));
								}
							}
							catch (Exception ex)
							{
								errorMessage = "Connection failed";
								Log.Write("client", $"Connection to {endpoint} failed: {ex.Message}");
							}
							finally
							{
								connectionState = ConnectionState.NotConnected;
							}
						}

						netMessage.Destroy();
					}
				}
			}
		}

		int IConnection.LocalClientId => clientId;

		void IConnection.StartGame() { }

		void IConnection.Send(int frame, IEnumerable<Order> orders)
		{
			var o = new OrderPacket(orders.ToArray());
			sentOrders.Enqueue((frame, o));
			Send(o.Serialize(frame));
		}

		void IConnection.SendImmediate(IEnumerable<Order> orders)
		{
			var o = new OrderPacket(orders.ToArray());
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
				ms.WriteArray(BitConverter.GetBytes(packet.Length));
				ms.WriteArray(packet);

				foreach (var s in queuedSyncPackets)
				{
					var q = OrderIO.SerializeSync(s);

					ms.WriteArray(BitConverter.GetBytes(q.Length));
					ms.WriteArray(q);

					sentSync.Enqueue(s);
				}

				queuedSyncPackets.Clear();
				client.SendMessageToConnection(connection, ms.GetBuffer());
			}
			catch (ObjectDisposedException) { /* drop this on the floor; we'll pick up the disconnect from the reader thread */ }
			catch (InvalidOperationException) { /* ditto */ }
			catch (IOException) { /* ditto */ }
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

		public ConnectionState ConnectionState => connectionState;

		public EndPoint EndPoint => endpoint;

		public string ErrorMessage => errorMessage;

		void IDisposable.Dispose()
		{
			if (disposed)
				return;

			disposed = true;

			// Closing the stream will cause any reads on the receiving thread to throw.
			// This will mark the connection as no longer connected and the thread will terminate cleanly.
			client.CloseConnection(connection);

			Recorder?.Dispose();
		}
	}
}
