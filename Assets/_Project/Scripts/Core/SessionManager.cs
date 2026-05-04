using LightBender.Audio;
using LightBender.Mind;
using LightBender.Visuals;
using LightBender.Voice;
using UnityEngine;

namespace LightBender.Core
{
    /// <summary>
    /// Top-level scene orchestrator. Wires <see cref="MicCapture"/> → <see cref="OllamaClient"/> →
    /// <see cref="VisualController"/>. Lives on a single GameObject in the scene; one instance per session.
    /// Glue layer only — no per-frame audio/visual logic lives here.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        /// <summary>Mic capture component. Drives the live audio source.</summary>
        public MicCapture micCapture;

        /// <summary>LLM classifier. Translates transcripts into ControlSignals.</summary>
        public OllamaClient ollamaClient;

        /// <summary>Visual driver. Receives ControlSignals and tweens scene visuals.</summary>
        public VisualController visualController;

        /// <summary>Music driver. Receives ControlSignals and crossfades between cluster tracks.</summary>
        public MusicManager musicManager;

        /// <summary>When true, debug text-field replaces mic-driven auto-classify. Useful before SenseVoice lands.</summary>
        public bool useDebugInput = true;

        /// <summary>Default transcript shown in the debug text area.</summary>
        public string debugTranscript = "I feel calm and at peace today";

        /// <summary>Seconds between auto-classify ticks when running on mic input.</summary>
        public float autoClassifyIntervalSec = 10f;

        bool          isProcessing;        // gate to prevent overlapping Ollama calls
        float         autoClassifyTimer;   // sec since last auto-classify
        ControlSignal lastSignal;          // most recent classifier output, for OnGUI display
        Vector2       debugScroll;         // scroll state for the debug transcript area

        void Start()
        {
            if (micCapture == null)        Debug.LogError("[SessionManager] micCapture not assigned.");
            if (ollamaClient == null)      Debug.LogError("[SessionManager] ollamaClient not assigned.");
            if (visualController == null)  Debug.LogError("[SessionManager] visualController not assigned.");
            if (musicManager == null)      Debug.LogError("[SessionManager] musicManager not assigned.");

            // In live mode, make sure the mic is up. StartRecording is idempotent.
            if (!useDebugInput && micCapture != null) micCapture.StartRecording();
        }

        void Update()
        {
            if (useDebugInput) return; // manual submission via OnGUI button — no auto tick
            if (micCapture == null || !micCapture.isRecording) return;

            autoClassifyTimer += Time.deltaTime;
            if (autoClassifyTimer >= autoClassifyIntervalSec && !isProcessing)
            {
                autoClassifyTimer = 0f;
                // TODO: replace placeholder with real transcript from TranscriptionManager once SenseVoice lands.
                SendToClassifier("[mic audio — transcription pending]");
            }
        }

        /// <summary>
        /// Hands <paramref name="transcript"/> to the LLM and routes the resulting signal to the visual layer.
        /// Guarded by <c>isProcessing</c> — overlapping calls are dropped at the call site (Update gates it,
        /// debug button can spam-click but the gate still holds).
        /// </summary>
        void SendToClassifier(string transcript)
        {
            if (ollamaClient == null)
            {
                Debug.LogWarning("[SessionManager] ollamaClient missing — dropping classify request.");
                return;
            }
            if (isProcessing) return;

            isProcessing = true;
            ollamaClient.Classify(transcript, signal =>
            {
                lastSignal = signal;
                if (visualController != null) visualController.ApplySignal(signal);
                if (musicManager != null) musicManager.ApplySignal(signal);
                Debug.Log($"[SessionManager] Signal: register={signal.register}, intensity={signal.intensity:F2}, " +
                          $"palette={signal.visualPalette}, transitionSpeed={signal.transitionSpeed:F2}");
                isProcessing = false;
            });
        }

        void OnGUI()
        {
            // Pipeline status panel (always shown). Sits below MicCapture's debug box.
            GUILayout.BeginArea(new Rect(10, 170, 360, 110), GUI.skin.box);
            GUILayout.Label("SessionManager");
            bool rec = micCapture != null && micCapture.isRecording;
            GUILayout.Label($"Recording: {rec}   Processing: {isProcessing}");
            string reg = visualController != null && visualController.CurrentRegister.HasValue
                ? visualController.CurrentRegister.Value.ToString() : "(none)";
            string pal = visualController != null ? (visualController.CurrentPalette ?? "(none)") : "(none)";
            GUILayout.Label($"Register: {reg}");
            GUILayout.Label($"Palette: {pal}");
            GUILayout.EndArea();

            if (!useDebugInput) return;

            // Debug input panel: text area + classify button + last signal readout.
            GUILayout.BeginArea(new Rect(10, 290, 360, 220), GUI.skin.box);
            GUILayout.Label("Debug Input");
            debugScroll = GUILayout.BeginScrollView(debugScroll, GUILayout.Width(340), GUILayout.Height(60));
            debugTranscript = GUILayout.TextArea(debugTranscript ?? string.Empty, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUI.enabled = !isProcessing;
            if (GUILayout.Button(isProcessing ? "Classifying…" : "Classify"))
            {
                SendToClassifier(debugTranscript);
            }
            GUI.enabled = true;

            GUILayout.Space(4);
            GUILayout.Label("Last result:");
            if (lastSignal == null)
            {
                GUILayout.Label("  (none yet)");
            }
            else
            {
                GUILayout.Label($"  register: {lastSignal.register}");
                GUILayout.Label($"  intensity: {lastSignal.intensity:F2}");
                GUILayout.Label($"  palette: {lastSignal.visualPalette}");
                GUILayout.Label($"  transitionSpeed: {lastSignal.transitionSpeed:F2}");
            }
            GUILayout.EndArea();
        }
    }
}
