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

        public event Action<long, string, string> ReadyToSend = delegate { };

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
                    var datetime = DateTime.Now;
                    _logger.LogTrace("checking at {date}", datetime);
                    var activeJobs = _dbservice.Jobs.Where(x => x.IsActive).GroupBy(x => x.Url);

                    foreach (var group in activeJobs)
                    {
                        var hasToBeRun = group.Any(x => x.NextRun < datetime);

                        if (!hasToBeRun)
                        {
                            continue;
                        }

                        bool isTheSame;
                        IList<string> localFilePath;
                        try
                        {
                            isTheSame = Compare(group.Key, out localFilePath);
                        }
                        catch (KeyNotFoundException ex)
                        {
                            _logger.LogError("fetching {key} failed: {message}", group.Key, ex.Message);
                            UpdateNextRun(group, 10);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("{key} error: {message}", group.Key, ex.Message);
                            continue;
                        }

                        if (!isTheSame && localFilePath != null)
                        {
                            string caption = "Оновлений графік на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                            foreach (var job in group)
                            {
                                foreach (var image in localFilePath)
                                {
                                    ReadyToSend?.Invoke(job.ChatId, image, caption);
                                }
                            }

                        }
                        else
                        {
                            _logger.LogTrace("{key} was not updated", group.Key);
                        }
                        UpdateNextRun(group, 30);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }, token);
        }

        private bool Compare(string url, out IList<string> localFilePath)
        {
            var baseDirectory = "monitor";

            var folder = Path.Combine(baseDirectory, "url_" + ConvertUrlToValidFilename(url));
            var imgUrls = GetUrlFromHtml(url);
            localFilePath = new List<string>();


            if (Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any())
            {

                var lastFile = Directory.EnumerateFiles(folder).Order().Last();
                var img2 = File.ReadAllBytes(lastFile);

                foreach (var imgUrl in imgUrls)
                {
                    byte[] img = GetBytes(imgUrl);

                    if (!ImageComparator.Compare(img, img2))
                    {
                        localFilePath.Add(SaveFile(imgUrl, folder, img));
                    }
                }

                if(localFilePath.Count == 0)
                {
                    return true;
                }
                else
                {
                    return false; 
                }
            }
            else
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                foreach (var imgUrl in imgUrls)
                {
                    byte[] img = GetBytes(imgUrl);

                    localFilePath.Add(SaveFile(imgUrl, folder, img));
                }

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

        private static IEnumerable<string> GetUrlFromHtml(string url)
        {
            HttpClient client = new HttpClient();

            var htmltask = client.GetStringAsync(url);
            htmltask.Wait();
            var urlToUse = ParsePage(htmltask.Result);

            Uri uri = new Uri(url);
            foreach (var urlFromPage in urlToUse)
            {
                yield return $"{uri.Scheme}://{uri.Host}{urlFromPage}";
            }
        }


        private static IEnumerable<string?>? ParsePage(string html)
        {
            var document = new HtmlParser().ParseDocument(html);

            var url = document.QuerySelectorAll("div > figure > picture > img");

            if (url is null || !url.Any())
            {
                var filename = "failure_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".html";
                //File.WriteAllText(filename, html);
                throw new KeyNotFoundException("cannot find url. Check file " + filename);
            }

            foreach (var images in url)
            {
                var path = images.GetAttribute("src");
                yield return path;
            }

            //return url?.Select(x => x.GetAttribute("src"));

        }

        private static string SaveFile(string url, string folder, byte[] bytes)
        {
            var extention = new FileInfo(url).Extension;
            if (string.IsNullOrEmpty(extention))
            {
                extention = ".jpg";
            }
            var filename = DateTime.Now.ToString("yyyyMMddHHmmssfff") + extention;
            var filePath = Path.Combine(folder, filename);
            File.WriteAllBytes(filePath, bytes);
            Console.WriteLine(Path.GetFullPath(filePath));
            return filePath;
        }


        internal int AddDtekJob(long chatId, string region)
        {
            string url;

            url = GetFromRegion(region);

            if (url == null)
            {
                return -1;
            }

            var existing = _dbservice.Jobs.SingleOrDefault(x => x.ChatId == chatId && x.Url == url);

            if (existing != null && existing.IsActive)
            {
                return -1;
            }
            else if (existing != null && existing.IsActive == false)
            {
                existing.IsActive = true;
                existing.DeactivationReason = null;
                _dbservice.SaveChanges();
                return existing.Id;
            }


            var job = new MonitorJob() { ChatId = chatId, Url = url, IsDtekJob = true, NextRun = DateTime.Now };
            _dbservice.Jobs.Add(job);
            _dbservice.SaveChanges();
            return job.Id;
        }

        private string? GetFromRegion(string region)
        {
            switch (region)
            {
                case "krem":
                    return "https://www.dtek-krem.com.ua/ua/shutdowns";

                default:
                    return null;

            }
        }

        internal bool SendExisiting(int jobAdded)
        {
            var job = _dbservice[jobAdded];
            if (job == null)
                return false;


            var baseDirectory = "monitor";
            var folder = Path.Combine(baseDirectory, "url_" + ConvertUrlToValidFilename(job.Url));

            if (Directory.Exists(folder))
            {
                var files = Directory.EnumerateFiles(folder);
                if (files.Any())
                {
                    var fileToSend = files.Order().Last();
                    string caption = "Задача додана. Актуальний графік на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                    ReadyToSend?.Invoke(job.ChatId, fileToSend, caption);
                    return true;
                }
            }

            return false;
        }

        public void UpdateNextRun(IGrouping<string, MonitorJob> jobs, int minutes)
        {
            foreach (var job in jobs)
            {
                job.NextRun = DateTime.Now.AddMinutes(minutes);
            }

            _dbservice.SaveChanges();
        }






        internal bool DisableJob(long chatId, string region, string reason)
        {
            var url = GetFromRegion(region);

            if (url == null)
                return false;

            var jobs = _dbservice.Jobs.Where(x => x.ChatId == chatId && x.Url == url);
            DisableJobs(jobs, reason);
            return true;
        }




        public void DisableJob(long chatId, string reason)
        {
            var jobs = _dbservice.Jobs.Where(x => x.ChatId == chatId);
            DisableJobs(jobs, reason);
        }

        private void DisableJobs(IEnumerable<MonitorJob> jobs, string reason)
        {
            foreach (var job in jobs)
            {
                job.IsActive = false;
                job.DeactivationReason = reason;
            }

            _dbservice.SaveChanges();
        }

        internal IEnumerable<MonitorJob> GetJobs(long chatId)
        {
            return _dbservice.Jobs.Where(x => x.ChatId == chatId);
        }

        internal IEnumerable<MonitorJob> GetActiveJobs(long chatId)
        {
            return _dbservice.Jobs.Where(x => x.ChatId == chatId && x.IsActive);
        }
    }
}
