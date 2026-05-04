using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LightBender.Mind
{
    /// <summary>
    /// Classifies user transcripts into <see cref="ControlSignal"/> payloads via a local Ollama server.
    /// Talks to /api/chat with a system prompt instructing strict-JSON output.
    /// </summary>
    public class OllamaClient : MonoBehaviour
    {
        /// <summary>Ollama model tag — must already be pulled locally (`ollama pull qwen2.5:3b`).</summary>
        public string modelName = "qwen2.5:3b";

        /// <summary>Ollama chat endpoint. Localhost-only by default; no auth assumed.</summary>
        public string endpoint = "http://localhost:11434/api/chat";

        // System prompt is fixed: any drift here changes the entire affective vocabulary downstream.
        const string SystemPrompt =
            "You are a therapeutic environment controller. Given the user's spoken input, " +
            "classify their emotional register as exactly one of: Grounded, Searching, Overwhelmed. " +
            "Also assign intensity (0.0-1.0), musicCluster (warm-ambient, cool-structured, minimal-sparse), " +
            "visualPalette (golden, blue-teal, dark-muted), and transitionSpeed (0.0-1.0 where higher=gentler). " +
            "Respond ONLY with a JSON object, no other text. " +
            "Example: {\"register\":\"Grounded\",\"intensity\":0.4,\"musicCluster\":\"warm-ambient\",\"visualPalette\":\"golden\",\"transitionSpeed\":0.6}";

        /// <summary>
        /// Send a transcript to Ollama and invoke <paramref name="onResult"/> with the parsed signal.
        /// On any failure (connection refused, malformed JSON, unknown enum) returns <see cref="ControlSignal.CreateDefault"/>.
        /// </summary>
        public void Classify(string transcript, System.Action<ControlSignal> onResult)
        {
            StartCoroutine(ClassifyRoutine(transcript, onResult));
        }

        IEnumerator ClassifyRoutine(string transcript, System.Action<ControlSignal> onResult)
        {
            // Build request body via JsonUtility-friendly DTOs (anonymous types won't serialize).
            var payload = new ChatRequest
            {
                model = modelName,
                stream = false, // single-shot response — no NDJSON streaming
                messages = new[]
                {
                    new ChatMessage { role = "system", content = SystemPrompt },
                    new ChatMessage { role = "user",   content = transcript ?? string.Empty }
                }
            };

            string body = JsonUtility.ToJson(payload);

            using (var req = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                // ConnectionError covers Ollama-not-running; ProtocolError covers 4xx/5xx.
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[OllamaClient] Request failed ({req.result}): {req.error}. Falling back to default ControlSignal.");
                    onResult?.Invoke(ControlSignal.CreateDefault());
                    yield break;
                }

                onResult?.Invoke(ParseResponse(req.downloadHandler.text));
            }
        }

        ControlSignal ParseResponse(string responseText)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<ChatResponse>(responseText);
                string content = wrapper?.message?.content;
                if (string.IsNullOrEmpty(content))
                {
                    Debug.LogWarning("[OllamaClient] Empty content in Ollama response. Using default.");
                    return ControlSignal.CreateDefault();
                }

                // LLMs occasionally wrap JSON in prose or code fences — slice to the outermost object.
                int first = content.IndexOf('{');
                int last  = content.LastIndexOf('}');
                if (first < 0 || last <= first)
                {
                    Debug.LogWarning($"[OllamaClient] No JSON object found in LLM output: {content}");
                    return ControlSignal.CreateDefault();
                }
                string json = content.Substring(first, last - first + 1);

                // JsonUtility can't deserialize string-valued enums, so go through a DTO and convert manually.
                var dto = JsonUtility.FromJson<ControlSignalDto>(json);
                if (dto == null)
                {
                    Debug.LogWarning("[OllamaClient] Failed to parse ControlSignal JSON. Using default.");
                    return ControlSignal.CreateDefault();
                }

                if (!System.Enum.TryParse(dto.register, true, out Register reg))
                {
                    Debug.LogWarning($"[OllamaClient] Unknown register '{dto.register}', defaulting to Grounded.");
                    reg = Register.Grounded;
                }

                return new ControlSignal
                {
                    register        = reg,
                    intensity       = Mathf.Clamp01(dto.intensity),
                    musicCluster    = string.IsNullOrEmpty(dto.musicCluster)  ? "warm-ambient" : dto.musicCluster,
                    visualPalette   = string.IsNullOrEmpty(dto.visualPalette) ? "golden"       : dto.visualPalette,
                    transitionSpeed = Mathf.Clamp01(dto.transitionSpeed),
                    timestamp       = Time.timeAsDouble
                };
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[OllamaClient] Parse exception: {e.Message}. Using default.");
                return ControlSignal.CreateDefault();
            }
        }

        // --- Serializable DTOs for JsonUtility ---

        [System.Serializable]
        private class ChatRequest
        {
            public string model;
            public ChatMessage[] messages;
            public bool stream;
        }

        [System.Serializable]
        private class ChatMessage
        {
            public string role;
            public string content;
        }

        /// <summary>Subset of Ollama's /api/chat non-streamed response — only the fields we read.</summary>
        [System.Serializable]
        private class ChatResponse
        {
            public ChatMessage message;
        }

        /// <summary>String-typed mirror of <see cref="ControlSignal"/> for JsonUtility compatibility (enum-as-string).</summary>
        [System.Serializable]
        private class ControlSignalDto
        {
            public string register;
            public float  intensity;
            public string musicCluster;
            public string visualPalette;
            public float  transitionSpeed;
        }
    }
}
