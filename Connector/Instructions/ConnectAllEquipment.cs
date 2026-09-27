using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugins.Connector.Instructions
{
    public class ConnectAllEquipment : SequenceItem, IValidatable
    {
        private readonly IProfileService _profileService;
        private readonly ICameraMediator _cameraMediator;
        private readonly IFilterWheelMediator _fwMediator;
        private readonly IFocuserMediator _focuserMediator;
        private readonly IRotatorMediator _rotatorMediator;
        private readonly ITelescopeMediator _telescopeMediator;
        private readonly IGuiderMediator _guiderMediator;
        private readonly ISwitchMediator _switchMediator;
        private readonly IFlatDeviceMediator _flatDeviceMediator;
        private readonly IWeatherDataMediator _weatherDataMediator;
        private readonly IDomeMediator _domeMediator;
        private readonly ISafetyMonitorMediator _safetyMonitorMediator;

        [ImportingConstructor]
        public ConnectAllEquipment(IProfileService profileService,
                                ICameraMediator cameraMediator,
                                IFilterWheelMediator fwMediator,
                                IFocuserMediator focuserMediator,
                                IRotatorMediator rotatorMediator,
                                ITelescopeMediator telescopeMediator,
                                IGuiderMediator guiderMediator,
                                ISwitchMediator switchMediator,
                                IFlatDeviceMediator flatDeviceMediator,
                                IWeatherDataMediator weatherDataMediator,
                                IDomeMediator domeMediator,
                                ISafetyMonitorMediator safetyMonitorMediator)
        {
            _profileService = profileService;
            _cameraMediator = cameraMediator;
            _fwMediator = fwMediator;
            _focuserMediator = focuserMediator;
            _rotatorMediator = rotatorMediator;
            _telescopeMediator = telescopeMediator;
            _guiderMediator = guiderMediator;
            _switchMediator = switchMediator;
            _flatDeviceMediator = flatDeviceMediator;
            _weatherDataMediator = weatherDataMediator;
            _domeMediator = domeMediator;
            _safetyMonitorMediator = safetyMonitorMediator;
        }

        private ConnectAllEquipment(ConnectAllEquipment other)
            : this(other._profileService,
                other._cameraMediator,
                other._fwMediator,
                other._focuserMediator,
                other._rotatorMediator,
                other._telescopeMediator,
                other._guiderMediator,
                other._switchMediator,
                other._flatDeviceMediator,
                other._weatherDataMediator,
                other._domeMediator,
                other._safetyMonitorMediator)
        {
            CopyMetaData(other);
        }

        public override object Clone() => new ConnectAllEquipment(this);

        public IList<string> Issues => new List<string>();

        private MediatorWrapper GetMediator(string device)
        {
            return device switch
            {
                ConnectorConstants.CAMERA => new MediatorWrapper(_cameraMediator),
                ConnectorConstants.FILTER_WHEEL => new MediatorWrapper(_fwMediator),
                ConnectorConstants.FOCUSER => new MediatorWrapper(_focuserMediator),
                ConnectorConstants.ROTATOR => new MediatorWrapper(_rotatorMediator),
                ConnectorConstants.TELESCOPE => new MediatorWrapper(_telescopeMediator),
                ConnectorConstants.GUIDER => new MediatorWrapper(_guiderMediator),
                ConnectorConstants.SWITCH => new MediatorWrapper(_switchMediator),
                ConnectorConstants.FLAT_PANEL => new MediatorWrapper(_flatDeviceMediator),
                ConnectorConstants.WEATHER => new MediatorWrapper(_weatherDataMediator),
                ConnectorConstants.DOME => new MediatorWrapper(_domeMediator),
                ConnectorConstants.SAFETY_MONITOR => new MediatorWrapper(_safetyMonitorMediator),
                _ => null,
            };
        }

        public string GetDeviceProfileId(string device)
        {
            return device switch
            {
                ConnectorConstants.CAMERA => _profileService.ActiveProfile.CameraSettings.Id,
                ConnectorConstants.FILTER_WHEEL => _profileService.ActiveProfile.FilterWheelSettings.Id,
                ConnectorConstants.FOCUSER => _profileService.ActiveProfile.FocuserSettings.Id,
                ConnectorConstants.ROTATOR => _profileService.ActiveProfile.RotatorSettings.Id,
                ConnectorConstants.TELESCOPE => _profileService.ActiveProfile.TelescopeSettings.Id,
                ConnectorConstants.GUIDER => _profileService.ActiveProfile.GuiderSettings.GuiderName,
                ConnectorConstants.SWITCH => _profileService.ActiveProfile.SwitchSettings.Id,
                ConnectorConstants.FLAT_PANEL => _profileService.ActiveProfile.FlatDeviceSettings.Id,
                ConnectorConstants.WEATHER => _profileService.ActiveProfile.WeatherDataSettings.Id,
                ConnectorConstants.DOME => _profileService.ActiveProfile.DomeSettings.Id,
                ConnectorConstants.SAFETY_MONITOR => _profileService.ActiveProfile.SafetyMonitorSettings.Id,
                _ => null,
            };
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            var errors = new List<Exception>();
            var pluginSettings = new PluginOptionsAccessor(_profileService, ConnectorConstants.PLUGIN_ID);
            var savedOrder = pluginSettings.GetValueString(ConnectionOrder.SettingName, null);

            var devicesToConnect = pluginSettings.GetValueBoolean(ConnectionOrder.EnabledSettingName, false)
                ? ConnectionOrder.Normalize(savedOrder?.Split(ConnectionOrder.Separator, StringSplitOptions.RemoveEmptyEntries))
                : ConnectorConstants.Devices;

            foreach (var device in devicesToConnect)
            {
                var mediator = GetMediator(device);

                if (mediator.GetInfo().Connected)
                {
                    Logger.Info($"{device} is already connected");
                    continue;
                }

                var deviceProfileId = GetDeviceProfileId(device);

                if (!(deviceProfileId == "No_Device" || deviceProfileId == "No_Guider"))
                {
                    var devices = await mediator.Rescan();

                    if (devices.Contains(deviceProfileId))
                    {
                        var connected = await mediator.Connect() && mediator.GetInfo().Connected;
                        if (!connected)
                            errors.Add(new Exception($"Failed to connect to {device}"));
                    }
                    else
                    {
                        errors.Add(new Exception($"Failed to connect to {device} as it was not found"));
                    }
                }
            }

            if (errors.Count > 0)
                throw new AggregateException(errors);
        }

        public bool Validate() => true;
    }
}