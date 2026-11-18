#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
archive="$PWD/artifacts/minesweeper-wwwroot.tar.gz"
test -s "$archive"
remote=origin
ref=refs/heads/gh-pages
domain=minesweeper.kernport.com
gitdir=$(git rev-parse --absolute-git-dir)
stage=$(mktemp -d)
trap 'rm -rf -- "$stage"' EXIT
mkdir "$stage/site"
tar --exclude='*.br' --exclude='*.gz' -xzf "$archive" -C "$stage/site"
test -s "$stage/site/index.html"
touch "$stage/site/.nojekyll"
printf '%s\n' "$domain" > "$stage/site/CNAME"
export GIT_INDEX_FILE="$stage/index"
parent=()
if result=$(git ls-remote --exit-code "$remote" "$ref"); then
    git fetch --quiet "$remote" "$ref"
    previous=$(git rev-parse FETCH_HEAD)
    test "$(git show "$previous:CNAME")" = "$domain"
    # Preserve fingerprinted resources for browsers with the previous entry page.
    git read-tree "$previous"
    parent=(-p "$previous")
else
    status=$?
    if [[ $status != 2 ]]; then exit "$status"; fi
    git read-tree --empty
fi
git -C "$stage/site" --git-dir="$gitdir" --work-tree="$stage/site" add -f --ignore-removal .
tree=$(git write-tree)
release=$(git commit-tree "$tree" "${parent[@]}" -m "Publish Kernport Minesweeper $(git rev-parse --short HEAD)")
git push "$remote" "$release:$ref"
