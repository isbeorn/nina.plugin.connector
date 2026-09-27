using System;
using System.Collections.Generic;

namespace NINA.Plugins.Connector
{
    internal static class ConnectorConstants
    {
        // CONSTANTS
        internal const string CONNECTOR = "Connector";
        internal static readonly Guid PLUGIN_ID = Guid.Parse("52c17ee7-6d6c-4ee1-8fa0-85bcf6677bef");

        // DEVICE TYPE CONSTANTS
        internal const string CAMERA = "Camera";
        internal const string FILTER_WHEEL = "Filter Wheel";
        internal const string FOCUSER = "Focuser";
        internal const string ROTATOR = "Rotator";
        internal const string TELESCOPE = "Telescope";
        internal const string GUIDER = "Guider";
        internal const string SWITCH = "Switch";
        internal const string FLAT_PANEL = "Flat Panel";
        internal const string WEATHER = "Weather";
        internal const string DOME = "Dome";
        internal const string SAFETY_MONITOR = "Safety Monitor";

        internal static readonly IReadOnlyList<string> Devices = Array.AsReadOnly(new[] {
            CAMERA,
            FILTER_WHEEL,
            FOCUSER,
            ROTATOR,
            TELESCOPE,
            GUIDER,
            SWITCH,
            FLAT_PANEL,
            WEATHER,
            DOME,
            SAFETY_MONITOR
        });
    }
}
