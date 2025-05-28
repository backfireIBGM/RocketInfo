using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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

            // Get rocket launch data
            string launchData = await GetRocketLaunchDataAsync();

            string curDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            _logger.LogInformation($"Current UTC date and time: {curDate}");

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


                // Construct the prompt with the launch data
string prompt = $@"You are a helpful assistant with knowledge about rockets and space.
You have access to up-to-date information about upcoming rocket launches that the user does not directly see.

The current UTC date and time is: {curDate}

Here is the JSON data about the next 5 rocket launches:
{launchData}

When presenting launch information:
- If a launch name is ""TBD"" or similar placeholder text, present it as ""Unnamed Mission"" or describe it by its rocket/provider instead of using the placeholder
- For unnamed launches, you can refer to them as ""[Rocket Name] Mission"" or ""Unnamed [Provider] Launch""
- Always include all available mission details even for unnamed launches

Based strictly on this information, answer the following question. Only use details found in the data. 
If the question cannot be answered using this data, respond that you can only answer questions about the upcoming launches you know about.

Question:
{question}";

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