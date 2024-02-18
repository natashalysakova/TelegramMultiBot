using System.Globalization;
using Telegram.Bot;
using TelegramMultiBot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/echo", (ILogger<Program> logger) =>
{
    logger.LogInformation("Ok");
    return "ok";
}).WithOpenApi();

app.MapPost(BoberApiClient.successCallback, (object payload, ILogger<Program> logger) =>
{
    logger.LogInformation("Received payload: {payload}", payload);
    //bot.JobFinished(payload as JobInfo);
});

app.MapPost(BoberApiClient.failureCallback, (object payload, ILogger<Program> logger) =>
{
    logger.LogInformation("Received payload: {payload}", payload);
    //bot.JobFailed(payload as JobInfo, ex);
});
app.MapPost(BoberApiClient.progressCallback, (object payload, ILogger<Program> logger) =>
{
    logger.LogInformation("Received payload: {payload}", payload);
    //bot.JobProgress(payload as JobInfo, ex);
});

app.Run();
