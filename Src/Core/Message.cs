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

namespace SAE.J2534
{
    /// <summary>
    /// Represents a J2534 message with its associated metadata.
    /// </summary>
    public readonly struct Message
    {
        public byte[] Data { get; }
        public RxFlag RxStatus { get; }
        public TxFlag TxFlags { get; }
        public uint Timestamp { get; }

        public Message(byte[] data, TxFlag txFlags = TxFlag.NONE)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            TxFlags = txFlags;
            RxStatus = RxFlag.NONE;
            Timestamp = 0;
        }

        public Message(byte[] data, RxFlag rxStatus, uint timestamp)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            RxStatus = rxStatus;
            TxFlags = TxFlag.NONE;
            Timestamp = timestamp;
        }

        public Message(byte[] data, RxFlag rxStatus, TxFlag txFlags, uint timestamp)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            RxStatus = rxStatus;
            TxFlags = txFlags;
            Timestamp = timestamp;
        }

        public Message(ReadOnlySpan<byte> data, TxFlag txFlags = TxFlag.NONE)
        {
            Data = data.ToArray();
            TxFlags = txFlags;
            RxStatus = RxFlag.NONE;
            Timestamp = 0;
        }

        public Message(ReadOnlySpan<byte> data, RxFlag rxStatus, uint timestamp)
        {
            Data = data.ToArray();
            RxStatus = rxStatus;
            TxFlags = TxFlag.NONE;
            Timestamp = timestamp;
        }

        public override string ToString() => 
            $"Message[{Data.Length} bytes, RxStatus={RxStatus}, TxFlags={TxFlags}, Timestamp={Timestamp}]";
    }

    /// <summary>
    /// Represents the result of a GetMessages operation.
    /// </summary>
    public readonly record struct GetMessagesResult(Message[] Messages, ResultCode Status)
    {
        public bool IsSuccess => Status == ResultCode.STATUS_NOERROR;
        public bool IsTimeout => Status == ResultCode.TIMEOUT;
        public bool IsBufferEmpty => Status == ResultCode.BUFFER_EMPTY;
    }

    /// <summary>
    /// Represents a periodic message configuration.
    /// </summary>
    public sealed class PeriodicMessage
    {
        public byte[] Data { get; }
        public TxFlag TxFlags { get; }
        public int Interval { get; }
        public int MessageId { get; internal set; }

        public PeriodicMessage(byte[] data, int interval, TxFlag txFlags = TxFlag.NONE)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Interval = interval;
            TxFlags = txFlags;
        }

        public PeriodicMessage(ReadOnlySpan<byte> data, int interval, TxFlag txFlags = TxFlag.NONE)
        {
            Data = data.ToArray();
            Interval = interval;
            TxFlags = txFlags;
        }
    }

    /// <summary>
    /// Represents a message filter configuration.
    /// </summary>
    public sealed class MessageFilter
    {
        public Filter FilterType { get; set; }
        public byte[] Mask { get; set; } = Array.Empty<byte>();
        public byte[] Pattern { get; set; } = Array.Empty<byte>();
        public byte[] FlowControl { get; set; } = Array.Empty<byte>();
        public TxFlag TxFlags { get; set; }
        public int FilterId { get; internal set; }

        public MessageFilter() { }

        public MessageFilter(UserFilterType filterType, byte[] match)
        {
            switch (filterType)
            {
                case UserFilterType.PASSALL:
                    ConfigurePassAll();
                    break;
                case UserFilterType.PASS:
                    ConfigurePass(match);
                    break;
                case UserFilterType.BLOCK:
                    ConfigureBlock(match);
                    break;
                case UserFilterType.STANDARDISO15765:
                    ConfigureStandardISO15765(match);
                    break;
            }
        }

        private void ConfigurePassAll()
        {
            Mask = new byte[] { 0x00 };
            Pattern = new byte[] { 0x00 };
            FlowControl = Array.Empty<byte>();
            FilterType = Filter.PASS_FILTER;
        }

        private void ConfigurePass(byte[] match)
        {
            ConfigureExactMatch(match);
            FilterType = Filter.PASS_FILTER;
        }

        private void ConfigureBlock(byte[] match)
        {
            ConfigureExactMatch(match);
            FilterType = Filter.BLOCK_FILTER;
        }

        private void ConfigureExactMatch(byte[] match)
        {
            Mask = new byte[match.Length];
            Array.Fill(Mask, (byte)0xFF);
            Pattern = (byte[])match.Clone();
            FlowControl = Array.Empty<byte>();
        }

        private void ConfigureStandardISO15765(byte[] sourceAddress)
        {
            if (sourceAddress.Length != 4)
                throw new ArgumentException("ISO15765 address must be 4 bytes", nameof(sourceAddress));

            Mask = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            Pattern = (byte[])sourceAddress.Clone();
            Pattern[3] += 0x08;
            FlowControl = (byte[])sourceAddress.Clone();
            TxFlags = TxFlag.ISO15765_FRAME_PAD;
            FilterType = Filter.FLOW_CONTROL_FILTER;
        }
    }
}
