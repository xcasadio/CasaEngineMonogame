# SkinnedMeshDemo

Purpose:
Compare skinned-mesh animation rendering between linear blend skinning and dual quaternion skinning.

What to check:
- The left character stays on linear blend skinning.
- The right character uses dual quaternion skinning.
- Both characters play the same animation transitions so shape differences stay easy to compare.

Automation:
- Set `CASAENGINE_START_DEMO=Skinned mesh demo`.
- Set `CASAENGINE_CAPTURE_SCREENSHOT_PATH` to capture a validation frame and exit automatically.