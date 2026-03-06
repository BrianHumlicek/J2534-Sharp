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

namespace SAE.J2534
{
    public sealed partial class J2534API
    {
        // Delegate definitions
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruOpenDelegate(IntPtr pDeviceName, IntPtr pDeviceID);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruCloseDelegate(int deviceID);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruConnectDelegate(int deviceID, int protocolID, int connectFlags, int baudRate, IntPtr pChannelID);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruDisconnectDelegate(int channelID);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruReadMsgsDelegate(int channelID, IntPtr pMsgArray, IntPtr pNumMsgs, int timeout);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruWriteMsgsDelegate(int channelID, IntPtr pMsgArray, IntPtr pNumMsgs, int timeout);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruStartPeriodicMsgDelegate(int channelID, IntPtr pMsg, IntPtr pMsgID, int interval);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruStopPeriodicMsgDelegate(int channelID, int msgID);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruStartMsgFilterDelegate(int channelID, int filterType, IntPtr pMaskMsg, IntPtr pPatternMsg, IntPtr pFlowControlMsg, IntPtr pFilterID);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruStopMsgFilterDelegate(int channelID, int filterID);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruSetProgrammingVoltageDelegate(int deviceID, int pinNumber, int voltage);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruReadVersionDelegate(int deviceID, IntPtr pFirmwareVersion, IntPtr pDllVersion, IntPtr pApiVersion);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruGetLastErrorDelegate(IntPtr pErrorDescription);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruIoctlDelegate(int handleID, int ioctlID, IntPtr pInput, IntPtr pOutput);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate ResultCode PassThruGetNextCarDAQDelegate(IntPtr pName, IntPtr pVersion, IntPtr pAddress);

        // v2.02 specific
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ResultCode PassThruConnectv202Delegate(int protocolID, int connectFlags, IntPtr pChannelID);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ResultCode PassThruSetProgrammingVoltagev202Delegate(int pinNumber, int voltage);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate ResultCode PassThruReadVersionv202Delegate(IntPtr pFirmwareVersion, IntPtr pDllVersion, IntPtr pApiVersion);

        // Function pointers
        internal PassThruOpenDelegate PTOpen = (_, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruCloseDelegate PTClose = _ => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruConnectDelegate PTConnect = (_, _, _, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruDisconnectDelegate PTDisconnect = _ => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruReadMsgsDelegate PTReadMsgs = (_, _, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruWriteMsgsDelegate PTWriteMsgs = (_, _, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruStartPeriodicMsgDelegate PTStartPeriodicMsg = (_, _, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruStopPeriodicMsgDelegate PTStopPeriodicMsg = (_, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruStartMsgFilterDelegate PTStartMsgFilter = (_, _, _, _, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruStopMsgFilterDelegate PTStopMsgFilter = (_, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruSetProgrammingVoltageDelegate PTSetProgrammingVoltage = (_, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruReadVersionDelegate PTReadVersion = (_, _, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruGetLastErrorDelegate PTGetLastError = _ => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruIoctlDelegate PTIoctl = (_, _, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        internal PassThruGetNextCarDAQDelegate PTGetNextCarDAQ = (_, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;

        private PassThruConnectv202Delegate PTConnectv202 = (_, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        private PassThruSetProgrammingVoltagev202Delegate PTSetProgrammingVoltagev202 = (_, _) => ResultCode.FUNCTION_NOT_ASSIGNED;
        private PassThruReadVersionv202Delegate PTReadVersionv202 = (_, _, _) => ResultCode.FUNCTION_NOT_ASSIGNED;

        private API_Signature LoadDelegates()
        {
            var signature = new API_Signature();

            // Try to load each function and update signature
            TryLoadFunction("PassThruOpen", ref PTOpen, ref signature, SAE_API.OPEN, OpenShim);
            TryLoadFunction("PassThruClose", ref PTClose, ref signature, SAE_API.CLOSE, CloseShim);
            
            // Connect needs special handling for v2.02 compatibility
            if (TryLoadFunction("PassThruConnect", out PassThruConnectDelegate? connect))
            {
                if (signature.SAE_API.HasFlag(SAE_API.OPEN))
                {
                    PTConnect = connect!;
                }
                else
                {
                    PTConnectv202 = Marshal.GetDelegateForFunctionPointer<PassThruConnectv202Delegate>(
                        NativeLibrary.GetExport(_libraryHandle, "PassThruConnect"));
                    PTConnect = ConnectShim;
                }
                signature.SAE_API |= SAE_API.CONNECT;
            }

            TryLoadFunction("PassThruDisconnect", ref PTDisconnect, ref signature, SAE_API.DISCONNECT);
            TryLoadFunction("PassThruReadMsgs", ref PTReadMsgs, ref signature, SAE_API.READMSGS);
            TryLoadFunction("PassThruWriteMsgs", ref PTWriteMsgs, ref signature, SAE_API.WRITEMSGS);
            TryLoadFunction("PassThruStartPeriodicMsg", ref PTStartPeriodicMsg, ref signature, SAE_API.STARTPERIODICMSG);
            TryLoadFunction("PassThruStopPeriodicMsg", ref PTStopPeriodicMsg, ref signature, SAE_API.STOPPERIODICMSG);
            TryLoadFunction("PassThruStartMsgFilter", ref PTStartMsgFilter, ref signature, SAE_API.STARTMSGFILTER);
            TryLoadFunction("PassThruStopMsgFilter", ref PTStopMsgFilter, ref signature, SAE_API.STOPMSGFILTER);
            
            // SetProgrammingVoltage with v2.02 compatibility
            if (TryLoadFunction("PassThruSetProgrammingVoltage", out PassThruSetProgrammingVoltageDelegate? setVoltage))
            {
                if (signature.SAE_API.HasFlag(SAE_API.OPEN))
                {
                    PTSetProgrammingVoltage = setVoltage!;
                }
                else
                {
                    PTSetProgrammingVoltagev202 = Marshal.GetDelegateForFunctionPointer<PassThruSetProgrammingVoltagev202Delegate>(
                        NativeLibrary.GetExport(_libraryHandle, "PassThruSetProgrammingVoltage"));
                    PTSetProgrammingVoltage = SetVoltageShim;
                }
                signature.SAE_API |= SAE_API.SETPROGRAMMINGVOLTAGE;
            }

            // ReadVersion with v2.02 compatibility
            if (TryLoadFunction("PassThruReadVersion", out PassThruReadVersionDelegate? readVersion))
            {
                if (signature.SAE_API.HasFlag(SAE_API.OPEN))
                {
                    PTReadVersion = readVersion!;
                }
                else
                {
                    PTReadVersionv202 = Marshal.GetDelegateForFunctionPointer<PassThruReadVersionv202Delegate>(
                        NativeLibrary.GetExport(_libraryHandle, "PassThruReadVersion"));
                    PTReadVersion = ReadVersionShim;
                }
                signature.SAE_API |= SAE_API.READVERSION;
            }

            TryLoadFunction("PassThruGetLastError", ref PTGetLastError, ref signature, SAE_API.GETLASTERROR);
            TryLoadFunction("PassThruIoctl", ref PTIoctl, ref signature, SAE_API.IOCTL);
            
            // DrewTech extensions
            if (TryLoadFunctionNoFlag("PassThruGetNextCarDAQ", ref PTGetNextCarDAQ))
                signature.DREWTECH_API |= DrewTech_API.GETNEXTCARDAQ;

            return signature;
        }

        private bool TryLoadFunction<T>(string name, ref T field, ref API_Signature signature, SAE_API flag, T? shimFunction = null) where T : Delegate
        {
            if (NativeLibrary.TryGetExport(_libraryHandle, name, out IntPtr address))
            {
                field = Marshal.GetDelegateForFunctionPointer<T>(address);
                signature.SAE_API |= flag;
                return true;
            }
            else if (shimFunction != null)
            {
                field = shimFunction;
                return true;
            }
            return false;
        }

        private bool TryLoadFunctionNoFlag<T>(string name, ref T field) where T : Delegate
        {
            if (NativeLibrary.TryGetExport(_libraryHandle, name, out IntPtr address))
            {
                field = Marshal.GetDelegateForFunctionPointer<T>(address);
                return true;
            }
            return false;
        }

        private bool TryLoadFunction<T>(string name, out T? function) where T : Delegate
        {
            if (NativeLibrary.TryGetExport(_libraryHandle, name, out IntPtr address))
            {
                function = Marshal.GetDelegateForFunctionPointer<T>(address);
                return true;
            }
            function = null;
            return false;
        }
    }
}
