#!/bin/zsh
# Builds the Windows version inside the Parallels "Windows 11" VM and packages it:
#   release/windows/XenZarg-<version>-<x64|arm64>.msix  (upload both to Partner Center; the Store signs them)
#   release/windows/XenZarg-<version>-<x64|arm64>.zip   (the same game unpackaged, for testing on a PC)
#
# The VM shares the Mac's Downloads folder (Z:\Downloads), so the source is staged in a temporary
# folder there, built and tested with the Windows .NET SDK in C:\Build\XenZarg, and the published
# builds are copied back. Pass "capture" to also record the Microsoft Store screenshots with the
# Windows build (written to release/windows/capture).
set -euo pipefail
cd "$(dirname "$0")/.."

VM=${XENZARG_VM:-"Windows 11"}
VERSION=$(dotnet msbuild XenZarg.WindowsDX/XenZarg.WindowsDX.csproj -getProperty:Version)
BUILD=$(dotnet msbuild XenZarg.WindowsDX/XenZarg.WindowsDX.csproj -getProperty:BuildNumber)
STAGE="$HOME/Downloads/xenzarg-vm-transfer"
WSTAGE='Z:\Downloads\xenzarg-vm-transfer'
out=release/windows

prlctl status "$VM" | grep -q running || prlctl resume "$VM" || prlctl start "$VM"
rm -rf "$STAGE" && mkdir -p "$STAGE/src"
# Stage the working tree, not just HEAD, so uncommitted changes are built too.
rsync -a --exclude '.git' --exclude 'release' --exclude 'stores' --exclude 'bin' --exclude 'obj' \
  --exclude '.DS_Store' ./ "$STAGE/src/"
tar -cf "$STAGE/src.tar" -C "$STAGE/src" .
rm -rf "$STAGE/src"

prlctl exec "$VM" --current-user powershell -NoProfile -Command "
  \$ErrorActionPreference = 'Stop'
  Remove-Item -Recurse -Force C:\Build\XenZarg, C:\Build\out -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force C:\Build\XenZarg | Out-Null
  tar -xf $WSTAGE\src.tar -C C:\Build\XenZarg
  Set-Location C:\Build\XenZarg
  dotnet test XenZarg.Tests
  if (\$LASTEXITCODE -ne 0) { throw 'tests failed' }
  foreach (\$a in 'x64','arm64') {
    dotnet publish XenZarg.WindowsDX -c Release -r win-\$a --self-contained -o C:\Build\out\\\$a -p:DebugType=none
    if (\$LASTEXITCODE -ne 0) { throw 'publish failed' }
    Remove-Item C:\Build\out\\\$a\createdump.exe -ErrorAction SilentlyContinue
    Compress-Archive -Force -Path C:\Build\out\\\$a\* -DestinationPath $WSTAGE\win-\$a.zip
  }
"

if [[ ${1:-} == capture ]]; then
  prlctl exec "$VM" --current-user powershell -NoProfile -Command "
    Remove-Item -Recurse -Force C:\Build\cap -ErrorAction SilentlyContinue
    Start-Process -Wait -FilePath C:\Build\out\arm64\XenZarg.exe -ArgumentList '--capture','C:\Build\cap','stills'
    Copy-Item -Recurse -Force C:\Build\cap $WSTAGE\capture
  "
fi

rm -rf $out && mkdir -p $out
for arch in x64 arm64; do
  cp "$STAGE/win-$arch.zip" "$out/XenZarg-$VERSION-$arch.zip"
  pkg=$(mktemp -d)/package
  mkdir -p $pkg && (cd $pkg && unzip -q "$OLDPWD/$out/XenZarg-$VERSION-$arch.zip")
  mkdir -p $pkg/Assets
  cp XenZarg.WindowsDX/Windows/Assets/*.png $pkg/Assets/
  sed -e "s/\$(VERSION)/$VERSION.$BUILD/" -e "s/\$(ARCH)/$arch/" XenZarg.WindowsDX/Windows/AppxManifest.xml > $pkg/AppxManifest.xml
  python3 tools/make_msix.py $pkg "$out/XenZarg-$VERSION-$arch.msix"
  rm -rf "$(dirname $pkg)"
done
[[ -d "$STAGE/capture" ]] && cp -R "$STAGE/capture" "$out/capture"
rm -rf "$STAGE"
echo "Windows: $out/XenZarg-$VERSION-{x64,arm64}.msix (version $VERSION.$BUILD)"
ls -la $out
