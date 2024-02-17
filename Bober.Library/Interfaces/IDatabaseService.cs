using Bober.Library.Contract;

namespace Bober.Library.Interfaces
{
    public interface IDatabaseService
    {
        int RunningJobs { get; }

        int ActiveJobsCount(long userId);
        void CancelUnfinishedJobs();
        void Enqueue(IInputData message);
        JobInfo? GetJob(string jobId);
        JobResultInfo? GetJobResult(string jobResultId);
        IEnumerable<JobInfo> GetJobsOlderThan(DateTime date);
        void PostProgress(string id, double progress, string status);
        void RemoveJobs(IEnumerable<string> jobsToDelete);
        bool TryDequeue(out JobInfo job);
    }
}