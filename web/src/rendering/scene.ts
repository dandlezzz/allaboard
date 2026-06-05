// Procedurally builds the static scene furniture — a port of Unity
// `Core/SceneBuilder.cs`. A clean, flat solid light-blue field fills the view.

import { Container, Graphics } from "pixi.js";
import * as Config from "../core/config";

export function buildSea(parent: Container): void {
  const seaX = Config.ArenaHalfX * 2.6;
  const seaZ = Config.ArenaHalfZ * 2.6;

  const sea = new Graphics();
  sea.rect(-seaX / 2, -seaZ / 2, seaX, seaZ).fill({ color: 0x6ba8db }); // (0.42,0.66,0.86)
  parent.addChild(sea);
}
