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

namespace SAE.J2534
{
    /// <summary>
    /// Represents an opened J2534 Pass Thru device.
    /// </summary>
    public sealed class J2534Device : IDisposable
    {
        private readonly J2534API _api;
        private readonly int _deviceId;
        private readonly object _syncRoot;
        private readonly List<J2534Channel> _channels = new List<J2534Channel>();
        private bool _disposed;

        public string DeviceName { get; }
        public string FirmwareVersion { get; }
        public string DriverVersion { get; }
        public string ApiVersion { get; }

        internal J2534Device(J2534API api, int deviceId, string deviceName, object syncRoot)
        {
            _api = api;
            _deviceId = deviceId;
            DeviceName = deviceName;
            _syncRoot = syncRoot;

            // Read version information
            var versionResult = ReadVersionInfo();
            if (versionResult.IsSuccess)
            {
                FirmwareVersion = versionResult.Value.Firmware;
                DriverVersion = versionResult.Value.Driver;
                ApiVersion = versionResult.Value.Api;
            }
            else
            {
                FirmwareVersion = DriverVersion = ApiVersion = "Unknown";
            }
        }

        /// <summary>
        /// Opens a communication channel on the device.
        /// </summary>
        public J2534Result<J2534Channel> OpenChannel(Protocol protocol, Baud baudRate, ConnectFlag connectFlags = ConnectFlag.NONE)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Device));

            unsafe
            {
                int channelId;
                
                lock (_syncRoot)
                {
                    var result = _api.PTConnect(_deviceId, (int)protocol, (int)connectFlags, (int)baudRate, (IntPtr)(&channelId));
                    
                    if (result != ResultCode.STATUS_NOERROR)
                        return J2534Result<J2534Channel>.Error(result, _api.GetLastError());
                }

                var channel = new J2534Channel(_api, this, channelId, protocol, baudRate, connectFlags, _syncRoot);
                _channels.Add(channel);
                
                return J2534Result<J2534Channel>.Success(channel);
            }
        }

        /// <summary>
        /// Sets programming voltage on a specific pin.
        /// </summary>
        public J2534Result SetProgrammingVoltage(Pin pin, int voltageMillivolts)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Device));

            lock (_syncRoot)
            {
                var result = _api.PTSetProgrammingVoltage(_deviceId, (int)pin, voltageMillivolts);
                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result, _api.GetLastError());
            }
        }

        /// <summary>
        /// Measures the vehicle battery voltage.
        /// </summary>
        public J2534Result<int> MeasureBatteryVoltage()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Device));

            unsafe
            {
                int voltage;
                lock (_syncRoot)
                {
                    var result = _api.PTIoctl(_deviceId, (int)IOCTL.READ_VBATT, IntPtr.Zero, (IntPtr)(&voltage));
                    return result == ResultCode.STATUS_NOERROR
                        ? J2534Result<int>.Success(voltage)
                        : J2534Result<int>.Error(result, _api.GetLastError());
                }
            }
        }

        /// <summary>
        /// Measures the programming voltage being delivered.
        /// </summary>
        public J2534Result<int> MeasureProgrammingVoltage()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Device));

            unsafe
            {
                int voltage;
                lock (_syncRoot)
                {
                    var result = _api.PTIoctl(_deviceId, (int)IOCTL.READ_PROG_VOLTAGE, IntPtr.Zero, (IntPtr)(&voltage));
                    return result == ResultCode.STATUS_NOERROR
                        ? J2534Result<int>.Success(voltage)
                        : J2534Result<int>.Error(result, _api.GetLastError());
                }
            }
        }

        /// <summary>
        /// Queries a single device capability.
        /// Returns the capability value and whether it is supported by the device.
        /// </summary>
        public J2534Result<(int Value, bool Supported)> GetDeviceCapability(DeviceInfo capability)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Device));

            return Interop.NativeHelper.GetDeviceCapability(capability,
                (input, output) =>
                {
                    lock (_syncRoot)
                    {
                        return _api.PTIoctl(_deviceId, (int)IOCTL.GET_CONFIG, input, output);
                    }
                });
        }

        /// <summary>
        /// Queries multiple device capabilities.
        /// </summary>
        public J2534Result<SParam[]> GetDeviceCapabilities(params DeviceInfo[] capabilities)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534Device));
            if (capabilities == null || capabilities.Length == 0)
                return J2534Result<SParam[]>.Error(ResultCode.NULL_PARAMETER);

            var parameters = new SParam[capabilities.Length];
            for (int i = 0; i < capabilities.Length; i++)
            {
                parameters[i] = new SParam(capabilities[i], 0, 0);
            }

            return Interop.NativeHelper.GetDeviceCapabilities(parameters,
                (input, output) =>
                {
                    lock (_syncRoot)
                    {
                        return _api.PTIoctl(_deviceId, (int)IOCTL.GET_CONFIG, input, output);
                    }
                });
        }

        private unsafe J2534Result<(string Firmware, string Driver, string Api)> ReadVersionInfo()
        {
            const int bufferSize = 80;
            byte* firmwareBuffer = stackalloc byte[bufferSize];
            byte* driverBuffer = stackalloc byte[bufferSize];
            byte* apiBuffer = stackalloc byte[bufferSize];

            lock (_syncRoot)
            {
                var result = _api.PTReadVersion(
                    _deviceId,
                    (IntPtr)firmwareBuffer,
                    (IntPtr)driverBuffer,
                    (IntPtr)apiBuffer);

                if (result != ResultCode.STATUS_NOERROR)
                    return J2534Result<(string, string, string)>.Error(result);

                var firmware = System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)firmwareBuffer) ?? string.Empty;
                var driver = System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)driverBuffer) ?? string.Empty;
                var api = System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)apiBuffer) ?? string.Empty;

                return J2534Result<(string, string, string)>.Success((firmware, driver, api));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Dispose all channels first
                foreach (var channel in _channels)
                {
                    channel?.Dispose();
                }
                _channels.Clear();

                // Close the device
                _api.CloseDeviceInternal(_deviceId);
            }
        }
    }
}
