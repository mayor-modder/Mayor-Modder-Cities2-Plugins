# Mayor Modder Cities2 Plugins

This repository is the shared Codex plugin marketplace for Mayor Modder's
Cities: Skylines II plugins.

It publishes installable plugin package snapshots for:

- Cities2 MCP and Modding Toolkit
- Cities2 Chief of Staff

The source projects remain separate:

- `mayor-modder/Cities2-MCP` owns wiki knowledge and modding workflows.
- `mayor-modder/Cities2-Chief-of-Staff` owns local city evidence analysis.

## Install

Add this repository as a Codex plugin marketplace:

```sh
codex plugin marketplace add mayor-modder/Mayor-Modder-Cities2-Plugins
```

Then open `/plugin` in Codex and install the plugin you want.

Restart Codex after installing or enabling plugin MCP tools.

## Marketplace

The marketplace manifest is:

```text
.agents/plugins/marketplace.json
```

Each plugin entry uses a local source path under:

```text
plugins/
```

## Updating Plugin Snapshots

Refresh each plugin payload from its source project release process, then verify
the packaged plugin manifests before publishing this catalog.

The catalog should not become the development home for either plugin. Keep
source changes in their source repositories, then copy the generated installable
plugin package into this repository.

