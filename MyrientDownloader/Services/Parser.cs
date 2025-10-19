using System.Xml.Serialization;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using MyrientDownloader.Interfaces;

namespace MyrientDownloader.Services;

public partial class Parser
{
    private readonly ILogger<Parser> _logger;

    public Parser()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<Parser>();
    }
    
    public static DatFile ParseDatFile(FileInfo datFile)
    {
        using var reader = new StreamReader(datFile.FullName);
        return (DatFile)new XmlSerializer(typeof(DatFile)).Deserialize(reader)!;
    }
    
    public Dictionary<string, MyrientMetadata> GetMyrientRoms(Header header)
    {
        var web = new HtmlWeb();
        var myrientPage = web.Load(Constants.MyrientUrl);
        var catalogNode = myrientPage.DocumentNode.Descendants("a")
            .Where(x => x.Attributes.Contains("title"))
            .First(x => x.Attributes["title"].Value == header.Catalog);
        
        var catalogUri = new Uri(Path.Join(Constants.MyrientUrl, catalogNode.Attributes["href"].Value));

        myrientPage = web.Load(catalogUri);
        var systemNode = myrientPage.DocumentNode.Descendants("a")
            .Where(x => x.Attributes.Contains("title"))
            .First(x => x.Attributes["title"].Value == header.System);
        
        var systemUri = new Uri(catalogUri, systemNode.Attributes["href"].Value);
        
        myrientPage = web.Load(systemUri);
        
        var myrientMetadata = myrientPage.DocumentNode.Descendants("a")
            .Where(x => x.Attributes.Contains("title")
                        && !Constants.TitleFilters.Contains(x.Attributes["title"].Value))
            .Select(x =>
            {
                var uri = new Uri(systemUri, x.Attributes["href"].Value);
                var fileName = x.Attributes["title"].Value;
        
                var myrientRom = new MyrientMetadata(uri, fileName);
        
                return new KeyValuePair<string, MyrientMetadata>(
                    myrientRom.Title, myrientRom
                );
            }).ToDictionary();
        
        Log_Complete();
        return myrientMetadata;
    }

    [LoggerMessage(LogLevel.Information, Message = "Completed downloading wanted ROMs.")]
    private partial void Log_Complete();
}