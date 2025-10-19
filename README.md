# myrient-downloader

A DotNet 9.0 tool for downloading ROMs from Myrient, with a No-Intro or Redump DAT file as input.
Using the input file, the program will:
- Gather relevant URLs and metadata (file name, file size) from Myrient.
- Check for existing Zips in the output directory.
- Download needed Zips and check the file's CRC checksum against the input DAT.
- If selected, unzip the files into the output directory.

### Parameters
#### Required
- ```--input, -i``` An input DAT file. Will be processed as ROMs you wish to have.
- ```--output, -o``` The output directory. Zip archives or output files will be placed here.

#### Optional
- ```--task-count``` The number of parallel downloads to process at once. Min is ```1```, max is ```# of CPU Cores```.
- ```--unzip``` The program will unzip the downloaded files from Myrient to the output directory.
- ```--chunk-size``` Number of bytes per request while downloading. Defaults to 8192

### Example
```.\MyrientDownloader.exe -i test.dat -o output --task-count 2 --unzip --chunk-size 16384```