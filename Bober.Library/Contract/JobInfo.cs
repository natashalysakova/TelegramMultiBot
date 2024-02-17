using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bober.Library.Contract
{


    public class JobInfo
    {
        public long ChatId { get; set; }
        public int? MessageThreadId { get; set; }
        public int BotMessageId { get; set; }
        public int MessageId { get; set; }

        public JobResultInfo[] Results { get; set; }
        public bool PostInfo { get; set; }
        public ImagineCommands Type { get; set; }
        public double? UpscaleModifyer { get; set; }
        public string Id { get; set; }
        public string PreviousJobResultId { get; set; }
        public string Text { get; set; }
        public Exception Exception { get; set; }
    }
}
