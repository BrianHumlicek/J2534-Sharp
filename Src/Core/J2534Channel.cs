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

            lock (_syncRoot)
            {
                _messageBuffer.Count = count;
                var result = _api.PTReadMsgs(_channelId, _messageBuffer.GetMessagePointer(), _messageBuffer.CountPointer, timeoutMs);

                // These are expected results, not errors
                if (result == ResultCode.TIMEOUT || result == ResultCode.BUFFER_EMPTY)
                    return new GetMessagesResult(_messageBuffer.ReadMessages(), result);

                if (result != ResultCode.STATUS_NOERROR)
                    return new GetMessagesResult(Array.Empty<Message>(), result);

                return new GetMessagesResult(_messageBuffer.ReadMessages(), ResultCode.STATUS_NOERROR);
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

            lock (_syncRoot)
            {
                _messageBuffer.WriteSingleMessage(data, txFlags);
                var result = _api.PTWriteMsgs(_channelId, _messageBuffer.GetMessagePointer(), _messageBuffer.CountPointer, timeoutMs);
                
                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result, _api.GetLastError());
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

            lock (_syncRoot)
            {
                _messageBuffer.WriteSingleMessage(message.Data, message.TxFlags != TxFlag.NONE ? message.TxFlags : DefaultTxFlags);
                var result = _api.PTWriteMsgs(_channelId, _messageBuffer.GetMessagePointer(), _messageBuffer.CountPointer, timeoutMs);
                
                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result, _api.GetLastError());
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

            lock (_syncRoot)
            {
                _messageBuffer.WriteMessages(messages, DefaultTxFlags);
                var result = _api.PTWriteMsgs(_channelId, _messageBuffer.GetMessagePointer(), _messageBuffer.CountPointer, timeoutMs);
                
                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result, _api.GetLastError());
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _messageBuffer?.Dispose();
                _api.PTDisconnect(_channelId);
            }
        }
    }
}
