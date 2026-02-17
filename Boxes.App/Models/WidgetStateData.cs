namespace Boxes.App.Models;

/// <summary>
/// Persisted geometry and layout state for a widget window.
/// </summary>
public class WidgetStateData
{
    public double Width { get; set; } = 600;
    public double Height { get; set; } = 400;
    public double X { get; set; } = 100;
    public double Y { get; set; } = 100;

    /// <summary>
    /// Splitter position as a fraction (0.0–1.0) for widgets with split views (e.g. notepad editor/preview).
    /// </summary>
    public double SplitterPosition { get; set; } = 0.5;
}
