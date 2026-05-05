using System;

namespace LightBender.CueTheLight
{
    /// <summary>
    /// Shared scaffolding for the three tabular agents: Q-table storage, epsilon-greedy
    /// action selection, epsilon decay, and bookkeeping. Subclasses only override Update
    /// (the TD target) and GetName, so the only difference between agents is the math
    /// that defines them — everything else is identical and directly comparable.
    /// </summary>
    public abstract class CueAgentBase : ICueAgent
    {
        protected readonly int stateCount;
        protected readonly int actionCount;
        protected readonly float learningRate;     // alpha
        protected readonly float discountFactor;   // gamma
        protected readonly float epsilonStart;
        protected readonly float epsilonEnd;
        protected readonly float epsilonDecayRate;

        protected float[,] qTable;
        protected float epsilon;
        protected int totalSteps;
        protected Random rng;

        protected CueAgentBase(
            int stateCount = 27,
            int actionCount = 12,
            float learningRate = 0.1f,
            float discountFactor = 0.95f,
            float epsilonStart = 0.3f,
            float epsilonEnd = 0.05f,
            float epsilonDecayRate = 0.995f)
        {
            this.stateCount = stateCount;
            this.actionCount = actionCount;
            this.learningRate = learningRate;
            this.discountFactor = discountFactor;
            this.epsilonStart = epsilonStart;
            this.epsilonEnd = epsilonEnd;
            this.epsilonDecayRate = epsilonDecayRate;

            qTable = new float[stateCount, actionCount];
            epsilon = epsilonStart;
            totalSteps = 0;
            rng = new Random(42); // fixed seed -> reproducible runs across agents
        }

        /// <summary>Epsilon-greedy: with probability epsilon explore uniformly, otherwise act greedy with random tie-break.</summary>
        public int SelectAction(int stateIndex)
        {
            if (rng.NextDouble() < epsilon)
                return rng.Next(actionCount);
            return GetGreedyAction(stateIndex);
        }

        /// <summary>Multiplicative decay clamped to epsilonEnd. Subclass-agnostic.</summary>
        public void DecayEpsilon()
        {
            epsilon = Math.Max(epsilonEnd, epsilon * epsilonDecayRate);
        }

        public float GetQValue(int state, int action) => qTable[state, action];

        public float GetMaxQ(int state)
        {
            float best = qTable[state, 0];
            for (int a = 1; a < actionCount; a++)
                if (qTable[state, a] > best) best = qTable[state, a];
            return best;
        }

        /// <summary>Argmax over actions with uniform random tie-break, so ties don't bias toward action 0.</summary>
        public int GetGreedyAction(int state)
        {
            float best = qTable[state, 0];
            int tieCount = 1;
            int chosen = 0;
            for (int a = 1; a < actionCount; a++)
            {
                float q = qTable[state, a];
                if (q > best)
                {
                    best = q;
                    tieCount = 1;
                    chosen = a;
                }
                else if (q == best)
                {
                    tieCount++;
                    // reservoir-style replacement: keeps the tie selection uniform
                    if (rng.Next(tieCount) == 0) chosen = a;
                }
            }
            return chosen;
        }

        public float GetEpsilon() => epsilon;
        public int GetTotalSteps() => totalSteps;
        public float[,] GetQTable() => qTable;

        /// <summary>Restore initial conditions: zeroed Q-table, epsilon=start, step counter=0, rng re-seeded.</summary>
        public void Reset()
        {
            for (int s = 0; s < stateCount; s++)
                for (int a = 0; a < actionCount; a++)
                    qTable[s, a] = 0f;
            epsilon = epsilonStart;
            totalSteps = 0;
            rng = new Random(42);
        }

        public abstract void Update(int state, int action, float reward, int nextState);
        public abstract string GetName();
    }
}
