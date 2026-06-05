// Entry point for the Trafalgar — Age of Sail web port (the analogue of Unity's
// `Core/GameBootstrap`). Wires the PixiJS renderer, the HUD, the Board/pointer
// input adapter, and the game loop. The Board Web SDK is optional: in a normal
// browser the game runs fully with the mouse; on a real Board it uses the SDK.
//
//   src/core/       game loop, config, match state, nav/math    (← Unity Core/)
//   src/ships/      ship model, classes, sail/ammo              (← Unity Ships/)
//   src/combat/     wind, broadsides, projectile/smoke effects  (← Unity Combat/)
//   src/ai/         the solo Fleet AI                           (← Unity AI/)
//   src/rendering/  PixiJS sea / ships / effects                (← Unity Rendering/)
//   src/ui/         the DOM HUD                                 (← Unity UI/)
//   src/board/      Board SDK input adapter (+ optional loader) (← Unity Input/)

import { Renderer } from "./rendering/renderer";
import { Hud } from "./ui/hud";
import { Game } from "./core/game";
import { createInputAdapter } from "./board/input";
import { loadBoard } from "./board/sdk";
import { PauseMenu } from "./board/pauseMenu";
import { preloadArt } from "./rendering/assets";

async function main(): Promise<void> {
  const canvas = getElement<HTMLCanvasElement>("game-canvas");

  const board = await loadBoard();
  const onDevice = !!board?.isOnDevice;
  document.body.classList.toggle("board-device", onDevice);
  document.body.classList.toggle("browser-preview", !onDevice);

  const renderer = new Renderer();
  await renderer.init(canvas);

  // Load ship/smoke textures before building the fleets (failures degrade to
  // the procedural ShipView art).
  await preloadArt();

  // HUD callbacks need the Game and the Game needs the HUD, so the closures
  // below capture `game`, which is assigned immediately after construction.
  let game: Game;
  const hud = new Hud(
    () => game.toggleSecondPlayer(),
    () => game.restart(),
    (persona) => game.selectPersona(persona),
  );
  game = new Game(renderer, hud, onDevice);

  // Wire the OS pause overlay (Board hardware menu button). The PauseMenu is a
  // no-op driver in the browser; on a Board the game drives it across phase
  // transitions (set context in Playing, clear on game over / Setup). Attach it
  // before `start()` so the initial Setup phase can clear any stale context.
  game.setPauseMenu(
    new PauseMenu(board, {
      onRestart: () => game.restart(),
    }),
  );

  game.start();

  const disposeInput = await createInputAdapter(canvas, (samples) =>
    game.onPointerSamples(samples),
  );
  window.addEventListener("beforeunload", disposeInput);

  // Drive the simulation from Pixi's ticker (dt clamped to avoid huge steps
  // after a tab regains focus).
  renderer.app.ticker.add((ticker) => {
    const dt = Math.min(0.05, ticker.deltaMS / 1000);
    game.update(dt);
  });
}

main().catch((err) => {
  console.error("[Trafalgar] failed to start:", err);
});

function getElement<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Missing required element #${id}`);
  return element as T;
}
