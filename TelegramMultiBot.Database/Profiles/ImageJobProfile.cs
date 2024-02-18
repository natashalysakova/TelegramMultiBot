using AutoMapper;
using TelegramMultiBot.Database.DTO;

namespace TelegramMultiBot.Database.Profiles
{
    public class ImageJobProfile : Profile
    {
        public ImageJobProfile()
        {
            CreateMap<ImageJob, JobInfo>()
                .ForMember(x => x.Exception, act => act.Ignore())
                .ReverseMap()
                .ForMember(x=>x.Progress, act=> act.Ignore());
            CreateMap<JobResult, JobResultInfo>()
                //.ForMember(x=>x.Id, act=> act.MapFrom(x=>x.Id.ToString()))
                .ForMember(x => x.Seed, act => act.MapFrom(y => GetSeed(y.Info)))
                .ForMember(x => x.RenderTime, act => act.MapFrom(x => x.RenderTime.Microseconds))
                .ReverseMap()
                .ForMember(x => x.RenderTime, act => act.MapFrom(x => TimeSpan.FromMicroseconds(x.RenderTime)))
                //.ForMember(x => x.Id, act => act.MapFrom(x => Guid.Parse(x.Id)))
                ;
        }

        private static long GetSeed(string info)
        {
            var split = info.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var hasSeed = split.Any(x => x.Contains("Seed:"));
            if (hasSeed)
                return long.Parse(ParseParemeter(split.Single(x => x.Contains("Seed:"))));
            else
                return -1;
        }

        private static string ParseParemeter(string paremeter)
        {
            return paremeter.Substring(paremeter.IndexOf(":") + 1).Trim();
        }
    }
}
    

    //private JobResultInfo GetInfo(JobResult jobResult)
    //{
    //    var split = jobResult.Info.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    //    var item = new JobResultInfo
    //    {
    //        Seed = long.Parse(ParseParemeter(split.Single(x => x.Contains("Seed:")))),
    //        FilePath = jobResult.FilePath,
    //        Info = jobResult.Info,
    //        RenderTime = jobResult.RenderTime.Milliseconds,
    //        Id = jobResult.Id.ToString()
    //    };

    //    return item;
    //}
    //private JobInfo GetInfo(ImageJob job)
    //{
    //    var result = new JobInfo()
    //    {
    //        Id = job.Id.ToString(),
    //        BotMessageId = job.BotMessageId,
    //        ChatId = job.ChatId,
    //        MessageId = job.MessageId,
    //        MessageThreadId = job.MessageId,
    //        PostInfo = job.PostInfo,
    //        Type = job.Type,
    //        UpscaleModifyer = job.UpscaleModifyer,
    //        Results = new JobResultInfo[job.Results.Count]
    //    };


    //    for (int i = 0; i < result.Results.Length; i++)
    //    {
    //        result.Results[i] = GetInfo(job.Results.ElementAt(i));
    //    }

    //    return result;
    //}
    
