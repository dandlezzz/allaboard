> Source: https://docs.dev.board.fun/faq — fetched 2026-06-04T18:38 (UTC-7)

# FAQ

Frequently asked questions about the Board Developer Program.

## Program Structure & Phases

### Is Board an open platform?

Yes. Anyone can build games using AI coding tools alongside Board's SDKs (Unity, Web, or Godot). Board is open for creation and curated for families. Experimentation is encouraged.

Later this year, we're also launching Board Studio, a creation platform that makes building games even easier. Board Studio will make it more accessible for families, educators, hobbyists, and developers to build their own games and custom Pieces for Board using natural language prompts and AI-powered tools. The result: anyone can prompt their way from idea to playable prototype in under an hour and then share it with the community.

### What are the community and Discord expectations?

The Board Developer Discord exists to:
- Support SDK usage
- Answer technical questions
- Share official updates and documentation
- Showcase your projects and solicit feedback

It is not a roadmap or product ideation forum.
- Product feedback is welcome but should be emailed to: hello@board.fun
- Platform decisions are explained, not debated
- Moderators may close threads once answered

This keeps the space useful, respectful, and sustainable.

### What are the phases of the Board Developer program?

The Board Developer Program is rolled out in phases. See board.fun/create for the current phases and what each one unlocks.

### What does Board think makes a good game for Board?

**Physical meets Digital Magic**

The use of Pieces is where we find the most value to players. That moment where players experience a blending of the physical and digital world is where we find true wonder.
- Physical Pieces should meaningfully affect gameplay and not simply be tokens.
- Games should acknowledge physical actions clearly (placing a Piece, rotation, sliding, etc). When a Piece is placed on the Board, there should always be a reaction.
- Piece interactions should not feel gimmicky. If a finger tap could be better used for an action, use the finger instead (e.g., don't use a Piece as a stylus)
- Be mindful of the screen. Don't use Pieces in a way that would damage the screen such as dropping them or slamming them.

**Accessible yet Deep**

Games on Board are meant to be played by a wide range of skill sets and age groups. We want games where parents have genuine fun playing with their kids but also have fun playing together as a group of adults.
- New players should be able to start playing quickly
- No external instructions should be required (PDFs, videos, Discord explanations, etc)
- Rules should be understandable without prior gaming knowledge
- Games should be appropriate for all audiences
- Themes, language, and visuals should be broadly accessible
- Difficulty should scale or accommodate mixed age groups

**Unique to Board**

The best games on Board are those that could only exist on Board. This means games that would not be possible as traditional video games using a controller or mouse and keyboard but also not possible as a board game with dice and tokens. Ports of existing games without significant changes to account for the unique features of Board do not work.
- Games should follow all the best practices for creating physical meets digital magic.
- Games should use the unique placement of a tabletop screen with players sitting around it. Consider this when designing UIs and interactivity.
- Games should utilize the digital aspect of Board to enforce rules but also do things like simulation or visualization not possible in other games.
- Games should use the Pieces and multitouch to be the most intuitive way for players to control and interact with the game.

**Made to Play Together**

We want players to gather around Board together to invest quality time with each other. The goal is to bring people closer, not simply entertain them.
- Design games for 2+ people around one Board, but solo mode is a bonus.
- Allow up to 6 or even unlimited players to play together.
- Encourage conversation and interaction, not heads-down focus.
- Keep active session times short, typically under 8-10 minutes.

## Pieces

### How many Pieces can be supported at one time?

Board's Piece detection is trained on each individual game's set of Pieces (e.g.: the Mushka Piece Set, the Strata Piece Set).

When building with the Board SDK, you'll select one game's Piece Set to use. So you can make a game that uses the robots and ships from Board Arcade, or the blocks from Strata, for example.

You can have any number of Pieces from the same set active at once, along with unlimited finger touches, but only one Piece Set can be detected at a time. The only constraint is physical space on the display—for example, ~170 Board Arcade Pieces can fit on the screen simultaneously and will all be tracked. As new games are developed, Piece Sets will be expanded to support additional unique Pieces and larger collections or combined to work across games.

You can begin developing with existing Piece Sets and more Piece Sets will continue to be released.

### Can I build with any combination of the 49 Pieces?

Not at this time. Our current Piece models are available on the Developer Portal. We will be adding additional Piece models in the future.

### Will I be able to make new Pieces for my game?

Currently, you can make games using a selection of existing Pieces and models.

When submitting your game for review you can make a proposal for a custom Piece Set that will be manufactured in collaboration with Board.

Later this year we will launch Board Studio, a creation platform that will make it more accessible for families, educators, hobbyists, and developers to build their own games and custom Pieces for Board using natural language prompts and AI-powered tools.

## SDK Details

### What's included in the SDK?

Board offers Unity SDK, Web SDK, and Godot SDK. Each SDK provides everything needed to build games for the Board platform, including:
- input detection and tracking for game Pieces and finger touches
- session management for multiplayer gameplay
- save game functionality
- pause screen and profile controls
- Board Simulator (Unity only) for testing Piece interactions without physical hardware
- sample scenes demonstrating common usage patterns

### What languages or frameworks are supported?

The Board SDK currently supports Unity with C#, with a minimum version of Unity 2021.3.56f2 (Unity 6 is also supported), Godot, and Web.

### What input data does the SDK provide?

Board provides two contact types:
1. Finger contacts: standard touch points (e.g., finger) with ID, position, and touch phase
2. Glyph contacts: tangible Pieces with ID, position, touch phase, along with orientation and touched status

Developers can fine-tune tracking parameters like translation, rotation smoothing and Piece persistence, allowing you to customize behavior for your game. See the Touch guide for the full contact model and per-SDK conventions.

### How does Board handle noise such as arms, hands, etc?

Board's AI is trained to filter out unintentional contact such as hands or arms resting on the display, even when in close contact with Pieces. This allows for natural gameplay where players can rest their hands on the display, reach across the board, or manipulate Pieces without generating false inputs. The system distinguishes between intentional finger touches and incidental contact, ensuring a smooth and intuitive gaming experience.

### Is there a cost for the SDK?

No, the SDK documentation and libraries are free.

## Development Process

### How does Board Simulator work?

The Board Simulator (Unity only) replicates Piece interactions, allowing you to place, move, touch, lift and rotate virtual Pieces using mouse and keyboard controls. These actions generate the same input events as real hardware, so you can iterate quickly without deploying to a device. You can manage multiple Pieces simultaneously and configure the input mappings to match your development workflow.

### Do I need to purchase a dev kit to develop?

No, you can start with just the SDK to develop and test gameplay (the Unity SDK also includes a Board Simulator). When ready for testing on real hardware, any retail Board device will work—there are no separate developer kits.

### Can I develop for Board if I am outside the US?

Absolutely. We already work with studios worldwide. However, physical testing requires a Board, which is currently available to ship in the U.S. Some customers have shipped to a U.S. address and brought their Boards abroad and others have used a U.S.-based freight forwarder. Note that Board ships with a 100-240V, 50/60Hz power adapter.

We're working on expanding availability internationally.

### What is Board Connect?

Board Connect is a new web-based server and interface for working with Board that replaces the Board Developer Bridge (`bdb`). It makes loading apps onto Board much simpler for both developers and players. Learn more in the Board Connect reference.

### How do I load apps onto Board?

Use Board Connect to load apps onto Board. To pair your Board with Board Connect:
1. On the Board go to Settings then System
2. Under Pair a Computer select Show Pairing Code
3. Follow on-screen instructions

To see your apps on Board, drag and drop an APK on the Board Connect site and your game will automatically load on the Board.
