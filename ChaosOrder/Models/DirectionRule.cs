using System.Globalization;
using ChaosOrder.ViewModels;

namespace ChaosOrder.Models
{
    // One randomly-selectable movement rule: pick a target point (from TargetType),
    // then move the current point some fraction of the way toward it.
    public class DirectionRule : ViewModelBase
    {
        private bool _isEnabled = true;
        private DirectionTargetType _targetType = DirectionTargetType.Corners;
        private int _n = 2;
        private string _step = "1/2";
        private double _weight = 1.0;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetField(ref _isEnabled, value);
        }

        public DirectionTargetType TargetType
        {
            get => _targetType;
            set => SetField(ref _targetType, value);
        }

        // Used when TargetType == NthPointOfSides: point is at 1/N along each side.
        public int N
        {
            get => _n;
            set => SetField(ref _n, value);
        }

        // How far to move toward the chosen target point, as free-form text, evaluated
        // directly as the ratio: "1/2" and "0.5" both mean half the remaining distance,
        // "2/3" means two thirds, "2" and "2/1" both mean double (an overshoot past the target).
        public string Step
        {
            get => _step;
            set => SetField(ref _step, value);
        }

        // Parsed move ratio for Step, or NaN if it can't be parsed / is not a usable fraction.
        public double Ratio => ParseRatio(_step);

        // Relative probability weight used when randomly selecting among enabled rules.
        public double Weight
        {
            get => _weight;
            set => SetField(ref _weight, value);
        }

        public static double ParseRatio(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return double.NaN;

            text = text.Trim();
            int slash = text.IndexOf('/');
            if (slash >= 0)
            {
                var numText = text[..slash].Trim();
                var denText = text[(slash + 1)..].Trim();
                if (double.TryParse(numText, NumberStyles.Float, CultureInfo.InvariantCulture, out double num) &&
                    double.TryParse(denText, NumberStyles.Float, CultureInfo.InvariantCulture, out double den) &&
                    den != 0)
                {
                    return num / den;
                }
                return double.NaN;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val)
                ? val
                : double.NaN;
        }
    }
}
