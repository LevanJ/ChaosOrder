using System.Windows;
using ChaosOrder.ViewModels;

namespace ChaosOrder.Models
{
    public class ObservablePoint : ViewModelBase
    {
        private double _x;
        private double _y;

        public double X
        {
            get => _x;
            set => SetField(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetField(ref _y, value);
        }

        public ObservablePoint() { }

        public ObservablePoint(double x, double y)
        {
            _x = x;
            _y = y;
        }

        public Point ToPoint() => new(X, Y);
    }
}
