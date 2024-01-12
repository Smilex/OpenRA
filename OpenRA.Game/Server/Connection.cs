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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using Valve.Sockets;

namespace OpenRA.Server
{
	public sealed class Connection : IDisposable
	{
		public const int MaxOrderLength = 131072;

		// Cap ping history at 15 seconds as a balance between expiring stale state and having enough data for decent statistics
		const int MaxPingSamples = 15;

		public readonly int PlayerIndex;
		public readonly string AuthToken;
		public readonly EndPoint EndPoint;
		public readonly Stopwatch ConnectionTimer = Stopwatch.StartNew();

		public long TimeSinceLastResponse => Game.RunTime - lastReceivedTime;

		public bool TimeoutMessageShown;
		public bool Validated;
		public int LastOrdersFrame;

		long lastReceivedTime = 0;

		readonly BlockingCollection<byte[]> sendQueue = new();
		readonly Queue<int> pingHistory = new();

		public Connection(Server server, uint connection, string authToken)
		{
			PlayerIndex = server.ChooseFreePlayerIndex();
			AuthToken = authToken;
			var connectionInfo = new Valve.Sockets.ConnectionInfo();
			server.networkingSockets.GetConnectionInfo(connection, ref connectionInfo);
			var ipAddress = new IPAddress(connectionInfo.address.ip);
			EndPoint = new IPEndPoint(ipAddress, connectionInfo.address.port);

			new Thread(SendReceiveLoop)
			{
				Name = $"Client communication ({EndPoint}",
				IsBackground = true
			}.Start((server, connection));
		}

		static byte[] CreatePingFrame()
		{
			var ms = new MemoryStream(21);
			ms.Write(13);
			ms.Write(0);
			ms.Write(0);
			ms.WriteByte((byte)OrderType.Ping);
			ms.Write(Game.RunTime);
			return ms.GetBuffer();
		}

		void SendReceiveLoop(object s)
		{
			var (server, connection) = ((Server, uint))s;

			var readBuffer = new List<byte>();
			var state = ReceiveState.Header;
			var expectLength = 8;
			var frame = 0;
			var lastPingSent = Stopwatch.StartNew();

			try {
				const int maxMessages = 20;

				Valve.Sockets.NetworkingMessage[] netMessages = new Valve.Sockets.NetworkingMessage[maxMessages];
				while (true)
				{
					Thread.Sleep(21);
					
					var info = new Valve.Sockets.ConnectionInfo();
					server.networkingSockets.GetConnectionInfo(connection, ref info);

					int netMessagesCount = server.networkingSockets.ReceiveMessagesOnConnection(connection, netMessages, maxMessages);

					if (netMessagesCount > 0) {
						for (int i = 0; i < netMessagesCount; i++) {
							ref Valve.Sockets.NetworkingMessage netMessage = ref netMessages[i];

							Console.WriteLine("Message received from server - Channel ID: " + netMessage.channel + ", Data length: " + netMessage.length);

							if (netMessage.length > 0) {
								var bytes = new byte[netMessage.length];
								Marshal.Copy(netMessage.data, bytes, 0, netMessage.length);
								readBuffer.AddRange(bytes);
								lastReceivedTime = Game.RunTime;
								TimeoutMessageShown = false;
							}
							netMessage.Destroy();

							while (readBuffer.Count >= expectLength)
							{
								var bytes = readBuffer.GetRange(0, expectLength).ToArray();
								readBuffer.RemoveRange(0, expectLength);

								switch (state)
								{
									case ReceiveState.Header:
									{
										expectLength = BitConverter.ToInt32(bytes, 0) - 4;
										frame = BitConverter.ToInt32(bytes, 4);
										state = ReceiveState.Data;

										if (expectLength < 0 || (server.IsMultiplayer && expectLength > MaxOrderLength))
										{
											Log.Write("server", $"Closing socket connection to {EndPoint} because of excessive order length: {expectLength}");
											return;
										}

										break;
									}

									case ReceiveState.Data:
									{
										// Ping packets are sent and processed internally within this thread to reduce
										// server-introduced latencies from polling loops
										if (expectLength == 10 && bytes[0] == (byte)OrderType.Ping)
										{
											if (pingHistory.Count == MaxPingSamples)
												pingHistory.Dequeue();

											pingHistory.Enqueue((int)(Game.RunTime - BitConverter.ToInt64(bytes, 1)));
											server.OnConnectionPing(this, pingHistory.ToArray(), bytes[9]);
										}
										else
											server.OnConnectionPacket(this, frame, bytes);

										expectLength = 8;
										state = ReceiveState.Header;

										break;
									}
								}
							}
						}
					}

					// Client has been dropped by the server
					if (sendQueue.IsCompleted)
						return;

					// Regularly check player ping
					if (lastPingSent.ElapsedMilliseconds > 1000 && TrySendData(CreatePingFrame()))
						lastPingSent.Restart();

					// Send all data immediately, we will block again on read
					while (sendQueue.TryTake(out var data, 0))
					{
						var length = data.Length;
						var result = server.networkingSockets.SendMessageToConnection(connection, data, Valve.Sockets.SendFlags.Reliable);
					}
				}
			} catch (Exception ex) {
			}

			server.OnConnectionDisconnect(this);
		}

		public bool TrySendData(byte[] data)
		{
			if (sendQueue.IsAddingCompleted)
				return false;

			try
			{
				sendQueue.Add(data);
				return true;
			}
			catch (InvalidOperationException)
			{
				// Occurs if the collection is marked completed for adding by another thread.
				return false;
			}
		}

		public void Dispose()
		{
			// Tell the sendReceiveThread that the socket should be closed
			sendQueue.CompleteAdding();
		}
	}

	public enum ReceiveState { Header, Data }
}
