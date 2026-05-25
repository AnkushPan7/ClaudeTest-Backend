using ClaudeCertPractice.Api.Configuration;
using ClaudeCertPractice.Api.Models;
using ClaudeCertPractice.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ClaudeCertPractice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuizController : ControllerBase
{
    private readonly QuestionBankService _bank;
    private readonly QuizSessionService _sessions;
    private readonly AiQuestionGeneratorService _ai;
    private readonly ExamGuideService _examGuide;
    private readonly QuizSettings _settings;

    public QuizController(
        QuestionBankService bank,
        QuizSessionService sessions,
        AiQuestionGeneratorService ai,
        ExamGuideService examGuide,
        IOptions<QuizSettings> settings)
    {
        _bank = bank;
        _sessions = sessions;
        _ai = ai;
        _examGuide = examGuide;
        _settings = settings.Value;
    }

    [HttpGet("metadata")]
    public ActionResult<ExamMetadata> GetMetadata()
    {
        var meta = _bank.GetMetadata();
        var source = _settings.QuestionSource.Trim();
        return Ok(meta with
        {
            QuestionSource = source,
            AiGenerationAvailable = _ai.IsConfigured,
            LearningUrl = _settings.LearningUrl,
            MaxQuestionsPerSession = _settings.MaxQuestionsPerSession,
        });
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<SessionDto>> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken ct)
    {
        var source = (request.Source ?? _settings.QuestionSource).Trim();
        var useAi = source.Equals("Ai", StringComparison.OrdinalIgnoreCase);

        if (useAi && !_ai.IsConfigured)
            return BadRequest("AI mode requires ANTHROPIC_API_KEY or Quiz:AnthropicApiKey.");

        List<Question> questions;

        if (useAi)
        {
            var url = string.IsNullOrWhiteSpace(request.LearningUrl)
                ? _settings.LearningUrl
                : request.LearningUrl.Trim();
            var count = Math.Clamp(request.Count ?? 10, 1, _settings.MaxQuestionsPerSession);

            try
            {
                questions = await _ai.GenerateAsync(count, url, request.SectionIds, ct);
            }
            catch (Exception ex)
            {
                return StatusCode(502, ex.Message);
            }
        }
        else
        {
            var meta = _bank.GetMetadata();
            var count = Math.Clamp(request.Count ?? meta.TotalQuestions, 1, meta.TotalQuestions);
            var ids = _bank.PickRandomIds(count, request.SectionIds);
            questions = ids
                .Select(id => _bank.GetById(id))
                .Where(q => q is not null)
                .Cast<Question>()
                .ToList();
        }

        if (questions.Count == 0)
            return BadRequest("No questions available for this session.");

        var session = _sessions.Create(questions, useAi ? "Ai" : "Json");

        return Ok(new SessionDto(
            session.SessionId,
            session.Questions.Count,
            session.Questions.Select(q => q.Id).ToList(),
            session.SourceMode));
    }

    [HttpGet("sessions/{sessionId}/questions/{index:int}")]
    public ActionResult<QuestionPublicDto> GetQuestion(string sessionId, int index)
    {
        var session = _sessions.Get(sessionId);
        if (session is null) return NotFound();

        if (index < 0 || index >= session.Questions.Count) return BadRequest();

        var q = session.Questions[index];
        var sectionName = session.SourceMode == "Ai"
            ? _examGuide.GetDomainName(q.SectionId)
            : _bank.GetDomainNameForQuestion(q);

        return Ok(new QuestionPublicDto(
            q.Id,
            q.SectionId,
            sectionName,
            q.Title,
            q.Text,
            q.Options,
            index,
            session.Questions.Count));
    }

    [HttpPost("sessions/{sessionId}/questions/{index:int}/answer")]
    public ActionResult<AnswerSubmitDto> SubmitAnswer(
        string sessionId,
        int index,
        [FromBody] AnswerRequest request)
    {
        var session = _sessions.Get(sessionId);
        if (session is null) return NotFound();

        if (index < 0 || index >= session.Questions.Count) return BadRequest();

        var q = session.Questions[index];

        var selected = (request.SelectedAnswer ?? "").Trim().ToUpperInvariant();
        if (selected.Length != 1 || !"ABCD".Contains(selected))
            return BadRequest("SelectedAnswer must be A, B, C, or D.");

        var isCorrect = selected == q.CorrectAnswer.ToUpperInvariant();
        session.Answers[index] = (selected, isCorrect);

        return Ok(new AnswerSubmitDto(index, session.Questions.Count, selected));
    }

    [HttpGet("sessions/{sessionId}/review")]
    public ActionResult<SessionReviewDto> GetReview(string sessionId)
    {
        var session = _sessions.Get(sessionId);
        if (session is null) return NotFound();

        var items = new List<QuestionReviewItem>();
        for (var i = 0; i < session.Questions.Count; i++)
        {
            var q = session.Questions[i];
            if (!session.Answers.TryGetValue(i, out var answer))
                continue;

            var sectionName = session.SourceMode == "Ai"
                ? _examGuide.GetDomainName(q.SectionId)
                : _bank.GetDomainNameForQuestion(q);

            items.Add(new QuestionReviewItem(
                i,
                sectionName,
                q.Title,
                q.Text,
                q.Options,
                answer.Selected,
                q.CorrectAnswer,
                answer.IsCorrect,
                q.Explanation));
        }

        return Ok(new SessionReviewDto(sessionId, items));
    }

    [HttpGet("sessions/{sessionId}/summary")]
    public ActionResult<SessionSummaryDto> GetSummary(string sessionId)
    {
        var session = _sessions.Get(sessionId);
        if (session is null) return NotFound();

        var answered = session.Answers.Count;
        var correct = session.Answers.Values.Count(a => a.IsCorrect);
        var pct = answered == 0 ? 0 : Math.Round(100.0 * correct / answered, 1);

        return Ok(new SessionSummaryDto(sessionId, session.Questions.Count, answered, correct, pct));
    }

}
