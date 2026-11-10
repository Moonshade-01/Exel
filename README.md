# Exel

A Ukrainian-language C# / .NET MAUI spreadsheet for OOP lab 1, **variant 6 (operation sets 1, 2, 4, 6)**. Integer arithmetic uses `System.Numerics.BigInteger`.

The application provides a native Windows interface, formula parsing and calculation, local documents and Google Drive integration.

## Run Exel

Download `Exel-Windows.zip`, extract the entire archive and double-click **Exel.exe**. Keep all neighboring files. Do not run it inside the ZIP. This is a self-contained Windows x64 Release deployment bundling .NET 10 and Windows App SDK. Running it requires no SDK, Visual Studio, MAUI workload, restore or rebuild. A normal shortcut can point to the executable.

For development, open `Exel.sln` in Visual Studio with the .NET MAUI Windows workload, or use the commands below.

## Interface and controls

Native .NET MAUI UI, with **Exel at the far left** of the main toolbar, a smaller document name beneath it, grouped top controls, a large canvas grid and a collapsible document sidebar. The original dark slate, blue and amber design is inspired by spreadsheet conventions. Buttons animate on press and selected cells pulse their border.

Available controls: Undo/Redo for up to 40 document states; syntax-only checking; append/remove the last row or column; recent documents and folder selection; native Windows Save/Save As dialogs; cell navigation and one-shot reference insertion.

To insert a reference, select the destination cell, type `=` or the beginning of an expression, place the caret, press **«↗ Посилання»**, then click the source cell. Its address is inserted into the original formula. Repeat the reference button for another address. Ordinary clicks select cells.

Both `=A1*2` and `A1*2` are accepted. For example, A1 = `10`, B1 = `A1*2` gives B1 = 20; changing A1 to 15 updates B1 to 30. Compact formulas such as `5^8`, `17div5`, `A1modB2` work; spaces between tokens are also supported.

Single-click selects; double-click, F2 or Ctrl+A opens the native in-cell editor. Ctrl+A selects its formula. Enter commits and moves down; Tab in the cell editor commits and moves right; Escape cancels its draft. The top formula bar is synchronized. Outside editing, arrow keys navigate, Home selects column A, Ctrl+Home selects A1, PageUp/PageDown moves twelve rows and Delete clears the cell. Ctrl+N/O/S provides New/Open/Save. Ctrl+Z/Y uses document history in the grid and native text history inside a text field.

Wheel scrolls rows, Shift+wheel scrolls columns; edge scrollbars and address navigation are also available. The default document is blank, 100 × 100; the maximum size is 100 rows and 100 columns (A through CV). Removing a row/column removes the **last** one, including its contents. References do not shift; newly out-of-bounds addresses report #REF. Undo restores dimensions and data.

The sidebar offers seven recent files, up to twelve newest JSON files in a chosen folder. Recent paths and the chosen folder persist locally. Document history does not persist after restart or document replacement.

«Синтаксис» parses without arithmetic: `10div0` passes syntax checking and reports #DIV0 when calculated. «Формули / Значення» switches source/result display. «Повне значення» shows a long result for viewing/copying; the problems button lists cell errors.

## Build from source

**Windows only**: Windows 10 1809+ / Windows 11 and **.NET 10**. Requires .NET SDK 10.0.402 (or a compatible patch) and the .NET 10 MAUI Windows workload. Other platforms are not configured as build targets.

```powershell
dotnet workload install maui-windows
dotnet restore Exel.sln
dotnet build Exel.sln
dotnet build src/Exel/Exel.csproj -t:Run
```

NuGet restore requires internet access. Changing SDK versions requires corresponding changes to workloads and both project target frameworks.

## Features

- Ukrainian menus, toolbar, formula editor, help and application information.
- Expression/value display modes; full-value viewer with clipboard support.
- Two-stage syntax parsing and evaluation with source positions.
- Cell references, per-calculation caching, cycle detection and propagated errors.
- Local JSON open/save and Google Drive create/update/list/download.
- Initial and maximum grid size: 100 × 100 (A1 through CV100).

Select a cell, type a formula with or without an initial `=`, then press Enter or Apply. Switching cells commits the previous draft. Changing the display mode preserves formulas. Grid text is abbreviated; use the full-value viewer for long numbers. Before replacing a document or closing the window, choose Save, Don’t save, or Cancel. A failed or cancelled save keeps the document open.

## Expression language

Supported: decimal integer literals, parentheses, ASCII addresses (`A1`, `CV100`), binary `+-*/`, `div`, `mod`, `^`, and two-argument `max(x,y)` / `min(x,y)`. Addresses and keywords are case insensitive. Unary signs are excluded by variant 6: write `(0 - 5)` for a negative number.

Precedence: parentheses/functions, exponentiation, multiplication/division/remainder, addition/subtraction. Exponentiation is right associative (`2 ^ 3 ^ 2 = 512`); other binary operators are left associative. `/` and `div` truncate toward zero. Remainder follows the dividend sign. Blank references evaluate to zero; `0 ^ 0 = 1`. Negative exponents are rejected.

Examples: `max(A1,2^100)`, `(0-17)div5`, `17mod5`. Adjacent operands such as `12A1` are invalid. [Formal BNF](docs/GRAMMAR.md).

## Implementation

`Exel.Core` is independent of MAUI: workbook, positioned tokenizer, recursive descent parser, AST, BigInteger evaluator and JSON codec. `Exel` supplies the MAUI UI; `Exel.Integration` provides independently testable Google Drive access. All populated cells are parsed before evaluation begins. A DFS active set detects cycles; a fresh cache is created for every calculation. Exponentiation uses repeated squaring. Calculations use a snapshot on a background task. The entire sheet is recalculated after edits, while selection and display-mode changes reuse unchanged results; incremental recalculation is not implemented. Undo/Redo snapshots share immutable formula strings and retain up to 40 changes.

Errors: `#SYNTAX`, `#DIV0`, `#REF`, `#CYCLE`, `#DOMAIN`, `#LIMIT`. The UI reports a one-based character position. A dependency error points to the reference in the current formula.

Practical limits: 4096 characters per formula, 65536 bits per result, exponent ≤ 100000, parser nesting and reference chains ≤ 128, combined AST/reference evaluation depth ≤ 256, 1000000 evaluation nodes and 5 seconds per recalculation. BigInteger avoids fixed machine integer overflow; these explicit limits protect the UI from excessive work. Input JSON is limited to 4 MB and validated before replacing the active workbook.

## Files and Drive

`.isheet.json` stores schema version, name, dimensions and source formulas, not cached results. It is a custom format, not XLSX or a Google Sheets document. Open uses the system file picker. New local saves use the native Windows Save dialog. Writes use a temporary file beside the destination followed by replacement. Save As opens the native dialog for another destination; cancellation preserves the current path. There is no autosave.

For Drive: enable Google Drive API in your own Google Cloud project, configure the consent screen and test users if needed, then create a **Desktop app** OAuth client and download its JSON. In the app, use Google Drive → Import OAuth JSON and sign in. Authorization uses the system browser, PKCE S256, state validation and an ephemeral IPv4 loopback callback. The sign-in timeout is three minutes.

The `drive.file` scope limits access to app-created files. Save creates a JSON file; subsequent saves update its ID. Open lists files tagged by Exel; a manually uploaded JSON file will not appear in that list. Listing handles pagination. OAuth client settings and the refresh token are saved with MAUI SecureStorage. Restarting restores the saved session without opening a browser; the first Drive operation obtains a fresh access token. Access tokens remain in memory. Sign out deletes the saved session without revoking Google consent. Revoked or expired refresh tokens require signing in again; transient network failures preserve the saved session. Never commit real credentials.

Live Google authentication/upload/download require user credentials and still need end-to-end verification. See [Google Desktop OAuth](https://developers.google.com/identity/protocols/oauth2/native-app), [Drive uploads](https://developers.google.com/workspace/drive/api/guides/manage-uploads), [MAUI setup](https://learn.microsoft.com/dotnet/maui/get-started/first-app).

## Documentation and status

[BNF](docs/GRAMMAR.md), [design notes](docs/DESIGN.uk.md), [validation status](docs/VALIDATION.md),  The xUnit suite is in `tests/Exel.Tests`; real Google consent and cloud exchange still need account-owner verification. Bonus simplified-language statements are not implemented.

## Generative AI disclosure

Generative AI assisted with UI preparation, program implementation and documentation. The student must review, understand and validate the result and adapt it to the instructor's requirements.

## Project organization and tests

`src/Exel` contains the Windows MAUI UI and native services. `src/Exel.Core` separates documents, parsing, calculation and JSON persistence. `src/Exel.Integration` contains OAuth and Drive HTTP access. `tests/Exel.Tests` covers arithmetic, syntax, references, cycles, limits, files, history and mocked Drive workflows. `docs` contains the language specification, design notes and guides.

```powershell
dotnet test tests/Exel.Tests/Exel.Tests.csproj -c Release
```

Tests target .NET 10 and do not require MAUI or Google credentials. OAuth tests use IPv4 loopback and a fake HTTP transport; they do not contact Google.

The custom JSON format preserves source formulas. XLSX import/export is not implemented: it requires syntax conversion and explicit treatment of arbitrary-size integers. Renaming a JSON file does not convert it to XLSX. See the [file-format explanation](docs/FILE_FORMAT.uk.md) and [step-by-step Drive guide](docs/GOOGLE_DRIVE.uk.md).
