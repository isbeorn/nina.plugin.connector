using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Plugins.Connector.Instructions;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugins.Connector
{
    [Export(typeof(IPluginManifest))]
    public class ConnectorPlugin : PluginBase, INotifyPropertyChanged
    {
        private readonly ISequenceMediator _sequenceMediator;
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
        private readonly IApplicationStatusMediator _applicationStatusMediator;

        [ImportingConstructor]
        public ConnectorPlugin(IProfileService profileService,
                               ISequenceMediator sequenceMediator,
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
                               ISafetyMonitorMediator safetyMonitorMediator,
                               IApplicationStatusMediator applicationStatusMediator)
        {
            if (Properties.Settings.Default.UpdateSettings)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Properties.Settings.Default);
            }

            _sequenceMediator = sequenceMediator;
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
            _applicationStatusMediator = applicationStatusMediator;

            ProfileService = profileService;

            PluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(Identifier));
            DeviceConnectionOrder = new ObservableCollection<string>(LoadDeviceConnectionOrder());
            MoveDeviceConnectionOrderUpCommand = new RelayCommand<string>(MoveDeviceConnectionOrderUp, CanMoveDeviceConnectionOrderUp);
            MoveDeviceConnectionOrderDownCommand = new RelayCommand<string>(MoveDeviceConnectionOrderDown, CanMoveDeviceConnectionOrderDown);

            ProfileService.ProfileChanged += ProfileService_ProfileChanged;
        }

        public IPluginOptionsAccessor PluginSettings { get; }
        public IProfileService ProfileService { get; }
        public ObservableCollection<string> DeviceConnectionOrder { get; }
        public RelayCommand<string> MoveDeviceConnectionOrderUpCommand { get; }
        public RelayCommand<string> MoveDeviceConnectionOrderDownCommand { get; }

        public override async Task Initialize()
        {
            if (!AutoConnectEquipment)
                return;

            _ = Task.Run(
                async () =>
                {
                    while (!_sequenceMediator.Initialized)
                        await Task.Delay(100);

                    var ct = new CancellationToken();
                    var progress = new Progress<ApplicationStatus>(p =>
                    {
                        p.Source = "Connector";
                        _applicationStatusMediator.StatusUpdate(p);
                    }) as IProgress<ApplicationStatus>;

                    var connectEquipment = new ConnectAllEquipment(ProfileService,
                                                                _cameraMediator,
                                                                _fwMediator,
                                                                _focuserMediator,
                                                                _rotatorMediator,
                                                                _telescopeMediator,
                                                                _guiderMediator,
                                                                _switchMediator,
                                                                _flatDeviceMediator,
                                                                _weatherDataMediator,
                                                                _domeMediator,
                                                                _safetyMonitorMediator);

                    await connectEquipment.Run(progress, ct);
                    await UnparkTelescopeWhenEnabled(progress, ct);
                    await OpenFlatCoverWhenEnabled(progress, ct);
                    await ChangeFilterWhenEnabled(progress, ct);
                    await MoveFocuserWhenEnabled(ct);
                    await MoveRotatorWhenEnabled(ct);
                    await CoolCameraWhenEnabled(progress, ct);

                    progress.Report(new ApplicationStatus() { Status = string.Empty });
                });
        }

        public override Task Teardown()
        {
            ProfileService.ProfileChanged -= ProfileService_ProfileChanged;
            return base.Teardown();
        }

        private async Task UnparkTelescopeWhenEnabled(IProgress<ApplicationStatus> progress, CancellationToken ct)
        {
            if (UnparkTelescope)
            {
                if (_telescopeMediator.GetInfo().Connected)
                {
                    await RunAndCatchExceptionsAsync(async () =>
                    {
                        Notification.ShowInformation($"Connector - Unparking telescope");
                        await _telescopeMediator.UnparkTelescope(progress, ct);
                    }, "Connector - An error occurred while unparking telescope");
                }
                else
                {
                    Notification.ShowWarning("Connector set to auto unpark, but no telescope could be connected!");
                }
            }
        }

        private async Task OpenFlatCoverWhenEnabled(IProgress<ApplicationStatus> progress, CancellationToken ct)
        {
            if (OpenFlatCover)
            {
                if (_flatDeviceMediator.GetInfo().Connected)
                {
                    await RunAndCatchExceptionsAsync(async () =>
                    {
                        Notification.ShowInformation($"Connector - Opening flat device cover");
                        await _flatDeviceMediator.OpenCover(progress, ct);
                    }, "Connector - An error occurred while opening flat device cover");
                }
                else
                {
                    Notification.ShowWarning("Connector set to auto open flat device cover, but no flat device could be connected!");
                }
            }
        }

        private async Task ChangeFilterWhenEnabled(IProgress<ApplicationStatus> progress, CancellationToken ct)
        {
            if (ChangeFilter)
            {
                if (_fwMediator.GetInfo().Connected)
                {
                    if (Filter != null)
                    {
                        await RunAndCatchExceptionsAsync(async () =>
                        {
                            Notification.ShowInformation($"Changing filter to {Filter.Name}");
                            await _fwMediator.ChangeFilter(Filter, ct, progress);
                        }, "Connector - An error occurred while changing filter");
                    }
                }
                else
                {
                    Notification.ShowWarning("Connector set to auto set filter wheel filter, but no filter wheel could be connected!");
                }
            }
        }

        private async Task MoveFocuserWhenEnabled(CancellationToken ct)
        {
            if (MoveFocuserToPosition)
            {
                if (_focuserMediator.GetInfo().Connected)
                {
                    await RunAndCatchExceptionsAsync(async () =>
                    {
                        Notification.ShowInformation($"Moving focuser to position {FocuserPosition}");
                        await _focuserMediator.MoveFocuser(FocuserPosition, ct);
                    }, "Connector - An error occurred while moving focuser to position");
                }
                else
                {
                    Notification.ShowWarning("Connector set to auto set focuser position, but no focuser could be connected!");
                }
            }
        }

        private async Task MoveRotatorWhenEnabled(CancellationToken ct)
        {
            if (MoveRotatorToPosition)
            {
                if (_rotatorMediator.GetInfo().Connected)
                {
                    await RunAndCatchExceptionsAsync(async () =>
                    {
                        Notification.ShowInformation($"Connector - Moving rotator to position {RotatorPosition}");
                        await _rotatorMediator.MoveMechanical((float)RotatorPosition, ct);
                    }, "Connector - An error occurred while moving rotator to position");
                }
                else
                {
                    Notification.ShowWarning("Connector set to auto set rotator position, but no rotator could be connected!");
                }
            }
        }

        private async Task CoolCameraWhenEnabled(IProgress<ApplicationStatus> progress, CancellationToken ct)
        {
            if (AutoCoolCamera)
            {
                var cameraInfo = _cameraMediator.GetInfo();

                if (cameraInfo.Connected)
                {
                    if (cameraInfo.CanSetTemperature)
                    {
                        if (ProfileService.ActiveProfile.CameraSettings.Temperature.HasValue)
                        {
                            await RunAndCatchExceptionsAsync(async () =>
                            {
                                await _cameraMediator.CoolCamera(
                                    ProfileService.ActiveProfile.CameraSettings.Temperature.Value,
                                    TimeSpan.FromMinutes(ProfileService.ActiveProfile.CameraSettings.CoolingDuration),
                                    progress,
                                    ct);
                            }, "Connector - An error occurred while cooling the camera");
                        }
                        else
                        {
                            Notification.ShowWarning("Connector - No cooling temperature set in the current profile. Skipped cooling camera!");
                        }
                    }
                    else
                    {
                        Notification.ShowWarning("Connector set to auto cool camera, but camaera has no cooler!");
                    }
                }
                else
                {
                    Notification.ShowWarning("Connector set to auto cool camera, but no camera could be connected!");
                }
            }
        }

        public bool AutoConnectEquipment
        {
            get => PluginSettings.GetValueBoolean(nameof(AutoConnectEquipment), false);
            set
            {
                PluginSettings.SetValueBoolean(nameof(AutoConnectEquipment), value);
                RaisePropertyChanged();
            }
        }

        public bool UseCustomDeviceConnectionOrder
        {
            get => PluginSettings.GetValueBoolean(ConnectionOrder.EnabledSettingName, false);
            set
            {
                PluginSettings.SetValueBoolean(ConnectionOrder.EnabledSettingName, value);
                RaisePropertyChanged();
            }
        }

        public bool AutoCoolCamera
        {
            get => PluginSettings.GetValueBoolean(nameof(AutoCoolCamera), false);
            set
            {
                PluginSettings.SetValueBoolean(nameof(AutoCoolCamera), value);
                RaisePropertyChanged();
            }
        }

        public bool ChangeFilter
        {
            get => PluginSettings.GetValueBoolean(nameof(ChangeFilter), false);
            set
            {
                PluginSettings.SetValueBoolean(nameof(ChangeFilter), value);
                RaisePropertyChanged();
            }
        }

        public FilterInfo Filter
        {
            get
            {
                var filterName = PluginSettings.GetValueString(nameof(Filter), null);
                return ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.FirstOrDefault(x => x.Name == filterName);
            }
            set
            {
                PluginSettings.SetValueString(nameof(Filter), value?.Name);
                RaisePropertyChanged();
            }
        }

        public bool MoveFocuserToPosition
        {
            get => PluginSettings.GetValueBoolean(nameof(MoveFocuserToPosition), false);
            set
            {
                PluginSettings.SetValueBoolean(nameof(MoveFocuserToPosition), value);
                RaisePropertyChanged();
            }
        }

        public int FocuserPosition
        {
            get => PluginSettings.GetValueInt32(nameof(FocuserPosition), 0);
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                PluginSettings.SetValueInt32(nameof(FocuserPosition), value);
                RaisePropertyChanged();
            }
        }

        public bool UnparkTelescope
        {
            get => PluginSettings.GetValueBoolean(nameof(UnparkTelescope), false);
            set
            {
                PluginSettings.SetValueBoolean(nameof(UnparkTelescope), value);
                RaisePropertyChanged();
            }
        }

        public bool OpenFlatCover
        {
            get => PluginSettings.GetValueBoolean(nameof(OpenFlatCover), false);
            set
            {
                PluginSettings.SetValueBoolean(nameof(OpenFlatCover), value);
                RaisePropertyChanged();
            }
        }

        public bool MoveRotatorToPosition
        {
            get => PluginSettings.GetValueBoolean(nameof(MoveRotatorToPosition), false);
            set
            {
                PluginSettings.SetValueBoolean(nameof(MoveRotatorToPosition), value);
                RaisePropertyChanged();
            }
        }

        public double RotatorPosition
        {
            get => PluginSettings.GetValueDouble(nameof(RotatorPosition), 0d);
            set
            {
                value = AstroUtil.EuclidianModulus(value, 360);
                PluginSettings.SetValueDouble(nameof(RotatorPosition), value);
                RaisePropertyChanged();
            }
        }

        private IEnumerable<string> LoadDeviceConnectionOrder()
        {
            var savedOrder = PluginSettings.GetValueString(ConnectionOrder.SettingName, null);
            return ConnectionOrder.Normalize(savedOrder?.Split(ConnectionOrder.Separator, StringSplitOptions.RemoveEmptyEntries));
        }

        private void MoveDeviceConnectionOrderUp(string device)
        {
            MoveDeviceConnectionOrder(device, -1);
        }

        private void MoveDeviceConnectionOrderDown(string device)
        {
            MoveDeviceConnectionOrder(device, 1);
        }

        private bool CanMoveDeviceConnectionOrderUp(string device)
        {
            return DeviceConnectionOrder.IndexOf(device) > 0;
        }

        private bool CanMoveDeviceConnectionOrderDown(string device)
        {
            var index = DeviceConnectionOrder.IndexOf(device);
            return index >= 0 && index < DeviceConnectionOrder.Count - 1;
        }

        private void MoveDeviceConnectionOrder(string device, int offset)
        {
            var currentIndex = DeviceConnectionOrder.IndexOf(device);
            var targetIndex = currentIndex + offset;

            if (currentIndex < 0 || targetIndex < 0 || targetIndex >= DeviceConnectionOrder.Count)
                return;

            DeviceConnectionOrder.Move(currentIndex, targetIndex);
            PluginSettings.SetValueString(ConnectionOrder.SettingName, string.Join(ConnectionOrder.Separator, DeviceConnectionOrder));

            MoveDeviceConnectionOrderUpCommand.NotifyCanExecuteChanged();
            MoveDeviceConnectionOrderDownCommand.NotifyCanExecuteChanged();
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e)
        {
            DeviceConnectionOrder.Clear();

            foreach (var device in LoadDeviceConnectionOrder())
                DeviceConnectionOrder.Add(device);

            RaisePropertyChanged(nameof(UseCustomDeviceConnectionOrder));
            MoveDeviceConnectionOrderUpCommand.NotifyCanExecuteChanged();
            MoveDeviceConnectionOrderDownCommand.NotifyCanExecuteChanged();
        }

        private async Task RunAndCatchExceptionsAsync(Func<Task> action, string errorMessage)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Notification.ShowError($"{errorMessage}: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) =>
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
