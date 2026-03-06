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
    /// Shim functions to adapt v2.02 API calls to v4.04 interface.
    /// </summary>
    public sealed partial class J2534API
    {
        private string _shimDeviceName = string.Empty;
        private int _shimDeviceId = 0;
        private bool _shimIsOpen = false;

        private ResultCode OpenShim(IntPtr pDeviceName, IntPtr pDeviceID)
        {
            string deviceName = pDeviceName == IntPtr.Zero 
                ? string.Empty 
                : System.Runtime.InteropServices.Marshal.PtrToStringAnsi(pDeviceName) ?? string.Empty;

            if (!_shimIsOpen)
            {
                _shimDeviceName = deviceName;
                _shimIsOpen = true;
                System.Runtime.InteropServices.Marshal.WriteInt32(pDeviceID, _shimDeviceId);
                return ResultCode.STATUS_NOERROR;
            }

            if (_shimIsOpen && deviceName == _shimDeviceName)
                return ResultCode.DEVICE_IN_USE;

            return ResultCode.INVALID_DEVICE_ID;
        }

        private ResultCode CloseShim(int deviceID)
        {
            if (!_shimIsOpen || deviceID != _shimDeviceId)
                return ResultCode.INVALID_DEVICE_ID;
            
            _shimIsOpen = false;
            return ResultCode.STATUS_NOERROR;
        }

        private ResultCode ConnectShim(int deviceID, int protocolID, int connectFlags, int baudRate, IntPtr pChannelID)
        {
            if (deviceID != _shimDeviceId)
                return ResultCode.INVALID_DEVICE_ID;
            
            return PTConnectv202(protocolID, connectFlags, pChannelID);
        }

        private ResultCode SetVoltageShim(int deviceID, int pinNumber, int voltage)
        {
            if (deviceID != _shimDeviceId)
                return ResultCode.INVALID_DEVICE_ID;
            
            return PTSetProgrammingVoltagev202(pinNumber, voltage);
        }

        private ResultCode ReadVersionShim(int deviceID, IntPtr pFirmwareVersion, IntPtr pDllVersion, IntPtr pApiVersion)
        {
            if (deviceID != _shimDeviceId)
                return ResultCode.INVALID_DEVICE_ID;
            
            return PTReadVersionv202(pFirmwareVersion, pDllVersion, pApiVersion);
        }
    }
}
