using UnityEngine;
using LightBender.Mind;

namespace LightBender.CueTheLight
{
    /// <summary>
    /// MDP environment for Cue the Light. Discretizes ControlSignal into a finite
    /// state space and exposes a proxy reward over palette/motion actions, so a
    /// learning agent can be trained against the live affective signal.
    /// </summary>
    public class CueEnvironment : MonoBehaviour
    {
        /// <summary>Total discrete states: 3 registers x 3 intensity bins x 3 trends.</summary>
        public const int STATE_COUNT = 27;

        /// <summary>Total discrete actions: 3 palettes x 4 motion profiles.</summary>
        public const int ACTION_COUNT = 12;

        /// <summary>Discretized environment state, indexed for table-based RL.</summary>
        public struct StateInfo
        {
            public int register;       // 0=Grounded, 1=Searching, 2=Overwhelmed
            public int intensityBin;   // 0=Low, 1=Medium, 2=High
            public int trend;          // 0=Improving, 1=Stable, 2=Declining
            public int stateIndex;     // 0..26 packed index

            /// <summary>
            /// Build a StateInfo from a ControlSignal. Trend defaults to Stable when no
            /// previous state is given; UpdateState supplies the prior to compute real trend.
            /// </summary>
            public static StateInfo FromControlSignal(ControlSignal signal, StateInfo? previous = null)
            {
                int reg = (int)signal.register;
                int bin = BinIntensity(signal.intensity);
                int trend = 1; // default Stable

                if (previous.HasValue)
                {
                    var prev = previous.Value;
                    // register shift dominates trend; fall back to intensity delta when unchanged
                    if (reg < prev.register) trend = 0;        // moving toward Grounded -> Improving
                    else if (reg > prev.register) trend = 2;   // moving toward Overwhelmed -> Declining
                    else if (bin < prev.intensityBin) trend = 0;
                    else if (bin > prev.intensityBin) trend = 2;
                    else trend = 1;
                }

                return new StateInfo
                {
                    register = reg,
                    intensityBin = bin,
                    trend = trend,
                    stateIndex = reg * 9 + bin * 3 + trend
                };
            }

            // 0-0.33 Low, 0.34-0.66 Medium, 0.67-1.0 High
            private static int BinIntensity(float intensity)
            {
                if (intensity < 0.34f) return 0;
                if (intensity < 0.67f) return 1;
                return 2;
            }
        }

        /// <summary>Decomposed action: which palette + which motion profile.</summary>
        public struct ActionInfo
        {
            public int palette;        // 0=golden, 1=blue-teal, 2=dark-muted
            public int motionProfile;  // 0=still, 1=breathing, 2=flowing, 3=pulsing
            public int actionIndex;    // 0..11 packed index

            /// <summary>Decompose a packed action index back into its palette and motion components.</summary>
            public static ActionInfo FromIndex(int index)
            {
                return new ActionInfo
                {
                    palette = index / 4,
                    motionProfile = index % 4,
                    actionIndex = index
                };
            }

            /// <summary>Human-readable palette label for logging and shader binding.</summary>
            public string GetPaletteName()
            {
                switch (palette)
                {
                    case 0: return "golden";
                    case 1: return "blue-teal";
                    case 2: return "dark-muted";
                    default: return "unknown";
                }
            }

            /// <summary>Human-readable motion profile label for logging and animator routing.</summary>
            public string GetMotionName()
            {
                switch (motionProfile)
                {
                    case 0: return "still";
                    case 1: return "breathing";
                    case 2: return "flowing";
                    case 3: return "pulsing";
                    default: return "unknown";
                }
            }
        }

        // tracked across UpdateState calls so trend reflects real motion through the state space
        private StateInfo previousState;
        private StateInfo currentState;

        private void Start()
        {
            Reset();
        }

        /// <summary>Most recent StateInfo computed by UpdateState (or default after Reset).</summary>
        public StateInfo GetCurrentState() => currentState;

        /// <summary>
        /// Ingest a fresh ControlSignal: derive new StateInfo (trend computed against previous),
        /// shift current -> previous, and store the new one as current.
        /// </summary>
        public void UpdateState(ControlSignal signal)
        {
            StateInfo next = StateInfo.FromControlSignal(signal, currentState);
            previousState = currentState;
            currentState = next;
        }

        /// <summary>
        /// Proxy reward for (action, state). Encodes the design heuristic that palette
        /// should match register and motion should match intensity; full miss is penalized.
        /// </summary>
        public float ComputeReward(int actionIndex, StateInfo state)
        {
            ActionInfo action = ActionInfo.FromIndex(actionIndex);

            // palette<->register: golden=Grounded, blue-teal=Searching, dark-muted=Overwhelmed
            bool paletteMatch = action.palette == state.register;

            // motion<->intensity: still=Low, breathing=Medium, flowing/pulsing=High
            bool motionMatch =
                (state.intensityBin == 0 && action.motionProfile == 0) ||
                (state.intensityBin == 1 && action.motionProfile == 1) ||
                (state.intensityBin == 2 && (action.motionProfile == 2 || action.motionProfile == 3));

            if (!paletteMatch && !motionMatch) return -0.5f; // total mismatch penalty

            float reward = 0f;
            if (paletteMatch) reward += 1.0f;
            if (motionMatch) reward += 0.5f;
            return reward;
        }

        /// <summary>Reset to the canonical starting cell: Grounded / Low / Stable (index 1).</summary>
        public void Reset()
        {
            currentState = new StateInfo
            {
                register = 0,
                intensityBin = 0,
                trend = 1,
                stateIndex = 0 * 9 + 0 * 3 + 1
            };
            previousState = currentState;
        }
    }
}
