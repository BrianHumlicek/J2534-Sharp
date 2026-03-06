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
    /// Helper methods for working with native memory and marshaling.
    /// </summary>
    internal static unsafe class NativeHelper
    {
        /// <summary>
        /// Calls a native function with an int output parameter and returns the value.
        /// </summary>
        public static J2534Result<int> CallWithIntOut(Func<IntPtr, ResultCode> nativeCall)
        {
            int value;
            var result = nativeCall((IntPtr)(&value));
            return result == ResultCode.STATUS_NOERROR
                ? J2534Result<int>.Success(value)
                : J2534Result<int>.Error(result);
        }

        /// <summary>
        /// Calls a native function with a string output buffer and returns the string.
        /// </summary>
        public static J2534Result<string> CallWithStringOut(int bufferSize, Func<IntPtr, ResultCode> nativeCall)
        {
            byte* buffer = stackalloc byte[bufferSize];
            var result = nativeCall((IntPtr)buffer);
            
            if (result != ResultCode.STATUS_NOERROR)
                return J2534Result<string>.Error(result);
            
            string value = Marshal.PtrToStringAnsi((IntPtr)buffer) ?? string.Empty;
            return J2534Result<string>.Success(value);
        }

        /// <summary>
        /// Allocates native memory for a string parameter.
        /// </summary>
        public static IntPtr AllocString(string? value)
        {
            return string.IsNullOrEmpty(value) 
                ? IntPtr.Zero 
                : Marshal.StringToHGlobalAnsi(value);
        }

        /// <summary>
        /// Frees native string memory if allocated.
        /// </summary>
        public static void FreeString(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }

        /// <summary>
        /// Creates a native SConfig list structure on the stack for single parameter get/set.
        /// </summary>
        public static J2534Result<int> GetSingleConfig(ConfigParameter parameter, Func<IntPtr, IntPtr, ResultCode> ioctl)
        {
            var config = new SConfig(parameter, 0);
            var list = new NativeSConfigList(1, &config);

            var result = ioctl((IntPtr)(&list), IntPtr.Zero);

            return result == ResultCode.STATUS_NOERROR
                ? J2534Result<int>.Success(config.Value)
                : J2534Result<int>.Error(result);
        }

        /// <summary>
        /// Sets a single configuration parameter.
        /// </summary>
        public static J2534Result SetSingleConfig(ConfigParameter parameter, int value, Func<IntPtr, IntPtr, ResultCode> ioctl)
        {
            var config = new SConfig(parameter, value);
            var list = new NativeSConfigList(1, &config);

            var result = ioctl((IntPtr)(&list), IntPtr.Zero);

            return result == ResultCode.STATUS_NOERROR
                ? J2534Result.Success()
                : J2534Result.Error(result);
        }

        /// <summary>
        /// Gets multiple configuration parameters.
        /// </summary>
        public static J2534Result<SConfig[]> GetMultipleConfig(SConfig[] configs, Func<IntPtr, IntPtr, ResultCode> ioctl)
        {
            if (configs == null || configs.Length == 0)
                return J2534Result<SConfig[]>.Error(ResultCode.NULL_PARAMETER);

            fixed (SConfig* pConfigs = configs)
            {
                var list = new NativeSConfigList(configs.Length, pConfigs);
                var result = ioctl((IntPtr)(&list), IntPtr.Zero);

                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result<SConfig[]>.Success(configs)
                    : J2534Result<SConfig[]>.Error(result);
            }
        }

        /// <summary>
        /// Sets multiple configuration parameters.
        /// </summary>
        public static J2534Result SetMultipleConfig(SConfig[] configs, Func<IntPtr, IntPtr, ResultCode> ioctl)
        {
            if (configs == null || configs.Length == 0)
                return J2534Result.Error(ResultCode.NULL_PARAMETER);

            fixed (SConfig* pConfigs = configs)
            {
                var list = new NativeSConfigList(configs.Length, pConfigs);
                var result = ioctl((IntPtr)(&list), IntPtr.Zero);

                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result.Success()
                    : J2534Result.Error(result);
            }
        }

        /// <summary>
        /// Queries a single device capability parameter.
        /// Returns the value and whether the capability is supported by the device.
        /// </summary>
        public static J2534Result<(int Value, bool Supported)> GetDeviceCapability(DeviceInfo capability, Func<IntPtr, IntPtr, ResultCode> ioctl)
        {
            var param = new SParam(capability, 0, 0);
            var list = new NativeSParamList(1, &param);

            var result = ioctl((IntPtr)(&list), IntPtr.Zero);

            return result == ResultCode.STATUS_NOERROR
                ? J2534Result<(int, bool)>.Success((param.Value, param.Supported != 0))
                : J2534Result<(int, bool)>.Error(result);
        }

        /// <summary>
        /// Queries multiple device capability parameters.
        /// </summary>
        public static J2534Result<SParam[]> GetDeviceCapabilities(SParam[] parameters, Func<IntPtr, IntPtr, ResultCode> ioctl)
        {
            if (parameters == null || parameters.Length == 0)
                return J2534Result<SParam[]>.Error(ResultCode.NULL_PARAMETER);

            fixed (SParam* pParams = parameters)
            {
                var list = new NativeSParamList(parameters.Length, pParams);
                var result = ioctl((IntPtr)(&list), IntPtr.Zero);

                return result == ResultCode.STATUS_NOERROR
                    ? J2534Result<SParam[]>.Success(parameters)
                    : J2534Result<SParam[]>.Error(result);
            }
        }
    }

    /// <summary>
    /// Native J2534 SConfig list structure (array header + pointer to array).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeSConfigList
    {
        public int NumOfParams;
        public SConfig* ConfigPtr;

        public NativeSConfigList(int count, SConfig* ptr)
        {
            NumOfParams = count;
            ConfigPtr = ptr;
        }
    }

    /// <summary>
    /// Native J2534 SParam list structure for device capability queries.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeSParamList
    {
        public int NumOfParams;
        public SParam* ParamPtr;

        public NativeSParamList(int count, SParam* ptr)
        {
            NumOfParams = count;
            ParamPtr = ptr;
        }
    }

    /// <summary>
    /// Native J2534 byte array structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeByteArray
    {
        public int NumOfBytes;
        public byte* BytePtr;

        public NativeByteArray(int count, byte* ptr)
        {
            NumOfBytes = count;
            BytePtr = ptr;
        }

        public static NativeByteArray Create(ReadOnlySpan<byte> data, byte* stackBuffer)
        {
            data.CopyTo(new Span<byte>(stackBuffer, data.Length));
            return new NativeByteArray(data.Length, stackBuffer);
        }
    }
}
