#!/usr/bin/env bash
# Publish the netstandard2.1 core into the Unity project's (gitignored) plugin folder.
# Unity netstandard2.1 profile already provides System.Buffers/Memory/Numerics.Vectors/
# Threading.Tasks.Extensions, so those are not copied. CompilerServices.Unsafe 6.0 IS needed
# (STJ 8 binds to it; without it StartNewGame fails with FileLoadException under Mono).
set -euo pipefail
root="$(git rev-parse --show-toplevel)"
export PATH="$HOME/.dotnet:$PATH"
out="$(mktemp -d)"
dest="$root/src/WalkGame.Unity/Assets/Plugins/WalkGame"
dotnet publish "$root/src/WalkGame.Infrastructure/WalkGame.Infrastructure.csproj" -c Release -o "$out" --nologo -v q
mkdir -p "$dest"
rm -f "$dest"/*.dll
for a in WalkGame.Domain WalkGame.Application WalkGame.Infrastructure System.Text.Json System.Text.Encodings.Web Microsoft.Bcl.AsyncInterfaces System.Runtime.CompilerServices.Unsafe; do
  cp "$out/$a.dll" "$dest/"
done
rm -rf "$out"
ls -l "$dest"
