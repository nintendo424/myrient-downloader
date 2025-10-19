using System.Text.RegularExpressions;

namespace MyrientDownloader.Interfaces;

public partial class MyrientMetadata(Uri uri, string fileName)
{
    [GeneratedRegex(@"\.[(a-zA-Z0-9)]{1,3}\Z")]
    private static partial Regex Clean();

    public Uri Uri { get; } = uri;
    public string FileName { get; } = fileName;
    public string Title { get; } = Clean().Replace(fileName, "");
    public bool Downloaded { get; set; }
    public bool Unzipped { get; set; }
}