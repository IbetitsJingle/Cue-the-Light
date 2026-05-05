namespace LightBender.CueTheLight
{
    /// <summary>
    /// Common contract for tabular RL agents driving the Cue the Light environment.
    /// Lets the trainer swap Expected SARSA, SARSA, and Q-Learning behind the same surface
    /// so head-to-head comparisons stay apples-to-apples.
    /// </summary>
    public interface ICueAgent
    {
        /// <summary>Choose an action for the given state under the current (epsilon-greedy) policy.</summary>
        int SelectAction(int stateIndex);

        /// <summary>Apply the agent's TD update for the (s, a, r, s') transition.</summary>
        void Update(int state, int action, float reward, int nextState);

        /// <summary>Decay exploration toward the floor epsilonEnd. Called once per episode/step by the trainer.</summary>
        void DecayEpsilon();

        /// <summary>Read a single Q-table cell.</summary>
        float GetQValue(int state, int action);

        /// <summary>Max Q-value over all actions for a state. Used for diagnostics and Q-learning targets.</summary>
        float GetMaxQ(int state);

        /// <summary>Argmax action for a state (random tie-break). The on-policy greedy choice.</summary>
        int GetGreedyAction(int state);

        /// <summary>Current exploration rate.</summary>
        float GetEpsilon();

        /// <summary>Number of Update calls since construction or Reset.</summary>
        int GetTotalSteps();

        /// <summary>Direct handle to the underlying Q-table for visualization/serialization.</summary>
        float[,] GetQTable();

        /// <summary>Zero Q-values, restore epsilon to its start, clear step counter.</summary>
        void Reset();

        /// <summary>Display name for logs and UI ("Expected SARSA", "SARSA", "Q-Learning").</summary>
        string GetName();
    }
}
