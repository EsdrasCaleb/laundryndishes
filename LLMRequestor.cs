using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Packages.LaundryNDishes
{
    public class LLMRequestor
    {
        private readonly HttpClient _httpClient;
        private bool _isBusy;
        private LnDConfig _config; // Reference to the LnDConfig
        private Action<string> _onResponseReceived;

        public bool IsBusy => _isBusy;

        public LLMRequestor()
        {
            _httpClient = new HttpClient();
            _isBusy = false;
        }

        // Call this to start a request with a provided code path and method name
        public void MakeRequest(string prompt, Action<string> onResponseReceived)
        {
            if (_isBusy)
            {
                Debug.LogWarning("LLM Request is already in progress.");
                return;
            }

            _isBusy = true;
            _onResponseReceived = onResponseReceived;

            // Load the configuration from Project Settings
            LoadConfig();

            // Run the request in a separate thread
            Thread requestThread = new Thread(() =>
            {
                try
                {
                    // Build the prompt to send to the LLM

                    // Start the LLM request asynchronously and wait for it
                    string response = MakeAsyncRequest(prompt).Result;

                    // Handle the response (call the callback action)
                    _onResponseReceived?.Invoke(response);
                }
                catch (Exception ex)
                {
                    Debug.LogError("Error making LLM request: " + ex.Message);
                    _onResponseReceived?.Invoke($"Error: {ex.Message}");
                }
                finally
                {
                    _isBusy = false; // Reset the busy flag
                }
            });

            requestThread.Start();
        }


        // Asynchronously makes the request to the OpenAI completions endpoint
        private async Task<string> MakeAsyncRequest(string prompt)
        {
            try
            {
                // Prepare the request body in the format expected by OpenAI
                var requestBody = new
                {
                    model = _config.llmModel, // Use the model from the config
                    prompt = prompt,
                    temperature = Math.Round(_config.llTemperature,2), // Use the temperature from the config
                    max_tokens = _config.llMaxTokens // Use max tokens from the config
                };

                var jsonRequestBody = JsonUtility.ToJson(requestBody);

                // Prepare the content for the HTTP request
                var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

                // Set up the request headers (including the Bearer token)
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _config.llmApiKey);

                // Send the POST request to the LLM server (this could be OpenAI or your local server)
                HttpResponseMessage response = await _httpClient.PostAsync(_config.llmServer, content);

                if (response.IsSuccessStatusCode)
                {
                    // Read and return the response as a string
                    string responseData = await response.Content.ReadAsStringAsync();
                    return responseData;
                }
                else
                {
                    Debug.LogError("LLM request failed with status: " + response.StatusCode);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in making LLM request: " + ex.Message);
                return string.Empty;
            }
        }

        // Loads the configuration from the Project Settings (LnDConfig)
        private void LoadConfig()
        {
            // Assuming the config asset is stored at this path in Project Settings
            _config = UnityEditor.AssetDatabase.LoadAssetAtPath<LnDConfig>("Assets/Settings/LnDConfig.asset");

            if (_config == null)
            {
                Debug.LogError("Config not found! Please check the LnDConfig asset.");
                // Default fallback values if no config found
                _config = ScriptableObject.CreateInstance<LnDConfig>();
                _config.llmServer = "https://api.openai.com/v1/completions"; // Default OpenAI server
                _config.llmApiKey = "";  // Default API key
                _config.llmModel = "text-davinci-003"; // Default model
                _config.llTemperature = 0.7;
                _config.llMaxTokens = 150;
            }
        }
    }
}
