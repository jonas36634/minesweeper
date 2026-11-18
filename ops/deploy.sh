#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
target=${1:?Ziel als SSH-Host angeben}
archive=artifacts/minesweeper-wwwroot.tar.gz
test -s "$archive"
# Upload to a fresh directory, switch only after successful extraction.
release=$(ssh -- "$target" 'mkdir -p /srv/minesweeper/releases && mktemp -d /srv/minesweeper/releases/site.XXXXXXXX')
[[ "$release" =~ ^/srv/minesweeper/releases/site\.[A-Za-z0-9]+$ ]]
ssh -- "$target" "tar --no-same-owner -xzf - -C '$release' && chmod -R a+rX '$release' && test -s '$release/index.html'" < "$archive"
ssh -- "$target" "set -eu; if test -d /srv/minesweeper/current/_framework; then cp -aln /srv/minesweeper/current/_framework/. '$release/_framework/'; fi; ln -s '$release' '$release.next'; mv -Tf '$release.next' /srv/minesweeper/current"
