using System.Windows.Media;

namespace ChaosOrder.Models
{
    public class NamedColor
    {
        public string Name { get; }
        public Color Value { get; }

        public NamedColor(string name, Color value)
        {
            Name = name;
            Value = value;
        }
    }
}
