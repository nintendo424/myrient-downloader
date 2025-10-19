using System.CommandLine;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using MyrientDownloader.Services;
using Polly;

namespace MyrientDownloader;

public class MyrientDownloader
{
    private static readonly ServiceProvider ServiceProvider;
    private static readonly ILogger<MyrientDownloader> Logger;
    
    static MyrientDownloader()
    {
        ServiceProvider = new ServiceCollection()
            .AddHttpClient()
            .ConfigureHttpClientDefaults(builder =>
            {
                builder.ConfigureHttpClient(httpClient =>
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(120);
                        httpClient.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
                    })
                    .ConfigurePrimaryHttpMessageHandler(_ => new HttpClientHandler
                        { AutomaticDecompression = DecompressionMethods.All })
                    .AddResilienceHandler("Default", x => 
                        x.AddRetry(new HttpRetryStrategyOptions
                        {
                            UseJitter = true
                        }));
            })
            .BuildServiceProvider();
        
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        Logger = loggerFactory.CreateLogger<MyrientDownloader>();
    }
    
    public static async Task<int> Main(string[] args)
    {
        var outputOption = new Option<DirectoryInfo>("--output", "-o")
        {
            Required = true,
            Description = "Output directory for downloaded files, like ~/output",
            Arity = ArgumentArity.ExactlyOne
        };
        outputOption.AcceptLegalFilePathsOnly();

        var inputOption = new Option<FileInfo>("--input", "-i")
        {
            Required = true,
            Description = "Input DAT file, like ~/input.dat",
            Arity = ArgumentArity.ExactlyOne
        };
        inputOption.AcceptExistingOnly();

        var taskCountOption = new Option<int>("--task-count")
        {
            DefaultValueFactory = _ => 1,
            Description = "The number of downloads to run in parallel.",
            Arity = ArgumentArity.ExactlyOne
        };
        taskCountOption.Validators.Add(result =>
        {
            if (result.GetValue(taskCountOption) < 1
                || result.GetValue(taskCountOption) > Environment.ProcessorCount)
            {
                result.AddError($"Task Count must be between 1 and {Environment.ProcessorCount}");
            }
        });

        var unzipOption = new Option<bool>("--unzip")
        {
            Description = "Unzip the Myrient zips into the output directory.",
            Arity = ArgumentArity.Zero
        };
        
        var chunkSizeOption = new Option<int>("--chunk-size")
        {
            DefaultValueFactory = _ => 8192,
            Description = "Set the chunk size for downloads.",
            Arity = ArgumentArity.ExactlyOne
        };

        var rootCommand = new RootCommand();
        rootCommand.Options.Add(outputOption);
        rootCommand.Options.Add(inputOption);
        rootCommand.Options.Add(taskCountOption);
        rootCommand.Options.Add(unzipOption);
        rootCommand.Options.Add(chunkSizeOption);

        rootCommand.SetAction((result, token) => Command(
            result.GetRequiredValue(inputOption),
            result.GetRequiredValue(outputOption),
            result.GetValue(taskCountOption),
            result.GetValue(unzipOption),
            result.GetValue(chunkSizeOption),
            token));
        
        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task<int> Command(FileInfo inputDat, DirectoryInfo outputDir, int taskCount, bool unzip, int chunkSize, CancellationToken cancellationToken)
    {
        using var httpClient = ServiceProvider.GetRequiredService<HttpClient>();
        
        var parser = new Parser();
        
        var datFile = Parser.ParseDatFile(inputDat);

        var myrientRoms = parser.GetMyrientRoms(datFile.Header);
        var wantedRoms = datFile.Games;

        var downloader = new Downloader(httpClient, taskCount, outputDir, chunkSize, cancellationToken);
        await downloader.DownloadRoms(myrientRoms, wantedRoms, unzip);
        
        var myrientMissingRoms = wantedRoms.FindAll(x => !myrientRoms.ContainsKey(x.Name));
        
        Logger.LogInformation("Completed fetching {catalog} {system} ROMs from Myrient. Successfully downloaded {found}/{wanted} ROMs.",
            datFile.Header.Catalog,
            datFile.Header.System,
            myrientRoms.Values.Count(x => x.Downloaded),
            wantedRoms.Count);

        if (myrientMissingRoms.Count > 0)
        {
            Logger.LogInformation("The following {notFound} ROMs were not found on Myrient:\n{roms}", myrientMissingRoms.Count, 
                string.Join("\n", myrientMissingRoms.Select(x => x.Name)));
        }

        Logger.LogInformation("Complete.");
        return 0;
    }
}