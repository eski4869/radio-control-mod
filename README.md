# Radio Control Mod

Radio Control Mod receives short frame-based input programs through JumpKingHttpCommandBroker and plays them back in Jump King.

## Broker Target

`radio_control`

## Command Format

Commands are separated by spaces. Numbers are frame counts.

| Command | Meaning |
| --- | --- |
| `j35` | Hold jump for 35 frames |
| `jr35` | Hold jump + right for 35 frames |
| `jl35` | Hold jump + left for 35 frames |
| `r10` | Hold right for 10 frames |
| `l10` | Hold left for 10 frames |
| `w60` | Wait for 60 frames |

`j`, `r`, and `l` without a number use 35 frames.

Example:

```text
jr35 w10 l5 w2 j20
```

## Limits

| Limit | Value |
| --- | --- |
| Commands per message | 20 |
| Frames per command | 600 |
| Total frames per message | 1800 |

If any token is invalid, the whole message is rejected.

## HTTP Example

```text
http://127.0.0.1:8081/command?target=radio_control&command=jr35%20w10%20l5
```

The HTTP server is provided by JumpKingHttpCommandBroker.
