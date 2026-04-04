---
status: Stable
updated: 2026-04-04 03:00h
---

# Publishing to the Visual Studio Marketplace

This extension uses the **VisualStudio.Extensibility SDK** (out-of-proc model) and requires **VS 2022 17.9+**.

## 1. Create a Publisher Account

- Go to the [Visual Studio Marketplace Publishing Portal](https://marketplace.visualstudio.com/manage)
- Sign in with a Microsoft account
- Create a **publisher** (e.g., `Ardimedia`) -- this is your publisher ID

## 2. Create a Personal Access Token (PAT)

- Go to `dev.azure.com` > User Settings > Personal Access Tokens
- Create a token with scope **Marketplace > Manage**
- Save the token securely -- you will need it for publishing
- Add it as a GitHub secret: `VS_MARKETPLACE_PAT`

## 3. Build the VSIX

```bash
dotnet build src/taskrunnerextended.slnx -c Release
```

This produces a `.vsix` file in `src/TaskRunnerExtended/bin/Release/net10.0/`.

## 4. Publish via Release Pipeline

The automated way (recommended):

1. Bump the version in `TaskRunnerExtended.csproj`
2. Update `CHANGELOG.md` and `README.md`
3. Commit and push
4. Create and push a tag: `git tag v0.1.0 && git push origin v0.1.0`
5. The `release.yml` workflow will:
   - Build and test
   - Find the VSIX
   - Create a GitHub Release
   - Publish to VS Marketplace via `VsixPublisherAction`

## 5. Manual Publish (alternative)

```bash
# Install VsixPublisher if not already installed
dotnet tool install -g Microsoft.VSCE

# Publish
npx @anthropic-ai/vsce publish -p <PAT> --vsix src/TaskRunnerExtended/bin/Release/net10.0/TaskRunnerExtended.vsix
```

Or use the VS Marketplace web upload:
1. Go to https://marketplace.visualstudio.com/manage
2. Select your publisher
3. Click "New Extension" > "Visual Studio"
4. Upload the `.vsix` file

## Checklist Before Publishing

- [ ] Version bumped in `TaskRunnerExtended.csproj`
- [ ] `CHANGELOG.md` updated with new version entry
- [ ] `README.md` features list is current
- [ ] `.claude/overview.md` (Marketplace description) is current
- [ ] All tests passing: `dotnet test --filter "TestCategory=Unit"`
- [ ] Extension tested manually in VS (install VSIX, open solution, verify tree)
- [ ] Icons present in all sizes (16, 32, 48, 96, 128, 256)
- [ ] `publishManifest.json` has correct tags and categories
