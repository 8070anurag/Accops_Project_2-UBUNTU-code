namespace ContextMenuApp.Interfaces
{
    /// <summary>
    /// Evaluates whether a context menu option should be visible
    /// for a given file or folder path based on application logic.
    /// </summary>
    public interface IVisibilityService
    {
        bool IsVisible(string operation, string path);
    }
}
