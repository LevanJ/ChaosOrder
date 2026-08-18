using System.Windows;

namespace ChaosOrder.Services
{
    public static class PolygonFactory
    {
        // Regular n-gon. Defaults to first vertex pointing straight up.
        public static List<Point> CreateRegular(int sides, Point center, double radius, double startAngle = -Math.PI / 2)
        {
            var points = new List<Point>();
            for (int i = 0; i < sides; i++)
            {
                double angle = startAngle + i * 2 * Math.PI / sides;
                points.Add(new Point(
                    center.X + radius * Math.Cos(angle),
                    center.Y + radius * Math.Sin(angle)));
            }
            return points;
        }
    }
}
