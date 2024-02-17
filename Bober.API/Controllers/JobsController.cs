using Bober.Library.Contract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Bober.Library.Interfaces;

namespace Bober.API.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly ILogger<JobsController> _logger;
        private readonly IDatabaseService _databaseService;

        public JobsController(ILogger<JobsController> logger, IDatabaseService databaseService)
        {
            _logger = logger;
            _databaseService = databaseService;
        }

        [HttpGet(Name = "GetJobResultInfo")]
        public JobResultInfo? GetJobResultInfo(string jobResultId)
        {
            return _databaseService.GetJobResult(jobResultId);
        }

        [HttpGet(Name = "GetJobInfo")]
        public JobInfo? GetJobInfo(string jobId)
        {
            return _databaseService.GetJob(jobId);
        }

    }

}
