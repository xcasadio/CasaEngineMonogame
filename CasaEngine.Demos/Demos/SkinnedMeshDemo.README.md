# SkinnedMeshDemo

Purpose:
Validate skinned-mesh animation rendering, including dual quaternion skinning for twist-heavy poses.

What to check:
- The demo starts on the animated kid model.
- Cross-fades continue to work while dual quaternion skinning is enabled.
- Forearms, shoulders, and hips keep their volume better than linear blend skinning during rotations.

Automation:
- Set `CASAENGINE_START_DEMO=Skinned mesh demo`.
- Set `CASAENGINE_CAPTURE_SCREENSHOT_PATH` to capture a validation frame and exit automatically.