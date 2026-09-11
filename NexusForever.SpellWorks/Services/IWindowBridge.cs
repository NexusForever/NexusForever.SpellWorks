namespace NexusForever.SpellWorks.Services
{
    /// <summary>
    /// The window a Blazor root component is hosted in.
    /// </summary>
    /// <remarks>
    /// <c>WindowStyle=None</c> plus <c>CaptionHeight=0</c> means WebView2 owns every pixel, so the drag,
    /// maximize and close gestures a title bar normally gets for free are forwarded through here.
    /// </remarks>
    public interface IWindowBridge
    {
        /// <summary>
        /// Begin a title-bar drag. Restores a maximized window first, so the drag continues at normal size.
        /// </summary>
        void BeginDrag();

        void ToggleMaximize();

        void Minimize();

        void Close();
    }

    /// <summary>
    /// Chooses a folder from the machine.
    /// </summary>
    public interface IFolderPicker
    {
        /// <summary>The folder the user chose, or <c>null</c> when they cancelled.</summary>
        string Pick(string title, string initialDirectory);
    }
}
