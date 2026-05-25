using ClaudeCertPractice.Api.Configuration;
using ClaudeCertPractice.Api.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5174")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.Run();
