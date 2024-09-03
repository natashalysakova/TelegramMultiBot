using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;
using TelegramMultiBot.Database.Services;

namespace TelegramMultiBot.ImageCompare
{
    internal class MonitorService
    {
        private readonly ILogger<JobManager> _logger;
        private readonly IMonitorDataService _dbservice;
        CancellationToken _cancellationToken;

        public event Action<long, string> ReadyToSend = delegate { };

        public MonitorService(ILogger<JobManager> logger, IMonitorDataService dbservice)
        {
            _logger = logger;
            _dbservice = dbservice;
        }

        //https://www.dtek-krem.com.ua/media/page/page-chart-8596-1050.jpg
        //public void Run(CancellationToken token)
        //{
        //    _cancellationToken = token;
        //    _ = Task.Run(async () =>
        //    {
        //        while (!_cancellationToken.IsCancellationRequested)
        //        {
        //            var activeJobs = _dbservice.GetActiveJobs();

        //            foreach (var activeJob in activeJobs)
        //            {
        //                bool isTheSame;
        //                string? localFilePath;
        //                try
        //                {
        //                    isTheSame = Compare(activeJob, out localFilePath);
        //                }
        //                catch(KeyNotFoundException ex)
        //                {
        //                    _logger.LogError($"{activeJob.Id} {activeJob.Url} failed:" + ex.Message);
        //                    continue;
        //                }
        //                catch (Exception ex)
        //                {
        //                    _dbservice.DisableJob(activeJob, ex.Message);
        //                    _logger.LogError($"{activeJob.Id} {activeJob.Url} disabled:" + ex.Message);
        //                    continue;
        //                }

        //                if (!isTheSame && localFilePath != null)
        //                {
        //                    ReadyToSend(activeJob.ChatId, localFilePath);
        //                }
        //                else
        //                {
        //                    _logger.LogTrace("shedule is the same");
        //                }
        //                _dbservice.UpdateNextRun(activeJob);
        //                await Task.Delay(500);
        //            }
        //            await Task.Delay(TimeSpan.FromSeconds(1));
        //        }
        //    }, token);
        //}

        public void Run(CancellationToken token)
        {
            _cancellationToken = token;
            _ = Task.Run(async () =>
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var activeJobs = _dbservice.GetActiveJobs().GroupBy(x=>x.Url);

                    foreach (var group in activeJobs)
                    {
                        bool isTheSame;
                        string? localFilePath;
                        try
                        {
                            isTheSame = Compare(group.Key, out localFilePath);
                        }
                        catch (KeyNotFoundException ex)
                        {
                            _logger.LogError($"fetching {group.Key} failed:" + ex.Message);
                            _dbservice.UpdateNextRun(group);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"{group.Key} error:" + ex.Message);
                            continue;
                        }

                        if (!isTheSame && localFilePath != null)
                        {

                            foreach (var job in group)
                            {
                                ReadyToSend(job.ChatId, localFilePath);
                            }

                        }
                        else
                        {
                            _logger.LogTrace(group.Key + " was not updated");
                        }
                        _dbservice.UpdateNextRun(group);
                        await Task.Delay(500);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }, token);
        }

        private bool Compare(string url, out string? localFilePath)
        {
            var baseDirectory = "monitor";

            var folder = Path.Combine(baseDirectory, "url_" + ConvertUrlToValidFilename(url) );

            if (Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any())
            {
                var imgUrl = GetUrlFromHtml(url);
                byte[] img = GetBytes(imgUrl);

                var lastFile = Directory.EnumerateFiles(folder).Order().Last();
                var img2 = File.ReadAllBytes(lastFile);

                if (ImageComparator.Compare(img, img2))
                {
                    localFilePath = null;
                    return true;
                }
                else
                {
                    localFilePath = SaveFile(imgUrl, folder, img);
                    return false;
                }
            }
            else
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var imgUrl = GetUrlFromHtml(url);
                byte[] img = GetBytes(imgUrl);

                localFilePath = SaveFile(imgUrl, folder, img);
                return false;
            }
        }

        static string ConvertUrlToValidFilename(string url)
        {
            // Convert the URL to a file-safe format
            string filename = url
                .Replace("https://", "") // Remove protocol part
                .Replace("http://", "")  // Remove protocol part
                .Replace("/", "_")       // Replace slashes with underscores
                .Replace(":", "_")       // Replace colons with underscores
                .Replace("?", "_")       // Replace question marks with underscores
                .Replace("&", "_")       // Replace ampersands with underscores
                .Replace("=", "_")       // Replace equal signs with underscores
                .Replace(" ", "_");      // Replace spaces with underscores

            filename = RemoveInvalidFilenameChars(filename);
            filename = filename.Length > 128 ? filename.Substring(0, 128) : filename;

            return filename;
        }

        static string RemoveInvalidFilenameChars(string filename)
        {
            string invalidCharsPattern = new string(Path.GetInvalidFileNameChars());
            return Regex.Replace(filename, $"[{Regex.Escape(invalidCharsPattern)}]", "_");
        }

        //private bool Compare(MonitorJob activeJob, out string? localFilePath)
        //{
        //    var baseDirectory = "monitor";

        //    var folder = Path.Combine(baseDirectory, "chat_" + activeJob.ChatId);

        //    if (Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any())
        //    {
        //        byte[] img = GetBytes(activeJob);

        //        var lastFile = Directory.EnumerateFiles(folder).Order().Last();
        //        var img2 = File.ReadAllBytes(lastFile);

        //        if (ImageComparator.Compare(img, img2))
        //        {
        //            localFilePath = null;
        //            return true;
        //        }
        //        else
        //        {
        //            localFilePath = SaveFile(activeJob.Url, folder, img);
        //            return false;
        //        }
        //    }
        //    else
        //    {
        //        if(!Directory.Exists(folder))
        //        {
        //            Directory.CreateDirectory(folder);
        //        }

        //        byte[] img = GetBytes(activeJob);

        //        localFilePath = SaveFile(activeJob.Url, folder, img);
        //        return false;
        //    }
        //}

        //private static byte[] GetBytes(MonitorJob activeJob)
        //{
        //    HttpClient client = new HttpClient();

        //    var urlToUse = activeJob.Url;
        //    if (activeJob.IsDtekJob)
        //    {
        //        var htmltask = client.GetStringAsync(activeJob.Url);
        //        htmltask.Wait();
        //        urlToUse = ParsePage(htmltask.Result);

        //        Uri uri = new Uri(activeJob.Url);
        //        urlToUse = $"{uri.Scheme}://{uri.Host}{urlToUse}";
        //    }



        //    var task = client.GetByteArrayAsync(urlToUse);
        //    task.Wait();
        //    return task.Result;
        //}

        private static byte[] GetBytes(string url)
        {
            HttpClient client = new HttpClient();
            var task = client.GetByteArrayAsync(url);
            task.Wait();
            return task.Result;
        }

        private static string GetUrlFromHtml(string url)
        {
            HttpClient client = new HttpClient();

            var htmltask = client.GetStringAsync(url);
            htmltask.Wait();
            var urlToUse = ParsePage(htmltask.Result);

            Uri uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}{urlToUse}";
        }


        private static string ParsePage(string html)
        {
            var document = new HtmlParser().ParseDocument(html);

            var url = document.QuerySelectorAll("div > figure > picture > img");

            if (url is null || !url.Any())
            {
                var filename = "failure_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".html";
                //File.WriteAllText(filename, html);
                throw new KeyNotFoundException("cannot find url. Check file " + filename);
            }

            return url.First().GetAttribute("src");

        }

        private static string SaveFile(string url, string folder, byte[] bytes)
        {
            var extention = new FileInfo(url).Extension;
            if (string.IsNullOrEmpty(extention))
            {
                extention = ".jpg";
            }
            var filename = DateTime.Now.ToString("yyyyMMddHHmmss") + extention;
            var filePath = Path.Combine(folder, filename);
            File.WriteAllBytes(filePath, bytes);
            return filePath;
        }

        internal void DeactivateJob(long chatId, string reason)
        {
            _dbservice.DisableJob(chatId, reason);
        }
    }
}
