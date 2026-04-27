# Light Bender — Claude Code Configuration

## Project
Light Bender — A Voice-Adaptive Therapeutic Audiovisual Engine
Unity 6 (6000.0.41f1) with URP. Steam target. Solo developer.
Repository: github.com/universe-visioneer/Light_Bender

## Architecture — Trinity Engine
Voice Input (SenseVoice via sherpa-onnx) → LLM Processing (Ollama localhost HTTP) → Audiovisual Output (Unity URP)
Three registers: grounded / searching / overwhelmed
ControlSignal is the single data struct flowing from Mind to Output layer
All LLM calls return structured JSON via tool-calling pattern, never free text

## Unity Conventions
- All project scripts under Assets/_Project/Scripts/
- sherpa-onnx wrapper in Assets/Plugins/SherpaOnnx/
- ONNX model files in StreamingAssets/Models/
- Ollama communication via HttpClient to localhost (NOT Python bridge, NOT LLMUnity addon)
- URP pipeline — respect SRP Batcher, use UnityPerMaterial CBUFFER
- MonoBehaviour lifecycle: Awake for self-init, Start for cross-references
- New Input System only
- Assembly definitions per system: LightBender.Voice, LightBender.Mind, LightBender.Audio, LightBender.Visuals, LightBender.Core

## Script Folders
Scripts/Voice/ — MicCapture, TranscriptionManager, EmotionMetadata
Scripts/Mind/ — OllamaClient, ClaudeApiFallback, RegisterClassifier, ControlSignal
Scripts/Audio/ — MusicManager, CrossfadeEngine, ClusterMap
Scripts/Visuals/ — VisualController, PaletteManager, TransitionDriver
Scripts/Core/ — SessionManager, StateLogger, Config

## Audio Pipeline
- Crossfade between tracks using AudioMixer.TransitionToSnapshots with weights
- Arousal scalar drives AudioMixer.SetFloat on exposed filter cutoff param, smoothed
- Three clusters of ~40 tracks each mapped to registers
- Transition speed governed by intensity score — high intensity = slower transitions

## Visual Pipeline
- Shader.SetGlobalVector for emotion vec4 → scene-wide shader params
- ParticleSystem.MainModule + gradient swap for dominant emotion
- Warm golden tones (grounded), cooler blue-gray (searching), minimal dark (overwhelmed)
- Vector4.Lerp with smoothTime 0.3-1.0s for all transitions
- Static elements must subtly breathe/pulse — no frozen frames

## Latency Budget
- VAD window: ~200ms
- SenseVoice transcription: ~250ms
- Ollama classification: ~200ms
- Smoothing ramp: 300-1000ms
- Total end-to-speech to visible change: <1s target
- SenseVoice emotion token = fast path (skip LLM for immediate response)

## Behavior
- Caveman mode: ON
- Max effort reasoning
- Include inline comments on non-obvious lines in C# scripts
- Architectural decisions: explain WHY in 1-2 sentences max
- Do NOT over-engineer — soft, simple, alive (Sharky Principle)
- Do NOT add features not in the project plan without asking
- Do NOT edit .meta files or YAML scene files directly
- Do NOT use legacy Input Manager or Built-in Render Pipeline APIs
- Commit messages: manual only, never auto-commit
