// Builds a valid, empty VPM listing (index.json) and renders the Website templates
// with zero packages. Used until the repository has at least one package release,
// because vrchat-community/package-list-action crashes when a repo has no releases.
//
// Usage: node .github/scripts/build-empty-listing.mjs <source.json> <websiteDir>

import fs from "node:fs";
import path from "node:path";

const [, , sourcePath = "source.json", websiteDir = "Website"] = process.argv;
const source = JSON.parse(fs.readFileSync(sourcePath, "utf8"));

// ---- index.json (VPM repository listing format) ----
const listing = {
  name: source.name,
  id: source.id,
  url: source.url,
  author: source.author?.name ?? "",
  packages: {},
};
fs.writeFileSync(path.join(websiteDir, "index.json"), JSON.stringify(listing, null, 2) + "\n");

// ---- render the Scriban-style templates with no packages ----
const data = {
  listingInfo: {
    Name: source.name,
    Id: source.id,
    Url: source.url,
    Description: source.description ?? "",
    Author: { Name: source.author?.name ?? "", Email: source.author?.email ?? "", Url: source.author?.url ?? "" },
    InfoLink: { Url: source.infoLink?.url ?? "", Text: source.infoLink?.text ?? "" },
  },
  packages: [],
};

const lookup = (scope, expr) => expr.split(".").reduce((o, k) => (o == null ? undefined : o[k]), scope);

// Minimal renderer for the subset used by Website/: values, if/else/end, for/end,
// and inline `if A; B; else; "C"; end;`.
function render(template, scope) {
  const tokens = template.split(/(\{\{~?[\s\S]*?~?\}\})/);
  let i = 0;

  function block(sc, nested) {
    let out = "";
    while (i < tokens.length) {
      const token = tokens[i++];
      const tag = token.match(/^\{\{~?\s*([\s\S]*?)\s*~?\}\}$/);
      if (!tag) { out += token; continue; }
      const expr = tag[1];
      let m;

      if (expr === "end" || expr === "else") {
        if (nested) return [out, expr];
        continue;
      }
      if ((m = expr.match(/^for (\w+) in ([\w.]+)$/))) {
        const items = lookup(sc, m[2]) || [];
        const start = i;
        if (items.length === 0) { block(sc, true); continue; }
        let after = i;
        for (const item of items) { i = start; out += block({ ...sc, [m[1]]: item }, true)[0]; after = i; }
        i = after;
        continue;
      }
      if ((m = expr.match(/^if ([\w.]+);?$/))) {
        const truthy = !!lookup(sc, m[1]);
        const [whenTrue, stop] = block(sc, true);
        const whenFalse = stop === "else" ? block(sc, true)[0] : "";
        out += truthy ? whenTrue : whenFalse;
        continue;
      }
      if ((m = expr.match(/^if ([\w.]+); ([\w.]+); else; "([^"]*)"; end;$/))) {
        out += lookup(sc, m[1]) ? lookup(sc, m[2]) : m[3];
        continue;
      }
      const value = lookup(sc, expr);
      out += value == null ? "" : String(value);
    }
    return [out, null];
  }

  return block(scope, false)[0];
}

for (const file of fs.readdirSync(websiteDir)) {
  if (!/\.(html|js)$/.test(file)) continue;
  const full = path.join(websiteDir, file);
  fs.writeFileSync(full, render(fs.readFileSync(full, "utf8"), data));
}

console.log(`Wrote empty listing for ${source.id} -> ${path.join(websiteDir, "index.json")}`);
