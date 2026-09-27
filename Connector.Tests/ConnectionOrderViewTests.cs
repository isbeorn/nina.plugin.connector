using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Moq;
using NINA.Core.Utility.Converters;
using NINA.Plugins.Connector;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using EquipmentProfile = NINA.Profile.Profile;

namespace NINA.Plugins.Test {
    [TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
    public class ConnectionOrderViewTests {
        private Mock<IProfileService> profileService;
        private IProfile profile;
        private ConnectorPlugin plugin;
        private FrameworkElement view;
        private GroupBox group;
        private ConnectionOrderListBox list;
        private Button up;
        private Button down;
        private CheckBox toggle;

        [OneTimeSetUp]
        public void InitializeApplication() {
            _ = Application.Current ?? new Application();
            NINA.Plugins.Connector.Properties.Settings.Default.UpdateSettings = false;
            profileService = new Mock<IProfileService>();
            profileService.SetupGet(x => x.ActiveProfile).Returns(() => profile);
            Application.Current.Resources["ProfileService"] = profileService.Object;
            foreach (var path in new[] { "StaticResources/Brushes.xaml", "StaticResources/SVGDictionary.xaml", "Styles/Button.xaml", "Styles/GroupBox.xaml", "Styles/CheckBox.xaml", "Styles/TextBlock.xaml" }) {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
                    Source = new Uri($"/NINA.WPF.Base;component/Resources/{path}", UriKind.Relative)
                });
            }
            Application.Current.Resources["FilterWheelFilterConverter"] = new FilterWheelFilterConverter();
        }

        [SetUp]
        public void SetUp() {
            profile = new EquipmentProfile("Connection order test");
            plugin = new ConnectorPlugin(profileService.Object, null, null, null, null, null, null, null, null, null, null, null, null, null);
            var resources = new Options();
            view = (FrameworkElement)((DataTemplate)resources["Connector_Options"]).LoadContent();
            view.DataContext = plugin;
            Layout();
            group = Descendants<GroupBox>(view).Single();
            toggle = Descendants<CheckBox>(view).Last();
            toggle.IsChecked = true;
            Layout();
            list = Descendants<ConnectionOrderListBox>(view).Single();
            up = Descendants<Button>(group).Single(x => AutomationProperties.GetName(x) == "Move selected device up");
            down = Descendants<Button>(group).Single(x => AutomationProperties.GetName(x) == "Move selected device down");
        }

        [TearDown]
        public void TearDown() {
            plugin.Teardown().GetAwaiter().GetResult();
            if (view != null) {
                view.DataContext = null;
                view = null;
            }
        }

        [Test]
        public void TemplateUsesTwoSharedThemedButtonsAndAllDevices() {
            Assert.That(Descendants<Button>(group).Count(), Is.EqualTo(2));
            Assert.That(list.Items.Count, Is.EqualTo(11));
            Assert.That(list.SelectedIndex, Is.Zero);
            Assert.That(up.Style, Is.SameAs(Application.Current.FindResource("BackgroundButton")));
            Assert.That(Descendants<System.Windows.Shapes.Path>(up).Single().Data, Is.SameAs(Application.Current.FindResource("ArrowUpSVG")));
            Assert.That(Descendants<System.Windows.Shapes.Path>(down).Single().Data, Is.SameAs(Application.Current.FindResource("ArrowDownSVG")));
            Assert.That(list.InputBindings.Count, Is.EqualTo(2));
        }

        [TestCase(0, 1)]
        [TestCase(10, -1)]
        [TestCase(5, 1)]
        [TestCase(5, -1)]
        public void SharedButtonsMoveSelectionAndPersist(int index, int offset) {
            list.SelectedIndex = index;
            Layout();
            var device = (string)list.SelectedItem;
            Click(offset < 0 ? up : down);
            Assert.That(list.SelectedItem, Is.EqualTo(device));
            Assert.That(list.SelectedIndex, Is.EqualTo(index + offset));
            AssertSavedOrder();
            Click(offset < 0 ? down : up);
            Assert.That(list.SelectedItem, Is.EqualTo(device));
            Assert.That(list.SelectedIndex, Is.EqualTo(index));
            AssertSavedOrder();
        }

        [Test]
        public void SharedButtonsDisableAtBothBoundariesAndWithoutSelection() {
            list.SelectedIndex = 0;
            Layout();
            Assert.That(up.IsEnabled, Is.False);
            Assert.That(down.IsEnabled, Is.True);
            list.SelectedIndex = list.Items.Count - 1;
            Layout();
            Assert.That(up.IsEnabled, Is.True);
            Assert.That(down.IsEnabled, Is.False);
            list.SelectedIndex = -1;
            Layout();
            Assert.That(up.IsEnabled, Is.False);
            Assert.That(down.IsEnabled, Is.False);
        }

        [TestCase(Key.Up, -1)]
        [TestCase(Key.Down, 1)]
        public void AltArrowBindingUsesCurrentSelection(Key key, int offset) {
            list.SelectedIndex = 5;
            Layout();
            var device = list.SelectedItem;
            var binding = list.InputBindings.OfType<KeyBinding>().Single(x => x.Key == key);
            Assert.That(binding.Modifiers, Is.EqualTo(ModifierKeys.Alt));
            Assert.That(binding.CommandParameter, Is.EqualTo(device));
            Assert.That(binding.Command.CanExecute(binding.CommandParameter), Is.True);
            binding.Command.Execute(binding.CommandParameter);
            Layout();
            Assert.That(list.SelectedIndex, Is.EqualTo(5 + offset));
            Assert.That(list.SelectedItem, Is.EqualTo(device));
            Assert.That(binding.CommandParameter, Is.EqualTo(device));
            AssertSavedOrder();
        }

        [TestCase(0, 10, true, 10)]
        [TestCase(10, 0, false, 0)]
        [TestCase(2, 7, false, 6)]
        [TestCase(7, 2, true, 3)]
        [TestCase(3, 3, false, 3)]
        [TestCase(3, 3, true, 3)]
        [TestCase(0, 1, false, 0)]
        [TestCase(10, 9, true, 10)]
        public void DropUsesInsertionPointInBothDirections(int from, int target, bool after, int expected) {
            var device = plugin.DeviceConnectionOrder[from];
            var data = DeviceData(device);
            var point = DropPoint(target, after);
            var over = DragEvent(data, point, DragDrop.DragOverEvent);
            list.RaiseEvent(over);
            Assert.That(over.Effects, Is.EqualTo(DragDropEffects.Move));
            Assert.That(AdornerLayer.GetAdornerLayer(list).GetAdorners(list), Has.Length.EqualTo(1));
            var drop = DragEvent(data, point, DragDrop.DropEvent);
            list.RaiseEvent(drop);
            Layout();
            Assert.That(drop.Effects, Is.EqualTo(DragDropEffects.Move));
            Assert.That(plugin.DeviceConnectionOrder.IndexOf(device), Is.EqualTo(expected));
            Assert.That(list.SelectedItem, Is.EqualTo(device));
            Assert.That(plugin.DeviceConnectionOrder.Distinct().Count(), Is.EqualTo(11));
            Assert.That(AdornerLayer.GetAdornerLayer(list).GetAdorners(list), Is.Null);
            if (from != expected) {
                AssertSavedOrder();
            }
        }

        [Test]
        public void LeavingTheListRemovesInsertionMarkerWithoutChangingOrder() {
            var before = plugin.DeviceConnectionOrder.ToArray();
            var data = DeviceData(before[0]);
            list.RaiseEvent(DragEvent(data, DropPoint(5, true), DragDrop.DragOverEvent));
            list.RaiseEvent(DragEvent(data, DropPoint(5, true), DragDrop.DragLeaveEvent));
            Assert.That(AdornerLayer.GetAdornerLayer(list).GetAdorners(list), Is.Null);
            Assert.That(plugin.DeviceConnectionOrder, Is.EqualTo(before));
        }

        [Test]
        public void UnrelatedDragIsRejected() {
            var before = plugin.DeviceConnectionOrder.ToArray();
            var drop = DragEvent(new DataObject(DataFormats.Text, before[0]), DropPoint(10, true), DragDrop.DropEvent);
            list.RaiseEvent(drop);
            Assert.That(drop.Effects, Is.EqualTo(DragDropEffects.None));
            Assert.That(plugin.DeviceConnectionOrder, Is.EqualTo(before));
        }

        [Test]
        public void DisablingCustomOrderDuringDragRejectsTheDrop() {
            var before = plugin.DeviceConnectionOrder.ToArray();
            var data = DeviceData(before[0]);
            var point = DropPoint(10, true);
            list.RaiseEvent(DragEvent(data, point, DragDrop.DragOverEvent));
            plugin.UseCustomDeviceConnectionOrder = false;
            var drop = DragEvent(data, point, DragDrop.DropEvent);
            list.RaiseEvent(drop);
            Assert.That(drop.Effects, Is.EqualTo(DragDropEffects.None));
            Assert.That(AdornerLayer.GetAdornerLayer(list).GetAdorners(list), Is.Null);
            Assert.That(plugin.DeviceConnectionOrder, Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DropBeyondTheRowsMovesToTheNearestEnd(bool atEnd) {
            var device = plugin.DeviceConnectionOrder[5];
            var point = new Point(10, atEnd ? list.ActualHeight + 10 : -10);
            list.RaiseEvent(DragEvent(DeviceData(device), point, DragDrop.DropEvent));
            Layout();
            Assert.That(list.SelectedIndex, Is.EqualTo(atEnd ? list.Items.Count - 1 : 0));
            Assert.That(list.SelectedItem, Is.EqualTo(device));
            AssertSavedOrder();
        }

        [Test]
        public void DragCannotMoveDevicesAfterProfileChanges() {
            var data = DeviceData(plugin.DeviceConnectionOrder[0]);
            profile = new EquipmentProfile("Other profile");
            profileService.Raise(x => x.ProfileChanged += null, EventArgs.Empty);
            plugin.UseCustomDeviceConnectionOrder = true;
            Layout();
            var before = plugin.DeviceConnectionOrder.ToArray();
            var drop = DragEvent(data, DropPoint(10, true), DragDrop.DropEvent);
            list.RaiseEvent(drop);
            Assert.That(drop.Effects, Is.EqualTo(DragDropEffects.None));
            Assert.That(plugin.DeviceConnectionOrder, Is.EqualTo(before));
        }

        [Test]
        public void ProfileSwitchAndToggleRefreshBoundListInBothDirections() {
            var firstProfile = profile;
            list.SelectedIndex = 0;
            Click(down);
            var firstOrder = plugin.DeviceConnectionOrder.ToArray();
            profile = new EquipmentProfile("Other profile");
            profileService.Raise(x => x.ProfileChanged += null, EventArgs.Empty);
            Layout();
            Assert.That(toggle.IsChecked, Is.False);
            Assert.That(group.Visibility, Is.EqualTo(Visibility.Collapsed));
            toggle.IsChecked = true;
            Layout();
            Assert.That(group.Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(list.SelectedIndex, Is.Zero);
            Click(down);
            Click(down);
            var secondOrder = plugin.DeviceConnectionOrder.ToArray();
            var secondProfile = profile;
            profile = firstProfile;
            profileService.Raise(x => x.ProfileChanged += null, EventArgs.Empty);
            Layout();
            Assert.That(toggle.IsChecked, Is.True);
            Assert.That(plugin.DeviceConnectionOrder, Is.EqualTo(firstOrder));
            Assert.That(list.Items.Cast<string>(), Is.EqualTo(firstOrder));
            profile = secondProfile;
            profileService.Raise(x => x.ProfileChanged += null, EventArgs.Empty);
            Layout();
            Assert.That(plugin.DeviceConnectionOrder, Is.EqualTo(secondOrder));
            toggle.IsChecked = false;
            Layout();
            Assert.That(plugin.UseCustomDeviceConnectionOrder, Is.False);
            Assert.That(group.Visibility, Is.EqualTo(Visibility.Collapsed));
        }

        private void AssertSavedOrder() {
            Assert.That(plugin.PluginSettings.GetValueString("DeviceConnectionOrder", null),
                Is.EqualTo(string.Join('|', plugin.DeviceConnectionOrder)));
        }

        private void Click(Button button) {
            var peer = new ButtonAutomationPeer(button);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            Layout();
        }

        private void Layout() {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.Measure(new Size(600, 1000));
            view.Arrange(new Rect(0, 0, 600, 1000));
            view.UpdateLayout();
        }

        private Point DropPoint(int index, bool after) {
            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);
            return item.TranslatePoint(new Point(10, after ? item.ActualHeight - 1 : 1), list);
        }

        private IDataObject DeviceData(string device) {
            // Construct the control's drag payload without entering the native modal drag loop.
            var type = typeof(ConnectionOrderListBox).GetNestedType("DeviceDrag", BindingFlags.NonPublic);
            var data = Activator.CreateInstance(type, list, device, profile);
            return new DataObject(type, data);
        }

        private DragEventArgs DragEvent(IDataObject data, Point point, RoutedEvent routedEvent) {
            var constructor = typeof(DragEventArgs).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var args = (DragEventArgs)constructor.Invoke(new object[] { data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, list, point });
            args.RoutedEvent = routedEvent;
            return args;
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject {
            if (root is T match) {
                yield return match;
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) {
                foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, index))) {
                    yield return child;
                }
            }
        }
    }
}
