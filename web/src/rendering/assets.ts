// Art asset loading for the PixiJS renderer. Textures are loaded once up front
// (see `preloadArt`, awaited in main.ts before the fleets are built). Every
// loader is individually guarded: if an asset fails to resolve, its slot stays
// null and the renderer falls back to the procedural drawing, so the game never
// renders blank.
//
// Assets live in `web/public/assets/...`, which Vite serves at `/assets/...` in
// dev and copies to the dist root on build. We use document-relative URLs (no
// leading slash) so they resolve under the dev server AND from a file:// Android
// WebView (where `base: "./"` makes index.html load from the bundle root).

import { Assets, type Texture } from "pixi.js";
import { ShipClass } from "../ships/shipClass";

const SHIP_URLS: Record<ShipClass, string> = {
  [ShipClass.Frigate]: "assets/ships/frigate.png",
  [ShipClass.ThirdRate]: "assets/ships/third_rate.png",
  [ShipClass.FirstRate]: "assets/ships/first_rate.png",
};

const SMOKE_URL = "assets/fx/smoke.png";

/** Loaded ship textures by class (missing entries → procedural fallback). */
export const shipTextures: Partial<Record<ShipClass, Texture>> = {};

/** Loaded cannon-smoke texture (null → procedural smoke fallback). */
export let smokeTexture: Texture | null = null;

async function tryLoad(url: string): Promise<Texture | null> {
  try {
    return await Assets.load<Texture>(url);
  } catch (err) {
    console.warn(`[Trafalgar] art asset failed to load: ${url}`, err);
    return null;
  }
}

/** Loads all art assets. Safe to call once; failures degrade to procedural art. */
export async function preloadArt(): Promise<void> {
  const classes: ShipClass[] = [ShipClass.Frigate, ShipClass.ThirdRate, ShipClass.FirstRate];
  await Promise.all(
    classes.map(async (cls) => {
      const tex = await tryLoad(SHIP_URLS[cls]);
      if (tex) shipTextures[cls] = tex;
    }),
  );
  smokeTexture = await tryLoad(SMOKE_URL);
}
