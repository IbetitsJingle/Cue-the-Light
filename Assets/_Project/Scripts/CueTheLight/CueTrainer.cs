using System.IO;
using System.Text;
using UnityEngine;
using LightBender.Mind;

namespace LightBender.CueTheLight
{
    /// <summary>
    /// Orchestrates head-to-head training of Expected SARSA, SARSA, and Q-Learning
    /// against a shared CueEnvironment. All three agents replay the same scripted
    /// episode sequences, so reward differences reflect algorithm choice rather than
    /// data luck. Also tracks two non-learning baselines (static rule + random) so
    /// the learning curves have a meaningful floor and reference line.
    /// </summary>
    public class CueTrainer : MonoBehaviour
    {
        [Header("Environment")]
        public CueEnvironment environment;

        [Header("Training")]
        public int episodesPerRun = 1000;
        public int maxStepsPerEpisode = 50;

        [Header("Status")]
        [SerializeField] private bool _trainingComplete = false;
        public bool trainingComplete { get { return _trainingComplete; } private set { _trainingComplete = value; } }

        [Header("Debug")]
        public bool showDebug = true;

        // 3 agents trained in lockstep on identical episode sequences
        private ICueAgent[] agents;
        private string[] agentNames;

        // [agentIndex, episode] cumulative reward per episode
        private float[,] rewardHistory;
        private float[] staticBaselineRewards;
        private float[] randomBaselineRewards;

        private int currentEpisode;
        private bool isTraining;

        // dedicated rngs so agent rng (seeded 42) and trainer rng don't entangle
        private System.Random sequenceRng;
        private System.Random baselineRng;

        private void Start()
        {
            CreateAgents();
            AllocateHistory();
        }

        // build fresh agents + name table; called from Start and StartTraining
        private void CreateAgents()
        {
            agents = new ICueAgent[]
            {
                new CueAgentExpectedSARSA(),
                new CueAgentSARSA(),
                new CueAgentQLearning()
            };
            agentNames = new string[agents.Length];
            for (int i = 0; i < agents.Length; i++) agentNames[i] = agents[i].GetName();
        }

        private void AllocateHistory()
        {
            rewardHistory = new float[3, episodesPerRun];
            staticBaselineRewards = new float[episodesPerRun];
            randomBaselineRewards = new float[episodesPerRun];
            sequenceRng = new System.Random(123);
            baselineRng = new System.Random(7);
            currentEpisode = 0;
        }

        /// <summary>
        /// Reset agents and history, then run TrainAllEpisodes synchronously.
        /// 27x12 Q-tables x 1000 episodes x 50 steps is well under a second on desktop,
        /// so there's no need to spread training across frames.
        /// </summary>
        public void StartTraining()
        {
            if (environment == null)
            {
                Debug.LogError("[CueTrainer] No CueEnvironment assigned.");
                return;
            }

            CreateAgents();
            AllocateHistory();
            trainingComplete = false;
            isTraining = true;
            currentEpisode = 0;

            TrainAllEpisodes();

            isTraining = false;
            trainingComplete = true;
            LogSummary();
        }

        /// <summary>
        /// Core training loop. For each episode: generate one scripted state sequence,
        /// then replay it identically across all three agents (each on a freshly Reset
        /// environment) plus the two baselines. Same data, fair comparison.
        /// </summary>
        private void TrainAllEpisodes()
        {
            for (int ep = 0; ep < episodesPerRun; ep++)
            {
                int[] sequence = GenerateEpisodeSequence(maxStepsPerEpisode);

                // each agent traverses the SAME sequence
                for (int ai = 0; ai < agents.Length; ai++)
                {
                    rewardHistory[ai, ep] = RunAgentOnSequence(agents[ai], sequence);
                }

                staticBaselineRewards[ep] = RunStaticBaselineOnSequence(sequence);
                randomBaselineRewards[ep] = RunRandomBaselineOnSequence(sequence);

                // anneal exploration after each episode
                for (int ai = 0; ai < agents.Length; ai++) agents[ai].DecayEpsilon();

                currentEpisode = ep + 1;
            }
        }

        /// <summary>
        /// Build a 50-step (or whatever maxStepsPerEpisode is) scripted sequence that
        /// mimics a therapeutic session arc: start Grounded, drift to Searching around
        /// step 15, sometimes escalate to Overwhelmed near step 30, then return. Each
        /// emitted state is a full 0-26 index with randomized intensity bin and trend.
        /// </summary>
        private int[] GenerateEpisodeSequence(int steps)
        {
            int[] seq = new int[steps];

            // randomized phase boundaries -> episodes vary, agents still see the same one
            int searchStart = Mathf.Clamp(15 + sequenceRng.Next(-4, 5), 5, steps - 5);
            int peakStart = Mathf.Clamp(30 + sequenceRng.Next(-5, 6), searchStart + 3, steps - 3);
            bool escalates = sequenceRng.NextDouble() < 0.6;          // 60% of sessions hit Overwhelmed
            int returnStart = Mathf.Clamp(peakStart + 5 + sequenceRng.Next(0, 6), peakStart + 1, steps);

            for (int i = 0; i < steps; i++)
            {
                int register;
                if (i < searchStart) register = 0;                    // Grounded
                else if (i < peakStart) register = 1;                 // Searching
                else if (escalates && i < returnStart) register = 2;  // Overwhelmed
                else if (i < returnStart + 5) register = 1;           // climbing back through Searching
                else register = 0;                                    // Grounded again

                // intensity tracks register loosely — high register skews high bin
                int bin;
                double r = sequenceRng.NextDouble();
                if (register == 0) bin = (r < 0.6) ? 0 : (r < 0.9 ? 1 : 2);
                else if (register == 1) bin = (r < 0.3) ? 0 : (r < 0.8 ? 1 : 2);
                else bin = (r < 0.1) ? 0 : (r < 0.4 ? 1 : 2);

                int trend = sequenceRng.Next(3);

                seq[i] = register * 9 + bin * 3 + trend;
            }
            return seq;
        }

        // unpack a state index back into StateInfo so the env can score actions
        private CueEnvironment.StateInfo MakeStateInfo(int idx)
        {
            int reg = idx / 9;
            int rem = idx % 9;
            return new CueEnvironment.StateInfo
            {
                register = reg,
                intensityBin = rem / 3,
                trend = rem % 3,
                stateIndex = idx
            };
        }

        // step a single agent through the sequence, accumulate reward, return total
        private float RunAgentOnSequence(ICueAgent agent, int[] sequence)
        {
            environment.Reset();
            float total = 0f;

            // SARSA needs the next-action chain; for others LastSelectedNextAction is unused
            var sarsa = agent as CueAgentSARSA;
            int currentAction = agent.SelectAction(sequence[0]);

            for (int t = 0; t < sequence.Length - 1; t++)
            {
                int s = sequence[t];
                int sNext = sequence[t + 1];

                var stateInfo = MakeStateInfo(s);
                float r = environment.ComputeReward(currentAction, stateInfo);
                total += r;

                agent.Update(s, currentAction, r, sNext);

                // chain a' for SARSA's on-policy guarantee; otherwise re-sample
                if (sarsa != null) currentAction = sarsa.LastSelectedNextAction;
                else currentAction = agent.SelectAction(sNext);
            }

            // terminal step: score the last (s, a) without a TD update, so its reward still counts
            total += environment.ComputeReward(currentAction, MakeStateInfo(sequence[sequence.Length - 1]));
            return total;
        }

        // static rule: palette matches register, motion=breathing(1) -> action = register*4 + 1
        private float RunStaticBaselineOnSequence(int[] sequence)
        {
            float total = 0f;
            for (int t = 0; t < sequence.Length; t++)
            {
                var info = MakeStateInfo(sequence[t]);
                int action = info.register * 4 + 1;
                total += environment.ComputeReward(action, info);
            }
            return total;
        }

        // pure random action floor
        private float RunRandomBaselineOnSequence(int[] sequence)
        {
            float total = 0f;
            for (int t = 0; t < sequence.Length; t++)
            {
                var info = MakeStateInfo(sequence[t]);
                int action = baselineRng.Next(CueEnvironment.ACTION_COUNT);
                total += environment.ComputeReward(action, info);
            }
            return total;
        }

        /// <summary>Cumulative-reward-per-episode time series for one agent. For charting.</summary>
        public float[] GetRewardHistory(int agentIndex)
        {
            float[] copy = new float[episodesPerRun];
            for (int e = 0; e < episodesPerRun; e++) copy[e] = rewardHistory[agentIndex, e];
            return copy;
        }

        /// <summary>
        /// Dump training results as CSV (one row per episode) so external tools
        /// (Python/pandas, spreadsheets) can plot the curves.
        /// </summary>
        public void ExportCSV(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Episode,ExpectedSARSA,SARSA,QLearning,StaticBaseline,RandomBaseline");
            for (int e = 0; e < episodesPerRun; e++)
            {
                sb.Append(e).Append(',')
                  .Append(rewardHistory[0, e]).Append(',')
                  .Append(rewardHistory[1, e]).Append(',')
                  .Append(rewardHistory[2, e]).Append(',')
                  .Append(staticBaselineRewards[e]).Append(',')
                  .Append(randomBaselineRewards[e]).Append('\n');
            }
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[CueTrainer] CSV exported to {path}");
        }

        /// <summary>Agent with the highest mean reward over the final 100 episodes (or all, if fewer).</summary>
        public ICueAgent GetBestAgent()
        {
            int window = Mathf.Min(100, episodesPerRun);
            int start = episodesPerRun - window;
            int bestIdx = 0;
            float bestAvg = float.NegativeInfinity;
            for (int ai = 0; ai < agents.Length; ai++)
            {
                float sum = 0f;
                for (int e = start; e < episodesPerRun; e++) sum += rewardHistory[ai, e];
                float avg = sum / window;
                if (avg > bestAvg) { bestAvg = avg; bestIdx = ai; }
            }
            return agents[bestIdx];
        }

        /// <summary>Direct accessor by index, e.g. for live policy swapping in the runtime visualizer.</summary>
        public ICueAgent GetAgent(int index) => agents[index];

        // mean of last `window` entries of a 1D series
        private float TailAverage(float[] series, int window)
        {
            int w = Mathf.Min(window, series.Length);
            float sum = 0f;
            for (int e = series.Length - w; e < series.Length; e++) sum += series[e];
            return sum / w;
        }

        // mean of last `window` entries of a row of rewardHistory
        private float TailAverage(int agentIndex, int window)
        {
            int w = Mathf.Min(window, episodesPerRun);
            float sum = 0f;
            for (int e = episodesPerRun - w; e < episodesPerRun; e++) sum += rewardHistory[agentIndex, e];
            return sum / w;
        }

        private void LogSummary()
        {
            int window = Mathf.Min(100, episodesPerRun);
            for (int ai = 0; ai < agents.Length; ai++)
                Debug.Log($"[CueTrainer] {agentNames[ai]} avg(last {window}) = {TailAverage(ai, window):F3}");
            Debug.Log($"[CueTrainer] StaticBaseline avg(last {window}) = {TailAverage(staticBaselineRewards, window):F3}");
            Debug.Log($"[CueTrainer] RandomBaseline avg(last {window}) = {TailAverage(randomBaselineRewards, window):F3}");
            Debug.Log($"[CueTrainer] Winner: {GetBestAgent().GetName()}");
        }

        private void OnGUI()
        {
            if (!showDebug) return;

            GUILayout.BeginArea(new Rect(Screen.width - 380, Screen.height - 350, 370, 340), GUI.skin.box);
            GUILayout.Label("<b>Cue Trainer</b>");

            if (isTraining)
            {
                GUILayout.Label($"Training... {currentEpisode}/{episodesPerRun}");
            }
            else if (trainingComplete)
            {
                GUILayout.Label($"Complete. {episodesPerRun} episodes.");
            }
            else
            {
                GUILayout.Label("Idle.");
            }

            if (!isTraining && GUILayout.Button($"Train All ({episodesPerRun} episodes)"))
            {
                StartTraining();
            }

            if (trainingComplete)
            {
                int window = Mathf.Min(100, episodesPerRun);
                GUILayout.Space(6);
                GUILayout.Label($"Avg over last {window} episodes:");
                for (int ai = 0; ai < agents.Length; ai++)
                    GUILayout.Label($"  {agentNames[ai]}: {TailAverage(ai, window):F3}");
                GUILayout.Label($"  StaticBaseline: {TailAverage(staticBaselineRewards, window):F3}");
                GUILayout.Label($"  RandomBaseline: {TailAverage(randomBaselineRewards, window):F3}");
                GUILayout.Label($"<b>Winner:</b> {GetBestAgent().GetName()}");

                if (GUILayout.Button("Export CSV"))
                {
                    string path = Path.Combine(Application.persistentDataPath, "cue_training_results.csv");
                    ExportCSV(path);
                }
            }

            GUILayout.EndArea();
        }
    }
}
