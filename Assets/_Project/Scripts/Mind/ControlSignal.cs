using UnityEngine;

namespace LightBender.Mind
{
    /// <summary>Affective register classifying the user's current state.</summary>
    public enum Register
    {
        Grounded,
        Searching,
        Overwhelmed
    }

    /// <summary>
    /// Single data payload flowing from the Mind layer to the Audio/Visual output layer.
    /// </summary>
    [System.Serializable]
    public class ControlSignal
    {
        /// <summary>Which of the three registers the user currently occupies.</summary>
        public Register register = Register.Grounded;

        /// <summary>Affective intensity, 0 (calm) to 1 (peak). Drives arousal-mapped audio/visual params.</summary>
        [Range(0f, 1f)] public float intensity = 0.5f;

        /// <summary>Identifier for the active audio cluster (~40 tracks per cluster, mapped to register).</summary>
        public string musicCluster = "warm-ambient";

        /// <summary>Identifier for the active visual palette feeding shaders and particle gradients.</summary>
        public string visualPalette = "golden";

        /// <summary>Crossfade/lerp pacing, 0 (snap) to 1 (slowest). Higher = gentler transitions.</summary>
        [Range(0f, 1f)] public float transitionSpeed = 0.5f;

        /// <summary>Unity time (seconds since session start) when this signal was emitted.</summary>
        public double timestamp;

        /// <summary>Construct a baseline ControlSignal with grounded defaults and timestamp=now.</summary>
        public static ControlSignal CreateDefault()
        {
            return new ControlSignal
            {
                register = Register.Grounded,
                intensity = 0.5f,
                musicCluster = "warm-ambient",
                visualPalette = "golden",
                transitionSpeed = 0.5f,
                timestamp = Time.timeAsDouble
            };
        }
    }
}
