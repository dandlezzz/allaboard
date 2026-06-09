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
import { Menu } from "./ui/menu";
import { StartScreen } from "./ui/startScreen";
import { RangeToggle } from "./ui/rangeToggle";
import { Game } from "./core/game";
import { createInputAdapter } from "./board/input";
import { loadBoard } from "./board/sdk";
import { PauseMenu } from "./board/pauseMenu";
import { preloadArt } from "./rendering/assets";
import { hydrateCustomScenarios } from "./core/scenarioStore";

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

  // HUD / Menu callbacks need the Game and the Game needs the HUD, so the
  // closures below capture `game`, which is assigned immediately after
  // construction.
  let game: Game;
  const hud = new Hud(() => game.restart());
  // The antique-chart menu drives scenario / side / opponent selection; it sits
  // on top of the live canvas and starts a match via configureMatch.
  const menu = new Menu({
    onBegin: (scenarioId, playerFaction, opponent) =>
      game.configureMatch(scenarioId, playerFaction, opponent),
  });

  // On device, pull custom scenarios out of the durable Board.save store and
  // refresh the gallery once they arrive (the Menu subscribed in its ctor). The
  // menu renders immediately from the synchronous localStorage cache; this only
  // adds/repairs the durable list. Fire-and-forget — never blocks startup.
  void hydrateCustomScenarios();
  game = new Game(renderer, hud, onDevice);

  // Per-player firing-range overlay toggles (one corner button per fleet). Each
  // flips its own side's range fans on/off independently.
  new RangeToggle((faction, enabled) => game.setRangeOverlay(faction, enabled));

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

  // Launch START SCREEN: the existing "How to Play" overlay is shown first. The
  // player dismisses it by PLACING THEIR PIECE on the board (a Board contact on
  // device; a pointer tap in the browser preview), which reveals a "Begin
  // Battle" button. Tapping it opens the scenario menu — the normal path into a
  // match (pick battle → side → opponent → Begin). The "?" help button keeps
  // reopening the same content later.
  const startScreen = new StartScreen(() => menu.open());
  void startScreen.begin();

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
