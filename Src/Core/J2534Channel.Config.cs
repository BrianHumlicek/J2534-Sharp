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
using SAE.J2534.Interop;

namespace SAE.J2534
{
    public sealed partial class J2534Channel
    {
        /// <summary>
        /// Gets a single configuration parameter value.
        /// </summary>
        public J2534Result<int> GetConfig(ConfigParameter parameter)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            return NativeHelper.GetSingleConfig(parameter, 
                (input, output) =>
                {
                    lock (_syncRoot)
                    {
                        return _api.PTIoctl(_channelId, (int)IOCTL.GET_CONFIG, input, output);
                    }
                });
        }

        /// <summary>
        /// Sets a single configuration parameter value.
        /// </summary>
        public J2534Result SetConfig(ConfigParameter parameter, int value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            return NativeHelper.SetSingleConfig(parameter, value,
                (input, output) =>
                {
                    lock (_syncRoot)
                    {
                        return _api.PTIoctl(_channelId, (int)IOCTL.SET_CONFIG, input, output);
                    }
                });
        }

        /// <summary>
        /// Gets multiple configuration parameters.
        /// </summary>
        public J2534Result<SConfig[]> GetConfig(SConfig[] parameters)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            return NativeHelper.GetMultipleConfig(parameters,
                (input, output) =>
                {
                    lock (_syncRoot)
                    {
                        return _api.PTIoctl(_channelId, (int)IOCTL.GET_CONFIG, input, output);
                    }
                });
        }

        /// <summary>
        /// Sets multiple configuration parameters.
        /// </summary>
        public J2534Result SetConfig(SConfig[] parameters)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            return NativeHelper.SetMultipleConfig(parameters,
                (input, output) =>
                {
                    lock (_syncRoot)
                    {
                        return _api.PTIoctl(_channelId, (int)IOCTL.SET_CONFIG, input, output);
                    }
                });
        }

        /// <summary>
        /// Performs a 5-baud initialization (ISO9141).
        /// </summary>
        public J2534Result<byte[]> FiveBaudInit(byte targetAddress)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            unsafe
            {
                byte* inputBuffer = stackalloc byte[9];
                byte* outputBuffer = stackalloc byte[9];

                // Setup input: NumOfBytes + pointer
                *(int*)inputBuffer = 1;
                *(byte**)(inputBuffer + 4) = inputBuffer + 8;
                inputBuffer[8] = targetAddress;

                // Setup output: NumOfBytes + pointer  
                *(int*)outputBuffer = 2;
                *(byte**)(outputBuffer + 4) = outputBuffer + 8;

                lock (_syncRoot)
                {
                    var result = _api.PTIoctl(_channelId, (int)IOCTL.FIVE_BAUD_INIT, (IntPtr)inputBuffer, (IntPtr)outputBuffer);
                    
                    if (result != ResultCode.STATUS_NOERROR)
                        return J2534Result<byte[]>.Error(result, _api.GetLastError());

                    return J2534Result<byte[]>.Success(new byte[] { outputBuffer[8], outputBuffer[9] });
                }
            }
        }

        /// <summary>
        /// Performs a fast initialization (ISO14230).
        /// </summary>
        public J2534Result<Message> FastInit(Message txMessage)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));
            if (txMessage.Data == null) throw new ArgumentNullException(nameof(txMessage));

            using var inputBuffer = new NativeMessageBuffer(Protocol, 1);
            using var outputBuffer = new NativeMessageBuffer(Protocol, 1);

            inputBuffer.WriteSingleMessage(txMessage.Data, txMessage.TxFlags);

            lock (_syncRoot)
            {
                var result = _api.PTIoctl(_channelId, (int)IOCTL.FAST_INIT, inputBuffer.GetMessagePointer(), outputBuffer.GetMessagePointer());
                
                if (result != ResultCode.STATUS_NOERROR)
                    return J2534Result<Message>.Error(result, _api.GetLastError());

                return J2534Result<Message>.Success(outputBuffer.ReadMessage(0));
            }
        }

        /// <summary>
        /// Clears the functional message lookup table.
        /// </summary>
        public J2534Result ClearFunctionalMessageLookupTable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            lock (_syncRoot)
            {
                var result = _api.PTIoctl(_channelId, (int)IOCTL.CLEAR_FUNCT_MSG_LOOKUP_TABLE, IntPtr.Zero, IntPtr.Zero);
                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result, _api.GetLastError());
            }
        }

        /// <summary>
        /// Adds an address to the functional message lookup table.
        /// </summary>
        public J2534Result AddToFunctionalMessageLookupTable(byte address)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            unsafe
            {
                byte* buffer = stackalloc byte[9];
                *(int*)buffer = 1;
                *(byte**)(buffer + 4) = buffer + 8;
                buffer[8] = address;

                lock (_syncRoot)
                {
                    var result = _api.PTIoctl(_channelId, (int)IOCTL.ADD_TO_FUNCT_MSG_LOOKUP_TABLE, (IntPtr)buffer, IntPtr.Zero);
                    return result == ResultCode.STATUS_NOERROR
                        ? J2534Result.Success()
                        : J2534Result.Error(result, _api.GetLastError());
                }
            }
        }

        /// <summary>
        /// Adds multiple addresses to the functional message lookup table.
        /// </summary>
        public J2534Result AddToFunctionalMessageLookupTable(ReadOnlySpan<byte> addresses)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));
            if (addresses.Length == 0) return J2534Result.Success();

            unsafe
            {
                byte* buffer = stackalloc byte[8 + addresses.Length];
                *(int*)buffer = addresses.Length;
                *(byte**)(buffer + 4) = buffer + 8;
                addresses.CopyTo(new Span<byte>(buffer + 8, addresses.Length));

                lock (_syncRoot)
                {
                    var result = _api.PTIoctl(_channelId, (int)IOCTL.ADD_TO_FUNCT_MSG_LOOKUP_TABLE, (IntPtr)buffer, IntPtr.Zero);
                    return result == ResultCode.STATUS_NOERROR
                        ? J2534Result.Success()
                        : J2534Result.Error(result, _api.GetLastError());
                }
            }
        }

        /// <summary>
        /// Removes an address from the functional message lookup table.
        /// </summary>
        public J2534Result DeleteFromFunctionalMessageLookupTable(byte address)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));

            unsafe
            {
                byte* buffer = stackalloc byte[9];
                *(int*)buffer = 1;
                *(byte**)(buffer + 4) = buffer + 8;
                buffer[8] = address;

                lock (_syncRoot)
                {
                    var result = _api.PTIoctl(_channelId, (int)IOCTL.DELETE_FROM_FUNCT_MSG_LOOKUP_TABLE, (IntPtr)buffer, IntPtr.Zero);
                    return result == ResultCode.STATUS_NOERROR
                        ? J2534Result.Success()
                        : J2534Result.Error(result, _api.GetLastError());
                }
            }
        }

        /// <summary>
        /// Removes multiple addresses from the functional message lookup table.
        /// </summary>
        public J2534Result DeleteFromFunctionalMessageLookupTable(ReadOnlySpan<byte> addresses)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Channel));
            if (addresses.Length == 0) return J2534Result.Success();

            unsafe
            {
                byte* buffer = stackalloc byte[8 + addresses.Length];
                *(int*)buffer = addresses.Length;
                *(byte**)(buffer + 4) = buffer + 8;
                addresses.CopyTo(new Span<byte>(buffer + 8, addresses.Length));

                lock (_syncRoot)
                {
                    var result = _api.PTIoctl(_channelId, (int)IOCTL.DELETE_FROM_FUNCT_MSG_LOOKUP_TABLE, (IntPtr)buffer, IntPtr.Zero);
                    return result == ResultCode.STATUS_NOERROR
                        ? J2534Result.Success()
                        : J2534Result.Error(result, _api.GetLastError());
                }
            }
        }

        /// <summary>
        /// Convenience: Sets programming voltage through the parent device.
        /// </summary>
        public J2534Result SetProgrammingVoltage(Pin pin, int voltageMillivolts) =>
            _device.SetProgrammingVoltage(pin, voltageMillivolts);

        /// <summary>
        /// Convenience: Measures battery voltage through the parent device.
        /// </summary>
        public J2534Result<int> MeasureBatteryVoltage() =>
            _device.MeasureBatteryVoltage();

        /// <summary>
        /// Convenience: Measures programming voltage through the parent device.
        /// </summary>
        public J2534Result<int> MeasureProgrammingVoltage() =>
            _device.MeasureProgrammingVoltage();
    }
}
