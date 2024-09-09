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

app.MapPost("/sendImage", async (IFormFile file)=>{
    var directory = "path/to/input/directory";

    if(!Directory.Exists(directory))
        Directory.CreateDirectory(directory);

    var path = Path.Combine(directory, file.Name);
    await file.CopyToAsync(File.OpenWrite(path));
})
.WithName("PostImage")
.WithOpenApi();

app.Run();
