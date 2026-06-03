# Deploying the Trafalgar web game

Continuous deployment for the **web game** in [`web/`](web/) (Vite + TypeScript +
PixiJS). The build is `npm ci && npm run build`, output in `web/dist/`.

This setup mirrors the `abishag` repo, which deploys to **Vercel**. In abishag the
sub-projects (trading, hoenipedia, sworihow) are routes in one Next.js app
deployed to Vercel via Vercel's **native Git integration** (push to `main` →
Vercel builds & deploys; config in `vercel.json`, no GitHub Actions). Here the web
game is a Vite static SPA, so we deploy `web/` to Vercel the same way.

The Unity project in `unity/` is **not** deployed — only `web/`.

## Files

| File | Purpose |
|---|---|
| `web/vercel.json` | Vercel project config — mirrors abishag's `vercel.json` (framework, `iad1` region, security headers). For Vite, `outputDirectory` is `dist`. |
| `.github/workflows/deploy.yml` | Optional Actions-driven CD: builds `web/` and deploys to Vercel via the Vercel CLI on push to `main` (path-filtered to `web/**`). |
| `.gitignore` | Ignores `web/node_modules`, `web/dist`, and the `.vercel` link dirs. |

## Pick ONE deploy path

Both paths below deploy to Vercel. **Do not enable both** or you'll get duplicate
deployments.

### Path A — Vercel native Git integration (recommended; exactly what abishag does)

No workflow needed. Vercel watches the GitHub repo and deploys on every push to
`main`.

1. Create the GitHub repo and push (see "Make the repo live" below).
2. In the [Vercel dashboard](https://vercel.com/new), **Import** the GitHub repo.
3. Set **Root Directory** = `web` (so Vercel uses `web/vercel.json`,
   `web/package.json`, and `web/dist`). Framework auto-detects as **Vite**.
4. Deploy. Every push to `main` now redeploys automatically; pull requests get
   preview deployments.

If you use Path A, you can delete `.github/workflows/deploy.yml` (or leave it —
it only runs if the three secrets below are present).

### Path B — GitHub Actions → Vercel CLI

Use the included `.github/workflows/deploy.yml`. It triggers on push to `main`
when anything under `web/**` changes, installs deps, builds, and deploys to
Vercel. You must still create the Vercel project once (steps 1–3 of Path A, but
you do **not** need to enable Vercel's Git auto-deploy — turn it off under the
project's Git settings to avoid double deploys), then add these **repository
secrets** (Settings → Secrets and variables → Actions):

| Secret | Where to get it |
|---|---|
| `VERCEL_TOKEN` | Vercel → Account Settings → Tokens → create a token. |
| `VERCEL_ORG_ID` | After `vercel link` in `web/`, read `web/.vercel/project.json` (`orgId`), or from Vercel project settings. |
| `VERCEL_PROJECT_ID` | Same `web/.vercel/project.json` (`projectId`), or project settings. |

To get the org/project IDs locally: `cd web && npx vercel link` (creates the
gitignored `web/.vercel/project.json`).

## Make the repo live (manual, one-time)

`boarders` is not yet connected to a remote. After committing:

```bash
# Create the GitHub repo and push (uses GitHub CLI; adjust owner/visibility):
gh repo create <owner>/boarders --private --source=. --remote=origin --push

# …or manually:
git remote add origin git@github.com:<owner>/boarders.git
git push -u origin main
```

Then follow Path A or Path B above.

## Notes & assumptions

- `web/vite.config.ts` uses `base: "./"` (relative asset paths), which works at a
  Vercel root domain and on any subpath — no change needed.
- The standalone build only needs `pixi.js`; the optional private Board Web SDK
  (`@harrishill/board-sdk`, see `web/README.md`) is **not** required for the
  deployed browser build. If you later add it as a real dependency, CI will need
  access to that tarball/registry.
- **Optional board.fun packaging:** to additionally produce a `.webapp.zip` for
  board.fun, add a dev dependency on `@board.fun/web-pack` and a job/step that
  runs it against `web/dist` after the build, uploading the zip as an artifact or
  release asset. This is an extra, not part of the primary browser CD.
- **Fallback (not used here):** if you'd rather not use Vercel, deploy `web/dist`
  to GitHub Pages instead — build `web/` in Actions and publish `web/dist` with
  `actions/deploy-pages`. Vercel is the faithful mirror of abishag, so it's the
  default above.
