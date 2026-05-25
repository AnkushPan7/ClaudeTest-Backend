using System.Net;
using System.Text.RegularExpressions;
using ClaudeCertPractice.Api.Configuration;
using Microsoft.Extensions.Options;

namespace ClaudeCertPractice.Api.Services;

public class LearningContentService
{
    private readonly HttpClient _http;
    private readonly QuizSettings _settings;

    public LearningContentService(HttpClient http, IOptions<QuizSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeCertPractice/1.0");
        _http.Timeout = TimeSpan.FromSeconds(45);
    }

    public async Task<string> FetchTextAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("LearningUrl must be a valid http(s) URL.");

        using var response = await _http.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Could not fetch learning URL ({(int)response.StatusCode} {response.ReasonPhrase}).");

        var html = await response.Content.ReadAsStringAsync(ct);
        var text = StripHtml(html);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Learning URL returned no readable text.");

        if (text.Length > _settings.MaxSourceCharacters)
            text = text[.._settings.MaxSourceCharacters];

        return text;
    }

    private static string StripHtml(string html)
    {
        html = Regex.Replace(html, @"<script[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", " ");
        html = WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"\s+", " ");
        return html.Trim();
    }
}
