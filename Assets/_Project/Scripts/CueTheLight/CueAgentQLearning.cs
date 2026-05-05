namespace LightBender.CueTheLight
{
    /// <summary>
    /// Off-policy Q-learning agent. The TD target bootstraps from max_a Q(s', a),
    /// the value of acting greedily at the next state, regardless of which action
    /// the behavior policy will actually pick. This makes Q-learning fast to
    /// converge toward the optimal policy and decoupled from exploration noise,
    /// at the cost of optimism: the update assumes the agent will play optimally
    /// even while it's still exploring, which can over-estimate values and produce
    /// less stable learning curves than Expected SARSA.
    /// </summary>
    public class CueAgentQLearning : CueAgentBase
    {
        public CueAgentQLearning(
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
        /// Q-learning TD update:
        ///     Q(s, a) ← Q(s, a) + α · (r + γ · max_a' Q(s', a') − Q(s, a))
        /// Off-policy: the target depends only on the greedy value of the next
        /// state, not on what the exploration policy will actually do. Compared
        /// with Expected SARSA, the max operator replaces the policy-weighted
        /// expectation — same shape of update, more aggressive bootstrapping.
        /// </summary>
        public override void Update(int state, int action, float reward, int nextState)
        {
            float maxNextQ = GetMaxQ(nextState);

            float tdTarget = reward + discountFactor * maxNextQ;
            float tdError = tdTarget - qTable[state, action];
            qTable[state, action] += learningRate * tdError;

            totalSteps++;
        }

        public override string GetName() => "Q-Learning";
    }
}
