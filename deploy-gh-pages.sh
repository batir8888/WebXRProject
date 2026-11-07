#!/usr/bin/env bash
# Publishes Builds/WebGL to the gh-pages branch (GitHub Pages serves it over HTTPS).
set -euo pipefail
cd "$(dirname "$0")"
REMOTE=$(git remote get-url origin)
TMP=$(mktemp -d)
cp -r Builds/WebGL/. "$TMP/"
touch "$TMP/.nojekyll"
cd "$TMP"
git init -q -b gh-pages
git add -A
git commit -q -m "Deploy WebGL build $(date -u +%Y-%m-%dT%H:%M:%SZ)"
git push -f "$REMOTE" gh-pages
rm -rf "$TMP"
echo "Deployed to gh-pages"
