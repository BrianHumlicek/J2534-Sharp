#region License
/*Copyright(c) 2024, Brian Humlicek
* https://github.com/BrianHumlicek
* 
*Permission is hereby granted, free of charge, to any person obtaining a copy
*of this software and associated documentation files (the "Software"), to deal
*in the Software without restriction, including without limitation the rights
*to use, copy, modify, merge, publish, distribute, sub-license, and/or sell
*copies of the Software, and to permit persons to whom the Software is
*furnished to do so, subject to the following conditions:
*The above copyright notice and this permission notice shall be included in all
*copies or substantial portions of the Software.
*
*THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
*IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
*FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
*AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
*LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
*OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
*SOFTWARE.
*/
#endregion License
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using SAE.J2534.Interop;

namespace SAE.J2534
{
    /// <summary>
    /// Represents an open J2534 communication channel.
    /// </summary>
    public sealed partial class J2534Channel : IDisposable
    {
        private readonly J2534API _api;
        private readonly J2534Device _device;
        private readonly int _channelId;
        private readonly object _syncRoot;
        private readonly NativeMessageBuffer _messageBuffer;
        private readonly List<PeriodicMessage> _periodicMessages = new List<PeriodicMessage>();
        private readonly List<MessageFilter> _filters = new List<MessageFilter>();
        private bool _disposed;

        public Protocol Protocol { get; }
        public Baud BaudRate { get; }
        public ConnectFlag ConnectFlags { get; }
        
        public int DefaultTxTimeoutMs { get; set; } = 100;
        public int DefaultRxTimeoutMs { get; set; } = 300;
        public TxFlag DefaultTxFlags { get; set; } = TxFlag.NONE;

        public IReadOnlyList<PeriodicMessage> PeriodicMessages => _periodicMessages.AsReadOnly();
        public IReadOnlyList<MessageFilter> Filters => _filters.AsReadOnly();

        /// <summary>
        /// Optional logger for API call tracing. Inherits from parent device by default.
        /// </summary>
        public Microsoft.Extensions.Logging.ILogger Logger { get; set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        internal J2534Channel(J2534API api, J2534Device device, int channelId, Protocol protocol, Baud baudRate, ConnectFlag connectFlags, object syncRoot)
        {
            _api = api;
            _device = device;
            _channelId = channelId;
            Protocol = protocol;
            BaudRate = baudRate;
            ConnectFlags = connectFlags;
            _syncRoot = syncRoot;
            _messageBuffer = new NativeMessageBuffer(protocol, 200);
        }

        /// <summary>
        /// Reads a single message using the default timeout.
        /// </summary>
        public GetMessagesResult ReadMessage() => ReadMessages(1, DefaultRxTimeoutMs);

        /// <summary>
        /// Reads up to the specified number of messages using the default timeout.
        /// </summary>
        public GetMessagesResult ReadMessages(int count) => ReadMessages(count, DefaultRxTimeoutMs);

        /// <summary>
        /// Reads up to the specified number of messages with a custom timeout.
        /// </summary>
        public GetMessagesResult ReadMessages(int count, int timeoutMs)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));
            if (count < 1 || count > 200)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 200");

            Logger.LogTrace("PTReadMsgs({ChannelId}, {Count}, {TimeoutMs})",
                _channelId, count, timeoutMs);

            lock (_syncRoot)
            {
                _messageBuffer.Count = count;
                var result = _api.PTReadMsgs(_channelId, _messageBuffer.GetMessagePointer(), _messageBuffer.CountPointer, timeoutMs);

                // These are expected results, not errors
                if (result == ResultCode.TIMEOUT || result == ResultCode.BUFFER_EMPTY)
                {
                    Logger.LogTrace("   Returning {ResultValue}:{ResultCode}", (int)result, result);
                    return new GetMessagesResult(_messageBuffer.ReadMessages(), result);
                }

                if (result != ResultCode.STATUS_NOERROR)
                {
                    Logger.LogError("PTReadMsgs failed: {ResultCode}", result);
                    return new GetMessagesResult(Array.Empty<Message>(), result);
                }

                var messages = _messageBuffer.ReadMessages();

                // Log each received message with hex data
                if (Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace))
                {
                    for (int i = 0; i < messages.Length; i++)
                    {
                        var msg = messages[i];
                        Logger.LogTrace("   Msg[{Index}] {Timestamp:F6}s. {ProtocolValue}:{Protocol}. Actual data {DataLength} bytes. RxS={RxStatus:X8}\n   RxStatus: {RxStatusFormatted}\n{HexData}",
                            i, msg.Timestamp / 1000000.0, (int)Protocol, Protocol, msg.Data.Length, (int)msg.RxStatus,
                            new RxFlagsFormatter(msg.RxStatus),
                            new HexFormatter(msg.Data));  // msg.Data is byte[], no allocation
                    }

                    Logger.LogTrace("PTReadMsgs() complete (channel {ChannelId})", _channelId);
                }
                return new GetMessagesResult(messages, ResultCode.STATUS_NOERROR);
            }
        }

        /// <summary>
        /// Sends a single message with the default flags and timeout.
        /// </summary>
        public J2534Result SendMessage(ReadOnlySpan<byte> data) => 
            SendMessage(data, DefaultTxFlags, DefaultTxTimeoutMs);

        /// <summary>
        /// Sends a single message with custom flags and default timeout.
        /// </summary>
        public J2534Result SendMessage(ReadOnlySpan<byte> data, TxFlag txFlags) => 
            SendMessage(data, txFlags, DefaultTxTimeoutMs);

        /// <summary>
        /// Sends a single message with custom flags and timeout.
        /// </summary>
        public J2534Result SendMessage(ReadOnlySpan<byte> data, TxFlag txFlags, int timeoutMs)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            if (Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace))
            {
                Logger.LogTrace("PTWriteMsgs({ChannelId}, 1, {TimeoutMs})\n   Msg[0] {ProtocolValue}:{Protocol}. {DataLength} bytes. TxF={TxFlags:X8}\n   TxFlags: {TxFlagsFormatted}\n{HexData}",
                    _channelId, timeoutMs, (int)Protocol, Protocol, data.Length, (int)txFlags,
                    new TxFlagsFormatter(txFlags),
                    new HexFormatter(data.ToArray()));  // Explicit allocation only when trace enabled
            }

            lock (_syncRoot)
            {
                _messageBuffer.WriteSingleMessage(data, txFlags);
                var result = _api.PTWriteMsgs(_channelId, _messageBuffer.GetMessagePointer(), _messageBuffer.CountPointer, timeoutMs);

                if (result != ResultCode.STATUS_NOERROR)
                {
                    Logger.LogError("PTWriteMsgs failed: {ResultCode}", result);
                    return J2534Result.Error(result, _api.GetLastError());
                }

                Logger.LogTrace("   Sent 1/1");
                return J2534Result.Success();
            }
        }

        /// <summary>
        /// Sends a message object.
        /// </summary>
        public J2534Result SendMessage(Message message) =>
            SendMessage(message, DefaultTxTimeoutMs);

        /// <summary>
        /// Sends a message object with custom timeout.
        /// </summary>
        public J2534Result SendMessage(Message message, int timeoutMs)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            var txFlags = message.TxFlags != TxFlag.NONE ? message.TxFlags : DefaultTxFlags;

            Logger.LogTrace("PTWriteMsgs({ChannelId}, 1, {TimeoutMs})\n   Msg[0] {ProtocolValue}:{Protocol}. {DataLength} bytes. TxF={TxFlags:X8}\n   TxFlags: {TxFlagsFormatted}\n{HexData}",
                _channelId, timeoutMs, (int)Protocol, Protocol, message.Data.Length, (int)txFlags,
                new TxFlagsFormatter(txFlags),
                new HexFormatter(message.Data));

            lock (_syncRoot)
            {
                _messageBuffer.WriteSingleMessage(message.Data, txFlags);
                var result = _api.PTWriteMsgs(_channelId, _messageBuffer.GetMessagePointer(), _messageBuffer.CountPointer, timeoutMs);

                if (result != ResultCode.STATUS_NOERROR)
                {
                    Logger.LogError("PTWriteMsgs failed: {ResultCode}", result);
                    return J2534Result.Error(result, _api.GetLastError());
                }

                Logger.LogTrace("   Sent 1/1");
                return J2534Result.Success();
            }
        }

        /// <summary>
        /// Sends multiple messages.
        /// </summary>
        public J2534Result SendMessages(ReadOnlySpan<Message> messages) =>
            SendMessages(messages, DefaultTxTimeoutMs);

        /// <summary>
        /// Sends multiple messages with custom timeout.
        /// </summary>
        public J2534Result SendMessages(ReadOnlySpan<Message> messages, int timeoutMs)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));
            if (messages.Length > 200)
                throw new ArgumentException("Cannot send more than 200 messages at once");

            if (Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace))
            {
                Logger.LogTrace("PTWriteMsgs({ChannelId}, {MessageCount}, {TimeoutMs})",
                    _channelId, messages.Length, timeoutMs);

                // Log each message being sent
                for (int i = 0; i < messages.Length; i++)
                {
                    var msg = messages[i];
                    var txFlags = msg.TxFlags != TxFlag.NONE ? msg.TxFlags : DefaultTxFlags;
                    Logger.LogTrace("   Msg[{Index}] {ProtocolValue}:{Protocol}. {DataLength} bytes. TxF={TxFlags:X8}\n   TxFlags: {TxFlagsFormatted}\n{HexData}",
                        i, (int)Protocol, Protocol, msg.Data.Length, (int)txFlags,
                        new TxFlagsFormatter(txFlags),
                        new HexFormatter(msg.Data));
                }
            }

            lock (_syncRoot)
            {
                _messageBuffer.WriteMessages(messages, DefaultTxFlags);
                var result = _api.PTWriteMsgs(_channelId, _messageBuffer.GetMessagePointer(), _messageBuffer.CountPointer, timeoutMs);

                if (result != ResultCode.STATUS_NOERROR)
                {
                    Logger.LogError("PTWriteMsgs failed: {ResultCode}", result);
                    return J2534Result.Error(result, _api.GetLastError());
                }

                Logger.LogTrace("   Sent {SentCount}/{TotalCount}", messages.Length, messages.Length);
                return J2534Result.Success();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _messageBuffer?.Dispose();

                try
                {
                    Logger.LogTrace("PTDisconnect({ChannelId})", _channelId);
                    var result = _api.PTDisconnect(_channelId);

                    if (result != ResultCode.STATUS_NOERROR)
                        Logger.LogError("PTDisconnect failed: {ResultCode}", result);
                }
                catch
                {
                    // Ignore logging errors during disposal
                    _api.PTDisconnect(_channelId);
                }
            }
        }
    }
}
