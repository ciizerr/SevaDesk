namespace SevaDesk_App.Views.Controls;

/// <summary>
/// Represents a source file queued for document studio processing (PDF generation, editing, etc.).
/// Defined in its own file so the XAML compiler can resolve it via x:DataType before C# compilation.
/// </summary>
public class StudioSourceFile
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(FullPath);
}
