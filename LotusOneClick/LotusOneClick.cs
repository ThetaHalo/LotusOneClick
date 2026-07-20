using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace LotusOneClick;

public static class LotusOneClickProgram
{
    // hello snooper who may be trying to use this for their own mod, everything you'd want to configure is in this section. you're welcome.
    // just follow the license
    private const string RepoOwner = "Lotus-AU";
    private const string RepoName = "LotusContinued";
    private const string ModName = "Project: Lotus";
    private const string UserAgent = "lol.eps.LotusOneClick";
    
    private const string Divider = "-------------------------------------------";
    
    private const string DiscordInvite = "https://discord.gg/projectlotus";
    private const string RegionInfoURL = "https://media.lotusau.top/files/regionInfo.json";
    // ---

    public static GamePlatform Platform;

    public static async Task Main(string[] args)
    {
        Console.Title = $"{ModName} Simple Installer";
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"Welcome to {ModName}, {Environment.UserName}! This installer is designed to help you easily download the mod and get everything set up.");
        Console.WriteLine("If you want to download the mod in the traditional way, that is still possible.");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(Divider);
        Console.WriteLine("We'll be asking a few questions to help us set up the mod. First off...");
        Console.WriteLine("\n");
        Platform = AskForPlatform();
        Console.WriteLine($"You chose: {Platform}. If this is incorrect, please restart the installer.");;
        Console.WriteLine(Divider);
        Console.WriteLine("Trying to fetch install directory...");
        var installDirectory = TryGetInstallDirectory(Platform);
        if (!ValidateDirectory(installDirectory))
        {
            Console.WriteLine("Could not find the install directory." + 
            "\nPlease enter it manually. If you are unsure where it is installed, please join our discord server.");
            Console.Write("> ");
            installDirectory = Console.ReadLine()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
            {
                Console.WriteLine("That path doesn't exist either. Exiting.");
                return;
            }
        }
        Console.WriteLine("Install Directory: " + installDirectory);
        Console.WriteLine(Divider);
        Console.WriteLine("We'll now be downloading the mod, this may take a while based on your internet connection.");

        try
        {
            var (downloadUrl, assetName) = await GetLatestZipAssetUrlAsync(RepoOwner, RepoName, Platform);
            Console.WriteLine($"Found release asset: {assetName}");

            var tempZipPath = Path.Combine(Path.GetTempPath(), assetName);
            await DownloadFileAsync(downloadUrl, tempZipPath);
            Console.WriteLine("Download complete. Extracting...");

            ZipFile.ExtractToDirectory(tempZipPath, installDirectory, overwriteFiles: true);
            File.Delete(tempZipPath);

            Console.WriteLine($"Done! Extracted to: {installDirectory}");
            Console.WriteLine(Divider);
            
            OpenAmongUs(installDirectory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Something went wrong: {ex.Message}");
            Console.WriteLine("Unfortunately, you'll have to download the mod manually.");
        }
        
        Console.WriteLine("Next, we'll be installing modded regions.");
        try
        {
            var regionInfo = RegionInfoURL;
            var regionInfoJson = await new HttpClient().GetStringAsync(regionInfo);
            Console.WriteLine("Fetched from server, now installing modded regions...");
            
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var innerslothDataDir = Path.Combine(userProfile, "AppData", "LocalLow", "Innersloth", "Among Us");
            Directory.CreateDirectory(innerslothDataDir);

            var regionInfoPath = Path.Combine(innerslothDataDir, "regionInfo.json");
            await File.WriteAllTextAsync(regionInfoPath, regionInfoJson);
            Console.WriteLine("Done!");
        }
        catch
        {
            Console.WriteLine("Something went wrong while installing the modded regions.");
        }

        Console.WriteLine(Divider);
        Console.WriteLine("The mod should now be installed. If you have any issues, please join our discord server.");
        Console.WriteLine($"-> {DiscordInvite} <-");
        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
    }
    
    private static GamePlatform AskForPlatform()
    {
        while (true)
        {
            Console.WriteLine("Which platform did you get Among Us from?");
            Console.WriteLine("1 - Steam");
            Console.WriteLine("2 - Epic Games");
            Console.WriteLine("3 - Itch.io");
            Console.WriteLine("4 - Microsoft Store");
            Console.Write("> ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1": return GamePlatform.Steam;
                case "2": return GamePlatform.Epic;
                case "3": return GamePlatform.Itch;
                case "4": return GamePlatform.MicrosoftStore;
                default:
                    Console.WriteLine("Not a valid option, try again. (Put the number, not the platform name!!!)");
                    Console.WriteLine();
                    continue;
            }
        }
    }

    public static bool ValidateDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return false;

        var exeNames = new[] { "Among Us.exe", "AmongUs.exe" };
        var dataFolderNames = new[] { "Among Us_Data", "AmongUs_Data" };

        var fileNames = Directory.GetFiles(directory)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dirNames = Directory.GetDirectories(directory)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasExe = exeNames.Any(fileNames.Contains);
        var hasDataFolder = dataFolderNames.Any(dirNames.Contains);

        return hasExe && hasDataFolder;
    }


    private static string TryGetInstallDirectory(GamePlatform platform)
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var CDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? new DriveInfo("C").RootDirectory.FullName;

        return platform switch
        {
            GamePlatform.Steam => Path.Combine(programFilesX86, "Steam", "steamapps", "common", "Among Us"), // C:\Program Files (x86)\Steam\steamapps\common\Among Us
            GamePlatform.Epic => Path.Combine(programFiles, "Epic Games", "AmongUs"), // C:\Program Files\Epic Games\AmongUs
            GamePlatform.Itch => Path.Combine(appData, "itch", "apps", "among-us", "AmongUsItch", "Itch"), // C:\Users\(username)\AppData\Roaming\itch\apps\among-us\AmongUsItch\Itch
            GamePlatform.MicrosoftStore => Path.Combine(CDrive, "XboxGames", "Among Us", "Content"), // C:\XboxGames\Among Us\Content
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
    }
    
    private static string[] GetPlatformKeywords(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => new[] { "Steam" },
        GamePlatform.Itch => new[] { "Steam" },
        GamePlatform.Epic => new[] { "Epic" },
        GamePlatform.MicrosoftStore => new[] { "MicrosoftStore" },
        _ => throw new ArgumentOutOfRangeException(nameof(platform))
    };
    
    private static async Task<(string url, string assetName)> GetLatestZipAssetUrlAsync(string owner, string repo, GamePlatform platform)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var response = await client.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var assets = doc.RootElement.GetProperty("assets");
        var keywords = GetPlatformKeywords(platform);

        var candidates = assets.EnumerateArray()
            .Where(a =>
            {
                var name = a.GetProperty("name").GetString() ?? string.Empty;
                return name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    && keywords.Any(k => name.Contains((string)k, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException($"No matching .zip asset found for platform '{platform}' in the latest release.");

        if (candidates.Count > 1)
        {
            Console.WriteLine($"Warning: multiple zip assets matched {platform}, picking the first: {candidates[0].GetProperty("name").GetString()}");
        }

        var zipAsset = candidates[0];
        var downloadUrl = zipAsset.GetProperty("browser_download_url").GetString()!;
        var assetName = zipAsset.GetProperty("name").GetString()!;

        return (downloadUrl, assetName);
    }

    private static async Task DownloadFileAsync(string url, string destinationPath)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs);
    }

    public static void OpenAmongUs(string installDirectory)
    {
        var exeNames = new[] { "Among Us.exe", "AmongUs.exe" }; // x64 and x86 have different names

        if (Platform is GamePlatform.Epic)
        {
            Console.WriteLine("Due to Epic Games' being weird, you need to manually open Among Us through the Epic Games Launcher.");
            return;
        }
        
        var exePath = exeNames
            .Select(name => Path.Combine(installDirectory, name))
            .FirstOrDefault(File.Exists);

        if (exePath is null)
        {
            Console.WriteLine("Couldn't find the game executable to launch. You'll need to start it manually.");
            return;
        }
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = installDirectory,
                UseShellExecute = true
            });
            Console.WriteLine("Launching Among Us...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to launch the game: {ex.Message}");
        }
    }
}

public enum GamePlatform
{
    Steam,
    Epic,
    Itch,
    MicrosoftStore
}
