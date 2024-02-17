using Bober.Library.Contract;

namespace Bober.Worker.Interfaces
{
    public interface IDiffusor
    {
        public string UI { get; }
        bool isAvailable();
        Task Run(JobInfo job);
    }
}
