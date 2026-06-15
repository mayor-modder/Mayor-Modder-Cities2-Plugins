#!/usr/bin/env node

const fs = require("node:fs");
const { spawn, spawnSync } = require("node:child_process");
const path = require("node:path");

const selfRoot = path.resolve(__dirname, "..");

function candidates() {
  const configured = process.env.CITIES2_CHIEF_OF_STAFF_PYTHON;
  const values = [];
  if (configured) values.push({ command: configured, args: [] });
  if (process.platform === "win32") values.push({ command: "py", args: ["-3"] });
  values.push({ command: "python3", args: [] });
  values.push({ command: "python", args: [] });
  return values;
}

function findPython() {
  for (const candidate of candidates()) {
    const result = spawnSync(candidate.command, [...candidate.args, "-c", "import sys; raise SystemExit(0 if sys.version_info >= (3, 11) else 1)"], {
      stdio: "ignore",
      windowsHide: true,
    });
    if (result.status === 0) return candidate;
  }
  return null;
}

function invocationForRoot(pluginRoot) {
  const baseEnv = { ...process.env, PYTHONDONTWRITEBYTECODE: "1" };
  const vendoredScript = path.join(pluginRoot, "vendor", "run_server.py");
  if (fs.existsSync(vendoredScript)) return { args: [vendoredScript], env: baseEnv };

  const vendoredServer = path.join(pluginRoot, "vendor", "chief_of_staff", "mcp_server.py");
  if (fs.existsSync(vendoredServer)) {
    const env = { ...baseEnv };
    env.PYTHONPATH = [path.join(pluginRoot, "vendor"), env.PYTHONPATH].filter(Boolean).join(path.delimiter);
    return { args: ["-m", "chief_of_staff.mcp_server"], env };
  }

  const sourceServer = path.join(pluginRoot, "chief_of_staff", "mcp_server.py");
  if (fs.existsSync(sourceServer)) {
    const env = { ...baseEnv };
    env.PYTHONPATH = [pluginRoot, env.PYTHONPATH].filter(Boolean).join(path.delimiter);
    return { args: ["-m", "chief_of_staff.mcp_server"], env };
  }

  return null;
}

function serverInvocation() {
  const roots = [selfRoot, process.env.PLUGIN_ROOT].filter(Boolean).map((value) => path.resolve(value));
  for (const root of roots) {
    const invocation = invocationForRoot(root);
    if (invocation) return invocation;
  }
  console.error(`Unable to locate Cities2 Chief of Staff server files. Checked: ${roots.join("; ")}.`);
  process.exit(1);
}

const python = findPython();
if (!python) {
  console.error("Cities2 Chief of Staff requires Python 3.11 or newer. Set CITIES2_CHIEF_OF_STAFF_PYTHON to a Python interpreter if it is not on PATH.");
  process.exit(127);
}

const invocation = serverInvocation();
const child = spawn(python.command, [...python.args, ...invocation.args, ...process.argv.slice(2)], {
  env: invocation.env,
  stdio: ["inherit", "inherit", "inherit"],
  windowsHide: true,
});

child.on("exit", (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }
  process.exit(code ?? 1);
});

child.on("error", (error) => {
  console.error(`Unable to start Cities2 Chief of Staff: ${error.message}`);
  process.exit(1);
});
