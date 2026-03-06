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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace SAE.J2534
{
    /// <summary>
    /// Factory for loading and discovering J2534 APIs.
    /// </summary>
    public static class J2534APIFactory
    {
        private static readonly Dictionary<string, J2534API> _cache = new Dictionary<string, J2534API>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// Loads a J2534 API from a DLL file. APIs are cached and reused.
        /// </summary>
        public static J2534Result<J2534API> LoadAPI(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return J2534Result<J2534API>.Error(ResultCode.NULL_PARAMETER, "File name cannot be null or empty");

            if (!File.Exists(fileName))
                return J2534Result<J2534API>.Error(ResultCode.DEVICE_NOT_CONNECTED, $"DLL not found: {fileName}");

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(fileName, out var cached))
                    return J2534Result<J2534API>.Success(cached);

                try
                {
                    var api = new J2534API(fileName);

                    // Verify it's a valid J2534 API
                    if (api.Signature.SAE_API != SAE_API.V202_SIGNATURE &&
                        api.Signature.SAE_API != SAE_API.V404_SIGNATURE &&
                        api.Signature.SAE_API != SAE_API.V500_SIGNATURE)
                    {
                        api.Dispose();
                        return J2534Result<J2534API>.Error(
                            ResultCode.FUNCTION_NOT_ASSIGNED,
                            $"No compatible J2534 export signature found in {fileName}");
                    }

                    _cache[fileName] = api;
                    return J2534Result<J2534API>.Success(api);
                }
                catch (Exception ex)
                {
                    return J2534Result<J2534API>.Error(
                        ResultCode.FAILED,
                        $"Failed to load API: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Discovers all registered J2534 APIs from the Windows registry.
        /// </summary>
        public static IEnumerable<APIInfo> DiscoverAPIs()
        {
            const string PASSTHRU_REGISTRY_PATH = @"Software\PassThruSupport.04.04";
            const string PASSTHRU_REGISTRY_PATH_6432 = @"Software\Wow6432Node\PassThruSupport.04.04";

            var rootKey = Registry.LocalMachine.OpenSubKey(PASSTHRU_REGISTRY_PATH, false)
                       ?? Registry.LocalMachine.OpenSubKey(PASSTHRU_REGISTRY_PATH_6432, false);

            if (rootKey == null)
                yield break;

            var protocolLabels = new Dictionary<string, string>
            {
                ["CAN"] = "CAN Bus",
                ["ISO15765"] = "ISO15765",
                ["J1850PWM"] = "J1850 PWM",
                ["J1850VPW"] = "J1850 VPW",
                ["ISO9141"] = "ISO9141",
                ["ISO14230"] = "ISO14230",
                ["SCI_A_ENGINE"] = "SCI-A Engine",
                ["SCI_A_TRANS"] = "SCI-A Transmission",
                ["SCI_B_ENGINE"] = "SCI-B Engine",
                ["SCI_B_TRANS"] = "SCI-B Transmission"
            };

            foreach (var entryName in rootKey.GetSubKeyNames())
            {
                using var deviceKey = rootKey.OpenSubKey(entryName);
                if (deviceKey == null) continue;

                var name = deviceKey.GetValue("Name") as string ?? string.Empty;
                var fileName = deviceKey.GetValue("FunctionLibrary") as string ?? string.Empty;

                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                var details = new StringBuilder();

                var vendor = deviceKey.GetValue("Vendor") as string;
                if (!string.IsNullOrWhiteSpace(vendor))
                    details.AppendLine($"Vendor: {vendor}");

                var configApp = deviceKey.GetValue("ConfigApplication") as string;
                if (!string.IsNullOrWhiteSpace(configApp))
                    details.AppendLine($"Configuration Application: {configApp}");

                foreach (var protocol in protocolLabels)
                {
                    if ((deviceKey.GetValue(protocol.Key) as int? ?? 0) != 0)
                        details.AppendLine(protocol.Value);
                }

                yield return new APIInfo(name, fileName, details.ToString().TrimEnd());
            }
        }

        /// <summary>
        /// Clears the API cache and disposes all loaded APIs.
        /// </summary>
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                foreach (var api in _cache.Values)
                {
                    api?.Dispose();
                }
                _cache.Clear();
            }
        }

        /// <summary>
        /// Gets all currently cached APIs.
        /// </summary>
        public static IReadOnlyCollection<J2534API> GetCachedAPIs()
        {
            lock (_cacheLock)
            {
                return _cache.Values.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Information about a registered J2534 API.
    /// </summary>
    public readonly record struct APIInfo(string Name, string FileName, string Details);
}
