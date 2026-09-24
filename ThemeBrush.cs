using System.Windows.Media;

namespace VirtualADGV.WPF
{
    /// <summary>
    /// Theme brushes are never mutated after creation; freezing them lets WPF skip change
    /// tracking for every element that uses them (and makes them shareable across threads).
    /// </summary>
    internal static class ThemeBrush
    {
        public static SolidColorBrush Create(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
