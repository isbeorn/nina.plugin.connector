using NINA.Profile.Interfaces;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace NINA.Plugins.Connector {
    public class ConnectionOrderListBox : ListBox {
        private Point dragStart;
        private string pendingDragDevice;
        private InsertionAdorner insertionAdorner;

        public ConnectionOrderListBox() {
            AllowDrop = true;
            Unloaded += (_, _) => ClearInsertionMarker();
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
            base.OnPreviewMouseLeftButtonDown(e);
            dragStart = e.GetPosition(this);
            var item = ContainerFromElement(this, e.OriginalSource as DependencyObject) as ListBoxItem;
            pendingDragDevice = item?.Content as string;
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
            pendingDragDevice = null;
            base.OnPreviewMouseLeftButtonUp(e);
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e) {
            base.OnPreviewMouseMove(e);
            if (e.LeftButton != MouseButtonState.Pressed) {
                pendingDragDevice = null;
                return;
            }

            if (pendingDragDevice == null || DataContext is not ConnectorPlugin plugin || !plugin.UseCustomDeviceConnectionOrder) {
                return;
            }

            var position = e.GetPosition(this);
            if (Math.Abs(position.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) {
                return;
            }

            var drag = new DeviceDrag(this, pendingDragDevice, plugin.ProfileService.ActiveProfile);
            pendingDragDevice = null;
            SelectedItem = drag.Device;
            try {
                DragDrop.DoDragDrop(this, new DataObject(typeof(DeviceDrag), drag), DragDropEffects.Move);
            } finally {
                ClearInsertionMarker();
            }
        }

        protected override void OnDragOver(DragEventArgs e) {
            base.OnDragOver(e);
            e.Handled = true;
            e.Effects = DragDropEffects.None;
            if (GetDeviceDrag(e) == null) {
                ClearInsertionMarker();
                return;
            }

            GetInsertionIndex(e.GetPosition(this), out var markerY);
            var layer = AdornerLayer.GetAdornerLayer(this);
            if (layer != null) {
                if (insertionAdorner == null) {
                    insertionAdorner = new InsertionAdorner(this);
                    layer.Add(insertionAdorner);
                }
                insertionAdorner.Position = markerY;
                insertionAdorner.InvalidateVisual();
            }
            e.Effects = DragDropEffects.Move;
        }

        protected override void OnDragLeave(DragEventArgs e) {
            base.OnDragLeave(e);
            ClearInsertionMarker();
            e.Handled = true;
        }

        protected override void OnDrop(DragEventArgs e) {
            base.OnDrop(e);
            e.Handled = true;
            e.Effects = DragDropEffects.None;
            ClearInsertionMarker();
            var drag = GetDeviceDrag(e);
            if (drag == null) {
                return;
            }

            var insertionIndex = GetInsertionIndex(e.GetPosition(this), out _);
            var currentIndex = Items.IndexOf(drag.Device);
            var targetIndex = insertionIndex > currentIndex ? insertionIndex - 1 : insertionIndex;
            ((ConnectorPlugin)DataContext).MoveDeviceConnectionOrderTo(drag.Device, targetIndex);
            SelectedItem = drag.Device;
            FocusSelectedItem();
            e.Effects = DragDropEffects.Move;
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
            var restoreFocus = IsKeyboardFocusWithin;
            base.OnItemsChanged(e);
            pendingDragDevice = null;
            ClearInsertionMarker();
            if (e.Action == NotifyCollectionChangedAction.Move) {
                SelectedItem = e.NewItems[0];
                if (restoreFocus) {
                    FocusSelectedItem();
                }
            } else if (SelectedIndex < 0 && Items.Count > 0) {
                SelectedIndex = 0;
            }
        }

        private DeviceDrag GetDeviceDrag(DragEventArgs e) {
            if ((e.AllowedEffects & DragDropEffects.Move) == 0
                || e.Data.GetData(typeof(DeviceDrag)) is not DeviceDrag drag
                || drag.Source != this
                || DataContext is not ConnectorPlugin plugin
                || !plugin.UseCustomDeviceConnectionOrder
                || !ReferenceEquals(drag.Profile, plugin.ProfileService.ActiveProfile)
                || !Items.Contains(drag.Device)) {
                return null;
            }
            return drag;
        }

        private int GetInsertionIndex(Point position, out double markerY) {
            markerY = 0;
            for (var index = 0; index < Items.Count; index++) {
                if (ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item) {
                    var top = item.TranslatePoint(new Point(), this).Y;
                    if (position.Y < top + item.ActualHeight / 2) {
                        markerY = top;
                        return index;
                    }
                    markerY = top + item.ActualHeight;
                }
            }
            return Items.Count;
        }

        private void FocusSelectedItem() {
            ScrollIntoView(SelectedItem);
            UpdateLayout();
            (ItemContainerGenerator.ContainerFromItem(SelectedItem) as ListBoxItem)?.Focus();
        }

        private void ClearInsertionMarker() {
            if (insertionAdorner != null) {
                (VisualTreeHelper.GetParent(insertionAdorner) as AdornerLayer)?.Remove(insertionAdorner);
                insertionAdorner = null;
            }
        }

        private sealed record DeviceDrag(ConnectionOrderListBox Source, string Device, IProfile Profile);

        private sealed class InsertionAdorner : Adorner {
            internal double Position { get; set; }

            internal InsertionAdorner(UIElement element) : base(element) {
                IsHitTestVisible = false;
            }

            protected override void OnRender(DrawingContext drawingContext) {
                var brush = (Brush)((FrameworkElement)AdornedElement).FindResource("PrimaryBrush");
                drawingContext.DrawLine(new Pen(brush, 2), new Point(0, Position), new Point(AdornedElement.RenderSize.Width, Position));
            }
        }
    }
}
