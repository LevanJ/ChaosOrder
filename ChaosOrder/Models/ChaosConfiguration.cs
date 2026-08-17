namespace ChaosOrder.Models
{
    // Plain-data snapshot of everything needed to reproduce the current settings,
    // serialized to/from a JSON text file.
    public class ChaosConfiguration
    {
        public string FigureType { get; set; } = "Triangle";
        public List<PointDto> Corners { get; set; } = new();
        public PointDto StartPoint { get; set; } = new();
        public int NumberOfSimulations { get; set; } = 20000;
        public string ColorName { get; set; } = "Black";
        public double Thickness { get; set; } = 2;
        public List<RuleDto> Rules { get; set; } = new();
    }

    public class PointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class RuleDto
    {
        public bool IsEnabled { get; set; } = true;
        public string TargetType { get; set; } = "Corners";
        public int N { get; set; } = 2;
        public string Step { get; set; } = "2";
        public double Weight { get; set; } = 1;
    }
}
