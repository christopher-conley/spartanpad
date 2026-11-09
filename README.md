# Spartanpad

The classic Windows Notepad, rebuilt as a single native executable that runs where Notepad does
not: inside Windows Sandbox, and on Linux.

Microsoft loves change for the sake of change, so Windows 11 moved Notepad to the Store, because,
sure, that makes sense (it does not)! Windows Sandbox has no Store, so a fresh Sandbox has no text
editor, because an intern somewhere deep in the bowels of Microsoft had to leave their mark on the world
by porting Notepad to UWP and, as is the Microsoft way, didn't stop to consider the implications and
possible repercussions of that decision. Spartanpad is a lightweight .NET 10 NativeAOT (mostly) drop-in
replacement for Notepad made with Avalonia UI. It needs no .NET runtime install: just unzip and run.

The behavior is taken from the last classic `notepad.exe` (10.0.22621.1), read from the binary;
menu IDs, dialog layouts, default settings, the encoding detection order, etc. As much of the
original behavior as possible was preserved, with some fixes and QoL changes made. The places
where Spartanpad knowingly differs from Notepad are listed below.

## What it does

Everything the reference version of Notepad does, with one exception (printing, below):

- File: New, New Window, Open, Save, Save As, Page Setup, Exit. Open and Save use the same
  filter (`Text Documents (*.txt)` / `All Files`), the save prompt has the same three buttons.
- Edit: Undo, Cut, Copy, Paste, Delete, Search with Kagi (replaces Bing), Find, Find Next (F3),
  Find Previous (Shift+F3), Replace, Go To, Select All, Time/Date (F5). Find and Replace are
  modeless, with Match case, Wrap around and the Up/Down direction; the search is culture-aware
  like `FindNLSString`.
- Format: Word Wrap, Font. View: Zoom (Ctrl+Plus/Minus/0 and Ctrl+wheel, 10% to 500%), Status Bar.
- Status bar: `Ln, Col`, zoom, line ending (`Windows (CRLF)`, `Unix (LF)`, `Macintosh (CR)`) and
  encoding. Both of the last two are clickable. The line ending segment converts the document to
  the ending you pick, while Notepad only ever displayed it. The encoding segment is where the
  reference's file-dialog Encoding combo went (Avalonia's pickers can't host one): choosing an
  encoding on a clean document re-reads the file in it; otherwise it's what the next save writes.
- Encodings: ANSI, UTF-16 LE, UTF-16 BE, UTF-8, UTF-8 with BOM, detected in the reference's order
  (BOM, UTF-16 heuristic, strict UTF-8, ANSI). New documents are UTF-8. Saving as ANSI warns when
  characters would be lost.
- The `.LOG` trick: a file whose first line is `.LOG` gets the time and date appended on open.
- Command line: `Spartanpad [/A | /W] file` (force ANSI or UTF-16), a missing file asks whether
  to create it, and a dropped file opens.
- Settings are kept with the reference's value names (`fWrap`, `iPointSize`, `szHeader`, ...) in
  `settings.json`: `%APPDATA%\Spartanpad` on Windows, `~/.config/Spartanpad` on Linux.

**Not done: printing.** Avalonia has no printing API. Page Setup works and its settings are kept;
Print (and `/P`, `/PT`) shows a message saying it's not implemented yet.

## Where it differs from Notepad, on purpose

- Settings live in a JSON file, not the registry, but uses the registry's own value names.
- The encoding is chosen from the status bar, not from a combo in the file dialogs.
- The line ending is a selector that converts the document; Notepad only displayed it.
- The Font dialog is Spartanpad's own (Avalonia doesn't have one).
- Files of 8 MiB or more are refused for now; Notepad allowed 1 GiB. This text box is not built for that.
- Undo is multi-level; Notepad's was a single toggle.
- The theme follows the operating system; Notepad was always light.
- Search with Bing became Search with Kagi.
- The Store banner, Enterprise Data Protection, telemetry and the Restart Manager autosave are
  not reproduced.

## Running in Windows Sandbox

Unzip `Spartanpad_<version>_win-x64.zip` somewhere on the host, point the `HostFolder` in
[scripts/spartanpad.wsb](scripts/spartanpad.wsb) at that folder, and double-click the `.wsb`.
The Sandbox starts with the folder mapped read-only and a Spartanpad shortcut on its desktop.
Or map the folder yourself and run `Spartanpad.exe` from it: the executable and the two native
libraries beside it are all it needs.

## Building

Needs the .NET 10 SDK. On Linux, a native publish needs `clang` and the zlib headers; the Windows
publish additionally needs `lld-link` (package `lld`) and [`xwin`](https://github.com/Jake-Shadle/xwin)
on `PATH`. The first Windows publish downloads the Windows SDK sysroot (around ~2.4 GB) into
`~/.cache/xwin`, which means accepting the Visual Studio Build Tools license: use
`--accept-license`, set `ACCEPT_VS_BUILD_TOOLS_LICENSE=true`, or answer the prompt.

```sh
./build.sh test                      # build for the host and run the tests
./build.sh publish linux-x64         # out/linux-x64/Spartanpad
./build.sh publish win-x64           # out/win-x64/Spartanpad.exe, cross-compiled
./build.sh package                   # both, zipped as out/Spartanpad_<version>_<rid>.zip
dotnet run --project src/Spartanpad  # run from source on the host
```

`.github/workflows/build.yml` runs the tests and `dotnet format --verify-no-changes`, then
publishes both executables on Ubuntu runners with the same toolchain (xwin pinned by version and
checksum, the sysroot cached between runs) and uploads each folder as an artifact.

## Layout

```
src/Spartanpad.Core/   the document, encodings, settings, search, command line (no UI; unit tested)
src/Spartanpad/        the Avalonia application: main window, dialogs
tests/                 xunit tests for Core and headless UI tests for the window
scripts/               the Windows Sandbox configuration
```

The document model is separate from the window so that a tabbed interface can later be implemented.

## License

MIT. See [LICENSE](LICENSE).
