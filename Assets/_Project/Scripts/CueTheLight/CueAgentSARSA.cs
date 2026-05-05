namespace LightBender.CueTheLight
{
    /// <summary>
    /// Vanilla on-policy SARSA agent. The TD target uses Q(s', a') for a single
    /// next action a' actually sampled from the current epsilon-greedy policy.
    /// This is the most "honest" of the three — it learns the value of the policy
    /// it really follows, including its exploration — but it pays for that with
    /// variance, since each update hinges on one stochastic sample of a'.
    /// </summary>
    public class CueAgentSARSA : CueAgentBase
    {
        /// <summary>
        /// The next action sampled inside Update. Exposed so the trainer can chain
        /// it as the executed action of the next step (a' becomes the next a),
        /// which is what makes SARSA truly on-policy across a trajectory.
        /// </summary>
        public int LastSelectedNextAction { get; private set; }

        public CueAgentSARSA(
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
        /// SARSA TD update:
        ///     a' ~ π(· | s')                        (sample a single next action)
        ///     Q(s, a) ← Q(s, a) + α · (r + γ · Q(s', a') − Q(s, a))
        /// On-policy and unbiased w.r.t. the followed policy, but noisier than
        /// Expected SARSA because the bootstrap uses one realization of a' rather
        /// than its mean. The sampled a' is stashed in LastSelectedNextAction so
        /// the trainer can execute it next step instead of re-sampling.
        /// </summary>
        public override void Update(int state, int action, float reward, int nextState)
        {
            int nextAction = SelectAction(nextState); // epsilon-greedy sample of a'
            LastSelectedNextAction = nextAction;

            float tdTarget = reward + discountFactor * qTable[nextState, nextAction];
            float tdError = tdTarget - qTable[state, action];
            qTable[state, action] += learningRate * tdError;

            totalSteps++;
        }

        public override string GetName() => "SARSA";
    }
}
