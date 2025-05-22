using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace Microsoft.ManagedIdentity
{
    public class HttpExample
    {
        private readonly ILogger<HttpExample> _logger;
        private readonly HttpClient _httpClient;
        private const string RocketLaunchesUrl = "https://fdo.rocketlaunch.live/json/launches/next/5";

        public HttpExample(ILogger<HttpExample> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
        }

        // Get API key from environment variables - best practice for Azure
        private ChatClient GetChatClient()
        {
            string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("API key not found in environment variables");
                throw new InvalidOperationException("API key not found in environment variables");
            }

            return new ChatClient(
                model: "gpt-4o",
                apiKey: apiKey
            );
        }

        // Fetch rocket launch data
        private async Task<string> GetRocketLaunchDataAsync()
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(RocketLaunchesUrl);
                response.EnsureSuccessStatusCode(); // Throws if not 200-299
                
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching rocket launch data");
                throw;
            }
        }

        [Function("RocketInfo")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            _logger.LogInformation("RocketInfo function processing a request");
            
            

            try
            {
                // Get the question from query parameters
                string? question = req.Query["question"];

                // Default question if none provided
                if (string.IsNullOrWhiteSpace(question))
                {
                    question = "Tell me about the upcoming rocket launches";
                }

                _logger.LogInformation($"Processing question: {question}");

                // Get rocket launch data
                string launchData = await GetRocketLaunchDataAsync();

                // Construct the prompt with the launch data
                string prompt = $@"You are a helpful assistant with knowledge about rockets and space. 
Here is information about upcoming rocket launches:

{launchData}

Based on this information, please answer the following question, but do not go off topic:
{question}";

                // Get the chat client and send the enhanced prompt
                ChatClient client = GetChatClient();
                ChatCompletion completion = client.CompleteChat(prompt);

                // Return the response
                return new OkObjectResult(new
                {
                    Question = question,
                    Response = completion.Content[0].Text
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request");
                return new ObjectResult(new
                {
                    Error = "Failed to process your question",
                    Details = ex.Message
                })
                {
                    StatusCode = 500
                };
            }
        }
    }
}