# CAMERA

`IsometricCamera.cs` owns an orthographic camera looking diagonally down onto the XZ battlefield. WASD/arrows smoothly pan, wheel zooms; BENCHMARK suggests initial framing from deployment size. Camera processing continues while simulation is paused. No soldier, UI or combat dependency.

The BENCHMARK left panel has an expanded **Camera** section with a live **Rotation** slider from 0° to 360° (1° steps) and a reset button. **0°** looks straight along the world Z axis, **45°** is the original diagonal camera view. The slider changes horizontal yaw only, preserving the original pitch, perspective, orthographic zoom and battlefield focus. Screen-relative WASD/arrow-key pan axes rotate with yaw so pan remains intuitive. This changes camera presentation only and does not rebuild the battle or alter any simulation data.
