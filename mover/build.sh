#!/bin/bash
# AbyssMover 一键构建（纯命令行，不依赖 Gradle / IDE）
set -e
export MSYS_NO_PATHCONV=1
cd "$(dirname "$0")"

BT="C:/Users/8848/Android/Sdk/build-tools/36.0.0"
AJ="C:/Users/8848/Android/Sdk/platforms/android-34/android.jar"
JC="C:/Program Files/Microsoft/jdk-17.0.19.10-hotspot/bin"
D8JAR="$BT/lib/d8.jar"
PY="D:/ComfyUI-aki-v3（anima）/python/python.exe"
OUT="../out/AbyssMover-1.0.apk"

rm -rf classes base.ap_ unsigned.apk aligned.apk classes.dex
mkdir -p classes

echo "[1/5] javac"
"$JC/javac.exe" -encoding UTF-8 --release 11 -cp "$AJ" -d classes \
  src/ntr/best/dotabyssmover/MainActivity.java 2>&1 | grep -viE "^note|^warning" || true

echo "[2/5] d8"
"$JC/java.exe" -cp "$D8JAR" com.android.tools.r8.D8 --min-api 25 --lib "$AJ" \
  --output . $(find classes -name '*.class' | tr '\n' ' ') 2>&1 | grep -viE "^warning" || true

echo "[3/5] aapt2 link"
"$BT/aapt2.exe" link -I "$AJ" --manifest AndroidManifest.xml -o base.ap_

echo "[4/5] pack"
"$PY" _pack.py

echo "[5/5] align + sign"
"$BT/zipalign.exe" -p -f 4 unsigned.apk aligned.apk
"$BT/apksigner.bat" sign --ks ../keys/modkey-v2.jks --ks-key-alias abyssmod \
  --ks-pass pass:dotabyss --key-pass pass:dotabyss --min-sdk-version 25 \
  --out "$OUT" aligned.apk
"$BT/apksigner.bat" verify --print-certs "$OUT" | head -3
ls -la "$OUT"