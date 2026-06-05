> Source: https://docs.dev.board.fun/unity/getting-started/upgrading

# Upgrading the SDK

How to update the Board Unity SDK in an existing project.

1. Check the Changelog for breaking changes
2. Download the latest SDK `.tgz` from the developer portal
3. In Unity, open Window > Package Manager > + > Add package from tarball… and select the new `.tgz` file
4. Run Board > Configure Unity Project… to pick up any new project settings
5. Build and test your project

Tip: The Package Manager replaces the previous SDK version in-place. You don't need to remove the old version first.

## Troubleshooting

If you see compilation errors after updating:

- Check the Changelog for renamed or removed APIs and update your code accordingly
- Run Board > Configure Unity Project… to ensure new required settings are applied
