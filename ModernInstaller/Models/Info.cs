using System.Text.Json.Serialization;

namespace ModernInstaller.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Info))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
public class Info
{
    public string DisplayIcon { get; set; }
    public string DisplayName { get; set; }
    public string DisplayVersion { get; set; }
    public string Publisher { get; set; }
    public string CanExecutePath { get; set; }
    public bool Is64 { get; set; }
}