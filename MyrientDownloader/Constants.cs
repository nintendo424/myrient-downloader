using System.Reflection;

namespace MyrientDownloader;

public static class Constants
{
    public const string MyrientUrl = "https://myrient.erista.me/files/";

    private const string NoIntroUrl = "https://www.no-intro.org";
    private const string RedumpUrl = "http://redump.org";
    
    public static readonly string UserAgent = $"myrient-downloader/{Assembly.GetExecutingAssembly().GetName().Version}";

    public static readonly Dictionary<string, string> Catalogs = new() {
        { NoIntroUrl, "No-Intro" },
        { RedumpUrl, "Redump" }
    };

    public static readonly string[] TitleFilters =
    [
        ".",
        ".."
    ];

    public static readonly string[] SystemFilters =
    [
        " (Retool)"
    ];
}