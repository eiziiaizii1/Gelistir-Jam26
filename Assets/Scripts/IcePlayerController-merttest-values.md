# IcePlayerController tuning from merttest.unity (archived)

`IcePlayerController.cs` was deleted on 2026-08-08. Before deletion, `merttest.unity`
still carried a tuned instance of it (committed by Mert Kaya in `22b57a9`). These are the
serialized values from that component, kept so the tuning is not lost if anyone wants to
port it onto `IceSlideController`.

The component was removed from `merttest.unity` at the same time.

| Field | Value |
|---|---|
| controlMode | 0 (MouseFlickSlap) |
| moveForce | 1.22 |
| maxSpeed | 1.79 |
| torqueForce | 0.61 |
| jumpForce | 0.23 |
| enableMelting | true |
| meltRatePerSecond | 0.015 |
| minMeltScaleRatio | 0.2 |
| initialVisualScale | (0.85, 0.85, 0.85) |
| enableMouseFlick | true |
| flickThreshold | 2.43 |
| flickImpulseForce | 4.03 |
| flickCooldown | 0.84 |
| maxVisualTiltAngle | 4.97 |
| tiltDamping | 0.47 |
| groundStickyForce | 25 |
| maxVerticalVelocity | 8 |
| autoForward | true |
| autoForwardForce | 0.85 |
| sphereCastRadius | 0.4 |
| groundCheckDistance | 0.6 |
| groundLayer | Everything |
| alignSpeed | 12 |
| normalSmoothSpeed | 12 |
| unparentVisualOnStart | true |

The full deleted source is recoverable from git history:

```bash
git show 157b1cf:Assets/Scripts/IcePlayerController.cs
```
