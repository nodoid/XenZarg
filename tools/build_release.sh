#!/bin/zsh
# Builds the mobile store packages into release/:
#   release/android/*.aab  (upload to Google Play)
#   release/android/*.apk  (for side-loading / testing)
#   release/ios/*.ipa      (upload to App Store Connect)
# Android signing uses ~/keys/xenzarg-upload.jks; its password is read from the macOS Keychain
# (service "XenZarg Android upload keystore"). iOS signing uses "Apple Distribution: Paul Johnson
# (3UH7BE38T3)" and the rel-xenzarg-ios profile (installed locally, never committed).
set -euo pipefail
cd "$(dirname "$0")/.."

export XENZARG_KEYSTORE_PASS="$(security find-generic-password -a xenzarg-upload -s 'XenZarg Android upload keystore' -w)"
SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"

rm -rf release/android release/ios
for format in aab apk; do
  dotnet publish XenZarg.Android/XenZarg.Android.csproj -c Release -f net10.0-android \
    -p:AndroidPackageFormat=$format -p:AndroidSdkDirectory="$SDK" -o release/android/$format
done
mkdir -p release/android/out
mv release/android/aab/*-Signed.aab release/android/apk/*-Signed.apk release/android/out/
rm -rf release/android/aab release/android/apk
mv release/android/out/* release/android/ && rmdir release/android/out
dotnet publish XenZarg.iOS/XenZarg.iOS.csproj -c Release -f net10.0-ios -o release/ios

ls -la release/android release/ios/*.ipa
