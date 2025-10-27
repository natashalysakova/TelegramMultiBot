using AngleSharp.Html.Parser;
using DtekParsers;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.ImageCompare
{
    public class MonitorService
    {
        private readonly ILogger<MonitorService> _logger;
        private readonly IMonitorDataService _dbservice;
        CancellationToken _cancellationToken;

        public event Action<long, List<(string filename, string caption)>> ReadyToSend = delegate { };

        public MonitorService(ILogger<MonitorService> logger, IMonitorDataService dbservice)
        {
            _logger = logger;
            _dbservice = dbservice;
        }
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
                    var sendList = new Dictionary<long, List<(string file, string caption)>>();
                    foreach (var group in activeJobs)
                    {
                        var hasToBeRun = group.Any(x => x.NextRun < datetime);

                        if (!hasToBeRun)
                        {
                            continue;
                        }

                        bool isTheSame;

                        List<GroupSchedule> schedule;
                        try
                        {
                            schedule = await new ScheduleParser().Parse(group.Key);

                            var updateTime = schedule.First().Updated;

                            isTheSame = group.All(x => x.LastScheduleUpdate == updateTime);
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

                        if (!isTheSame && schedule != null)
                        {
                            string caption = $"Оновлений графік {GetLocation(group.Key)} станом на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                            try
                            {

                                var imagePathList = await GetImagePathList(group.Key, schedule);
                                var updateTime = schedule.First().Updated;

                                foreach (var job in group)
                                {
                                    foreach (var image in imagePathList)
                                    {
                                        if (!sendList.ContainsKey(job.ChatId))
                                            sendList.Add(job.ChatId, new List<(string file, string caption)>() { (image, caption) });
                                        else
                                            sendList[job.ChatId].Add((image, caption));
                                    }
                                }

                                UpdateLastSendScheduleTime(group, updateTime);

                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex.Message);
                                continue;
                            }
                        }
                        else
                        {
                            _logger.LogTrace("{key} was not updated", group.Key);
                        }
                        UpdateNextRun(group, 30);
                    }

                    foreach (var item in sendList)
                    {
                        ReadyToSend?.Invoke(item.Key, item.Value);
                    }


                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }, token);
        }

        private async Task<string[]> GetImagePathList(string url, List<GroupSchedule> schedule)
        {
            var baseDirectory = "monitor";

            var folder = Path.Combine(baseDirectory, "url_" + ConvertUrlToValidFilename(url));


            if (Directory.Exists(folder))
            {
                var files = Directory.EnumerateFiles(folder);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            else
            {
                Directory.CreateDirectory(folder);
            }
            var imageBytes = await ScheduleImageGenerator.GenerateAllGroupsRealSchedule(schedule);

            return [SaveFile(folder, imageBytes)];
        }

        private static string GetLocation(string url)
        {
            switch (url)
            {
                case "https://www.dtek-krem.com.ua/ua/shutdowns":
                    return "для Київської області";
                case "https://www.dtek-kem.com.ua/ua/shutdowns":
                    return "для м.Київ";
                default:
                    return string.Empty;
            }
        }

        private bool Compare(string url, out IList<string> localFilePath)
        {
            var baseDirectory = "monitor";

            var folder = Path.Combine(baseDirectory, "url_" + ConvertUrlToValidFilename(url));
            var imgUrls = GetUrlFromHtml(url);
            localFilePath = new List<string>();


            if (Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any())
            {
                var lastFiles = Directory.EnumerateFiles(folder).Order().TakeLast(imgUrls.Count());

                foreach (var imgUrl in imgUrls)
                {
                    var isFound = false;
                    byte[] img = GetBytes(imgUrl);

                    for (int i = 0; i < lastFiles.Count(); i++)
                    {
                        var img2 = File.ReadAllBytes(lastFiles.ElementAt(i));

                        if (ImageComparator.Compare(img, img2))
                        {
                            isFound = true;
                        }
                    }

                    if (!isFound)
                    {
                        localFilePath.Add(SaveFile(imgUrl, folder, img));
                    }
                }

                if (localFilePath.Count == 0)
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

            return urlToUse.Select(x => $"{uri.Scheme}://{uri.Host}{x}");
        }


        private static IEnumerable<string?>? ParsePage(string html)
        {
            var document = new HtmlParser().ParseDocument(html);

            var url = document.QuerySelectorAll("div > figure > picture > img");

            if (url is null || !url.Any())
            {
                //var filename = "failure_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".html";
                //File.WriteAllText(filename, html);
                throw new KeyNotFoundException("cannot find url.");
            }

            //foreach (var images in url)
            //{
            //    var path = images.GetAttribute("src");
            //    yield return path;
            //}

            return url?.Select(x => x.GetAttribute("src"));

        }

        private static string SaveFile(string folder, byte[] bytes)
        {
            var extention = ".png";
            var filename = DateTime.Now.ToString("yyyyMMddHHmmssfff") + extention;
            var filePath = Path.Combine(folder, filename);
            File.WriteAllBytes(filePath, bytes);
            Console.WriteLine(Path.GetFullPath(filePath));
            return filePath;
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
                case "kem":
                    return "https://www.dtek-kem.com.ua/ua/shutdowns";
                default:
                    return null;

            }
        }

        internal bool SendExisiting(long chatId, string region)
        {
            var job = _dbservice.Jobs.Where(x => x.Url == GetFromRegion(region)).FirstOrDefault();
            if(job is null)
            {
                return false;
            }

            var info = GetInfo(job.Id);

            if (info == default)
                return false;

            ReadyToSend?.Invoke(chatId, new List<(string filename, string caption)>() { (info.filename, info.caption) });
            return true;
        }

        internal bool SendExisiting(int jobAdded)
        {
            var info = GetInfo(jobAdded);

            if (info == default)
                return false;

            ReadyToSend?.Invoke(info.chatId, new List<(string filename, string caption)>() { (info.filename, info.caption) });
            return true;
        }

        internal (string filename, string caption, long chatId) GetInfo(int jobId)
        {
            var job = _dbservice[jobId];
            if (job == null)
                return default;

            var baseDirectory = "monitor";
            var folder = Path.Combine(baseDirectory, "url_" + ConvertUrlToValidFilename(job.Url));
            _logger.LogDebug(folder);
            if (Directory.Exists(folder))
            {
                var files = Directory.EnumerateFiles(folder);

                _logger.LogDebug(string.Join(',', files));

                if (files.Any())
                {
                    var fileToSend = files.Order().Last();
                    string caption = $"Актуальний графік {GetLocation(job.Url)} на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                    return new() { filename = fileToSend, caption = caption, chatId = job.ChatId };
                }
            }
            return default;
        }

        public void UpdateNextRun(IGrouping<string, MonitorJob> jobs, int minutes)
        {
            foreach (var job in jobs)
            {
                job.NextRun = DateTime.Now.AddMinutes(minutes);
            }

            _dbservice.SaveChanges();
        }

        public void UpdateLastSendScheduleTime(IGrouping<string, MonitorJob> jobs, DateTime updateTime)
        {
            foreach (var job in jobs)
            {
                job.LastScheduleUpdate = updateTime;
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

        internal bool IsSubscribed(long id, string region)
        {
            return _dbservice.Jobs.Any(x => x.ChatId == id && x.Url == GetFromRegion(region) && x.IsActive);
        }
    }
}
