# Publishing to NuGet.org

This guide explains how to publish the AnoiKeyedLock package to NuGet.org.

## Prerequisites

1. **NuGet Account**: Create an account at [nuget.org](https://www.nuget.org/)
2. **API Key**: Generate an API key from your NuGet account settings
   - Go to https://www.nuget.org/account/apikeys
   - Click "Create"
   - Give it a name (e.g., "AnoiKeyedLock Publishing")
   - Select appropriate scopes (Push new packages and package versions)
   - Select the package ID pattern (e.g., AnoiKeyedLock*)
   - Set expiration as needed

## Build the Package

The package is already configured in `AnoiKeyedLock.csproj`. To create a release build:

```bash
dotnet pack AnoiKeyedLock\AnoiKeyedLock.csproj --configuration Release --output .\nupkgs
```

This will create:
- `AnoiKeyedLock.{version}.nupkg` - Main package
- `AnoiKeyedLock.{version}.snupkg` - Symbol package for debugging

## Verify Package Contents

Before publishing, inspect the package contents:

```bash
# Using NuGet CLI (if installed)
nuget verify -All .\nupkgs\AnoiKeyedLock.1.0.0.nupkg

# Or extract and inspect manually
Expand-Archive .\nupkgs\AnoiKeyedLock.1.0.0.nupkg -DestinationPath .\package-contents
```

Check that the package includes:
- ✅ Compiled DLL (`lib/netstandard2.1/AnoiKeyedLock.dll`)
- ✅ XML documentation (`lib/netstandard2.1/AnoiKeyedLock.xml`)
- ✅ README.md
- ✅ LICENSE file
- ✅ Correct metadata in `.nuspec`

## Publish Manually

### Option 1: Using dotnet CLI (Recommended)

```bash
dotnet nuget push .\nupkgs\AnoiKeyedLock.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

### Option 2: Using NuGet.exe

```bash
nuget push .\nupkgs\AnoiKeyedLock.1.0.0.nupkg -ApiKey YOUR_API_KEY -Source https://api.nuget.org/v3/index.json
```

### Option 3: Upload via Web Interface

1. Go to https://www.nuget.org/packages/manage/upload
2. Browse and select `AnoiKeyedLock.1.0.0.nupkg`
3. Review the package details
4. Click "Submit"

## Automated Publishing (GitHub Actions)

The repository includes a GitHub Actions workflow (`.github/workflows/build-and-publish.yml`) that automatically:
1. Builds the project
2. Runs tests
3. Creates NuGet packages
4. Publishes to NuGet.org when you push a version tag

### Setup GitHub Actions

1. Add your NuGet API key as a GitHub secret:
   - Go to your repository settings
   - Navigate to Secrets and variables → Actions
   - Click "New repository secret"
   - Name: `NUGET_API_KEY`
   - Value: Your NuGet API key

2. Create and push a version tag:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

The workflow will automatically build, test, and publish the package.

## Version Management

Update the version in `AnoiKeyedLock\AnoiKeyedLock.csproj`:

```xml
<Version>1.0.0</Version>
```

Follow [Semantic Versioning](https://semver.org/):
- **Major** (1.x.x): Breaking changes
- **Minor** (x.1.x): New features, backward compatible
- **Patch** (x.x.1): Bug fixes, backward compatible

## Pre-release Versions

For pre-release versions, use suffixes:

```xml
<Version>1.0.0-beta.1</Version>
<Version>1.0.0-rc.1</Version>
<Version>1.0.0-preview.1</Version>
```

Publish with:
```bash
dotnet nuget push .\nupkgs\AnoiKeyedLock.1.0.0-beta.1.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Post-Publishing Checklist

After publishing, verify:

- [ ] Package appears on nuget.org
- [ ] README displays correctly
- [ ] Installation command works: `dotnet add package AnoiKeyedLock`
- [ ] Symbol package is available for debugging
- [ ] Update GitHub release with release notes
- [ ] Update CHANGELOG.md with the release date
- [ ] Tweet/announce the release (optional)

## Package Validation

NuGet.org performs validation that may take a few minutes:
- Malware scanning
- Dependency validation
- Metadata validation

You'll receive an email when validation completes.

## Troubleshooting

### Error: Package already exists
NuGet doesn't allow replacing packages. Increment the version number and rebuild.

### Error: API key invalid
Ensure your API key hasn't expired and has the correct permissions.

### Symbol package not showing
Symbol packages may take longer to validate. Check back after a few hours.

### Build warnings about nullable reference types
The current build shows some nullable reference type warnings. Consider fixing these before publishing:
- `ServiceCollectionExtensions.cs(19,43)`
- `KeyedLock.cs(92,35)`
- `KeyedLock.cs(377,52)`

## Support

For issues or questions:
- GitHub Issues: https://github.com/eeaquino/AnoiKeyedLock/issues
- NuGet Support: https://www.nuget.org/policies/Contact

## License

This package is published under the MIT License. See the LICENSE file for details.
