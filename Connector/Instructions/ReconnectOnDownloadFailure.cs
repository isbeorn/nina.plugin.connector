using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Interfaces;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Exceptions;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugins.Connector.Instructions {
    [ExportMetadata("Name", "Reconnect Camera on Download Failure")]
    [ExportMetadata("Description", "A trigger that will fire when the camera failed to download an exposure and will try to reconnect the camera.")]
    [ExportMetadata("Icon", "Connector.ConnectSVG")]
    [ExportMetadata("Category", "Connector")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class ReconnectOnDownloadFailure : SequenceTrigger {
        private IProfileService profileService;
        private ICameraMediator cameraMediator;
        private ISequenceMediator sequenceMediator;
        private ISequenceRootContainer failureHook;
        private bool shouldTrigger = false;


        [ImportingConstructor]
        public ReconnectOnDownloadFailure(IProfileService profileService,
                                          ICameraMediator cameraMediator,
                                          ISequenceMediator sequenceMediator) {

            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.sequenceMediator = sequenceMediator;

        }

        private ReconnectOnDownloadFailure(ReconnectOnDownloadFailure copyMe) : this(copyMe.profileService,
                                                                                     copyMe.cameraMediator,
                                                                                     copyMe.sequenceMediator) {
            CopyMetaData(copyMe);
        }

        public override void AfterParentChanged() {
            var root = ItemUtility.GetRootContainer(this.Parent);
            if (root == null) {
                if(failureHook != null) { 
                    failureHook.FailureEvent -= Root_FailureEvent;
                }
                cameraMediator.DownloadTimeout -= CameraMediator_DownloadTimeout;
                failureHook = null;
            } else if (root != null && root != failureHook && this.Parent.Status == SequenceEntityStatus.RUNNING) {
                failureHook = root;
                failureHook.FailureEvent += Root_FailureEvent;
                cameraMediator.DownloadTimeout += CameraMediator_DownloadTimeout;
            }
            base.AfterParentChanged();
        }

        public override void SequenceBlockInitialize() {
            failureHook = ItemUtility.GetRootContainer(this.Parent);
            if (failureHook != null) {
                failureHook.FailureEvent += Root_FailureEvent;
            }
            cameraMediator.DownloadTimeout += CameraMediator_DownloadTimeout;
            base.SequenceBlockInitialize();
        }

        public override void SequenceBlockTeardown() {
            // Unregister failure event when the parent context ends
            failureHook = ItemUtility.GetRootContainer(this.Parent);
            if (failureHook != null) {
                failureHook.FailureEvent -= Root_FailureEvent;
            }
            cameraMediator.DownloadTimeout -= CameraMediator_DownloadTimeout;
        }
        private async Task Root_FailureEvent(object sender, SequenceEntityFailureEventArgs e) {
            if (e.Entity == null) {
                return;
            }
            if(e.Exception is CameraDownloadFailedException || e.Exception is CameraExposureFailedException) {
                shouldTrigger = true;
            }
        }

        private async Task CameraMediator_DownloadTimeout(object sender, EventArgs e) {
            shouldTrigger = true;
        }

        public override object Clone() {
            return new ReconnectOnDownloadFailure(this);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ReconnectOnDownloadFailure)}";
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return shouldTrigger;
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            try { 
                var cameraInfo = cameraMediator.GetInfo();
                var coolAfter = false;
                var dewHeater = cameraInfo.DewHeaterOn;
                var temperatureToCool = 0d;
                if(cameraInfo.Connected) {

                    coolAfter = cameraInfo.CoolerOn;
                    temperatureToCool = cameraInfo.TemperatureSetPoint;

                    Logger.Info("Disconnecting camera");
                    await cameraMediator.Disconnect();
                }

                Logger.Info("Rescanning for camera devices");
                var devices = await cameraMediator.Rescan();
                var profileId = profileService.ActiveProfile.CameraSettings.Id;

                if(!devices.Contains(profileId)) {
                    throw new SequenceEntityFailedException($"Failed to connect to Camera with Id {profileId} as it was not found");
                }

                Logger.Info("Reconnect to camera");
                var success = await cameraMediator.Connect();

                var infoAfterConnect = cameraMediator.GetInfo();
                success = success && infoAfterConnect.Connected;
                if (!success) {
                    throw new SequenceEntityFailedException($"Failed to reconnect to camera");
                }

                if(coolAfter && infoAfterConnect.CanSetTemperature) {
                    Logger.Info("Restarting cooling, as it was cooling before");
                    await cameraMediator.CoolCamera(temperatureToCool, TimeSpan.Zero, progress, token);
                }

                if(dewHeater && infoAfterConnect.HasDewHeater) {
                    Logger.Info("Reenable dewheater as it was on before");
                    cameraMediator.SetDewHeater(true);
                }
            } finally { 
                shouldTrigger = false; 
            }
        }
    }
}
