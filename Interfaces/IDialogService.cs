namespace ContextMenuApp.Interfaces
{
    /// <summary>
    /// API interface for OS-native dialog/popup operations.
    /// Provides a way to display GUI feedback dialogs to the user
    /// using the operating system's native dialog system (Zenity on GNOME/Ubuntu).
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Displays an informational popup dialog to the user.
        /// On Ubuntu GNOME, this uses 'zenity --info' which creates
        /// a native GTK dialog window.
        /// </summary>
        /// <param name="title">The title text shown in the dialog window's title bar.</param>
        /// <param name="message">The main message body displayed inside the dialog.</param>
        void ShowInfoDialog(string title, string message);

        /// <summary>
        /// Displays an error popup dialog to the user.
        /// On Ubuntu GNOME, this uses 'zenity --error'.
        /// </summary>
        /// <param name="title">The title text shown in the dialog window's title bar.</param>
        /// <param name="message">The error message displayed inside the dialog.</param>
        void ShowErrorDialog(string title, string message);

        /// <summary>
        /// Displays a file selection dialog to the user.
        /// On Ubuntu GNOME, this uses 'zenity --file-selection'.
        /// </summary>
        /// <param name="title">The title text shown in the dialog window's title bar.</param>
        /// <param name="startFolder">The initial folder to open the dialog in.</param>
        /// <returns>The path of the selected file, or null if the user canceled.</returns>
        string? ShowFileOpenDialog(string title, string startFolder);
    }
}
