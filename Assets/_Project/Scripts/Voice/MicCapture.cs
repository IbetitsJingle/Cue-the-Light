using System.Collections.Generic;
using UnityEngine;

namespace LightBender.Voice
{
    /// <summary>
    /// Wraps Unity's <see cref="Microphone"/> API for the transcription pipeline. Holds a rolling
    /// buffer, periodically drains new samples into <see cref="pendingChunks"/> for near-real-time
    /// consumption via <see cref="ConsumePendingAudio"/>, and auto-recovers from device disconnects
    /// by polling <see cref="Microphone.devices"/>.
    /// </summary>
    public class MicCapture : MonoBehaviour
    {
        /// <summary>Mic device name. Empty = system default (auto-resolved to <c>Microphone.devices[0]</c>).</summary>
        public string deviceName = "";

        /// <summary>Sample rate in Hz. SenseVoice ONNX expects 16000.</summary>
        public int sampleRate = 16000;

        /// <summary>Rolling buffer length. Microphone.Start loops past this, overwriting oldest samples.</summary>
        public int recordingLengthSec = 300;

        /// <summary>Interval between chunk drains. Lower = lower latency, higher CPU/GC churn.</summary>
        public float chunkIntervalSec = 5f;

        /// <summary>Toggle the OnGUI debug overlay (top-left).</summary>
        public bool showDebug = false;

        AudioClip activeClip;     // rolling-buffer clip currently being filled by Unity (null when stopped)
        string    activeDevice;   // resolved device name actually passed to Microphone.Start
        int       prevPosition;   // last GetPosition reading; chunks span [prevPosition, currPosition)
        float     chunkTimer;     // sec since last chunk drain
        float     deviceCheckTimer; // sec since last device-presence poll
        bool      needsRecovery;  // true when no devices present — retry every 2s

        readonly List<float[]> pendingChunks = new List<float[]>();

        /// <summary>True while the underlying Unity mic is capturing.</summary>
        public bool isRecording => !string.IsNullOrEmpty(activeDevice) && Microphone.IsRecording(activeDevice);

        void OnEnable()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[MicCapture] No microphones detected on this system.");
                needsRecovery = true; // retry once a device shows up
                return;
            }
            if (!string.IsNullOrEmpty(deviceName) && System.Array.IndexOf(Microphone.devices, deviceName) < 0)
            {
                Debug.LogWarning($"[MicCapture] Configured device '{deviceName}' not found. Skipping auto-start.");
                return;
            }
            StartRecording();
        }

        void OnDisable()
        {
            StopRecording();
        }

        void Update()
        {
            // Retry path: no device — keep checking until one appears.
            if (needsRecovery)
            {
                deviceCheckTimer += Time.deltaTime;
                if (deviceCheckTimer >= 2f)
                {
                    deviceCheckTimer = 0f;
                    AttemptRecovery();
                }
                return;
            }

            if (!isRecording) return;

            chunkTimer += Time.deltaTime;
            if (chunkTimer >= chunkIntervalSec)
            {
                chunkTimer = 0f;
                CaptureChunk();
            }

            deviceCheckTimer += Time.deltaTime;
            if (deviceCheckTimer >= 2f)
            {
                deviceCheckTimer = 0f;
                if (System.Array.IndexOf(Microphone.devices, activeDevice) < 0)
                {
                    Debug.LogWarning($"[MicCapture] Device '{activeDevice}' disappeared. Attempting recovery.");
                    AttemptRecovery();
                }
            }
        }

        /// <summary>Begin capturing into a looping AudioClip. Idempotent — no-op if already recording.</summary>
        public void StartRecording()
        {
            if (isRecording) return;

            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[MicCapture] No microphones available.");
                needsRecovery = true;
                return;
            }

            // Honor a pre-set activeDevice (set by AttemptRecovery); otherwise resolve from deviceName.
            if (string.IsNullOrEmpty(activeDevice))
                activeDevice = string.IsNullOrEmpty(deviceName) ? Microphone.devices[0] : deviceName;

            if (System.Array.IndexOf(Microphone.devices, activeDevice) < 0)
            {
                Debug.LogError($"[MicCapture] Device '{activeDevice}' not present in Microphone.devices.");
                activeDevice = null;
                return;
            }

            activeClip = Microphone.Start(activeDevice, true, recordingLengthSec, sampleRate);
            if (activeClip == null)
            {
                Debug.LogError($"[MicCapture] Microphone.Start returned null for device '{activeDevice}'.");
                activeDevice = null;
                return;
            }

            prevPosition = 0;
            chunkTimer = 0f;
            Debug.Log($"[MicCapture] Recording on '{activeDevice}' @ {sampleRate}Hz, buffer {recordingLengthSec}s, chunk {chunkIntervalSec}s.");
        }

        /// <summary>
        /// Stops capture and returns a newly-allocated AudioClip trimmed to actual recorded length.
        /// Returns null if nothing was recording or no samples were captured. Pending chunks are not flushed —
        /// call <see cref="ConsumePendingAudio"/> separately if needed.
        /// </summary>
        public AudioClip StopRecording()
        {
            if (string.IsNullOrEmpty(activeDevice) || activeClip == null) return null;

            int pos = Microphone.GetPosition(activeDevice);
            Microphone.End(activeDevice);

            AudioClip trimmed = null;
            if (pos > 0)
            {
                int channels = activeClip.channels;
                var samples = new float[pos * channels];
                if (activeClip.GetData(samples, 0))
                {
                    trimmed = AudioClip.Create($"MicCapture_{activeDevice}_{pos}", pos, channels, activeClip.frequency, false);
                    trimmed.SetData(samples, 0);
                }
                else
                {
                    Debug.LogWarning("[MicCapture] AudioClip.GetData failed during stop.");
                }
            }

            activeClip   = null;
            activeDevice = null;
            prevPosition = 0;
            chunkTimer   = 0f;
            return trimmed;
        }

        /// <summary>Returns the live rolling-buffer clip for real-time transcription consumers. Null if stopped.</summary>
        public AudioClip GetCurrentClip() => activeClip;

        /// <summary>All microphone device names visible to Unity. Useful for inspector dropdowns and debug.</summary>
        public string[] GetAvailableDevices() => Microphone.devices;

        /// <summary>
        /// Concatenates and returns all pending chunks in capture order, then clears the queue.
        /// Returns null if no chunks are pending. Caller owns the returned array.
        /// </summary>
        public float[] ConsumePendingAudio()
        {
            if (pendingChunks.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < pendingChunks.Count; i++) total += pendingChunks[i].Length;

            var combined = new float[total];
            int offset = 0;
            for (int i = 0; i < pendingChunks.Count; i++)
            {
                var c = pendingChunks[i];
                System.Array.Copy(c, 0, combined, offset, c.Length);
                offset += c.Length;
            }
            pendingChunks.Clear();
            return combined;
        }

        // Drain new samples [prevPosition, currPosition) into a chunk. Handles loop-buffer wrap.
        void CaptureChunk()
        {
            if (activeClip == null || string.IsNullOrEmpty(activeDevice)) return;

            int currPos = Microphone.GetPosition(activeDevice);
            if (currPos == prevPosition) return; // no new audio

            int channels = activeClip.channels;
            int bufLen   = activeClip.samples; // per channel
            float[] chunk;

            if (currPos > prevPosition)
            {
                int n = currPos - prevPosition;
                chunk = new float[n * channels];
                activeClip.GetData(chunk, prevPosition);
            }
            else
            {
                // Loop wrapped: read tail [prevPosition..bufLen) then head [0..currPos).
                int tail = bufLen - prevPosition;
                int head = currPos;
                var tailBuf = new float[tail * channels];
                var headBuf = new float[head * channels];
                activeClip.GetData(tailBuf, prevPosition);
                activeClip.GetData(headBuf, 0);
                chunk = new float[tailBuf.Length + headBuf.Length];
                System.Array.Copy(tailBuf, 0, chunk, 0, tailBuf.Length);
                System.Array.Copy(headBuf, 0, chunk, tailBuf.Length, headBuf.Length);
            }

            pendingChunks.Add(chunk);
            prevPosition = currPos;
        }

        // Tear down current recording (preserving pendingChunks) and try to start on devices[0].
        // If no device available, raises needsRecovery for the Update retry loop.
        void AttemptRecovery()
        {
            // Salvage audio between last chunk and device loss before tearing down.
            if (isRecording)
            {
                CaptureChunk();
                Microphone.End(activeDevice);
            }
            activeClip   = null;
            activeDevice = null;
            prevPosition = 0;
            chunkTimer   = 0f;

            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[MicCapture] No devices available for recovery. Will retry every 2s.");
                needsRecovery = true;
                return;
            }

            needsRecovery = false;
            activeDevice = Microphone.devices[0]; // pre-set so StartRecording adopts it instead of re-resolving deviceName
            Debug.Log($"[MicCapture] Recovering on fallback device '{activeDevice}'.");
            StartRecording();
        }

        void OnGUI()
        {
            if (!showDebug) return;

            int chunkCount = pendingChunks.Count;
            int chunkSamples = 0;
            for (int i = 0; i < pendingChunks.Count; i++) chunkSamples += pendingChunks[i].Length;

            GUILayout.BeginArea(new Rect(10, 10, 360, 150), GUI.skin.box);
            GUILayout.Label("MicCapture");
            GUILayout.Label($"Device: {(string.IsNullOrEmpty(activeDevice) ? "(none)" : activeDevice)}");
            GUILayout.Label($"Recording: {isRecording}{(needsRecovery ? "  [recovering]" : "")}");
            int pos = isRecording ? Microphone.GetPosition(activeDevice) : 0;
            float seconds = sampleRate > 0 ? pos / (float)sampleRate : 0f;
            GUILayout.Label($"Position: {pos} samples ({seconds:F2}s)");
            GUILayout.Label($"Clip length: {(activeClip != null ? activeClip.samples : 0)} samples");
            GUILayout.Label($"Pending chunks: {chunkCount} ({chunkSamples} samples)");
            GUILayout.EndArea();
        }
    }
}
