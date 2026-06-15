# Mayor Modder Cities2 Plugins

This repository is the shared Claude and Codex plugin marketplace for Mayor Modder's
Cities: Skylines II plugins.

It publishes installable plugin package snapshots for:

- Cities2 MCP and Modding Toolkit
- Cities2 Chief of Staff

The source projects remain separate:

- `mayor-modder/Cities2-MCP` owns wiki knowledge and modding workflows.
- `mayor-modder/Cities2-Chief-of-Staff` owns local city evidence analysis.

## Install

### Claude

Add this repository as a Claude plugin marketplace:

```text
/plugin marketplace add mayor-modder/Mayor-Modder-Cities2-Plugins
```

Then install the plugin you want from Claude Code or Claude desktop.

### Codex CLI

Add this repository as a Codex plugin marketplace:

```sh
codex plugin marketplace add mayor-modder/Mayor-Modder-Cities2-Plugins
```

Then install the plugin you want from the marketplace.

### Codex app

Open **Plugins** in the sidebar, click the down arrow next to the `+` button, choose **Add marketplace**, then enter:

```text
mayor-modder/Mayor-Modder-Cities2-Plugins
```

Restart Codex after installing or enabling plugin MCP tools.

## Marketplace

The Codex marketplace manifest is:

```text
.agents/plugins/marketplace.json
```

The Claude marketplace manifest is:

```text
.claude-plugin/marketplace.json
```

Codex plugin entries use local source paths under:

```text
plugins/
```

Claude plugin entries use local source paths under:

```text
integrations/anthropic/
```

## Updating Plugin Snapshots

Refresh each plugin payload from its source project release process, then verify
the packaged plugin manifests before publishing this catalog.

The catalog should not become the development home for either plugin. Keep
source changes in their source repositories, then copy the generated installable
plugin package into this repository.

