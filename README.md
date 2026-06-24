# Cue-the-Light
# Cue the Light

**Reinforcement Learning for Adaptive Response Selection**

A tabular reinforcement learning system that learns optimal response policies for an adaptive AI application. The system maps emotional state classifications to visual/audio response configurations through reward-driven exploration.

## Overview

Cue the Light implements multiple RL algorithms to solve an adaptive response selection problem:

- **State Space:** 27 states derived from a 3×3×3 MDP (register × intensity × trend)
- **Action Space:** 12 possible response configurations (color palette × motion profile combinations)
- **Algorithms:** Expected SARSA, SARSA, Q-Learning (tabular implementations)
- **Environment:** Unity-based rendering environment for real-time visual feedback

The system receives classified emotional states as input and learns which visual/audio response configurations produce the best outcomes through iterative training episodes.

## Architecture

```
Input (State Classification)
        │
        ▼
┌─────────────────┐
│   RL Agent       │
│  (Tabular)       │
│                  │
│  Q-Table:        │
│  27 states ×     │
│  12 actions      │
└────────┬────────┘
         │
         ▼
   Action Selection
   (ε-greedy policy)
         │
         ▼
┌─────────────────┐
│  Unity Renderer  │
│  (Visual Output) │
└────────┬────────┘
         │
         ▼
   Reward Signal
   (feedback loop)
```

## Algorithms Implemented

| Algorithm | Update Rule | Characteristics |
|-----------|------------|-----------------|
| **Expected SARSA** | Uses expected value of next state | Lower variance, stable learning |
| **SARSA** | On-policy, uses actual next action | Conservative, follows current policy |
| **Q-Learning** | Off-policy, uses max Q of next state | Aggressive, learns optimal policy |

## State Representation

States are composed of three dimensions:

- **Register** (3 levels): The categorical classification of the input
- **Intensity** (3 levels): The magnitude/strength of the input signal
- **Trend** (3 levels): The directional change over recent inputs (increasing, stable, decreasing)

Total state space: 3 × 3 × 3 = **27 unique states**

## Technical Stack

- **Python** — Core RL agent implementation, training loops, Q-table management
- **Unity (C#)** — Real-time rendering environment for visual response output
- **NumPy** — Numerical computation for Q-value updates and policy evaluation

## Getting Started

```bash
# Clone the repository
git clone https://github.com/IbetitsJingle/Cue-the-Light.git

# Install Python dependencies
pip install numpy

# Run training
python train_agent.py
```

## Project Status

Active development. Currently implementing and comparing convergence rates across the three RL algorithms with different hyperparameter configurations (learning rate, discount factor, exploration rate).

## Author

**Jinge Zhou** — Johns Hopkins University, M.S. in Information Systems and AI for Business
