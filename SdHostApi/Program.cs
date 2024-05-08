
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SdHostApi
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddWindowsService();
            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            //app.UseAuthorization();

            _ = app.MapGet("/gpu", async (HttpContext context) =>
            {
                //engtype_3D
                var cat = new PerformanceCounterCategory("GPU Engine");
                var countersNames = cat.GetInstanceNames();

                var gpuCounters = countersNames
                    .Where(x => x.Contains("engtype_3D"))
                    .SelectMany(x => cat.GetCounters(x))
                    .Where(x => x.CounterName.Equals("Utilization Percentage"))
                    //.Select(x=> x.CounterName + " " + x.NextSample().RawValue)
                    .ToList();

                var res = await GetGPUUsage(gpuCounters);
                return res.Sum(x => x.value);
            })
                .WithOpenApi();


            app.Run();
        }


        public static async Task<IEnumerable<Result>> GetGPUUsage(List<PerformanceCounter> gpuCounters)
        {
            gpuCounters.ForEach(x => x.NextValue());

            await Task.Delay(1000);

            var result = 0f;
            var r = new List<Result>();
            gpuCounters.ForEach(x =>
            {
                var nextvalue = x.NextValue();
                result += nextvalue;
                r.Add(new(x.InstanceName, nextvalue));
            });

            return r;
        }

        public record Result(string name, float value);
    }
}
