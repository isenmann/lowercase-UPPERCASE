// Copies the game's fonts into public/fonts. They are third-party (SIL Open Font License) and stay out of git, like
// Assets/Fonts itself, so a fresh clone gets them from the Unity project's folder.
import { copyFileSync, existsSync, mkdirSync, readdirSync } from "node:fs";

const source = new URL("../../Assets/Fonts/", import.meta.url);
const target = new URL("../public/fonts/", import.meta.url);
const wanted = ["SpaceMono-Regular.ttf", "SpaceMono-Bold.ttf", "ArchivoBlack-Regular.ttf", "SpaceGrotesk-Medium.ttf"];

const missing = wanted.filter(name => !existsSync(new URL(name, source)));
if (missing.length > 0) {
  console.error(`Missing in Assets/Fonts: ${missing.join(", ")}. Add the game's fonts there first.`);
  process.exit(1);
}

mkdirSync(target, { recursive: true });
const licences = readdirSync(source).filter(name => /^OFL-.*\.txt$/.test(name));
for (const name of [...wanted, ...licences]) {
  copyFileSync(new URL(name, source), new URL(name, target));
}

console.log(`Copied ${wanted.length} fonts and ${licences.length} licences to public/fonts.`);
