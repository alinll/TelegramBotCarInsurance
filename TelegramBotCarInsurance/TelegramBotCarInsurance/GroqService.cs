﻿using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace TelegramBotCarInsurance
{
    internal class GroqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _destination;

        public GroqService(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _destination = "https://api.groq.com/openai/v1/chat/completions";
        }

        public async Task<string> GetGroqResponseAsync(string userMessage)
        {
            try
            {
                var requestBody = new
                {
                    model = "llama3-8b-8192",
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_destination, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Exception exception = new($"Groq API error: {responseString}");
                    throw exception;
                }

                using var jsonDoc = JsonDocument.Parse(responseString);
                var completion = jsonDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (completion is null)
                {
                    return "Sorry, I don't understand you. Please, answer me again.";
                }

                return completion;
            }
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }

        public async Task<string> GenerateDummyInsurancePolicyAsync(string extractedData)
        {
            try
            {
                var requestBody = new
                {
                    model = "llama3-70b-8192",
                    messages = new[]
                    {
                        new 
                        { 
                            role = "system", 
                            content = "You are a professional insurance document generator." 
                        },
                        new 
                        { 
                            role = "user", 
                            content = $"Generate a formatted dummy insurance policy document based on:\n\n{extractedData}" 
                        }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_destination, content);

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
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }
    }
}
