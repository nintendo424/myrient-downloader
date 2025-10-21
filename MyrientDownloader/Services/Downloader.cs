using System.IO.Compression;
using Microsoft.Extensions.Logging;
using MyrientDownloader.Interfaces;
using Spectre.Console;

namespace MyrientDownloader.Services;

public partial class Downloader
{
    private readonly ILogger<Downloader> _logger;
    private readonly HttpClient _httpClient;
    private readonly int _taskCount;
    private readonly string _outputPath;
    private readonly int _chunkSize;
    private readonly CancellationToken _cancellationToken;

    public Downloader(HttpClient httpClient, int taskCount, DirectoryInfo outputPath, int chunkSize,
        CancellationToken cancellationToken)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<Downloader>();
        
        _httpClient = httpClient;
        _taskCount = taskCount;
        
        if (!outputPath.Exists)
        {
            outputPath.Create();
        }
        _outputPath = outputPath.FullName;
        _chunkSize = chunkSize;
        _cancellationToken = cancellationToken;
    }
    
    public async Task DownloadRoms(Dictionary<string, MyrientMetadata> myrientRoms, List<Game> wantedRoms,
        bool unzip = false)
    {
        var matchingRoms = wantedRoms
            .FindAll(x => myrientRoms.ContainsKey(x.Name))
            .ToDictionary(x => x.Name, x => myrientRoms[x.Name]);

        Log_Started(matchingRoms.Count);

        await AnsiConsole.Progress()
            .AutoClear(true)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn(), new SpinnerColumn(), new DownloadedColumn(), new TransferSpeedColumn())
            .StartAsync(async ctx =>
        {
            var overall = ctx.AddTask("[green]Overall Progress[/]", true, matchingRoms.Count);

            while (matchingRoms.Values.Any(x => !x.Downloaded))
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = _taskCount,
                    CancellationToken = _cancellationToken
                };

                await Parallel.ForEachAsync(wantedRoms
                        .FindAll(x => myrientRoms.ContainsKey(x.Name) && !myrientRoms[x.Name].Downloaded),
                    parallelOptions,
                    async (x, cancellationToken) =>
                    {
                        {
                            var myrientRom = myrientRoms[x.Name];
                            var outputPath = Path.Combine(_outputPath, myrientRom.FileName);
                            
                            var response =
                                await _httpClient.GetAsync(myrientRom.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                            
                            var fileSize = response.EnsureSuccessStatusCode().Content.Headers.ContentLength.GetValueOrDefault();

                            var outputFile = new FileInfo(outputPath);
                            if (outputFile.Exists)
                            {
                                if (outputFile.Length == fileSize)
                                {
                                    myrientRom.Downloaded = true;
                                }
                                else
                                {
                                    File.Delete(outputFile.FullName);
                                }
                            }
                            if (!myrientRom.Downloaded)
                            { 
                                var download = ctx.AddTask($"[green]{x.Name.EscapeMarkup()}[/]", true, fileSize);
                                await using var fs = File.Open(outputPath, FileMode.Create);
                                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                                
                                var buffer = new byte[_chunkSize]; 
                                int bytesRead;
                                while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                                {
                                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                                    download.Increment(bytesRead);
                                }
                            }

                            using (var zip = new ZipArchive(File.OpenRead(outputPath), ZipArchiveMode.Read))
                            {
                                foreach (var rom in x.Rom)
                                {
                                    var fileHash = zip.GetEntry(rom.FileName)!.Crc32;

                                    if (fileHash != rom.Crc)
                                    {
                                        Log_InvalidCrc(fileHash, rom.Crc);
                                        File.Delete(outputPath);
                                        myrientRom.Downloaded = myrientRom.Unzipped = false;
                                        return;
                                    }
                                }


                                myrientRom.Downloaded = true;

                                if (!unzip)
                                {
                                    overall.Increment(1);
                                    return;
                                }
                                
                                zip.ExtractToDirectory(
                                    zip.Entries.Count > 1 
                                        ? Path.Join(_outputPath, x.Name) 
                                        : _outputPath,
                                    true);
                            }

                            File.Delete(outputPath);
                            myrientRom.Unzipped = true;
                            overall.Increment(1);
                        }
                    }
                ).ConfigureAwait(false);
            }
        });
    }
    
    [LoggerMessage(LogLevel.Information, Message = "Downloading {myrientRomsCount} from Myrient")]
    private partial void Log_Started(int myrientRomsCount);
    
    [LoggerMessage(LogLevel.Error, Message = "Hash {fileHash} does not match CRC {romCrc}.")]
    private partial void Log_InvalidCrc(uint fileHash, uint romCrc);


}