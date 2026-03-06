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
using System.Linq;
using SAE.J2534.Interop;

namespace SAE.J2534
{
    public sealed partial class J2534Channel
    {
        /// <summary>
        /// Starts a periodic message transmission.
        /// </summary>
        public J2534Result<int> StartPeriodicMessage(PeriodicMessage message) =>
            StartPeriodicMessage(message, Protocol);

        /// <summary>
        /// Starts a periodic message transmission with a specific protocol.
        /// </summary>
        public J2534Result<int> StartPeriodicMessage(PeriodicMessage message, Protocol protocol)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));
            if (message == null) throw new ArgumentNullException(nameof(message));

            unsafe
            {
                int msgId;

                // Create a temporary buffer for the periodic message
                using var tempBuffer = new NativeMessageBuffer(protocol, 1);
                tempBuffer.WriteSingleMessage(message.Data, message.TxFlags);

                lock (_syncRoot)
                {
                    var result = _api.PTStartPeriodicMsg(
                        _channelId,
                        tempBuffer.GetMessagePointer(),
                        (IntPtr)(&msgId),
                        message.Interval);

                    if (result != ResultCode.STATUS_NOERROR)
                        return J2534Result<int>.Error(result, _api.GetLastError());

                    message.MessageId = msgId;
                    _periodicMessages.Add(message);
                    return J2534Result<int>.Success(msgId);
                }
            }
        }

        /// <summary>
        /// Stops a periodic message transmission.
        /// </summary>
        public J2534Result StopPeriodicMessage(int messageId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            lock (_syncRoot)
            {
                var result = _api.PTStopPeriodicMsg(_channelId, messageId);
                
                if (result == ResultCode.STATUS_NOERROR)
                {
                    _periodicMessages.RemoveAll(m => m.MessageId == messageId);
                    return J2534Result.Success();
                }
                
                return J2534Result.Error(result, _api.GetLastError());
            }
        }

        /// <summary>
        /// Starts a message filter.
        /// </summary>
        public J2534Result<int> StartMessageFilter(MessageFilter filter) =>
            StartMessageFilter(filter, Protocol);

        /// <summary>
        /// Starts a message filter with a specific protocol.
        /// </summary>
        public J2534Result<int> StartMessageFilter(MessageFilter filter, Protocol protocol)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            unsafe
            {
                int filterId;

                // Create temporary buffers for the filter messages
                using var maskBuffer = new NativeMessageBuffer(protocol, 1);
                using var patternBuffer = new NativeMessageBuffer(protocol, 1);
                using var flowControlBuffer = new NativeMessageBuffer(protocol, 1);

                maskBuffer.WriteSingleMessage(filter.Mask, filter.TxFlags);
                patternBuffer.WriteSingleMessage(filter.Pattern, filter.TxFlags);

                IntPtr flowControlPtr = IntPtr.Zero;
                if (filter.FilterType == Filter.FLOW_CONTROL_FILTER && filter.FlowControl.Length > 0)
                {
                    flowControlBuffer.WriteSingleMessage(filter.FlowControl, filter.TxFlags);
                    flowControlPtr = flowControlBuffer.GetMessagePointer();
                }

                lock (_syncRoot)
                {
                    var result = _api.PTStartMsgFilter(
                        _channelId,
                        (int)filter.FilterType,
                        maskBuffer.GetMessagePointer(),
                        patternBuffer.GetMessagePointer(),
                        flowControlPtr,
                        (IntPtr)(&filterId));

                    if (result != ResultCode.STATUS_NOERROR)
                        return J2534Result<int>.Error(result, _api.GetLastError());

                    filter.FilterId = filterId;
                    _filters.Add(filter);
                    return J2534Result<int>.Success(filterId);
                }
            }
        }

        /// <summary>
        /// Stops a message filter.
        /// </summary>
        public J2534Result StopMessageFilter(int filterId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            lock (_syncRoot)
            {
                var result = _api.PTStopMsgFilter(_channelId, filterId);
                
                if (result == ResultCode.STATUS_NOERROR)
                {
                    _filters.RemoveAll(f => f.FilterId == filterId);
                    return J2534Result.Success();
                }
                
                return J2534Result.Error(result, _api.GetLastError());
            }
        }

        /// <summary>
        /// Stops all message filters.
        /// </summary>
        public J2534Result StopAllMessageFilters()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            lock (_syncRoot)
            {
                var filterIds = _filters.Select(f => f.FilterId).ToList();
                foreach (var filterId in filterIds)
                {
                    var result = StopMessageFilter(filterId);
                    if (!result.IsSuccess)
                        return result;
                }
                return J2534Result.Success();
            }
        }

        /// <summary>
        /// Clears all periodic messages using IOCTL.
        /// </summary>
        public J2534Result ClearPeriodicMessages()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            lock (_syncRoot)
            {
                var result = _api.PTIoctl(_channelId, (int)IOCTL.CLEAR_PERIODIC_MSGS, IntPtr.Zero, IntPtr.Zero);
                
                if (result == ResultCode.STATUS_NOERROR)
                {
                    _periodicMessages.Clear();
                    return J2534Result.Success();
                }
                
                return J2534Result.Error(result, _api.GetLastError());
            }
        }

        /// <summary>
        /// Clears all message filters using IOCTL.
        /// </summary>
        public J2534Result ClearMessageFilters()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            lock (_syncRoot)
            {
                var result = _api.PTIoctl(_channelId, (int)IOCTL.CLEAR_MSG_FILTERS, IntPtr.Zero, IntPtr.Zero);
                
                if (result == ResultCode.STATUS_NOERROR)
                {
                    _filters.Clear();
                    return J2534Result.Success();
                }
                
                return J2534Result.Error(result, _api.GetLastError());
            }
        }

        /// <summary>
        /// Clears the transmit buffer.
        /// </summary>
        public J2534Result ClearTxBuffer()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            lock (_syncRoot)
            {
                var result = _api.PTIoctl(_channelId, (int)IOCTL.CLEAR_TX_BUFFER, IntPtr.Zero, IntPtr.Zero);
                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result, _api.GetLastError());
            }
        }

        /// <summary>
        /// Clears the receive buffer.
        /// </summary>
        public J2534Result ClearRxBuffer()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            lock (_syncRoot)
            {
                var result = _api.PTIoctl(_channelId, (int)IOCTL.CLEAR_RX_BUFFER, IntPtr.Zero, IntPtr.Zero);
                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result, _api.GetLastError());
            }
        }
    }
}
