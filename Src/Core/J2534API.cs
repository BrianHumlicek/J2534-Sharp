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
using Microsoft.Extensions.Logging;
using SAE.J2534.Interop;

namespace SAE.J2534
{
    /// <summary>
    /// Represents a loaded J2534 PassThru API library.
    /// </summary>
    public sealed partial class J2534API : IDisposable
    {
        private readonly IntPtr _libraryHandle;
        private readonly object _syncRoot = new object();
        private readonly string _fileName;
        private bool _disposed;

        public API_Signature Signature { get; }
        public string FileName => _fileName;

        /// <summary>
        /// Optional logger for API call tracing. Set this to enable detailed J2534 API logging.
        /// </summary>
        public Microsoft.Extensions.Logging.ILogger Logger { get; set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        internal J2534API(string fileName)
        {
            _fileName = fileName;
            _libraryHandle = NativeLibrary.Load(fileName);
            Signature = LoadDelegates();
        }

        /// <summary>
        /// Opens a J2534 device.
        /// </summary>
        /// <param name="deviceName">Device name, or empty/null for first available device.</param>
        public J2534Result<J2534Device> OpenDevice(string? deviceName = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534API));

            var result = OpenDeviceInternal(deviceName ?? string.Empty);
            if (!result.IsSuccess)
                return J2534Result<J2534Device>.Error(result.Status, result.ErrorMessage);

            var device = new J2534Device(this, result.Value.DeviceId, result.Value.DeviceName, _syncRoot);
            return J2534Result<J2534Device>.Success(device);
        }

        /// <summary>
        /// Resets the GetNextCarDAQ enumerator (DrewTech devices only).
        /// </summary>
        public J2534Result ResetCarDAQEnumerator()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534API));
            
            lock (_syncRoot)
            {
                var result = PTGetNextCarDAQ(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result);
            }
        }

        /// <summary>
        /// Enumerates connected DrewTech devices.
        /// </summary>
        public J2534Result<CarDAQInfo?> GetNextCarDAQ()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(J2534API));

            unsafe
            {
                IntPtr namePtr, verPtr, addrPtr;
                
                lock (_syncRoot)
                {
                    var result = PTGetNextCarDAQ((IntPtr)(&namePtr), (IntPtr)(&verPtr), (IntPtr)(&addrPtr));
                    
                    if (result != ResultCode.STATUS_NOERROR)
                        return J2534Result<CarDAQInfo?>.Error(result);

                    if (namePtr == IntPtr.Zero)
                        return J2534Result<CarDAQInfo?>.Success(null);

                    byte* version = (byte*)verPtr;
                    string versionStr = $"{version[2]}.{version[1]}.{version[0]}";
                    
                    var info = new CarDAQInfo(
                        Marshal.PtrToStringAnsi(namePtr) ?? string.Empty,
                        versionStr,
                        Marshal.PtrToStringAnsi(addrPtr) ?? string.Empty
                    );

                    return J2534Result<CarDAQInfo?>.Success(info);
                }
            }
        }

        internal unsafe J2534Result<(int DeviceId, string DeviceName)> OpenDeviceInternal(string deviceName)
        {
            var displayName = string.IsNullOrWhiteSpace(deviceName) ? "<default>" : deviceName;
            Logger.LogTrace("PTOpen({DeviceName})", displayName);

            IntPtr pName = IntPtr.Zero;
            try
            {
                pName = string.IsNullOrWhiteSpace(deviceName) ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(deviceName);
                int deviceId;

                lock (_syncRoot)
                {
                    var result = PTOpen(pName, (IntPtr)(&deviceId));
                    if (result != ResultCode.STATUS_NOERROR)
                    {
                        Logger.LogError("PTOpen failed: {ResultCode} - {ErrorMessage}", 
                            result, GetLastError());
                        return J2534Result<(int, string)>.Error(result, GetLastError());
                    }

                    Logger.LogTrace("   returning DeviceID {DeviceId}", deviceId);
                    return J2534Result<(int, string)>.Success((deviceId, deviceName));
                }
            }
            finally
            {
                if (pName != IntPtr.Zero)
                    Marshal.FreeHGlobal(pName);
            }
        }

        internal J2534Result CloseDeviceInternal(int deviceId)
        {
            Logger.LogTrace("PTClose({DeviceId})", deviceId);

            lock (_syncRoot)
            {
                var result = PTClose(deviceId);

                if (result != ResultCode.STATUS_NOERROR)
                {
                    Logger.LogError("PTClose failed: {ResultCode}", result);
                    return J2534Result.Error(result, GetLastError());
                }

                return J2534Result.Success();
            }
        }

        internal string GetLastError()
        {
            unsafe
            {
                byte* buffer = stackalloc byte[256];
                lock (_syncRoot)
                {
                    var result = PTGetLastError((IntPtr)buffer);
                    return result == ResultCode.STATUS_NOERROR
                        ? Marshal.PtrToStringAnsi((IntPtr)buffer) ?? string.Empty
                        : string.Empty;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_libraryHandle != IntPtr.Zero)
                    NativeLibrary.Free(_libraryHandle);
            }
        }
    }

    /// <summary>
    /// Information about a DrewTech CarDAQ device.
    /// </summary>
    public readonly record struct CarDAQInfo(string Name, string Version, string Address);
}
