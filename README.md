# SpeechBuddy AI

SpeechBuddy AI is a therapist companion app concept for articulation therapy workflows.

## Core Features
- Real-time sound accuracy feedback with phoneme scoring
- AI-generated practice lists (words, phrases, sentences, minimal pairs)
- Progress tracking dashboard
- Home-practice assignment generator
- Session notes with AI summaries (SOAP + parent-friendly)

## Tech Direction
- App shell: .NET MAUI (single codebase for iOS, Android, desktop)
- Language: C# + XAML
- Data: SQLite (local)
- AI integrations: speech-to-phoneme, text generation, summarization APIs

## Current State
This repository currently contains a MAUI-style scaffold and domain structure.
On this machine, .NET SDK/MAUI workload was not available during setup, so this is prepared as a starter skeleton for development.

## Getting Started Once .NET Is Installed
1. Install .NET SDK and MAUI workloads.
2. Restore dependencies:
   - `dotnet restore`
3. Build:
   - `dotnet build`

