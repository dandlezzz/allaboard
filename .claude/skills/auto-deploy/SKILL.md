---
name: auto-deploy-after-task
description: Automatically deploy completed work to Daniel's Board hardware after finishing any change in this Trafalgar — Age of Sail web repo. Use proactively whenever you finish a unit of work the user asked for — a feature, fix, edit, or task — in the Board web app (web/), once it builds cleanly, WITHOUT waiting for the user to separately say "deploy it". Triggers on phrasings/situations like "I just finished the feature/fix", wrapping up an implementation, completing a requested edit, or otherwise reaching a clean stopping point on real gameplay/UI/SDK code in this repo. Defers to the deploy-board skill for the actual pipeline.
---

# Auto-deploy after completing a task

When you finish a unit of work the user asked for in this repo and it builds
cleanly, **deploy it to the Board automatically** — don't wait for a separate
"deploy it" instruction. This keeps the on-Board build in sync with the latest
work.

## When to auto-deploy

After completing a feature, fix, edit, or task that touches real app code
(gameplay, rendering, UI, SDK, config that affects the build), and the web
build passes.

## Workflow

1. **Verify the build passes first:**

   ```bash
   cd web && npm run build
   ```

2. **If the build fails** — do NOT deploy. Surface the error and fix it (or
   report it). A red build is never deployed.

3. **If the build passes** — deploy by following the **`deploy-board`** skill
   (`.claude/skills/deploy-board/SKILL.md`), which is the source of truth for
   the pipeline (build → `@board.fun/web-pack` → `board-connect install`).
   In short, from repo root:

   ```bash
   BOARD_HOST=192.168.4.85 scripts/deploy_board_web.sh --launch
   ```

4. **Commit after deploying** — the deploy script + AGENTS.md rule require
   committing the whole working tree immediately after any successful Board
   deploy, so every on-Board build maps to a commit. The `deploy-board` skill
   covers this; do it without asking.

## Guardrails (skip auto-deploy when)

- The build/typecheck fails (`cd web && npm run build`) — report the error
  instead of deploying.
- The work is trivial / a no-op (e.g. comment-only, docs/markdown-only edits
  that don't change the built app).
- The task was pure investigation, a read-only question, or analysis with no
  code change.
- The work is explicitly marked WIP or "don't deploy", or the user asked you
  to hold off / not deploy. Always respect an explicit instruction to wait.

When in doubt on a borderline case, finish the work and ask before deploying
rather than surprising the user.

## Defers to

- **`deploy-board`** skill — the full deploy pipeline, Board IP / pairing
  details, post-deploy commit rule, and troubleshooting. Don't duplicate it
  here; follow it.
