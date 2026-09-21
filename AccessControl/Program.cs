using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

foreach (var key in new[]
{
    "TWILIO_ACCOUNT_SID",
    "TWILIO_AUTH_TOKEN",
    "TWILIO_FROM_NUMBER",
    "TWILIO_TO_NUMBER",
    "TWILIO_VERIFY_SERVICE_SID"
})
{
    var value = builder.Configuration[key];
    if (!string.IsNullOrWhiteSpace(value) &&
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
    {
        Environment.SetEnvironmentVariable(key, value);
    }
}

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Access photos are written into the web root and served back from it, so it
// has to exist: a fresh clone has no wwwroot, which leaves WebRootPath null
// and UseStaticFiles with nothing to serve. Resolved explicitly here so the
// upload path in AccessProcessor.InsertAccessPhoto and the URL it hands back
// always point at the same folder.
var webRoot = string.IsNullOrWhiteSpace(app.Environment.WebRootPath)
    ? Path.Combine(app.Environment.ContentRootPath, "wwwroot")
    : app.Environment.WebRootPath;

Directory.CreateDirectory(webRoot);

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRoot)
});
app.UseAuthorization();
app.MapControllers();

app.Run();
