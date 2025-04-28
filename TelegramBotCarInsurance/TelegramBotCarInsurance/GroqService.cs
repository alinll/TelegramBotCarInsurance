using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace TelegramBotCarInsurance
{
    internal class GroqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GroqService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> GenerateDummyInsurancePolicyAsync(string extractedData)
        {
            var requestBody = new
            {
                model = "llama3-70b-8192",
                messages = new[]
                {
                new { role = "system", content = "You are a professional insurance document generator." },
                new { role = "user", content = $"Generate a formatted dummy insurance policy document based on:\n\n{extractedData}" }
            }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            string destination = "https://api.groq.com/openai/v1/chat/completions";
            var response = await _httpClient.PostAsync(destination, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Exception exception = new($"Groq API error: {error}");
                throw exception;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            var generatedText = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return generatedText ?? "No content generated.";
        }
    }
}
