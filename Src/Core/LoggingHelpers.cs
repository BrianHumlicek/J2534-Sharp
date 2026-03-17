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
using System.Text;

namespace SAE.J2534
{
    /// <summary>
    /// Deferred hex formatter - only formats when ToString() is called (i.e., when logging is enabled).
    /// Does not accept ReadOnlySpan to avoid hidden allocations. Use byte[] or ReadOnlyMemory directly.
    /// </summary>
    public readonly struct HexFormatter
    {
        private readonly ReadOnlyMemory<byte> _data;

        public HexFormatter(byte[] data) => _data = data;

        public HexFormatter(ReadOnlyMemory<byte> data) => _data = data;

        public override string ToString() => LoggingHelpers.FormatMultiLineHex(_data.Span);
    }

    /// <summary>
    /// Deferred TxFlag formatter - only formats when ToString() is called
    /// </summary>
    public readonly struct TxFlagsFormatter
    {
        private readonly TxFlag _flags;

        public TxFlagsFormatter(TxFlag flags) => _flags = flags;

        public override string ToString() => LoggingHelpers.FormatTxFlags(_flags);
    }

    /// <summary>
    /// Deferred RxFlag formatter - only formats when ToString() is called
    /// </summary>
    public readonly struct RxFlagsFormatter
    {
        private readonly RxFlag _flags;

        public RxFlagsFormatter(RxFlag flags) => _flags = flags;

        public override string ToString() => LoggingHelpers.FormatRxFlags(_flags);
    }

    /// <summary>
    /// Deferred ConnectFlag formatter - only formats when ToString() is called
    /// </summary>
    public readonly struct ConnectFlagsFormatter
    {
        private readonly ConnectFlag _flags;

        public ConnectFlagsFormatter(ConnectFlag flags) => _flags = flags;

        public override string ToString() => LoggingHelpers.FormatConnectFlags(_flags);
    }

    internal static class LoggingHelpers
    {
        /// <summary>
        /// Formats byte array as hex octets like "00 01 AB FF"
        /// </summary>
        public static string FormatHexOctets(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return "";
            
            var sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Formats byte array as bracketed hex like "[00][01][AB][FF]"
        /// </summary>
        public static string FormatBracketedHex(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return "";
            
            var sb = new StringBuilder(data.Length * 4);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append('[');
                sb.Append(data[i].ToString("x2"));
                sb.Append(']');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Formats byte array with line wrapping, using \__ prefix for continuation lines
        /// </summary>
        public static string FormatMultiLineHex(ReadOnlySpan<byte> data, int bytesPerLine = 16)
        {
            if (data.Length == 0) return "\\__ (empty)";
            
            var sb = new StringBuilder();
            sb.Append("\\__ ");
            
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0)
                {
                    if (i % bytesPerLine == 0)
                    {
                        sb.AppendLine();
                        sb.Append("     ");
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append(' ');
                sb.Append(data[i].ToString("x2"));
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Formats TxFlag enum for display
        /// </summary>
        public static string FormatTxFlags(TxFlag flags)
        {
            if (flags == TxFlag.NONE)
                return "{no TxFlags}";
            
            var parts = new System.Collections.Generic.List<string>();
            
            if ((flags & TxFlag.ISO15765_FRAME_PAD) != 0)
                parts.Add("ISO15765_FRAME_PAD");
            if ((flags & TxFlag.ISO15765_ADDR_TYPE) != 0)
                parts.Add("ISO15765_ADDR_TYPE");
            if ((flags & TxFlag.CAN_29BIT_ID) != 0)
                parts.Add("CAN_29BIT_ID");
            if ((flags & TxFlag.WAIT_P3_MIN_ONLY) != 0)
                parts.Add("WAIT_P3_MIN_ONLY");
            if ((flags & TxFlag.SCI_MODE) != 0)
                parts.Add("SCI_MODE");
            if ((flags & TxFlag.SCI_TX_VOLTAGE) != 0)
                parts.Add("SCI_TX_VOLTAGE");
            
            return "{" + string.Join(", ", parts) + "}";
        }

        /// <summary>
        /// Formats RxFlag enum for display
        /// </summary>
        public static string FormatRxFlags(RxFlag flags)
        {
            if (flags == RxFlag.NONE)
                return "{no RxFlags}";

            var parts = new System.Collections.Generic.List<string>();

            if ((flags & RxFlag.TX_MSG_TYPE) != 0)
                parts.Add("TX_MSG_TYPE");
            if ((flags & RxFlag.TX_INDICATION) != 0)
                parts.Add("TX_INDICATION");
            if ((flags & RxFlag.START_OF_MESSAGE) != 0)
                parts.Add("START_OF_MESSAGE");
            if ((flags & RxFlag.RX_BREAK) != 0)
                parts.Add("RX_BREAK");
            if ((flags & RxFlag.ISO15765_PADDING_ERROR) != 0)
                parts.Add("ISO15765_PADDING_ERROR");
            if ((flags & RxFlag.ISO15765_EXT_ADDR) != 0)
                parts.Add("ISO15765_EXT_ADDR");
            if ((flags & RxFlag.CAN_29BIT_ID) != 0)
                parts.Add("CAN_29BIT_ID");

            return "{" + string.Join(", ", parts) + "}";
        }

        /// <summary>
        /// Formats ConnectFlag enum for display
        /// </summary>
        public static string FormatConnectFlags(ConnectFlag flags)
        {
            if (flags == ConnectFlag.NONE)
                return "{no ConnectFlags}";
            
            var parts = new System.Collections.Generic.List<string>();
            
            if ((flags & ConnectFlag.CAN_29BIT_ID) != 0)
                parts.Add("CAN_29BIT_ID");
            if ((flags & ConnectFlag.ISO9141_NO_CHECKSUM) != 0)
                parts.Add("ISO9141_NO_CHECKSUM");
            if ((flags & ConnectFlag.CAN_ID_BOTH) != 0)
                parts.Add("CAN_ID_BOTH");
            if ((flags & ConnectFlag.ISO9141_K_LINE_ONLY) != 0)
                parts.Add("ISO9141_K_LINE_ONLY");
            
            return "{" + string.Join(", ", parts) + "}";
        }
    }
}
