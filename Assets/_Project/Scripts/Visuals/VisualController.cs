using LightBender.Mind;
using UnityEngine;

namespace LightBender.Visuals
{
    /// <summary>
    /// Drives camera background and main directional light from a <see cref="ControlSignal"/>.
    /// Maps the signal's <c>visualPalette</c> string to a target color + light intensity, then
    /// lerps frame-by-frame at a rate gated by <c>transitionSpeed</c> (higher = gentler).
    /// </summary>
    public class VisualController : MonoBehaviour
    {
        /// <summary>Camera whose background color shifts. Falls back to <see cref="Camera.main"/> in Start.</summary>
        public Camera mainCam;

        /// <summary>Optional directional light. Color + intensity track the palette when assigned.</summary>
        public Light mainLight;

        /// <summary>Base lerp rate (per second). Modulated by signal.transitionSpeed in <see cref="ApplySignal"/>.</summary>
        public float lerpSpeed = 2f;

        /// <summary>Toggle the OnGUI palette/lerp debug overlay.</summary>
        public bool showDebug = false;

        Color targetColor;
        float targetIntensity;
        ControlSignal currentSignal;
        float effectiveLerp;       // lerpSpeed scaled by (1.1 - transitionSpeed)
        bool  loggedMissingCam;    // suppress per-frame error spam

        /// <summary>Active register, or null if no signal has been applied yet.</summary>
        public Register? CurrentRegister => currentSignal?.register;

        /// <summary>Active palette string from the most recent signal, or null if none applied.</summary>
        public string CurrentPalette => currentSignal?.visualPalette;

        void Start()
        {
            // Default look while we wait for the first real signal.
            targetColor     = new Color(1.0f, 0.85f, 0.5f);
            targetIntensity = 1.2f;

            if (mainCam == null) mainCam = Camera.main;
            ApplySignal(ControlSignal.CreateDefault());
        }

        /// <summary>
        /// Adopts <paramref name="signal"/>: caches it, maps its palette to color+intensity targets,
        /// and recomputes the effective lerp rate. Visuals tween toward the new targets in <see cref="Update"/>.
        /// </summary>
        public void ApplySignal(ControlSignal signal)
        {
            if (signal == null) return;
            currentSignal = signal;

            switch (signal.visualPalette)
            {
                case "golden":
                    targetColor = new Color(0.95f, 0.85f, 0.4f);
                    targetIntensity = 1.2f;
                    break;
                case "blue-teal":
                    targetColor = new Color(0.3f, 0.55f, 0.85f);
                    targetIntensity = 0.8f;
                    break;
                case "dark-muted":
                    targetColor = new Color(0.45f, 0.15f, 0.2f);
                    targetIntensity = 0.5f;
                    break;
                default:
                    Debug.LogWarning($"[VisualController] Unknown palette '{signal.visualPalette}'. Defaulting to golden.");
                    targetColor = new Color(0.95f, 0.85f, 0.4f);
                    targetIntensity = 1.2f;
                    break;
            }

            // Higher transitionSpeed = gentler tween. Range maps [0..1] → multiplier [1.1..0.1].
            effectiveLerp = lerpSpeed * (1.1f - Mathf.Clamp01(signal.transitionSpeed));
        }

        void Update()
        {
            if (mainCam == null)
            {
                if (!loggedMissingCam)
                {
                    Debug.LogError("[VisualController] mainCam not assigned and Camera.main missing.");
                    loggedMissingCam = true;
                }
                return;
            }

            float t = effectiveLerp * Time.deltaTime;
            mainCam.backgroundColor = Color.Lerp(mainCam.backgroundColor, targetColor, t);

            if (mainLight != null)
            {
                mainLight.color     = Color.Lerp(mainLight.color, targetColor, t);
                mainLight.intensity = Mathf.Lerp(mainLight.intensity, targetIntensity, t);
            }
        }

        void OnGUI()
        {
            if (!showDebug) return;

            Color cur = mainCam != null ? mainCam.backgroundColor : Color.black;
            // Closeness % = average per-channel agreement between current and target (1.0 = matched).
            float closeness = 1f - (Mathf.Abs(targetColor.r - cur.r)
                                   + Mathf.Abs(targetColor.g - cur.g)
                                   + Mathf.Abs(targetColor.b - cur.b)) / 3f;
            float pct = Mathf.Clamp01(closeness) * 100f;

            GUILayout.BeginArea(new Rect(380, 10, 360, 130), GUI.skin.box);
            GUILayout.Label("VisualController");
            GUILayout.Label($"Palette: {(CurrentPalette ?? "(none)")}");
            GUILayout.Label($"Target RGB: {targetColor.r:F2}, {targetColor.g:F2}, {targetColor.b:F2}");
            GUILayout.Label($"Current RGB: {cur.r:F2}, {cur.g:F2}, {cur.b:F2}");
            GUILayout.Label($"Lerp progress: {pct:F1}%  (rate {effectiveLerp:F2}/s)");
            GUILayout.EndArea();
        }
    }
}
