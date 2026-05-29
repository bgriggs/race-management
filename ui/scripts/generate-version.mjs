// Reads the repo-root version.json (the single source of truth, shared with the
// .NET build via Directory.Build.props) and writes a typed constant the Angular
// apps can import. Wired into the build:*/start:* npm scripts via pre-hooks.
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const versionFile = resolve(here, '..', '..', 'version.json');
const outFile = resolve(here, '..', 'shared-ui', 'src', 'version.ts');

const { version } = JSON.parse(readFileSync(versionFile, 'utf8'));
if (!version) {
  throw new Error(`No "version" field found in ${versionFile}`);
}

const content =
  `// GENERATED FILE — do not edit. Source: /version.json (run \`npm run generate:version\`).\n` +
  `export const APP_VERSION = '${version}';\n`;
writeFileSync(outFile, content);
console.log(`Generated shared-ui/src/version.ts -> ${version}`);
