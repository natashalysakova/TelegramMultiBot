
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Profiles;

public static partial class Mappers
{
    public static JobInfo ToJobInfo(this ImageJob job)
    {
        var result = new JobInfo()
        {
            Id = job.Id.ToString(),
            BotMessageId = job.BotMessageId,
            ChatId = job.ChatId,
            MessageId = job.MessageId,
            MessageThreadId = job.MessageId,
            PostInfo = job.PostInfo,
            Type = job.Type,
            UpscaleModifyer = job.UpscaleModifyer,
            Results = job.Results.Select(x=>x.ToJobResultInfoView()).ToList(),
        };
        return result;
    }

    public static ImageJob ToImageJob(this JobInfo job)
    {
        var result = new ImageJob()
        {
            Id = Guid.Parse(job.Id),
            BotMessageId = job.BotMessageId,
            ChatId = job.ChatId,
            MessageId = job.MessageId,
            MessageThreadId = job.MessageId,
            PostInfo = job.PostInfo,
            Type = job.Type,
            UpscaleModifyer = job.UpscaleModifyer,
            Results = job.Results.Select(x=>x.ToJobResultInfoView()).ToList(),
        };
        return result;
    }

    public static JobResultInfoView ToJobResultInfoView(this JobResult jobResult)
    {
        var item = new JobResultInfoView
        {
            Seed = GetSeed(jobResult.Info),
            FilePath = jobResult.FilePath,
            Info = jobResult.Info,
            RenderTime = jobResult.RenderTime,
            Id = jobResult.Id.ToString(),
            FileId = jobResult.FileId
        };
        return item;
    }

    public static JobResult ToJobResultInfoView(this JobResultInfoView jobResult)
    {
        var item = new JobResult
        {
            FilePath = jobResult.FilePath,
            Info = jobResult.Info,
            RenderTime = jobResult.RenderTime,
            FileId = jobResult.FileId,
            Id = Guid.Parse(jobResult.Id)
        };
        return item;
    }

    public static JobResult ToJobResult(this JobResultInfoCreate jobResult)
    {
        var item = new JobResult
        {
            FilePath = jobResult.FilePath,
            Info = jobResult.Info,
            RenderTime = jobResult.RenderTime,
            
        };
        return item;
    }

    private static long GetSeed(string? info)
    {
        if (info is null)
            return -1;

        var split = info.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var hasSeed = split.Any(x => x.Contains("Seed:"));
        if (hasSeed)
            return long.Parse(ParseParemeter(split.Single(x => x.Contains("Seed:"))));
        else
            return -1;
    }

    private static string ParseParemeter(string paremeter)
    {
        return paremeter[(paremeter.IndexOf(':') + 1)..].Trim();
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