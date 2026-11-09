#!/usr/bin/env bash
# Builds, tests and publishes Spartanpad from Linux.
#
#   build.sh                 build for the host, run the tests, publish linux-x64 and win-x64
#   build.sh test            build and run the tests only (no cross toolchain needed)
#   build.sh publish [RID..] publish NativeAOT builds for the given runtime identifiers
#                            (default: linux-x64 win-x64) into out/<rid>/
#   build.sh package         publish, then zip each out/<rid>/ as out/Spartanpad_<version>_<rid>.zip
#   build.sh clean           remove bin/, obj/ and out/
#
# Options:
#   --accept-license   accept the Visual Studio Build Tools license for the Windows SDK sysroot
#                      that the first win-x64 publish downloads (about 2.4 GB into ~/.cache/xwin);
#                      without it the script asks, and refuses when there is no terminal to ask on
#   --skip-tests       do not run the tests in the default command
#
# Needs the .NET 10 SDK. A linux-x64 publish needs clang and zlib headers (Fedora: clang
# zlib-ng-compat-devel; Debian: clang zlib1g-dev). A win-x64 publish also needs lld-link (package
# "lld") and xwin on PATH: https://github.com/Jake-Shadle/xwin

set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
solution="$here/Spartanpad.slnx"
project="$here/src/Spartanpad/Spartanpad.csproj"
version=$(tr -d '[:space:]' < "$here/VERSION")

command=build
accept_license=${ACCEPT_VS_BUILD_TOOLS_LICENSE:-false}
skip_tests=false
rids=()

usage() {
    sed -n '2,/^set -euo/p' "$0" | sed '$d' | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        build|test|publish|package|clean) command=$1 ;;
        --accept-license) accept_license=true ;;
        --skip-tests) skip_tests=true ;;
        -h|--help) usage; exit 0 ;;
        -*) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
        *) rids+=("$1") ;;
    esac
    shift
done
[ ${#rids[@]} -gt 0 ] || rids=(linux-x64 win-x64)

say() { printf '\033[1m== %s\033[0m\n' "$*"; }
fail() { echo "error: $*" >&2; exit 1; }

need_dotnet() {
    command -v dotnet > /dev/null || fail "dotnet is not on PATH; install the .NET 10 SDK from https://dotnet.microsoft.com/download"
    local major
    major=$(dotnet --version 2>/dev/null | cut -d. -f1)
    [ "${major:-0}" -ge 10 ] || fail ".NET SDK 10 or newer is required (found $(dotnet --version 2>/dev/null || echo none))"
}

need_windows_toolchain() {
    command -v lld-link > /dev/null || fail "lld-link is not on PATH; install the \"lld\" package (it links the Windows binary)"
    command -v xwin > /dev/null || fail "xwin is not on PATH; see https://github.com/Jake-Shadle/xwin (it provides the Windows SDK sysroot)"
}

need_linux_toolchain() {
    command -v clang > /dev/null || fail "clang is not on PATH; the NativeAOT compiler links with it"
}

confirm_license() {
    [ "$accept_license" = true ] && return
    if [ ! -d "${XWinCache:-$HOME/.cache/xwin}" ]; then
        cat <<'NOTICE'

Publishing a Windows binary from Linux links against the Windows SDK, which xwin downloads
from Microsoft (about 2.4 GB, once, into ~/.cache/xwin). Using it means accepting the
Visual Studio Build Tools license: https://go.microsoft.com/fwlink/?LinkId=2086102

NOTICE
        if [ -t 0 ]; then
            read -r -p "Accept the license and continue? [y/N] " answer
            case "$answer" in y|Y|yes|YES) ;; *) fail "license not accepted; nothing published" ;; esac
        else
            fail "no terminal to ask on; pass --accept-license (or set ACCEPT_VS_BUILD_TOOLS_LICENSE=true) to accept the Visual Studio Build Tools license"
        fi
    fi
    accept_license=true
}

do_build() {
    say "building for the host"
    dotnet build "$solution" --nologo -v quiet
}

do_test() {
    say "running the tests"
    dotnet test "$solution" --nologo -v quiet
}

publish_rid() {
    local rid=$1
    local out="$here/out/$rid"
    case "$rid" in
        win-*) need_windows_toolchain; confirm_license ;;
        linux-*) need_linux_toolchain ;;
    esac
    say "publishing $rid (NativeAOT) to out/$rid"
    rm -rf "$out"
    dotnet publish "$project" -c Release -r "$rid" --nologo -v quiet -o "$out" \
        -p:AcceptVSBuildToolsLicense="$accept_license"
    # The AOT output is the executable plus the native Skia and HarfBuzz libraries beside it; the
    # debug symbols are not part of what ships.
    rm -f "$out"/*.pdb "$out"/*.dbg
    local exe="$out/Spartanpad"
    [ -f "$exe" ] || exe="$out/Spartanpad.exe"
    [ -f "$exe" ] || fail "publish finished but $out has no Spartanpad executable"
    say "published $exe ($(du -sh "$out" | cut -f1) in out/$rid)"
    ls -l "$out" | sed 's/^/    /'
}

do_publish() {
    for rid in "${rids[@]}"; do
        publish_rid "$rid"
    done
}

do_package() {
    do_publish
    for rid in "${rids[@]}"; do
        local zip="$here/out/Spartanpad_${version}_$rid.zip"
        rm -f "$zip"
        cp "$here/LICENSE" "$here/out/$rid/LICENSE.txt"
        cp "$here/THIRD-PARTY-NOTICES.txt" "$here/out/$rid/THIRD-PARTY-NOTICES.txt"
        cp "$here/README.md" "$here/out/$rid/README.md"
        (cd "$here/out/$rid" && zip -q -r -X "$zip" .) || fail "zip failed for $rid"
        say "packaged $zip ($(du -h "$zip" | cut -f1))"
    done
}

do_clean() {
    say "removing bin/, obj/ and out/"
    find "$here/src" "$here/tests" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
    rm -rf "$here/out"
}

need_dotnet
case "$command" in
    build)
        do_build
        [ "$skip_tests" = true ] || do_test
        do_publish
        ;;
    test) do_build; do_test ;;
    publish) do_publish ;;
    package) do_package ;;
    clean) do_clean ;;
esac
