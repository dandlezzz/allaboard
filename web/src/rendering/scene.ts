// Procedurally builds the static scene furniture — a port of Unity
// `Core/SceneBuilder.cs`'s sea + arena frame. A clean, flat solid light-blue
// field fills the view; a thin lighter frame marks the play bounds.

import { Container, Graphics } from "pixi.js";
import * as Config from "../core/config";

export function buildSea(parent: Container): void {
  const seaX = Config.ArenaHalfX * 2.6;
  const seaZ = Config.ArenaHalfZ * 2.6;

  const sea = new Graphics();
  sea.rect(-seaX / 2, -seaZ / 2, seaX, seaZ).fill({ color: 0x6ba8db }); // (0.42,0.66,0.86)
  parent.addChild(sea);

  const halfX = Config.ArenaHalfX;
  const halfZ = Config.ArenaHalfZ;
  const thick = 1.2 * Config.ShipScale;
  const edge = 0x7399b8; // (0.45,0.6,0.72)

  const frame = new Graphics();
  // Top / bottom edges (full width) and left / right edges (full height).
  frame.rect(-halfX, -halfZ - thick / 2, halfX * 2, thick).fill({ color: edge });
  frame.rect(-halfX, halfZ - thick / 2, halfX * 2, thick).fill({ color: edge });
  frame.rect(-halfX - thick / 2, -halfZ, thick, halfZ * 2).fill({ color: edge });
  frame.rect(halfX - thick / 2, -halfZ, thick, halfZ * 2).fill({ color: edge });
  parent.addChild(frame);
}
