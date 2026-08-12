# Building AI Agents in .NET: A Progressive Series

This is a ten-part, code-first series for building provider-configurable AI agents with C#, the official OpenAI .NET client, and either OpenAI or OpenRouter.

Each article begins with the working result from the previous article. New abstractions are introduced only when the course needs them, so the reader can see why each layer exists.

## Reading order

| Part | Article | Outcome | Status |
| ---: | --- | --- | --- |
| 1 | [Starter agent and multi-turn conversation](agentic-ai-crash-course/starter-agent/StarterAgent/01-starter-agent.md) | A configurable conversational agent with in-memory history | Complete |
| 2 | [Structured outputs](02-structured-outputs.md) | Type-safe support-ticket extraction using JSON Schema | In progress |
| 3 | [Function tools](03-function-tools.md) | An agent loop that invokes local C# functions | Planned |
| 4 | [Execution and streaming](04-execution-and-streaming.md) | Streaming, cancellation, retries, and execution events | Planned |
| 5 | [Context and state](05-context-and-state.md) | Explicit run context and controlled conversation state | Planned |
| 6 | [Guardrails](06-guardrails.md) | Input, output, and business-rule validation | Planned |
| 7 | [Persistent sessions](07-persistent-sessions.md) | Durable conversations that survive application restarts | Planned |
| 8 | [Handoffs and delegation](08-handoffs-and-delegation.md) | A triage agent that transfers work to specialists | Planned |
| 9 | [Multi-agent orchestration](09-multi-agent-orchestration.md) | Parallel and sequential workflows across agents | Planned |
| 10 | [Tracing and observability](10-tracing-and-observability.md) | End-to-end telemetry for model calls, tools, and workflows | Planned |

## Repository shape

```text
dotnet-agent-crash-course/
├── DotNetAgentCrashCourse.sln
├── docs/
│   └── series/
│       ├── README.md
│       ├── 01-starter-agent.md
│       └── ...
└── src/
    ├── StarterAgent/
    ├── StructuredOutputAgent/
    └── ...
```

Every article uses a separate runnable console project. That keeps examples focused and let you compare milestones. Provider configuration follows the same shape throughout the series.

## Article contract

Every article follows this order:

1. The problem introduced at this stage
2. What the reader will build
3. Concepts and request flow
4. Incremental implementation steps
5. OpenAI and OpenRouter verification
6. Failure cases and design tradeoffs
7. Exercises
8. What the next article adds

## Provider compatibility rule

The examples use OpenAI-compatible Chat Completions capabilities shared by the selected OpenAI and OpenRouter models. Provider-specific behavior is isolated behind configuration or an adapter and is called out explicitly.

Model capabilities vary. Each article should verify that its configured model supports the feature being taught, especially structured outputs and function calling.

