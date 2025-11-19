using assignment2.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace assignment2.Controllers
{
    [ApiController]
    [Route("api/ai")]
    public class AiController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public AiController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        [Authorize]
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AiRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest("Prompt is required.");

            var client = _httpClientFactory.CreateClient();

            string additionalContext = "";
            var yearMatch = System.Text.RegularExpressions.Regex.Match(request.Prompt, @"\b(1\d{3}|20\d{2})\b");
            if (yearMatch.Success)
            {
                int year = int.Parse(yearMatch.Value);
                var historyData = await GetHistoricalEvents(year);
                additionalContext = $"\n\nHistorical context for {year}: {historyData}";
            }

            var personKeywords = new[] { "who was", "who is", "tell me about", "biography" };
            if (personKeywords.Any(k => request.Prompt.ToLower().Contains(k)))
            {
                var person = ExtractPersonName(request.Prompt);
                if (!string.IsNullOrEmpty(person))
                {
                    var bioData = await GetPersonBiography(person);
                    additionalContext += $"\n\nBiographical info: {bioData}";
                }
            }


            client.BaseAddress = new Uri("https://models.inference.ai.azure.com/");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["GitHub:Token"]);
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2023-10-16");

            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
            new {
                role = "system",
                content =
                    "You are a History Guide AI.\n\n" +
                    "You explain historical events clearly and accurately.\n" +
                    "You can use any provided historical context to enhance your answers."
            },
            new {
                role = "user",
                content = request.Prompt + additionalContext
            }
        }
            };

            var response = await client.PostAsJsonAsync("chat/completions", payload);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Error communicating with AI service.");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return Ok(new { response = content });
        }

        private string ExtractPersonName(string prompt)
        {
            // Simple extraction after common phrases
            var patterns = new[]
            {
        @"who (?:was|is) (.+?)[\?]",
        @"tell me about (.+?)[\?]",
        @"biography of (.+?)[\?]"
    };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    prompt,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            return "";
        }

        private async Task<string> GetPersonBiography(string personName)
        {
            var client = _httpClientFactory.CreateClient();
            try
            {
                var url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(personName)}";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var extract = doc.RootElement.GetProperty("extract").GetString();
                    return extract ?? $"Biographical data for {personName}";
                }
            }
            catch { }

            return $"Biographical information about {personName}.";
        }

        private async Task<string> GetHistoricalEvents(int year)
        {
            var client = _httpClientFactory.CreateClient();

            try
            {
                var url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{year}";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var extract = doc.RootElement.GetProperty("extract").GetString();
                    return extract ?? $"Historical data for year {year}";
                }
            }
            catch
            {
            }
            return year switch
            {
                1776 => "American Declaration of Independence signed. American Revolutionary War ongoing.",
                1945 => "End of World War II. Atomic bombs dropped on Hiroshima and Nagasaki. United Nations founded.",
                1492 => "Christopher Columbus reaches the Americas. Beginning of European colonization.",
                1066 => "Norman Conquest of England. Battle of Hastings fought.",
                _ => $"Major historical events occurred in {year}."
            };
        }
    }
}