using UnityEngine;

namespace LightBender.Audio
{
    /// <summary>
    /// Two-source crossfader. One source holds the live track, the other is silent and ready;
    /// <see cref="CrossfadeTo"/> swaps roles by lerping their volumes over <see cref="fadeDuration"/>
    /// (scaled by the signal's transitionSpeed).
    /// </summary>
    public class CrossfadeEngine : MonoBehaviour
    {
        /// <summary>First AudioSource. Live by default.</summary>
        public AudioSource sourceA;

        /// <summary>Second AudioSource. Silent by default.</summary>
        public AudioSource sourceB;

        /// <summary>Base full-crossfade duration in seconds. Modulated by transitionSpeed at call time.</summary>
        public float fadeDuration = 3f;

        bool  aIsActive = true;   // which source is currently the outgoing/live one
        float fadeProgress;       // 0..1 across the active fade
        float effectiveDuration;  // resolved duration for the in-flight fade
        bool  isFading;

        /// <summary>The source currently designated active (outgoing during a fade, sole player otherwise).</summary>
        public AudioSource ActiveSource => aIsActive ? sourceA : sourceB;

        /// <summary>True while a crossfade is in progress.</summary>
        public bool IsFading => isFading;

        /// <summary>
        /// Begin a crossfade onto <paramref name="clip"/>. If a fade is already running it is force-completed
        /// first so the new fade starts from a clean state. Effective duration =
        /// fadeDuration * (0.5 + transitionSpeed) — higher transitionSpeed = longer/gentler fade.
        /// </summary>
        public void CrossfadeTo(AudioClip clip, float transitionSpeed)
        {
            if (clip == null) return;
            if (sourceA == null || sourceB == null)
            {
                Debug.LogError("[CrossfadeEngine] sourceA/sourceB not assigned.");
                return;
            }

            if (isFading) ForceCompleteCurrent();

            effectiveDuration = Mathf.Max(0.01f, fadeDuration * (0.5f + Mathf.Clamp01(transitionSpeed)));

            AudioSource active   = aIsActive ? sourceA : sourceB;
            AudioSource inactive = aIsActive ? sourceB : sourceA;

            inactive.clip = clip;
            inactive.volume = 0f;
            inactive.Play();
            // active source keeps its current clip + volume — Update lerps it down.

            fadeProgress = 0f;
            isFading = true;
        }

        /// <summary>Stops both sources and resets fade state. Volumes set to 0.</summary>
        public void StopAll()
        {
            if (sourceA != null) { sourceA.Stop(); sourceA.volume = 0f; }
            if (sourceB != null) { sourceB.Stop(); sourceB.volume = 0f; }
            isFading = false;
            fadeProgress = 0f;
        }

        void Update()
        {
            if (!isFading) return;

            fadeProgress += Time.deltaTime / effectiveDuration;

            AudioSource active   = aIsActive ? sourceA : sourceB;
            AudioSource inactive = aIsActive ? sourceB : sourceA;

            if (fadeProgress >= 1f)
            {
                // Snap-and-swap: outgoing fully off, incoming fully on, role flip.
                active.volume = 0f;
                active.Stop();
                inactive.volume = 1f;
                aIsActive = !aIsActive;
                isFading = false;
                fadeProgress = 0f;
                return;
            }

            active.volume   = 1f - fadeProgress;
            inactive.volume = fadeProgress;
        }

        // Snap to end-of-fade state without waiting. Used when a new CrossfadeTo arrives mid-fade.
        void ForceCompleteCurrent()
        {
            AudioSource active   = aIsActive ? sourceA : sourceB;
            AudioSource inactive = aIsActive ? sourceB : sourceA;
            active.volume = 0f;
            active.Stop();
            inactive.volume = 1f;
            aIsActive = !aIsActive;
            isFading = false;
            fadeProgress = 0f;
        }
    }
}
