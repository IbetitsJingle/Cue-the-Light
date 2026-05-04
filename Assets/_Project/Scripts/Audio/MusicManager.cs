using LightBender.Mind;
using UnityEngine;

namespace LightBender.Audio
{
    /// <summary>
    /// Holds the track library organized by affective cluster and routes <see cref="ControlSignal"/>
    /// updates into <see cref="CrossfadeEngine"/> calls. Each cluster is an inspector-assigned array
    /// of clips; cluster changes pick a random non-repeating track and crossfade to it.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        /// <summary>Crossfade driver. Receives the chosen clip + transition speed.</summary>
        public CrossfadeEngine crossfadeEngine;

        /// <summary>Tracks for the "warm-ambient" cluster (Grounded register).</summary>
        public AudioClip[] warmAmbientTracks;

        /// <summary>Tracks for the "cool-structured" cluster (Searching register).</summary>
        public AudioClip[] coolStructuredTracks;

        /// <summary>Tracks for the "minimal-sparse" cluster (Overwhelmed register).</summary>
        public AudioClip[] minimalSparseTracks;

        string currentCluster = "";
        int    lastTrackIndex = -1;

        /// <summary>Cluster identifier currently playing. Empty before first track is set.</summary>
        public string CurrentCluster => currentCluster;

        void Start()
        {
            // Boot directly into the first warm-ambient track at full volume — no fade-in on session open.
            if (warmAmbientTracks != null && warmAmbientTracks.Length > 0 && crossfadeEngine != null)
            {
                AudioSource src = crossfadeEngine.ActiveSource;
                if (src != null)
                {
                    src.clip = warmAmbientTracks[0];
                    src.volume = 1f;
                    src.Play();
                    lastTrackIndex = 0;
                    currentCluster = "warm-ambient";
                }
            }
            // ApplySignal with default cluster is a no-op now (cluster matches), but keeps state consistent
            // if the warm-ambient array was empty above.
            ApplySignal(ControlSignal.CreateDefault());
        }

        /// <summary>
        /// React to a new <see cref="ControlSignal"/>. If the cluster changed, picks a random track
        /// from the matching array (avoiding the most recent index) and triggers a crossfade.
        /// No-op if the cluster matches the current one.
        /// </summary>
        public void ApplySignal(ControlSignal signal)
        {
            if (signal == null) return;
            if (signal.musicCluster == currentCluster) return;

            AudioClip[] tracks = SelectCluster(signal.musicCluster);
            if (tracks == null || tracks.Length == 0)
            {
                Debug.LogWarning($"[MusicManager] No tracks loaded for cluster '{signal.musicCluster}'. Skipping.");
                return;
            }

            if (crossfadeEngine == null)
            {
                Debug.LogWarning("[MusicManager] crossfadeEngine not assigned — cannot start track.");
                return;
            }

            int idx = PickIndexAvoiding(tracks.Length, lastTrackIndex);
            crossfadeEngine.CrossfadeTo(tracks[idx], signal.transitionSpeed);

            currentCluster = signal.musicCluster;
            lastTrackIndex = idx;
        }

        AudioClip[] SelectCluster(string cluster)
        {
            switch (cluster)
            {
                case "warm-ambient":    return warmAmbientTracks;
                case "cool-structured": return coolStructuredTracks;
                case "minimal-sparse":  return minimalSparseTracks;
                default:
                    Debug.LogWarning($"[MusicManager] Unknown cluster '{cluster}'.");
                    return null;
            }
        }

        // Random pick that prefers not to repeat the last index. Caps retries to avoid pathological loops
        // when len is small — with len==2 we still escape after one retry.
        int PickIndexAvoiding(int len, int avoid)
        {
            if (len <= 1) return 0;
            int idx;
            int attempts = 0;
            do
            {
                idx = Random.Range(0, len);
                attempts++;
            } while (idx == avoid && attempts < 8);
            return idx;
        }
    }
}
