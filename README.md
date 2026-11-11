# NekoSune VRChat Tools

VCC/VPM package listing for NekoSune's VRChat creator tools.

## Add to VRChat Creator Companion

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

Or open the [listing page](https://nekosuneprojects.github.io/vcc-packages/) and click **Add to VCC**.

## How this repository works

- `main` holds only the listing: `source.json`, the `Website/` page template and the listing workflow.
- Each package lives on its own branch, with `package.json` at the branch root.
- When a branch's `package.json` changes, that branch's workflow publishes a release ZIP and rebuilds the listing on `main`.
- **Build VCC Package Listing** collects every compatible release into `index.json` and deploys the GitHub Page.

## Setup

1. **Settings → Pages → Source: GitHub Actions.**
2. **Settings → Actions → General → Workflow permissions: Read and write.**
3. Push each package to a branch whose name matches the `branches:` trigger in its `.github/workflows/release-*.yml`.
4. To rebuild the page manually: **Actions → Build VCC Package Listing → Run workflow.**

## License

All rights reserved by NekoSune unless a package states otherwise.
