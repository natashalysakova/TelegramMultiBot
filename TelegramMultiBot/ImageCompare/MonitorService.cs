using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.ImageCompare
{
    public class MonitorService
    {
        private readonly ILogger<MonitorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMonitorDataService _dataService;

        public event Action<SendInfo> ReadyToSend = delegate { };

        public MonitorService(ILogger<MonitorService> logger, IServiceProvider serviceProvider, IMonitorDataService dataService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _dataService = dataService;
        }
        public void Run(CancellationToken token)
        {
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await CheckForUpdates();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while checking for updates: {message}", ex.Message);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }

            }, token);
        }

        private async Task CheckForUpdates()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

            var activeJobs = await dbservice.GetActiveJobs();
            var sendList = new List<SendInfo>();
            foreach (var job in activeJobs)
            {
                try
                {
                    bool sendUpdate = DecideIfSendUpdate(job);

                    if (sendUpdate)
                    {
                        job.LastScheduleUpdate = job.Location.LastUpdated;

                        if (job.Group != null)
                        {
                            job.LastSentGroupSnapsot = job.Group.DataSnapshot;
                        }

                        await dbservice.Update(job);

                        string caption = $"Оновлений графік {GetLocationByUrl(job.Location.Region)} станом на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                        try
                        {
                            var imagePathList = await dbservice.GetImagesForJob(job.Id);
                            sendList.Add(new SendInfo()
                            {
                                ChatId = job.ChatId,
                                Filenames = imagePathList.ToList(),
                                Caption = caption,
                                MessageThreadId = job.MessageThreadId,
                            });

                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.Message);
                            continue;
                        }
                    }
                    else
                    {
                        _logger.LogTrace("{key} was not updated", job.Id);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError("{key} job error: {message}", job.Id, ex.Message);
                }
            }

            foreach (var item in sendList)
            {
                ReadyToSend?.Invoke(item);
            }
        }

        private bool DecideIfSendUpdate(MonitorJob job)
        {
            // Check if schedule has been updated since last send
            bool hasScheduleUpdate = job.LastScheduleUpdate != job.Location.LastUpdated;

            // For non-group jobs, send if schedule updated
            if (!job.GroupId.HasValue)
            {
                return hasScheduleUpdate;
            }

            // For group jobs, also check if group data changed
            bool hasGroupDataChange = HasGroupDataChanged(job);

            return hasScheduleUpdate && hasGroupDataChange;
        }

        private static bool HasGroupDataChanged(MonitorJob job)
        {
            if (job.Group == null)
            {
                return false;
            }

            // New snapshot available when previously none existed
            if (job.LastSentGroupSnapsot is null && job.Group.DataSnapshot is not null)
            {
                return true;
            }

            // Snapshot content has changed
            return job.LastSentGroupSnapsot != job.Group.DataSnapshot;
        }

        private static string GetLocationByUrl(string url)
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

        private static string GetLocationByRegion(string region)
        {
            switch (region)
            {
                case "krem":
                    return "для Київської області";
                case "kem":
                    return "для м.Київ";
                default:
                    return string.Empty;
            }
        }

        internal async Task<Guid> AddDtekJob(long chatId, int? messageThreadId, string region, string? group)
        {
            var existing = await _dataService.GetJobBySubscriptionParameters(chatId, region, group);

            if (existing != null && existing.IsActive)
            {
                return existing.Id;
            }
            else if (existing != null && existing.IsActive == false)
            {
                await _dataService.ReactivateJob(existing.Id, messageThreadId);
                return existing.Id;
            }


            var location = await _dataService.GetLocationByRegion(region);

            if (location == null)
            {
                return Guid.Empty;
            }

            ElectricityGroup? dbGroup = default;

            if (!string.IsNullOrEmpty(group))
            {
                dbGroup = await _dataService.GetGroupByCodeAndLocationRegion(region, group);
            }

            var job = new MonitorJob()
            {
                ChatId = chatId,
                LocationId = location.Id,
                IsDtekJob = true,
                MessageThreadId = messageThreadId,
                GroupId = dbGroup?.Id,
                Type = dbGroup is null ? ElectricityJobType.AllGroups : ElectricityJobType.SingleGroup,
            };
            await _dataService.Add(job);
            return job.Id;
        }

        public async Task<bool> SendExisiting(long chatId, string region, int? messageThreadId)
        {

            var existingInfo = await _dataService.GetCurrentScheduleImagesForRegion(region);

            string caption = $"Актуальний графік {GetLocationByRegion(region)} на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            var info = new SendInfo()
            {
                ChatId = chatId,
                Filenames = existingInfo.ToList(),
                Caption = caption,
                MessageThreadId = messageThreadId,
            };

            ReadyToSend?.Invoke(info);
            return true;
        }

        public async Task<bool> SendExisiting(long chatId, string region, string group, ElectricityJobType jobType, int? messageThreadId)
        {

            var existingInfo = await _dataService.GetCurrentScheduleImagesForGroupRegion(group, region, jobType);
            var groupFromDb = await _dataService.GetGroupByCodeAndLocationRegion(region, group);
            string groupName = groupFromDb?.GroupName ?? "";

            string caption = $"Актуальний графік {GetLocationByRegion(region)} {groupName} на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            var info = new SendInfo()
            {
                ChatId = chatId,
                Filenames = existingInfo.ToList(),
                Caption = caption,
                MessageThreadId = messageThreadId,
            };

            ReadyToSend?.Invoke(info);
            return true;
        }

        public async Task<bool> SendExisiting(Guid jobAdded)
        {
            var job = await _dataService.GetJobById(jobAdded);
            if (job is null)
            {
                return false;
            }

            var info = await GetInfo(job);

            if (info == default)
                return false;

            job.LastScheduleUpdate = job.Location.LastUpdated;
            await _dataService.Update(job);

            ReadyToSend?.Invoke(info);
            return true;
        }

        internal async Task<SendInfo> GetInfo(MonitorJob job)
        {
            var images = await _dataService.GetImagesForJob(job.Id);

            string caption = $"Актуальний графік {GetLocationByUrl(job.Location.Url)} {job.Group?.GroupName ?? ""} на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            return new SendInfo()
            {
                ChatId = job.ChatId,
                Filenames = images.ToList(),
                Caption = caption,
                MessageThreadId = job.MessageThreadId,
            };
        }

        internal async Task<bool> DisableJob(long chatId, string region, string? group, string reason)
        {
            await _dataService.DisableJobs(chatId, region, group, reason);
            return true;
        }

        public async Task DisableJob(long chatId, string reason)
        {
            await _dataService.DisableJobs(chatId, reason);
        }


        internal async Task<IEnumerable<MonitorJob>> GetActiveJobs(long chatId)
        {
            return await _dataService.GetActiveJobs(chatId);
        }

        internal async Task<Dictionary<string, bool>> IsSubscribed(long chatId, string region)
        {
            return await _dataService.GetSubscriptionList(chatId, region);
        }
    }

    public class SendInfo
    {
        public List<string> Filenames { get; set; } = new List<string>();
        public string Caption { get; set; }
        public long ChatId { get; set; }
        public int? MessageThreadId { get; set; }
    }
}
