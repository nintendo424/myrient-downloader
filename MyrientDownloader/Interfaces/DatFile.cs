using System.Globalization;
using System.Xml.Serialization;

namespace MyrientDownloader.Interfaces;

[XmlRoot("datafile")]
public class DatFile
{
    [XmlElement("header")]
    public required Header Header { get; set; }

    [XmlElement("game")]
    public required List<Game> Games { get; set; }
}

[XmlRoot("header")]
public class Header
{
    [XmlElement("name")]
    public required string SystemString { private get; set; }

    public string System =>
        Constants.SystemFilters.Aggregate(SystemString,
            (current, systemFilter) => current.Replace(systemFilter, ""));

    [XmlElement("url")]
    public required string CatalogString { private get; set; }

    public string Catalog
    {
        get {
            var key = Constants.Catalogs.Keys.First(x => CatalogString.Contains(x));
            return Constants.Catalogs[key];
        }
    }
}

[XmlRoot("game")]
public class Game
{
    [XmlAttribute("name")]
    public required string Name { get; set; }
    
    [XmlElement("rom")]
    public required List<Rom> Rom { get; set; }
}

[XmlRoot("rom")]
public class Rom
{
    [XmlAttribute("name")]
    public required string FileName { get; set; }
    
    public uint Crc => uint.Parse(CrcString, NumberStyles.HexNumber,  CultureInfo.InvariantCulture);

    [XmlAttribute("crc")] 
    public required string CrcString { private get; set; }
}
