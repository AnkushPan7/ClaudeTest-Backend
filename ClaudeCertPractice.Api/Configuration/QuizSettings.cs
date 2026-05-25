namespace ClaudeCertPractice.Api.Configuration;

public class QuizSettings
{
    public const string SectionName = "Quiz";

    /// <summary>Json = fixed bank from questions.json; Ai = generate per session via Claude.</summary>
    public string QuestionSource { get; set; } = "Json";

    public string LearningUrl { get; set; } =
        "https://docs.anthropic.com/en/docs/build-with-claude/overview";

    public string AnthropicModel { get; set; } = "claude-sonnet-4-20250514";

    public int MaxSourceCharacters { get; set; } = 80_000;

    public int MaxQuestionsPerSession { get; set; } = 60;

    public int AiBatchSize { get; set; } = 5;
}
