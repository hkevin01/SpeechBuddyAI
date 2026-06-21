# SpeechBuddyAI

![Platform](https://img.shields.io/badge/platform-.NET%20MAUI-0b7285)
![Language](https://img.shields.io/badge/language-C%23-1f6feb)
![UI](https://img.shields.io/badge/ui-XAML-7f5af0)
![Persistence](https://img.shields.io/badge/persistence-M1%20JSON%20store-f59f00)
![Roadmap](https://img.shields.io/badge/roadmap-M1%20implemented-1e7f3f)
![License](https://img.shields.io/badge/license-MIT-green)

SpeechBuddyAI is a speech therapy companion focused on practical articulation workflows, transparent score components, and session-to-session progress tracking that can be reviewed by clinicians and families. The project is intentionally designed to avoid black-box behavior in early milestones, because trust and interpretability are central in speech practice tools.

This README is both a product guide and a technical implementation reference. It explains what the app does, why each subsystem exists, how scoring is computed, what tradeoffs were chosen, and which free public libraries or APIs are realistic for future expansion.

> [!IMPORTANT]
> SpeechBuddyAI is support software and is not a medical device. The outputs should assist therapy planning, not replace professional clinical decision-making.

## Table of Contents

1. What This Project Does
2. Fast Comparison Tables (Use/Do Not Use)
3. Current Milestone Status
4. Tech Stack and Architecture
5. Algorithms and Formulas
6. Public Libraries and API Strategy
7. Collapsible API Reference
8. GitHub Workflow and Tracking
9. Research Citations
10. Build, Run, and Practical Notes

## What This Project Does

SpeechBuddyAI supports a feedback loop where a learner practices a target sound, receives component-level scoring feedback, and stores attempts for trend review. In the current M1 implementation, the focus is not on maximizing model complexity, but on making the loop reliable and understandable.

The app currently demonstrates a complete vertical slice for M1. A user can enter a target sound and transcript, run scoring, see phoneme/fluency/consistency components, and persist the attempt. That same data appears in the progress dashboard so the practice loop produces longitudinal records instead of one-off scores.

> [!NOTE]
> M1 persistence is currently app-local JSON storage. SQLite remains a planned upgrade for subsequent milestones.

## Fast Comparison Tables (Use/Do Not Use)

These tables are intentionally near the top so new contributors can quickly pick the right technical direction before coding.

### Deployment and Inference Modes

| # | Option | What It Does | Use When | Do Not Use When | How It Works (High Level) |
| --- | --- | --- | --- | --- | --- |
| 1 | Offline-first STT | Runs recognition on device without sending audio to cloud | Privacy-sensitive workflows and poor connectivity | You require managed cloud diarization now | Capture audio -> local model inference -> score pipeline |
| 2 | Cloud STT | Delegates recognition to hosted API | Rapid iteration and broad language support | Strict local-only policy or high latency constraints | Capture audio -> upload -> cloud transcript -> score pipeline |
| 3 | Hybrid STT | Uses local path first and cloud fallback | Production resilience with mixed network conditions | Team cannot maintain two adapters yet | Try local -> if unavailable/fails use cloud -> continue scoring |

> [!TIP]
> For this codebase, hybrid is the long-term target, but offline-first should remain the default experience.

### Scoring Strategy Comparison

| # | Strategy | What It Measures | Best For | Not Ideal For | Why Chosen or Deferred |
| --- | --- | --- | --- | --- | --- |
| 1 | Rule-based component scoring | Phoneme match proxy, fluency proxy, consistency proxy | MVP reliability and explainability | Fine-grained phonetic diagnostics at scale | Chosen for M1 due transparency and low complexity |
| 2 | GOP-style scoring | Phoneme-level posterior contrast confidence | Targeted phone-level coaching and minimal pairs | Pipelines without phoneme alignment/lexicon | Planned as M2/M3 enhancement |
| 3 | Joint APA + MDD models | Overall pronunciation quality plus error diagnosis | Research-grade CAPT with curated benchmark datasets | Small teams without ML ops and annotation capacity | Deferred due complexity and data requirements |

### Persistence Backends Comparison

| # | Backend | What It Does | Use When | Avoid When | Migration Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | JSON file store (current M1) | Simple local persistence with low setup overhead | Early milestone and local prototype workflows | Need robust query/reporting at scale | Current implementation in ProgressTrackingService |
| 2 | SQLite (planned) | Relational local database with indexed queries | Dashboard analytics and larger history datasets | Ultra-light demos without query complexity | Planned migration path for M2 or M3 |
| 3 | Cloud sync DB | Cross-device shared history and backup | Multi-device clinician-family workflows | Offline-only deployments | Add after local model and privacy policy are stable |

> [!IMPORTANT]
> Keep README claims aligned with code reality. M1 currently uses JSON persistence, not SQLite runtime persistence.

## Current Milestone Status

M1 is now implemented as an end-to-end slice. It captures practice attempts, computes score components, saves each attempt, and renders persisted entries in the progress view.

| # | M1 Contract Item | Status | Implementation Detail |
| --- | --- | --- | --- |
| 1 | Practice input capture | Done | Target and transcript are entered on Practice page |
| 2 | Component scoring loop | Done | Phoneme, fluency, consistency, and overall score computed |
| 3 | Persisted score components | Done | ProgressEntry includes component fields and transcript |
| 4 | Longitudinal attempt list | Done | Progress page loads and displays persisted entries |
| 5 | Error pattern tag | Done | Simple pattern inference for feedback categorization |

### M1 Runtime Loop

```mermaid
flowchart TD
    A[User enters target and transcript] --> B[PracticePage event handler]
    B --> C[AiSpeechService EvaluateAndPersistAttempt]
    C --> D[Compute phoneme, fluency, consistency]
    D --> E[Compose overall weighted score]
    E --> F[Build ProgressEntry and infer error pattern]
    F --> G[ProgressTrackingService writes JSON]
    G --> H[ProgressPage reads and renders history]
```

> [!NOTE]
> This flow is intentionally deterministic and transparent, making it suitable for milestone validation and debugging.

### M1 Sequence by Component

```mermaid
sequenceDiagram
    participant U as User
    participant P as PracticePage
    participant S as AiSpeechService
    participant T as ProgressTrackingService
    participant R as ProgressPage
    U->>P: Enter target + transcript
    P->>S: EvaluateAndPersistAttemptAsync
    S->>T: GetEntriesForSoundAsync
    T-->>S: Prior attempts
    S->>S: Compute components and overall
    S->>T: AddEntryAsync
    T-->>S: Persist success
    S-->>P: PracticeAttemptResult
    U->>R: Open Progress tab
    R->>T: GetEntriesAsync
    T-->>R: Persisted entries
```

### M1 Data Shape (Score Components)

| # | Field | Meaning | Range | Why Needed |
| --- | --- | --- | --- | --- |
| 1 | PhonemeScore | Target match proxy | 0.0 to 1.0 | Core articulation confidence dimension |
| 2 | FluencyScore | Stability/length proxy | 0.0 to 1.0 | Captures rhythm/continuity signal in simple form |
| 3 | ConsistencyScore | Variation across recent attempts | 0.0 to 1.0 | Distinguishes one-off success from stable performance |
| 4 | OverallScore | Weighted aggregate score | 0.0 to 1.0 | Supports quick clinician and learner interpretation |

## Tech Stack and Architecture

SpeechBuddyAI uses .NET MAUI and C# to keep mobile and desktop targets in one codebase. This keeps early milestones fast to iterate, while still allowing clear separation between pages, services, and models.

### Current vs Planned Stack

| # | Layer | Current (Implemented) | Planned Direction | Why This Matters |
| --- | --- | --- | --- | --- |
| 1 | UI Shell | .NET MAUI Shell with tab navigation | Same, with richer state and chart controls | Maintains cross-platform consistency |
| 2 | Scoring Service | AiSpeechService with transparent component formulas | Adapter-friendly architecture for offline/online models | Future model upgrades without UI rewrite |
| 3 | Persistence | JSON app-local file in M1 | SQLite for richer queries and analytics | Enables trend views and reporting depth |
| 4 | Practice Content | AiTextService generated words | Constraint-aware generators and personalized assignments | Improves carryover and relevance |

> [!TIP]
> Preserve service boundaries now so later migration from JSON to SQLite and from heuristics to model-based scoring remains low-risk.

### Architecture View

```mermaid
flowchart LR
    A[Pages: Practice, Progress, Notes, Home] --> B[Services Layer]
    B --> C[AiSpeechService]
    B --> D[AiTextService]
    B --> E[ProgressTrackingService]
    E --> F[(JSON persistence in M1)]
    C --> G[Scoring components]
    G --> E
```

### Data Lifecycle

```mermaid
flowchart TD
    A[Input event] --> B[Normalize strings]
    B --> C[Lookup historical attempts]
    C --> D[Compute component scores]
    D --> E[Assign error pattern]
    E --> F[Persist entry]
    F --> G[Load for dashboard]
    G --> H[Display user-facing history]
```

### Why This Architecture Was Chosen

The architecture favors readability and composability over early optimization. In speech therapy software, implementation clarity is a product feature because teams need to verify behavior and reason about edge cases with clinicians.

The same structure also makes A/B comparisons easier later. If you introduce a GOP-based scorer, you can keep the same output contract and compare old and new methods in parallel without destabilizing the pages.

## Algorithms and Formulas

The current M1 approach uses weighted component scoring instead of a single hidden confidence output. This is a deliberate choice for explainability and iterative calibration.

### Current M1 Formula

$$
S_{overall} = 0.60 S_{phoneme} + 0.25 S_{fluency} + 0.15 S_{consistency}
$$

Where each component is clamped to $[0,1]$ before aggregation.

### Why This Formula and Not a Single Black Box Score

| # | Option | Advantage | Limitation | Decision |
| --- | --- | --- | --- | --- |
| 1 | Single confidence score | Simple output | Low interpretability and difficult debugging | Not chosen for M1 |
| 2 | Weighted components (current) | Transparent and tunable | Not yet full phonetic rigor | Chosen for M1 |
| 3 | Learned end-to-end score | Potentially higher accuracy | Data and explainability burden | Deferred until later milestones |

### Consistency Computation Rationale

Consistency is estimated from recent score variance for the same target sound. Lower variance maps to higher consistency, which helps separate stable improvement from random fluctuation.

This gives the dashboard a more clinically useful behavior. Two attempts with the same current phoneme score can be interpreted differently if one learner is stable and another is oscillating.

### Future GOP-Compatible Formula (Planned)

$$
GOP(p) = \frac{1}{T_p}\sum_{t \in p} \log P(p \mid o_t) - \max_{q \ne p}\frac{1}{T_p}\sum_{t \in p} \log P(q \mid o_t)
$$

This formulation is included because it aligns with CAPT literature and supports phoneme-level diagnosis beyond the current heuristic approximation.

### M1 to M3 Algorithm Progression

| # | Stage | Method | What Improves | Risks |
| --- | --- | --- | --- | --- |
| 1 | M1 | Deterministic weighted heuristics | Speed, transparency, debuggability | Limited phonetic depth |
| 2 | M2 | Lexicon-backed phoneme checks | Better target-specific diagnostics | Lexicon and alignment complexity |
| 3 | M3 | GOP-like or segmentation-free features | Higher diagnostic fidelity and benchmarking potential | Model and data pipeline overhead |

### Formula and Algorithm Catalog (CAPT, MDD, and Speech Therapy)

| <sub>#</sub> | <sub>Algorithm or Formula</sub> | <sub>Expression</sub> | <sub>Therapy Use</sub> |
| --- | --- | --- | --- |
| <sub>1</sub> | <sub>Weighted composite score</sub> | <sub>$S=0.60S_p+0.25S_f+0.15S_c$</sub> | <sub>Combines articulation, fluency, and stability in one score</sub> |
| <sub>2</sub> | <sub>Classic GOP</sub> | <sub>$\text{GOP}(p)=\frac{1}{T_p}\sum\log P(p\mid o_t)-\max_{q\neq p}\frac{1}{T_p}\sum\log P(q\mid o_t)$</sub> | <sub>Phone-level mispronunciation feedback</sub> |
| <sub>3</sub> | <sub>Segmentation-free GOP</sub> | <sub>Alignment-free posterior scoring</sub> | <sub>Reduces forced-alignment error in MDD</sub> |
| <sub>4</sub> | <sub>Word Error Rate</sub> | <sub>$\text{WER}=\frac{S+D+I}{N}$</sub> | <sub>Checks transcript quality before scoring</sub> |
| <sub>5</sub> | <sub>Character Error Rate</sub> | <sub>$\text{CER}=\frac{S_c+D_c+I_c}{N_c}$</sub> | <sub>Fine-grained transcript quality check</sub> |
| <sub>6</sub> | <sub>F1 score for MDD</sub> | <sub>$F1=\frac{2PR}{P+R}$</sub> | <sub>Balances precision and recall for detection</sub> |
| <sub>7</sub> | <sub>Dynamic Time Warping</sub> | <sub>$D(i,j)=d(i,j)+\min\{D(i-1,j),D(i,j-1),D(i-1,j-1)\}$</sub> | <sub>Aligns variable speaking rate signals</sub> |
| <sub>8</sub> | <sub>Exponential smoothing</sub> | <sub>$E_t=\alpha x_t+(1-\alpha)E_{t-1}$</sub> | <sub>Smooths noisy progress trajectories</sub> |
| <sub>9</sub> | <sub>z-score normalization</sub> | <sub>$z=\frac{x-\mu}{\sigma}$</sub> | <sub>Normalizes cross-target score comparisons</sub> |
| <sub>10</sub> | <sub>Flesch Reading Ease</sub> | <sub>$206.835-1.015\frac{W}{S}-84.6\frac{SYL}{W}$</sub> | <sub>Keeps parent summaries understandable</sub> |
| <sub>11</sub> | <sub>SNR estimate</sub> | <sub>$\text{SNR}_{dB}=10\log_{10}(P_s/P_n)$</sub> | <sub>Flags noisy captures before scoring</sub> |
| <sub>12</sub> | <sub>Adaptive repetition interval</sub> | <sub>$I_{n+1}=I_n(1+\alpha R_n-\beta E_n)$</sub> | <sub>Improves home drill scheduling quality</sub> |

| <sub>#</sub> | <sub>Algorithm or Formula</sub> | <sub>Project Value</sub> | <sub>Reference</sub> |
| --- | --- | --- | --- |
| <sub>1</sub> | <sub>Weighted composite score</sub> | <sub>Current core scoring contract</sub> | <sub><a href="https://arxiv.org/abs/2506.19315">JCAPT</a></sub> |
| <sub>2</sub> | <sub>Classic GOP</sub> | <sub>Planned path to stronger diagnostics</sub> | <sub><a href="https://www.isca-archive.org/interspeech_2019/sudhakara19_interspeech.html">Sudhakara et al. 2019</a></sub> |
| <sub>3</sub> | <sub>Segmentation-free GOP</sub> | <sub>Candidate for M3+ scoring layer</sub> | <sub><a href="https://arxiv.org/abs/2507.16838">Cao et al. 2025</a></sub> |
| <sub>4</sub> | <sub>Word Error Rate</sub> | <sub>Validates STT adapter quality gates</sub> | <sub><a href="https://en.wikipedia.org/wiki/Word_error_rate">WER definition</a></sub> |
| <sub>5</sub> | <sub>Character Error Rate</sub> | <sub>Supports phoneme-proxy transcript checks</sub> | <sub><a href="https://apxml.com/courses/applied-speech-recognition/chapter-6-evaluating-deploying-asr-systems/asr-performance-metrics-wer-cer">ASR metrics reference</a></sub> |
| <sub>6</sub> | <sub>F1 score for MDD</sub> | <sub>Benchmark-ready evaluation metric</sub> | <sub><a href="https://arxiv.org/abs/2606.05569">Tu et al. 2026</a></sub> |
| <sub>7</sub> | <sub>Dynamic Time Warping</sub> | <sub>Useful for constrained drill alignment</sub> | <sub><a href="https://en.wikipedia.org/wiki/Dynamic_time_warping">DTW reference</a></sub> |
| <sub>8</sub> | <sub>Exponential smoothing</sub> | <sub>Improves dashboard trend stability</sub> | <sub><a href="https://otexts.com/fpp3/ses.html">SES reference</a></sub> |
| <sub>9</sub> | <sub>z-score normalization</sub> | <sub>Improves mixed-target comparability</sub> | <sub><a href="https://en.wikipedia.org/wiki/Standard_score">z-score reference</a></sub> |
| <sub>10</sub> | <sub>Flesch Reading Ease</sub> | <sub>Improves parent-report readability</sub> | <sub><a href="https://en.wikipedia.org/wiki/Flesch%E2%80%93Kincaid_readability_tests">Flesch reference</a></sub> |
| <sub>11</sub> | <sub>SNR estimate</sub> | <sub>Prevents noisy-input mis-scoring</sub> | <sub><a href="https://en.wikipedia.org/wiki/Signal-to-noise_ratio">SNR reference</a></sub> |
| <sub>12</sub> | <sub>Adaptive repetition interval</sub> | <sub>Improves assignment carryover plans</sub> | <sub><a href="https://www.supermemo.com/en/archives1990-2015/english/ol/sm2">SM-2 spacing concept</a></sub> |

This table is designed as a practical algorithm menu rather than a theory dump. The core idea is to separate formulas needed immediately for production-safe behavior from formulas that should be introduced only when evaluation infrastructure is ready. For the current app, weighted composite scoring, consistency estimation, and trend smoothing have the strongest implementation value because they directly improve user-facing reliability.

The second key point is that CAPT/MDD quality is often bottlenecked by alignment and data quality, not only by model architecture. That is why metrics like WER, CER, SNR, and F1 are listed alongside GOP and joint modeling methods. In practice, SpeechBuddyAI should gate advanced scoring with data-quality checks and clear diagnostics so users understand when a score is high-confidence versus low-confidence.

## Public Libraries and API Strategy

The project roadmap intentionally prioritizes free and public resources so the system remains reproducible for open development.

### Integration Candidates

| # | Tool | Type | License or Terms | Why Consider It |
| --- | --- | --- | --- | --- |
| 1 | Vosk | Offline STT toolkit | Apache-2.0 | On-device recognition path for privacy-first deployments |
| 2 | whisper.cpp | Offline ASR inference engine | MIT | Broad platform support and efficient local inference |
| 3 | CMUdict | Pronunciation lexicon | Free unrestricted use notice | Canonical pronunciation references for phone-level logic |
| 4 | Datamuse API | Word association and suggestions | Public usage with limits and API key changes timeline | Practice list generation and topic expansion |

> [!IMPORTANT]
> Datamuse policy text currently notes API-key-related changes from 2027 and request limits, so production integration should include caching and fallback behavior.

### External Project Pattern Mapping

| # | Open Project | Pattern | Practical Adoption in SpeechBuddyAI |
| --- | --- | --- | --- |
| 1 | fulldecent/vowel-practice | Focused vowel visual training | Add optional vowel-target mini mode for articulation sessions |
| 2 | assinscreedFC/ortholyse | Local-first transcription and metrics | Strengthen local evaluation/report pathways |
| 3 | KorayUlusan/delayed-auditory-feedback-online | DAF intervention tooling | Add configurable fluency support exercise mode |

> [!NOTE]
> These are architecture inspirations and workflow ideas, not source-code reuse.

## Collapsible API Reference

<details open>
<summary>Speech Evaluation Contracts (Current and Planned)</summary>

### 1) Practice Attempt Scoring

| # | Contract | Input | Output | Current Status |
| --- | --- | --- | --- | --- |
| 1 | evaluateAttempt | targetSound, transcript | phoneme, fluency, consistency, overall, errorPattern | Implemented in M1 |
| 2 | persistAttempt | ProgressEntry payload | stored entry id and timestamp | Implemented in M1 |
| 3 | listAttempts | optional target filter | ordered attempt history | Implemented in M1 |

> [!TIP]
> Keep output fields stable so model upgrades can be drop-in replacements.

### 2) Practice Content Generation

| # | Contract | Input | Output | Current Status |
| --- | --- | --- | --- | --- |
| 1 | generatePracticeWords | targetSound | word list for drills | Implemented baseline |
| 2 | generateAssignments | historical weak patterns | home plan with rationale | Planned M2 |

### 3) Notes and Reporting

| # | Contract | Input | Output | Current Status |
| --- | --- | --- | --- | --- |
| 1 | summarizeSessionNotes | free text notes | structured summary | Planned M5 |
| 2 | exportReport | session id and format | shareable artifact | Planned M5 |

</details>

<details>
<summary>Example Response Payload (Illustrative)</summary>

```json
{
  "targetSound": "initial r",
  "transcript": "rain rabbit",
  "scores": {
    "phonemeScore": 0.90,
    "fluencyScore": 0.73,
    "consistencyScore": 0.62,
    "overallScore": 0.82
  },
  "errorPattern": "none",
  "trialCount": 4,
  "timestamp": "2026-06-21T10:15:00Z"
}
```

</details>

## GitHub Workflow and Tracking

SpeechBuddyAI now uses roadmap-aligned issue templates and labels so milestone execution can be tracked clearly. This keeps implementation tasks connected to goals like M1 scoring reliability, M2 assignment quality, and M3 trend analytics.

```mermaid
flowchart TD
    A[Issue Template: Bug, Feature, Roadmap, Research] --> B[Labels attached]
    B --> C[Milestone tagged]
    C --> D[PR linked]
    D --> E[Validation]
    E --> F[Merge and roadmap update]
```

### Label Taxonomy Summary

| # | Label Group | Example Labels | Purpose |
| --- | --- | --- | --- |
| 1 | Type | type:bug, type:feature, type:task | Classify issue intent |
| 2 | Status | status:triage, status:ready, status:blocked | Track workflow state |
| 3 | Domain | domain:therapy-core, domain:ai-integration | Map workstream ownership |
| 4 | Roadmap | roadmap:m1..roadmap:m5 | Tie work to roadmap milestone |
| 5 | Priority | priority:p0-critical..priority:p3-low | Support execution ordering |

> [!IMPORTANT]
> Every issue should include both technical acceptance criteria and clinical rationale.

### GitHub Markdown Features Used in This README

| # | Feature | Why It Is Used Here |
| --- | --- | --- |
| 1 | Shields badges | Surface status and stack metadata at a glance |
| 2 | GitHub Alerts | Highlight risk, guidance, and required context clearly |
| 3 | Mermaid diagrams | Represent architecture and workflow as executable documentation |
| 4 | Collapsible details blocks | Keep README readable while preserving deep technical content |
| 5 | Math blocks | Document scoring formulas unambiguously |
| 6 | Dense comparison tables | Accelerate decision-making for contributors |

> [!NOTE]
> GitHub docs confirm support for Mermaid diagrams, alerts, and details blocks in Markdown contexts like repository files and discussions.

## Research Citations

This list captures articles and arXiv references used to inform architecture and algorithm choices.

### CAPT, MDD, and Scoring Papers (Linked)

| <sub>#</sub> | <sub>Paper</sub> | <sub>Focus Area</sub> | <sub>Practical Use in Project</sub> |
| --- | --- | --- | --- |
| <sub>1</sub> | <sub>Korzekwa et al.</sub> | <sub>Synthetic speech for CAPT data augmentation</sub> | <sub>Guides augmentation strategy for MDD robustness</sub> |
| <sub>2</sub> | <sub>Zhang et al. speechocean762</sub> | <sub>Open benchmark corpus for pronunciation assessment</sub> | <sub>Supports offline benchmark alignment</sub> |
| <sub>3</sub> | <sub>Yang et al. JCAPT</sub> | <sub>Joint APA and MDD modeling</sub> | <sub>Guides combined quality and diagnosis outputs</sub> |
| <sub>4</sub> | <sub>Cao et al. GOP-SF</sub> | <sub>Segmentation-free GOP and phone-level assessment</sub> | <sub>Candidate M3+ upgrade for diagnosis fidelity</sub> |
| <sub>5</sub> | <sub>Tu et al.</sub> | <sub>Domain-aware statistical graph MDD</sub> | <sub>Supports language-specific confusion modeling</sub> |
| <sub>6</sub> | <sub>Sudhakara et al.</sub> | <sub>Improved GOP with DNN-HMM transitions</sub> | <sub>Strong baseline for interpretable scoring</sub> |

| <sub>#</sub> | <sub>Paper</sub> | <sub>DOI</sub> | <sub>Direct Link</sub> |
| --- | --- | --- | --- |
| <sub>1</sub> | <sub>Korzekwa et al.</sub> | <sub>10.48550/arXiv.2207.00774</sub> | <sub><a href="https://arxiv.org/abs/2207.00774">arXiv 2207.00774</a></sub> |
| <sub>2</sub> | <sub>Zhang et al. speechocean762</sub> | <sub>10.48550/arXiv.2104.01378</sub> | <sub><a href="https://arxiv.org/abs/2104.01378">arXiv 2104.01378</a></sub> |
| <sub>3</sub> | <sub>Yang et al. JCAPT</sub> | <sub>10.48550/arXiv.2506.19315</sub> | <sub><a href="https://arxiv.org/abs/2506.19315">arXiv 2506.19315</a></sub> |
| <sub>4</sub> | <sub>Cao et al. GOP-SF</sub> | <sub>10.48550/arXiv.2507.16838</sub> | <sub><a href="https://arxiv.org/abs/2507.16838">arXiv 2507.16838</a></sub> |
| <sub>5</sub> | <sub>Tu et al.</sub> | <sub>10.48550/arXiv.2606.05569</sub> | <sub><a href="https://arxiv.org/abs/2606.05569">arXiv 2606.05569</a></sub> |
| <sub>6</sub> | <sub>Sudhakara et al.</sub> | <sub>Interspeech 2019</sub> | <sub><a href="https://www.isca-archive.org/interspeech_2019/sudhakara19_interspeech.html">ISCA page</a></sub> |

This table translates research into implementation value. Instead of listing papers as passive references, each row maps a publication to a concrete project decision, such as benchmark alignment, model choice, or diagnostic strategy. This keeps research work tied to shipping outcomes and avoids academic drift where citations exist but do not change the product.

The linked format also helps team execution. When a milestone task claims to implement "GOP-like scoring" or "domain-aware confusion modeling," contributors can open the exact source quickly and keep terminology consistent across issues, pull requests, and README updates. That consistency improves review quality and keeps AI-related roadmap changes auditable.

### AI and UI Improvement Matrix (Project-Focused)

| <sub>#</sub> | <sub>Improvement Theme</sub> | <sub>AI or UI Direction</sub> | <sub>Near-Term Action</sub> | <sub>Expected User Impact</sub> |
| --- | --- | --- | --- | --- |
| <sub>1</sub> | <sub>Input quality gating</sub> | <sub>AI reliability</sub> | <sub>Add SNR/noise pre-check before scoring</sub> | <sub>Fewer misleading low-confidence scores</sub> |
| <sub>2</sub> | <sub>Provider confidence visibility</sub> | <sub>AI transparency</sub> | <sub>Show scoring provider and confidence band in Practice and Progress</sub> | <sub>Users understand score reliability shifts</sub> |
| <sub>3</sub> | <sub>Weak-pattern assignment tuning</sub> | <sub>AI personalization</sub> | <sub>Rank home drills by error frequency and recency</sub> | <sub>More relevant home plans and better carryover</sub> |
| <sub>4</sub> | <sub>Trend explainability</sub> | <sub>UI analytics</sub> | <sub>Add trajectory interpretation text beside chart bars</sub> | <sub>Faster clinician and parent interpretation</sub> |
| <sub>5</sub> | <sub>Session comparison view</sub> | <sub>UI workflow</sub> | <sub>Add last-session vs current-session cards</sub> | <sub>Quicker in-session progress review</sub> |
| <sub>6</sub> | <sub>Adaptive prompt difficulty</sub> | <sub>AI adaptation</sub> | <sub>Escalate from words to phrases by sustained consistency</sub> | <sub>Better challenge calibration</sub> |
| <sub>7</sub> | <sub>Parent summary readability</sub> | <sub>UI communication</sub> | <sub>Apply readability target in M5 reports</sub> | <sub>Better caregiver understanding and adherence</sub> |
| <sub>8</sub> | <sub>Error pattern drill cards</sub> | <sub>UI coaching</sub> | <sub>Attach micro-coaching tips to each error tag</sub> | <sub>More actionable home practice</sub> |
| <sub>9</sub> | <sub>Offline-first resilience UX</sub> | <sub>AI infra plus UI</sub> | <sub>Display fallback state and retry options</sub> | <sub>Smoother sessions under poor connectivity</sub> |
| <sub>10</sub> | <sub>Benchmark mode</sub> | <sub>AI evaluation</sub> | <sub>Add developer mode to compute WER and F1</sub> | <sub>Faster evidence-based model iteration</sub> |

The project should evolve AI and UI together, not as separate tracks. If scoring sophistication improves without clear UI explanation, users will trust the app less. If UI improves without better scoring controls, users will get polished but unreliable feedback. The matrix above keeps those two dimensions synchronized in planning and implementation.

A practical execution approach is to pair each AI enhancement with one visible UX artifact. For example, when fallback scoring is added, show provider source in the result card; when trend smoothing is added, expose the interpretation text that explains why a trend is labeled improving, stable, or declining. This pairing improves usability and creates better testability in reviews.

### Tooling and Documentation References

1. GitHub Docs - Creating diagrams (Mermaid, GeoJSON, TopoJSON, STL)
2. GitHub Docs - Organizing information with collapsed sections
3. Vosk API documentation and repository
4. whisper.cpp documentation and repository
5. CMUdict repository and license notice
6. Datamuse API documentation

> [!TIP]
> Keep this section versioned as new roadmap decisions are made, so architectural tradeoffs remain evidence-backed and reviewable.

## Build, Run, and Practical Notes

### Prerequisites

1. .NET SDK and MAUI workloads available on the host machine.
2. Platform SDKs for targets you plan to run.
3. Local filesystem write access for app data persistence.

### Build

```bash
dotnet restore
dotnet build
```

### Practical M1 Verification Steps

1. Open Practice tab.
2. Enter target sound and transcript.
3. Press Score Attempt.
4. Confirm component scores and status text update.
5. Open Progress tab and verify entry appears with same component values.

### Next Milestone-Focused Steps

- [ ] M1.1: move JSON persistence to SQLite while preserving output contracts
- [ ] M2: add assignment generation from weak pattern history
- [ ] M3: add charted trend analysis and score trajectory interpretation
- [ ] M4: add offline-first adapter interface with fallback strategy
- [ ] M5: add clinician report and parent summary pipeline

> [!IMPORTANT]
> Favor backward-compatible output contracts in service responses so historical data and dashboard rendering remain stable across model and storage upgrades.
