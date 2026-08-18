using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ChaosOrder.Models;
using ChaosOrder.ViewModels;

namespace ChaosOrder
{
    public partial class MainWindow : Window
    {
        private const double MinZoom = 0.3;
        private const double MaxZoom = 6.0;

        public MainWindow()
        {
            InitializeComponent();

            if (DataContext is MainViewModel vm)
                vm.ZoomResetRequested += (_, __) =>
                {
                    DrawingScaleTransform.ScaleX = 1;
                    DrawingScaleTransform.ScaleY = 1;
                };
        }

        private void DrawingCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            e.Handled = true;

            double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            DrawingScaleTransform.ScaleX = Clamp(DrawingScaleTransform.ScaleX * factor, MinZoom, MaxZoom);
            DrawingScaleTransform.ScaleY = DrawingScaleTransform.ScaleX;
        }

        private void CornerThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ObservablePoint p })
            {
                p.X = Clamp(p.X + e.HorizontalChange, 0, MainViewModel.CanvasSize);
                p.Y = Clamp(p.Y + e.VerticalChange, 0, MainViewModel.CanvasSize);
            }
        }

        private void StartThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.StartPoint.X = Clamp(vm.StartPoint.X + e.HorizontalChange, 0, MainViewModel.CanvasSize);
                vm.StartPoint.Y = Clamp(vm.StartPoint.Y + e.VerticalChange, 0, MainViewModel.CanvasSize);
            }
        }

        private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));
    }
}
