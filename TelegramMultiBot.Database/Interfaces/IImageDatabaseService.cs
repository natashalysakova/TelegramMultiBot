using TelegramMultiBot.Database.DTO;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface IImageDatabaseService
    {
        int RunningJobs { get; }

        int ActiveJobsCount(long userId);
        void AddFile(string jobId, string fileId);
        void AddFiles(IEnumerable<string> jobResultIds, IEnumerable<string> fileIds);
        void AddResult(string id, JobResultInfoCreate jobResultInfo);

        void CancelUnfinishedJobs();
        int DeleteJob(Guid id);
        Guid Enqueue(IInputData message);

        JobInfo GetJob(string jobId);
        JobInfo? GetJobByFileId(string fileId);
        JobInfo? GetJobByResultId(string id);
        JobResultInfoView? GetJobResult(string jobResultId);
        IEnumerable<JobInfo> GetJobsFromQueue();
        IEnumerable<JobInfo> GetJobsOlderThan(DateTime date);

        void PostProgress(string id, double progress, string status);

        void PushBotId(string jobId, int messageId);

        void RemoveJobs(IEnumerable<string> jobsToDelete);

        void ReturnToQueue(JobInfo job);

        bool TryDequeue(out JobInfo? job);
    }
}