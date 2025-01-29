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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugins.Connector
{
    [Export(typeof(IPluginManifest))]
    public class ConnectorPlugin : PluginBase, INotifyPropertyChanged {
        private readonly ISequenceMediator sequenceMediator; 
        private readonly ICameraMediator cameraMediator;
        private readonly IFilterWheelMediator fwMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IRotatorMediator rotatorMediator;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IGuiderMediator guiderMediator;
        private readonly ISwitchMediator switchMediator;
        private readonly IFlatDeviceMediator flatDeviceMediator;
        private readonly IWeatherDataMediator weatherDataMediator;
        private readonly IDomeMediator domeMediator;
        private readonly ISafetyMonitorMediator safetyMonitorMediator;
        private readonly IApplicationStatusMediator applicationStatusMediator;

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
                               IApplicationStatusMediator applicationStatusMediator) {
            if (Properties.Settings.Default.UpdateSettings) {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Properties.Settings.Default);
            }

            this.PluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));

            this.sequenceMediator = sequenceMediator;
            this.ProfileService = profileService;
            this.cameraMediator = cameraMediator;
            this.fwMediator = fwMediator;
            this.focuserMediator = focuserMediator;
            this.rotatorMediator = rotatorMediator;
            this.telescopeMediator = telescopeMediator;
            this.guiderMediator = guiderMediator;
            this.switchMediator = switchMediator;
            this.flatDeviceMediator = flatDeviceMediator;
            this.weatherDataMediator = weatherDataMediator;
            this.domeMediator = domeMediator;
            this.safetyMonitorMediator = safetyMonitorMediator;
            this.applicationStatusMediator = applicationStatusMediator;
        }
        public IPluginOptionsAccessor PluginSettings { get; }
        public IProfileService ProfileService { get; }

        public override Task Initialize() {
            if(AutoConnectEquipment) { 
                _ = Task.Run(async () => {
                    while (!sequenceMediator.Initialized) {
                        await Task.Delay(100);
                    }

                    var ct = new CancellationToken();
                    IProgress<ApplicationStatus> progress = new Progress<ApplicationStatus>(p => {
                        p.Source = "Connector";
                        this.applicationStatusMediator.StatusUpdate(p);
                    });

                    var connectEquipment = new ConnectAllEquipment(ProfileService,
                                                                cameraMediator,
                                                                fwMediator,
                                                                focuserMediator,
                                                                rotatorMediator,
                                                                telescopeMediator,
                                                                guiderMediator,
                                                                switchMediator,
                                                                flatDeviceMediator,
                                                                weatherDataMediator,
                                                                domeMediator,
                                                                safetyMonitorMediator);

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
            return Task.CompletedTask;
        }

        private async Task UnparkTelescopeWhenEnabled(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            if (UnparkTelescope) {
                var telescopeInfo = telescopeMediator.GetInfo();
                if (telescopeInfo.Connected) {
                    try {
                        Notification.ShowInformation($"Connector - Unparking telescope");
                        await telescopeMediator.UnparkTelescope(progress, ct);
                    } catch (Exception ex) {
                        Logger.Error(ex);
                        Notification.ShowError($"Connector - An error occurred while unparking telescope: {ex.Message}");
                    }

                } else {
                    Notification.ShowWarning("Connector set to auto unpark, but no telescope could be connected!");
                }
            }
        }

        private async Task OpenFlatCoverWhenEnabled(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            if (OpenFlatCover) {
                var flatInfo = flatDeviceMediator.GetInfo();
                if (flatInfo.Connected) {
                    try {
                        Notification.ShowInformation($"Connector - Opening flat device cover");
                        await flatDeviceMediator.OpenCover(progress, ct);
                    } catch (Exception ex) {
                        Logger.Error(ex);
                        Notification.ShowError($"Connector - An error occurred while opening flat device cover: {ex.Message}");
                    }
                } else {
                    Notification.ShowWarning("Connector set to auto open flat device cover, but no flat device could be connected!");
                }
            }
        }

        private async Task ChangeFilterWhenEnabled(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            if(ChangeFilter) {
                var fwInfo = fwMediator.GetInfo();
                if(fwInfo.Connected) {
                    if(Filter != null) { 
                        try {
                            Notification.ShowInformation($"Changing filter to {Filter.Name}");
                            await fwMediator.ChangeFilter(Filter, ct, progress);
                        } catch (Exception ex) {
                            Logger.Error(ex);
                            Notification.ShowError($"Connector - An error occurred while changing filter: {ex.Message}");
                        }
                    }
                } else {
                    Notification.ShowWarning("Connector set to auto set filter wheel filter, but no filter wheel could be connected!");
                }
            }

        }

        private async Task MoveFocuserWhenEnabled(CancellationToken ct) {
            if (MoveFocuserToPosition) {
                var focuserInfo = focuserMediator.GetInfo();
                if (focuserInfo.Connected) {
                    try {
                        Notification.ShowInformation($"Moving focuser to position {FocuserPosition}");
                        await focuserMediator.MoveFocuser(FocuserPosition, ct);
                    } catch (Exception ex) {
                        Logger.Error(ex);
                        Notification.ShowError($"Connector - An error occurred while moving focuser to position: {ex.Message}");
                    }
                } else {
                    Notification.ShowWarning("Connector set to auto set focuser position, but no focuser could be connected!");
                }
            }
        }

        private async Task MoveRotatorWhenEnabled(CancellationToken ct) {
            if (MoveRotatorToPosition) {
                var rotatorInfo = rotatorMediator.GetInfo();
                if (rotatorInfo.Connected) {
                    try {
                        Notification.ShowInformation($"Connector - Moving rotator to position {RotatorPosition}");
                        await rotatorMediator.MoveMechanical((float)RotatorPosition, ct);
                    } catch (Exception ex) {
                        Logger.Error(ex);
                        Notification.ShowError($"Connector - An error occurred while moving rotator to position: {ex.Message}");
                    }
                } else {
                    Notification.ShowWarning("Connector set to auto set rotator position, but no rotator could be connected!");
                }
            }
        }

        private async Task CoolCameraWhenEnabled(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            if (AutoCoolCamera) {
                var cameraInfo = cameraMediator.GetInfo();
                if (cameraInfo.Connected) {
                    if (cameraInfo.CanSetTemperature) {
                        if (ProfileService.ActiveProfile.CameraSettings.Temperature.HasValue) {
                            try {
                                Notification.ShowInformation($"Connector - Cooling camera to {ProfileService.ActiveProfile.CameraSettings.Temperature.Value} °C");
                                await cameraMediator.CoolCamera(
                                    ProfileService.ActiveProfile.CameraSettings.Temperature.Value,
                                    TimeSpan.FromMinutes(ProfileService.ActiveProfile.CameraSettings.CoolingDuration),
                                    progress,
                                    ct);
                            } catch (Exception ex) {
                                Logger.Error(ex);
                                Notification.ShowError($"Connector - An error occurred while cooling the camera: {ex.Message}");
                            }
                        } else {
                            Notification.ShowWarning("Connector - No cooling temperature set in the current profile. Skipped cooling camera!");
                        }
                    } else {
                        Notification.ShowWarning("Connector set to auto cool cmaera, but camaera has no cooler!");
                    }
                } else {
                    Notification.ShowWarning("Connector set to auto cool cmaera, but no camera could be connected!");
                }
            }
        }

        public bool AutoConnectEquipment {
            get => PluginSettings.GetValueBoolean(nameof(AutoConnectEquipment), false);
            set {
                PluginSettings.SetValueBoolean(nameof(AutoConnectEquipment), value);
                RaisePropertyChanged();
            }
        }

        public bool AutoCoolCamera {
            get => PluginSettings.GetValueBoolean(nameof(AutoCoolCamera), false);
            set {
                PluginSettings.SetValueBoolean(nameof(AutoCoolCamera), value);
                RaisePropertyChanged();
            }
        }

        public bool ChangeFilter {
            get => PluginSettings.GetValueBoolean(nameof(ChangeFilter), false);
            set {
                PluginSettings.SetValueBoolean(nameof(ChangeFilter), value);
                RaisePropertyChanged();
            }
        }

        public FilterInfo Filter {
            get {
                var filterName = PluginSettings.GetValueString(nameof(Filter), null);
                return ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.FirstOrDefault(x => x.Name == filterName);
            }
            set {
                PluginSettings.SetValueString(nameof(Filter), value?.Name);
                RaisePropertyChanged();
            }
        }

        public bool MoveFocuserToPosition {
            get => PluginSettings.GetValueBoolean(nameof(MoveFocuserToPosition), false);
            set {
                PluginSettings.SetValueBoolean(nameof(MoveFocuserToPosition), value);
                RaisePropertyChanged();
            }
        }

        public int FocuserPosition {
            get => PluginSettings.GetValueInt32(nameof(FocuserPosition), 0);
            set {
                if(value < 0) { value = 0; }
                PluginSettings.SetValueInt32(nameof(FocuserPosition), value);
                RaisePropertyChanged();
            }
        }

        public bool UnparkTelescope {
            get => PluginSettings.GetValueBoolean(nameof(UnparkTelescope), false);
            set {
                PluginSettings.SetValueBoolean(nameof(UnparkTelescope), value);
                RaisePropertyChanged();
            }
        }

        public bool OpenFlatCover {
            get => PluginSettings.GetValueBoolean(nameof(OpenFlatCover), false);
            set {
                PluginSettings.SetValueBoolean(nameof(OpenFlatCover), value);
                RaisePropertyChanged();
            }
        }

        public bool MoveRotatorToPosition {
            get => PluginSettings.GetValueBoolean(nameof(MoveRotatorToPosition), false);
            set {
                PluginSettings.SetValueBoolean(nameof(MoveRotatorToPosition), value);
                RaisePropertyChanged();
            }
        }

        public double RotatorPosition {
            get => PluginSettings.GetValueDouble(nameof(RotatorPosition), 0d);
            set {
                value = AstroUtil.EuclidianModulus(value, 360);
                PluginSettings.SetValueDouble(nameof(RotatorPosition), value);
                RaisePropertyChanged();
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
