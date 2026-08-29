[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.Libvips.Runners.Linux/build-and-test.yml?style=for-the-badge)](https://github.com/soenneker/Soenneker.Libvips.Runners.Linux/actions/workflows/build-and-test.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/Soenneker.Libvips.Runners.Linux/daily-automatic-update.yml?style=for-the-badge&label=Daily%20Update)](https://github.com/soenneker/Soenneker.Libvips.Runners.Linux/actions/workflows/daily-automatic-update.yml)

# Soenneker.Libvips.Runners.Linux

Provides file cleanup and filesystem operations used by the generated-client update workflow.

> This is an automation runner, not a package intended for application consumption.

## What the runner does

- `IFileOperationsUtil.Process(cancellationToken)` — Processes the pending work managed by the File Operations.

## What you get

- `IFileOperationsUtil` — Provides file cleanup and filesystem operations used by the generated-client update workflow.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `IFileOperationsUtil.Process(cancellationToken)` | Processes the pending work managed by the File Operations. | A task whose result is the text returned by process. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
