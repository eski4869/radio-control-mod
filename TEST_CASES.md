# Test Cases

This document lists the expected behavior covered by the Radio Control Mod unit tests.

| Phase | Test cases |
| --- | ---: |
| Lexical analysis | 25 |
| Semantic validation and execution plan generation | 27 |
| Frame execution | 10 |
| **Total** | **62** |

## Lexical Analysis

The lexer converts an input string into command tokens. `|` separates the expected tokens below.

| ID | Input | Expected result |
| --- | --- | --- |
| L01 | `jrl` | Success: `jr|l` |
| L02 | `jlr` | Success: `jl|r` |
| L03 | `jrr` | Success: `jr|r` |
| L04 | `jll` | Success: `jl|l` |
| L05 | `jr` and `j,r` | `jr` becomes `jr`; `j,r` becomes `j|r` |
| L06 | `j r l` | Success: `j|r|l` |
| L07 | `j,r,l` | Success: `j|r|l` |
| L08 | `jr l` | Success: `jr|l` |
| L09 | `j,rl` | Success: `j|r|l` |
| L10 | `jr35l20` | Success: `jr35|l20` |
| L11 | `j35r35` | Success: `j35|r35` |
| L12 | `jrl35` | Success: `jr|l35` |
| L13 | `jlr35` | Success: `jl|r35` |
| L14 | `jr, l20, w5, o, p,` | Success: `jr|l20|w5|o|p` |
| L15 | `JR35,W10,P` | Success: `jr35|w10|p` |
| L16 | `,,  jr35  ,,  l20  ,,` | Success: `jr35|l20` |
| L17 | `o35` | Failure: token `o` is completed, then `35` produces `invalid command` |
| L18 | `p1` | Failure: token `p` is completed, then `1` produces `invalid command` |
| L19 | empty string | Success: no tokens |
| L20 | spaces only | Success: no tokens |
| L21 | `,,,` | Success: no tokens |
| L22 | ` , , ` | Success: no tokens |
| L23 | `35j` | Failure: `invalid command` |
| L24 | `j,35` | Failure: `invalid command` |
| L25 | `hello` | Failure: `invalid command` |

## Semantic Validation And Execution Plan Generation

These tests receive completed lexer tokens. Jump variance is fixed by the test when deterministic output is required.

| ID | Token input or condition | Expected result |
| --- | --- | --- |
| S01 | `Jump(frames=null, text="j")` | Success: `j`, 35F |
| S02 | `JumpLeft(frames=null, text="jl")` | Success: `jl`, 35F |
| S03 | `JumpRight(frames=null, text="jr")` | Success: `jr`, 35F |
| S04 | `Left(frames=null, text="l")` | Success: `l`, 35F |
| S05 | `Right(frames=null, text="r")` | Success: `r`, 35F |
| S06 | `Wait(frames=null, text="w")` | Success: `w`, 35F |
| S07 | `Snake("o")`, `Boots("p")` | Two 1F steps; `Snake=true` and `Boots=true` respectively |
| S08 | `Jump` at 1F and 300F, variance `0` | Both accepted unchanged |
| S09 | `JumpLeft` at 1F and 300F, variance `0` | Both accepted unchanged |
| S10 | `JumpRight` at 1F and 300F, variance `0` | Both accepted unchanged |
| S11 | `Left` at 1F and 300F | Both accepted unchanged |
| S12 | `Right` at 1F and 300F | Both accepted unchanged |
| S13 | `Wait` at 1F and 300F | Both accepted unchanged |
| S14 | `Jump(frames=0, text="j0")` | Failure: `frames must be between 1 and 300: j0` |
| S15 | `Left(frames=0, text="l0")` | Failure: `frames must be between 1 and 300: l0` |
| S16 | `Wait(frames=0, text="w0")` | Failure: `frames must be between 1 and 300: w0` |
| S17 | `Jump(frames=301, text="j301")` | Failure: `frames must be between 1 and 300: j301` |
| S18 | `Left(frames=301, text="l301")` | Failure: `frames must be between 1 and 300: l301` |
| S19 | `Wait(frames=301, text="w301")` | Failure: `frames must be between 1 and 300: w301` |
| S20 | 32 `Snake` tokens | Success: 32 steps |
| S21 | 33 `Snake` tokens | Failure: `command count must be 32 or fewer` |
| S22 | `w300|w300|w300|w300` | Success: total 1200F |
| S23 | `w300|w300|w300|w300|o` | Failure: total 1201F, `total frames must be 1200 or fewer` |
| S24 | `Jump(frames=35, text="j35")` | Success: 35F; variance sampler must not be called |
| S25 | `JumpRight(frames=34, text="jr34")`, injected variance `+2` | Success: 36F |
| S26 | `j1` with variance `-1000`; `j300` with variance `+1000` | Results are clamped to 1F and 300F |
| S27 | All eight command kinds | Generated Left, Right, Jump, Boots, and Snake flags match the table below |

### Generated Input Flags

| Command | Left | Right | Jump | Boots | Snake |
| --- | ---: | ---: | ---: | ---: | ---: |
| `j` | false | false | true | false | false |
| `jl` | true | false | true | false | false |
| `jr` | false | true | true | false | false |
| `l` | true | false | false | false | false |
| `r` | false | true | false | false | false |
| `w` | false | false | false | false | false |
| `o` | false | false | false | false | true |
| `p` | false | false | false | true | false |

## Frame Execution

The frame sequence is observed before calling `AdvanceOneFrame()`, matching the runtime update order. `release` means that `ActiveStep` is `null` and all inputs are released.

| ID | Program input | Expected result |
| --- | --- | --- |
| F01 | No steps | Already complete; no active step; 0 remaining frames; status `Done` |
| F02 | `r2|j1`, source `r2 j1` | Initial state: step 1/2, 2F remaining, active `r`, status `r 2f` |
| F03 | `r1` | `r|release|Complete` |
| F04 | `r3` | `r|r|r|release|Complete` |
| F05 | `r2|j2` | `r|r|release|j|j|release|Complete` |
| F06 | `j1|jl1|jr1|l1|r1|w1|o|p` | Every action is followed by exactly one `release` frame |
| F07 | `r1`, immediately after the input frame | Releasing; no active step; status `release 1f` |
| F08 | `r1|j1` | `j` starts only after the release frame; step 2/2 with 1F remaining |
| F09 | `r2` | Status sequence: `r 2f` → `r 1f` → `release 1f` → `Done` |
| F10 | Completed `r1` program, then advance twice | Remains complete with no active step and status `Done` |

## Run The Tests

```text
dotnet test RadioControlMod.Tests/RadioControlMod.Tests.csproj
```
