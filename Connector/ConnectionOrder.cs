using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugins.Connector
{
    internal static class ConnectionOrder
    {
        internal const string SettingName = "DeviceConnectionOrder";
        internal const string EnabledSettingName = "UseCustomDeviceConnectionOrder";
        internal const char Separator = '|';

        internal static IEnumerable<string> Normalize(IEnumerable<string> devices)
        {
            var configuredDevices = (devices ?? Enumerable.Empty<string>())
                .Where(ConnectorConstants.Devices.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return configuredDevices.Concat(ConnectorConstants.Devices.Where(device => !configuredDevices.Contains(device)));
        }
    }
}
