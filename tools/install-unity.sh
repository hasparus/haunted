#!/usr/bin/env bash
# Installs this project's Unity editor on Linux, headless, for tools/unity:
#
#   tools/install-unity.sh [module...]     e.g. tools/install-unity.sh WebGL Linux-IL2CPP
#
# The editor goes to $UNITY_ROOT/<version> (default /opt/unity/<version>) and builds Linux (Mono) players as it is.
# Modules are the Linux installer's own names: WebGL, Linux-IL2CPP, Linux-Server, iOS. Windows, macOS and Android
# support only ship as macOS packages, so those players build in CI (.github/workflows/deploy.yml) or on a Mac.
# Needs no license, so it works as a cloud environment's setup script; tools/unity activates one when it runs.
# Safe to run again: whatever is already installed is skipped.
set -euo pipefail
cd "$(dirname "$0")/.."

version=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)
changeset=$(sed -n 's/^m_EditorVersionWithRevision: .*(\(.*\))$/\1/p' ProjectSettings/ProjectVersion.txt)
root=${UNITY_ROOT:-/opt/unity}/$version
cdn=${UNITY_CDN:-https://download.unity3d.com/download_unity}/$changeset

sudo=""
[ "$(id -u)" -eq 0 ] || sudo=sudo

# What the editor links against that a slim image may lack.
if command -v apt-get >/dev/null; then
  missing=()
  # Ubuntu 24.04 renamed some of them with a t64 suffix; either name will do.
  for pkgs in libglu1-mesa libgtk-3-0t64:libgtk-3-0 libnss3 libxss1 libxtst6 libgbm1 libasound2t64:libasound2 xz-utils; do
    found=""
    for pkg in ${pkgs//:/ }; do dpkg -s "$pkg" >/dev/null 2>&1 && found=1; done
    [ -n "$found" ] || missing+=("$pkgs")
  done
  if [ ${#missing[@]} -gt 0 ]; then
    export DEBIAN_FRONTEND=noninteractive
    $sudo apt-get update -qq >/dev/null
    for pkgs in "${missing[@]}"; do
      for pkg in ${pkgs//:/ }; do
        if apt-cache show "$pkg" >/dev/null 2>&1; then
          echo "Installing $pkg"
          $sudo apt-get install -y -qq --no-install-recommends "$pkg" >/dev/null
          break
        fi
      done
    done
  fi
fi

$sudo mkdir -p "$root"

fetch() {
  echo "Downloading $1"
  curl -fsSL "$cdn/$1" | $sudo tar -xJ -C "$root"
}

if [ -x "$root/Editor/Unity" ]; then
  echo "Unity $version is already in $root"
else
  fetch LinuxEditorInstaller/Unity.tar.xz
fi

ini=$(curl -fsSL "$cdn/unity-$version-linux.ini")
for module in "$@"; do
  url=$(printf '%s\n' "$ini" | sed -n "/^\[$module\]/,/^\[/s/^url=//p")
  case "$url" in
    "") echo "No module called $module. Try: $(printf '%s\n' "$ini" | sed -n 's/^\[\(.*\)\]$/\1/p' | tr '\n' ' ')" >&2; exit 1 ;;
    *.tar.xz) ;;
    *) echo "$module only ships as a macOS package ($url); build that platform in CI or on a Mac." >&2; exit 1 ;;
  esac
  marker="$root/.installed-$module"
  if [ -f "$marker" ]; then
    echo "$module is already installed"
    continue
  fi
  fetch "$url"
  $sudo touch "$marker"
done

echo "Unity $version: $root/Editor/Unity"
