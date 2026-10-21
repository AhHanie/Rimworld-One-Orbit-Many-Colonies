#!/usr/bin/env node
"use strict";

/**
 * Deploys a RimWorld mod into the game's Mods folder, copying only what the game
 * loads (About, Defs, Patches, Languages, Textures, Sounds, Assemblies, version
 * and LoadFolders.xml folders, ...) and leaving out source code, build output,
 * git/editor files and everything else that is not RimWorld content.
 *
 * Works for any mod: the destination folder name and the safety check both come
 * from the mod's own About/About.xml. The destination is mirrored, i.e. deleted
 * and recreated, so files removed from the mod disappear from the game too.
 *
 * Usage:
 *   node deploy.js [modDir] [--mods-dir "<dir>"] [--dry-run]
 *
 *   modDir      Mod to deploy. Defaults to the folder containing this script, so a
 *               copy placed in a mod's root deploys that mod.
 *   --mods-dir  Target RimWorld Mods folder. Defaults to the Steam install.
 *   --dry-run   Print what would be copied without touching the destination.
 */

const fs = require("fs/promises");
const path = require("path");

const DEFAULT_MODS_DIR =
  "C:\\Program Files (x86)\\Steam\\steamapps\\common\\RimWorld\\Mods";

const CONTENT_FOLDERS = [
  "about",
  "assemblies",
  "common",
  "defs",
  "languages",
  "patches",
  "sounds",
  "textures",
];
const VERSION_FOLDER = /^v?\d+\.\d+$/i;
const LOAD_FOLDERS_FILE = "loadfolders.xml";

const EXCLUDED_DIRECTORIES = new Set([
  "source",
  "obj",
  "bin",
  "node_modules",
  ".git",
  ".vs",
  ".vscode",
  ".idea",
  ".claude",
  ".github",
]);
const EXCLUDED_FILES = new Set([
  ".gitkeep",
  ".gitignore",
  ".gitattributes",
  ".ds_store",
  "thumbs.db",
  "desktop.ini",
]);
const EXCLUDED_SUFFIXES = [
  ".cs",
  ".csproj",
  ".sln",
  ".slnx",
  ".user",
  ".pdb",
  ".deps.json",
  ".fingerprint",
  ".psd",
  ".xcf",
  ".kra",
  ".blend",
  ".aseprite",
  ".log",
  ".tmp",
  ".bak",
];

function parseArgs(argv) {
  const args = { modDir: undefined, modsDir: undefined, dryRun: false };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--dry-run") {
      args.dryRun = true;
    } else if (arg === "--mods-dir") {
      args.modsDir = argv[++i];
      if (!args.modsDir) throw new Error("--mods-dir requires a value.");
    } else if (arg.startsWith("--")) {
      throw new Error(`Unknown flag ${arg}.`);
    } else if (args.modDir === undefined) {
      args.modDir = arg;
    } else {
      throw new Error(`Unexpected argument "${arg}".`);
    }
  }
  return args;
}

async function pathExists(target) {
  try {
    await fs.access(target);
    return true;
  } catch {
    return false;
  }
}

function isSameOrInside(candidate, container) {
  const relative = path.relative(container, candidate);
  return (
    relative === "" ||
    (!relative.startsWith("..") && !path.isAbsolute(relative))
  );
}

function xmlUnescape(text) {
  return text
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}

function readTag(xml, tag) {
  const match = new RegExp(`<${tag}>([^<]*)</${tag}>`).exec(xml);
  return match ? xmlUnescape(match[1].trim()) : "";
}

async function readAbout(aboutPath) {
  if (!(await pathExists(aboutPath))) {
    throw new Error(`Missing ${aboutPath}; is this a RimWorld mod folder?`);
  }
  const xml = await fs.readFile(aboutPath, "utf8");
  const name = readTag(xml, "name");
  const packageId = readTag(xml, "packageId");
  if (!name || !packageId) {
    throw new Error(`${aboutPath} must contain <name> and <packageId>.`);
  }
  return { name, packageId };
}

function toFolderName(modName) {
  const cleaned = modName
    .replace(/[<>:"/\\|?*\x00-\x1f]/g, "")
    .replace(/[. ]+$/, "")
    .trim();
  if (!cleaned)
    throw new Error(`Cannot derive a folder name from mod name "${modName}".`);
  return cleaned;
}

function isExcludedFile(name) {
  const lower = name.toLowerCase();
  return (
    EXCLUDED_FILES.has(lower) ||
    EXCLUDED_SUFFIXES.some((suffix) => lower.endsWith(suffix))
  );
}

async function loadFolderRoots(modDir, loadFoldersName) {
  const roots = new Set();
  if (!loadFoldersName) return roots;
  const xml = await fs.readFile(path.join(modDir, loadFoldersName), "utf8");
  for (const match of xml.matchAll(/<li[^>]*>([^<]*)<\/li>/g)) {
    const first = match[1].trim().split(/[\\/]/).filter(Boolean)[0];
    if (first) roots.add(first.toLowerCase());
  }
  return roots;
}

async function findDeployables(modDir) {
  const entries = await fs.readdir(modDir, { withFileTypes: true });
  const loadFoldersEntry = entries.find(
    (e) => e.isFile() && e.name.toLowerCase() === LOAD_FOLDERS_FILE,
  );
  const referenced = await loadFolderRoots(
    modDir,
    loadFoldersEntry && loadFoldersEntry.name,
  );

  const folders = [];
  const files = [];
  for (const entry of entries) {
    const lower = entry.name.toLowerCase();
    if (entry.isDirectory()) {
      if (EXCLUDED_DIRECTORIES.has(lower)) continue;
      if (
        CONTENT_FOLDERS.includes(lower) ||
        VERSION_FOLDER.test(entry.name) ||
        referenced.has(lower)
      ) {
        folders.push(entry.name);
      }
    } else if (entry.isFile() && lower === LOAD_FOLDERS_FILE) {
      files.push(entry.name);
    }
  }
  return { folders: folders.sort(), files };
}

async function findCompiledProjects(modDir, folders) {
  const missing = [];
  for (const folder of folders) {
    const sourceDir = path.join(modDir, folder, "source");
    if (!(await pathExists(sourceDir))) continue;
    const hasProject = (await fs.readdir(sourceDir)).some((name) =>
      name.toLowerCase().endsWith(".csproj"),
    );
    if (!hasProject) continue;
    if (!(await containsAssembly(path.join(modDir, folder))))
      missing.push(folder);
  }
  return missing;
}

async function containsAssembly(dir, insideAssemblies = false) {
  for (const entry of await fs.readdir(dir, { withFileTypes: true })) {
    const lower = entry.name.toLowerCase();
    if (entry.isDirectory()) {
      if (EXCLUDED_DIRECTORIES.has(lower)) continue;
      if (
        await containsAssembly(
          path.join(dir, entry.name),
          insideAssemblies || lower === "assemblies",
        )
      )
        return true;
    } else if (insideAssemblies && lower.endsWith(".dll")) {
      return true;
    }
  }
  return false;
}

async function assertSafeDestination(destination, modDir, packageId) {
  if (destination === path.parse(destination).root) {
    throw new Error("Refusing to clear a filesystem root.");
  }
  if (isSameOrInside(modDir, destination)) {
    throw new Error(
      "Refusing to clear the mod folder or one of its ancestors.",
    );
  }
  if (isSameOrInside(destination, modDir)) {
    throw new Error("Refusing to deploy inside the mod folder.");
  }
  if (!(await pathExists(destination))) return;

  if ((await fs.readdir(destination)).length === 0) return;

  const aboutPath = path.join(destination, "About", "About.xml");
  if (!(await pathExists(aboutPath))) {
    throw new Error(
      `Refusing to clear "${destination}": it is not an existing deployment of this mod.`,
    );
  }
  const existing = readTag(await fs.readFile(aboutPath, "utf8"), "packageId");
  if (existing !== packageId) {
    throw new Error(
      `Refusing to clear "${destination}": its About.xml packageId is "${existing}", not "${packageId}".`,
    );
  }
}

async function copyDirectory(source, target, copied, dryRun) {
  if (!dryRun) await fs.mkdir(target, { recursive: true });

  for (const entry of await fs.readdir(source, { withFileTypes: true })) {
    const sourcePath = path.join(source, entry.name);
    const targetPath = path.join(target, entry.name);

    if (entry.isDirectory()) {
      if (!EXCLUDED_DIRECTORIES.has(entry.name.toLowerCase())) {
        await copyDirectory(sourcePath, targetPath, copied, dryRun);
      }
    } else if (entry.isFile() && !isExcludedFile(entry.name)) {
      if (!dryRun) await fs.copyFile(sourcePath, targetPath);
      copied.push(targetPath);
    }
  }
}

async function deploy() {
  const args = parseArgs(process.argv.slice(2));
  const modDir = path.resolve(args.modDir || __dirname);
  const modsDir = path.resolve(args.modsDir || DEFAULT_MODS_DIR);

  const about = await readAbout(path.join(modDir, "About", "About.xml"));
  const destination = path.join(
    modsDir,
    toFolderName(about.name.replace(",", "")),
  );
  await assertSafeDestination(destination, modDir, about.packageId);

  const { folders, files } = await findDeployables(modDir);
  if (folders.length === 0)
    throw new Error("No RimWorld content folders found.");

  const unbuilt = await findCompiledProjects(modDir, folders);
  if (unbuilt.length > 0) {
    throw new Error(
      `No built assembly found for ${unbuilt.join(", ")}; build ${unbuilt.length === 1 ? "it" : "them"} first.`,
    );
  }

  const copied = [];
  if (!args.dryRun) {
    await fs.rm(destination, { recursive: true, force: true });
    await fs.mkdir(destination, { recursive: true });
  }
  for (const folder of folders) {
    await copyDirectory(
      path.join(modDir, folder),
      path.join(destination, folder),
      copied,
      args.dryRun,
    );
  }
  for (const file of files) {
    if (!args.dryRun)
      await fs.copyFile(path.join(modDir, file), path.join(destination, file));
    copied.push(path.join(destination, file));
  }

  if (args.dryRun) {
    for (const file of copied) console.log(path.relative(destination, file));
    console.log(
      `\nDry run: would deploy ${copied.length} files of "${about.name}" to: ${destination}`,
    );
  } else {
    console.log(
      `Deployed "${about.name}" (${copied.length} files; ${folders.join(", ")}) to: ${destination}`,
    );
  }
}

deploy().catch((error) => {
  console.error(`Deploy failed: ${error.message}`);
  process.exitCode = 1;
});
