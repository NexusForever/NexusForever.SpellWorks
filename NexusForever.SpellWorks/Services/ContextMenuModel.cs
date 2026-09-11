namespace NexusForever.SpellWorks.Services
{
    public sealed record MenuItem(string Icon, string Label, string Hint, Action Invoke);

    /// <summary>
    /// An open context menu. Position is clamped against the viewport in JS once the surface is measured.
    /// </summary>
    public sealed class ContextMenuModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool Placed { get; set; }

        public string Title { get; init; }
        public string Sub { get; init; }
        public List<MenuItem> Items { get; init; } = [];

        public int Cursor { get; set; } = -1;
    }
}
