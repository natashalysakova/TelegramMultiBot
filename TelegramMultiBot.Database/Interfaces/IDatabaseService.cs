using TelegramMultiBot.Database.DTO;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface IDatabaseService
    {
        int RunningJobs { get; }

        int ActiveJobsCount(long userId);
        void AddResult(string id, JobResultInfoCreate jobResultInfo);
        void CancelUnfinishedJobs();
        Guid Enqueue(IInputData message);
        JobInfo? GetJob(string jobId);
        JobResultInfoView? GetJobResult(string jobResultId);
        IEnumerable<JobInfo> GetJobsOlderThan(DateTime date);
        void PostProgress(string id, double progress, string status);
        void PushBotId(string jobId, int messageId);
        void RemoveJobs(IEnumerable<string> jobsToDelete);
        void ReturnToQueue(JobInfo job);
        bool TryDequeue(out JobInfo? job);
    }
}