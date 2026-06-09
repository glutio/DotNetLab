#!/usr/bin/env bash
set -euo pipefail
curl -fsSL https://aka.ms/dotnetup/get-dotnetup.sh | bash -s -- --install-dir "$HOME/.dotnetup"
export PATH="$HOME/.dotnetup:$PATH"
dotnetup sdk install
source <(dotnetup print-env-script) || true
dotnet --version
dotnet workload install wasm-tools wasm-experimental
dotnet publish -o output src/WebAssembly
dotnet run --file eng/check-publish-output.cs -- output
