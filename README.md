# Radio Control Mod

Radio Control Mod receives short frame-based input programs through JumpKingHttpCommandBroker and plays them back in Jump King.

## Broker Target

`radio_control`

Single-button menu-style input uses a separate target:

`menu_control`

| Command | Button |
| --- | --- |
| `up` | Up |
| `down` | Down |
| `space` | Jump and confirm |
| `confirm` | Confirm |
| `jump` | Jump |
| `esc` | Pause and cancel |
| `pause` | Pause |
| `cancel` | Cancel |

Each command produces one pressed-button input and can be used at any point.

## Command Format

Numbers are frame counts. Spaces and commas separate commands. Commands can also be written consecutively when their boundaries are unambiguous.

| Command | Meaning |
| --- | --- |
| `j35` | Hold jump for 35 frames |
| `jr35` | Hold jump + right for 35 frames |
| `jl35` | Hold jump + left for 35 frames |
| `r10` | Hold right for 10 frames |
| `l10` | Hold left for 10 frames |
| `w60` | Wait for 60 frames |
| `o` | Press Snake |
| `p` | Press Boots |

`j`, `jr`, `jl`, `r`, `l`, and `w` without a number use 35 frames.
`o` and `p` do not accept a frame count.

Only `j` combines with an immediately following `l` or `r`. For example, `jrl` is parsed as `jr` followed by `l`, while `j r l` is parsed as three separate commands.

Example:

```text
jr35 w10 l5 w2 j20
jr35w10l5w2j20
```

## Processing Pipeline

A received command passes through five phases before it becomes game input.

| Phase | Responsibility | Example |
| --- | --- | --- |
| 1. Command reception | `BrokerCommandClient` dequeues one command string from the `radio_control` target | `jr35, l20` |
| 2. Lexical analysis | `RadioCommandLexer` splits the string into command tokens without executing them | `jr35` / `l20` |
| 3. Semantic validation | `RadioCommandParser` applies defaults and validates command, frame, and program limits | right jump for 35F / left for 20F |
| 4. Execution plan generation | Valid tokens are converted into `RadioStep` objects containing only the required input flags and frame count | `Jump + Right, 35F` |
| 5. Frame execution | `RadioProgram` applies each step, releases all inputs for one frame, and then advances to the next step | input 35F / release 1F / input 20F / release 1F |

The lexer only determines command boundaries. For example, `jr` is one token, while `j,r` becomes two tokens: `j` and `r`. Validation and game-input behavior belong to the later phases.

## Limits

| Limit | Value |
| --- | --- |
| Commands per message | 32 |
| Jump frames per command | 300 |
| Move frames per command | 300 |
| Wait frames per command | 300 |
| Total frames per message | 1200 |

If any command is invalid, the whole message is rejected.

## HTTP Example

```text
http://127.0.0.1:8081/command?target=radio_control&command=jr35%20w10%20l5
```

The HTTP server is provided by JumpKingHttpCommandBroker.

The optional `user` parameter is used for player routing:

```text
http://127.0.0.1:8081/command?target=radio_control&user=alice&command=jr35
http://127.0.0.1:8081/command?target=radio_control&user=nancy&command=jl35
```

Older requests without `user` continue to control Player 1 in single-player mode.

Menu example:

```text
http://127.0.0.1:8081/command?target=menu_control&command=down
http://127.0.0.1:8081/command?target=menu_control&command=space
http://127.0.0.1:8081/command?target=menu_control&command=confirm
http://127.0.0.1:8081/command?target=menu_control&command=jump
http://127.0.0.1:8081/command?target=menu_control&command=esc
http://127.0.0.1:8081/command?target=menu_control&command=pause
http://127.0.0.1:8081/command?target=menu_control&command=cancel
```

## Settings

`eski4869.RadioControlMod.Settings.xml` is generated next to the mod.

```xml
<RadioControlPreferences>
  <IsEnabled>true</IsEnabled>
  <IsDebugEnabled>false</IsDebugEnabled>
  <JumpFrameLaplaceAlpha>0.1</JumpFrameLaplaceAlpha>
  <MultiplayerEnabled>false</MultiplayerEnabled>
  <SingleMode>
    <Player1Users>*</Player1Users>
  </SingleMode>
  <MultiplayerMode>
    <Player1Users>[a-m]*</Player1Users>
    <Player2Users>[n-z]*</Player2Users>
  </MultiplayerMode>
</RadioControlPreferences>
```

`Player1Users` and `Player2Users` are comma-separated allow lists. They support exact
names (`alice`), prefix wildcards (`eski*`), first-character ranges (`[a-m]*`), and
the all-users wildcard (`*`). Matching is case-insensitive.

In single-player mode, a request without `user` controls Player 1. In multiplayer
mode, `user` is required. If a name matches both multiplayer lists, the same command
is queued for both players. A name that matches neither list is ignored.

Turning `Multiplayer Mode` on reloads this settings file, so the allow lists can be
changed without restarting the game. Invalid settings keep multiplayer disabled and
show an error in the Radio Control overlay.

`JumpFrameLaplaceAlpha` controls jump-frame variance for `j`, `jr`, and `jl`.
`35` frames stays exact.

`Radio Control`, `Radio Debug`, and `Multiplayer Mode` can be toggled from the main
menu or pause menu.

## Multiplayer Mode

Multiplayer mode creates a second player in the right half of a map designed as two
parallel 240-pixel lanes. Player 1 is drawn in the left half and Player 2 in the
right half; each half follows its own vertical screen. The first player to satisfy a
native ending condition wins, and the normal Jump King ending flow is then used.

Player 2 currently receives movement and jump commands (`j`, `jl`, `jr`, `l`, `r`,
and `w`). Player-specific Snake and Boots activation is not provided because those
systems read the game's shared controller state rather than `InputComponent`.

## Tests

The tests cover lexer command boundaries, semantic validation, generated input flags, and frame-by-frame execution without loading Jump King.

[Test case matrix](TEST_CASES.md)

```text
dotnet test RadioControlMod.Tests/RadioControlMod.Tests.csproj
```
