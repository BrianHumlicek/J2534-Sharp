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
using System.Runtime.InteropServices;

namespace SAE.J2534.Interop
{
    /// <summary>
    /// Manages a pinned native buffer for J2534 message arrays. This buffer is reused across
    /// multiple API calls to eliminate allocations in the hot path.
    /// </summary>
    internal sealed unsafe class NativeMessageBuffer : IDisposable
    {
        private const int MessageSize = 4152; // J2534 message struct size
        private const int MaxMessages = 200;  // Default buffer capacity

        private readonly byte* _buffer;
        private readonly Protocol _protocol;
        private readonly int _capacity;
        private readonly nuint _bufferSize;
        private bool _disposed;

        public IntPtr Pointer { get; }
        public IntPtr CountPointer { get; }

        public NativeMessageBuffer(Protocol protocol, int capacity = MaxMessages)
        {
            if (capacity < 1 || capacity > 1000)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be between 1 and 1000");

            _protocol = protocol;
            _capacity = capacity;

            // Allocate: 4 bytes for count + message array
            _bufferSize = (nuint)(4 + (MessageSize * capacity));
            _buffer = (byte*)NativeMemory.AllocZeroed(_bufferSize);

            Pointer = (IntPtr)_buffer;
            CountPointer = Pointer;
        }

        public int Count
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(NativeMessageBuffer));
                return *(int*)_buffer;
            }
            set
            {
                if (_disposed) throw new ObjectDisposedException(nameof(NativeMessageBuffer));
                if (value < 0 || value > _capacity)
                    throw new ArgumentOutOfRangeException(nameof(value));
                *(int*)_buffer = value;
            }
        }

        public IntPtr GetMessagePointer(int index = 0)
        {
            if (index < 0 || index >= _capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Pointer + 4 + (index * MessageSize);
        }

        public Span<byte> GetMessageSpan(int index)
        {
            if (index < 0 || index >= _capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
            int offset = 4 + (index * MessageSize);
            return new Span<byte>(_buffer + offset, MessageSize);
        }

        public void WriteMessage(int index, ReadOnlySpan<byte> data, TxFlag txFlags)
        {
            if (data.Length > MessageSize - 24)
                throw new ArgumentException($"Message data too large: {data.Length} bytes (max {MessageSize - 24})");

            var span = GetMessageSpan(index);
            span.Clear();

            int protocolId = (int)_protocol;
            int flags = (int)txFlags;
            int dataSize = data.Length;

            MemoryMarshal.Write(span, in protocolId);
            MemoryMarshal.Write(span.Slice(8), in flags);
            MemoryMarshal.Write(span.Slice(16), in dataSize);
            data.CopyTo(span.Slice(24));
        }

        public Message ReadMessage(int index)
        {
            var span = GetMessageSpan(index);
            
            var rxStatus = (RxFlag)MemoryMarshal.Read<int>(span.Slice(4, 4));
            var timestamp = MemoryMarshal.Read<uint>(span.Slice(12, 4));
            int dataSize = MemoryMarshal.Read<int>(span.Slice(16, 4));
            
            if (dataSize < 0 || dataSize > MessageSize - 24)
                throw new InvalidOperationException($"Invalid message data size: {dataSize}");
            
            var data = span.Slice(24, dataSize).ToArray();
            
            return new Message(data, rxStatus, timestamp);
        }

        public Message[] ReadMessages()
        {
            int count = Count;
            if (count == 0)
                return Array.Empty<Message>();

            var messages = new Message[count];
            for (int i = 0; i < count; i++)
            {
                messages[i] = ReadMessage(i);
            }
            return messages;
        }

        public void WriteMessages(ReadOnlySpan<Message> messages, TxFlag defaultFlags = TxFlag.NONE)
        {
            if (messages.Length > _capacity)
                throw new ArgumentException($"Too many messages: {messages.Length} (max {_capacity})");

            for (int i = 0; i < messages.Length; i++)
            {
                var msg = messages[i];
                WriteMessage(i, msg.Data, msg.TxFlags != TxFlag.NONE ? msg.TxFlags : defaultFlags);
            }
            Count = messages.Length;
        }

        public void WriteSingleMessage(ReadOnlySpan<byte> data, TxFlag txFlags = TxFlag.NONE)
        {
            WriteMessage(0, data, txFlags);
            Count = 1;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                NativeMemory.Free(_buffer);
            }
        }
    }
}
