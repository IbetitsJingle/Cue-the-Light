namespace LightBender.CueTheLight
{
    /// <summary>
    /// Expected SARSA agent. Instead of bootstrapping from a single sampled next action
    /// (SARSA) or the maximum next-action value (Q-learning), Expected SARSA bootstraps
    /// from the *expected* Q-value of the next state under the current epsilon-greedy
    /// policy. Averaging over the policy distribution removes the variance of single-sample
    /// SARSA while still being on-policy — smoother updates than SARSA, more realistic
    /// (and typically more stable) than Q-learning's optimistic max.
    /// </summary>
    public class CueAgentExpectedSARSA : CueAgentBase
    {
        public CueAgentExpectedSARSA(
            int stateCount = 27,
            int actionCount = 12,
            float learningRate = 0.1f,
            float discountFactor = 0.95f,
            float epsilonStart = 0.3f,
            float epsilonEnd = 0.05f,
            float epsilonDecayRate = 0.995f)
            : base(stateCount, actionCount, learningRate, discountFactor,
                   epsilonStart, epsilonEnd, epsilonDecayRate)
        { }

        /// <summary>
        /// Expected SARSA TD update:
        ///     E[Q(s', ·)] = Σ_a π(a | s') · Q(s', a)
        /// where π is the current epsilon-greedy policy:
        ///     π(greedy)    = (1 - ε) + ε / |A|
        ///     π(non-greedy) =           ε / |A|
        /// Then Q(s, a) ← Q(s, a) + α · (r + γ · E[Q(s', ·)] − Q(s, a)).
        /// The expectation eliminates the sampling noise of vanilla SARSA without
        /// abandoning the on-policy assumption that exploration will keep happening.
        /// </summary>
        public override void Update(int state, int action, float reward, int nextState)
        {
            int greedyNext = GetGreedyAction(nextState);
            float greedyProb = (1f - epsilon) + epsilon / actionCount;
            float nonGreedyProb = epsilon / actionCount;

            float expectedValue = 0f;
            for (int a = 0; a < actionCount; a++)
            {
                float prob = (a == greedyNext) ? greedyProb : nonGreedyProb;
                expectedValue += prob * qTable[nextState, a];
            }

            float tdTarget = reward + discountFactor * expectedValue;
            float tdError = tdTarget - qTable[state, action];
            qTable[state, action] += learningRate * tdError;

            totalSteps++;
        }

        public override string GetName() => "Expected SARSA";
    }
}
