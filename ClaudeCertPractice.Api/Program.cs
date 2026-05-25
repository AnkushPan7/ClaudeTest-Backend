using ClaudeCertPractice.Api.Configuration;
using ClaudeCertPractice.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Render (and similar hosts) inject PORT; bind Kestrel explicitly.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

EnvFileLoader.LoadFromContentRoot(Directory.GetCurrentDirectory());
EnvFileLoader.LoadFromContentRoot(builder.Environment.ContentRootPath);

builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.Configure<QuizSettings>(builder.Configuration.GetSection(QuizSettings.SectionName));
builder.Services.AddControllers();
builder.Services.AddSingleton<ExamGuideService>();
builder.Services.AddSingleton<QuestionBankService>();
builder.Services.AddSingleton<QuizSessionService>();
builder.Services.AddHttpClient<LearningContentService>();
builder.Services.AddHttpClient<AiQuestionGeneratorService>();
var corsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"];
var allowedOrigins = string.IsNullOrWhiteSpace(corsOrigins)
    ? new[]
    {
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5174"
    }
    : corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    service = "ClaudeCertPractice.Api",
    health = "/health",
    api = "/api/quiz/metadata"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapControllers();
app.Run();
