> Source: https://docs.dev.board.fun/tools/ — fetched 2026-06-04T18:38 (UTC-7)

# Tools

Two tools for installing and inspecting builds on Board hardware. Board Connect is the recommended developer workflow. `bdb` is the legacy command-line tool.

## Board Connect (recommended)

A web app plus HTTP API for working with a Board device from your computer. Pair once, then install builds, stream logs, and drive the device from your dev workflow without USB cables or platform-specific tooling. Works the same regardless of which SDK you build with.

This is the forward path. New workflows, scripts, and integrations should be built on Board Connect.

→ Get started with Board Connect

## bdb (legacy)

The original USB-based command-line tool: `bdb install`, `bdb launch`, `bdb logs`. Still functional and documented, but it is being phased out in favor of Board Connect and may be deprecated in a future release. Don't build new workflows on `bdb` — use Board Connect for anything you'd otherwise script around.

If you have an existing `bdb`-based workflow, it will keep working for the foreseeable future, and migrating to Board Connect is straightforward whenever you're ready.
