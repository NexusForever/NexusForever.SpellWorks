namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// Measured size of a floating surface, as <c>shell.js</c> reports it.
    /// </summary>
    public sealed record Size(double Width, double Height);

    /// <summary>
    /// A viewport-clamped position for a floating surface, as <c>shell.js</c> reports it.
    /// </summary>
    public sealed record Point(double X, double Y);
}
