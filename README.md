# Exel

## About the project

**Exel** is a Windows spreadsheet application for OOP laboratory work No. 1, **variant 6** (operation sets 1, 2, 4 and 6). It evaluates integer expressions and references between cells using `System.Numerics.BigInteger`.

The application includes:

- A Ukrainian interface with a dark theme, button animations and in-cell editing.
- A **100 × 100** grid, from `A1` to `CV100`.
- ANTLR 4 parsing with a separate syntax check and calculation stage.
- Binary `+`, `-`, `*`, `/`, `div`, `mod`, `^`, and two-argument `min` / `max` functions.
- Cell references, automatic recalculation after edits and circular-reference detection.
- Formula and value display modes, error explanations and source positions.
- Undo/Redo, recent documents, and adding or removing the last row or column.
- Local JSON files and Google Drive saving, opening and updating.
- Limits on expression length, nesting and calculation size to prevent excessive work.

The application targets **Windows only** and uses **.NET MAUI**.

### Built with

| Technology | Version / purpose |
| --- | --- |
| C# / .NET | .NET 10 |
| .NET MAUI | 10.0.110 — Windows interface |
| ANTLR | 4.13.1 — `Antlr4.Runtime.Standard` |
| BigInteger | Integer arithmetic beyond fixed machine integer sizes |
| System.Text.Json | Table serialization |
| Google Drive REST API v3 | Cloud files, accessed through `HttpClient` |
| MAUI SecureStorage | Saved Google authorization |
| xUnit | 2.9.3 — five core unit tests |

### Repository layout

```text
Exel/
├── Exel.sln
├── README.md
├── licenses/
│   └── ANTLR-LICENSE.txt
├── src/
│   ├── Exel/                  .NET MAUI Windows application
│   │   ├── UI/                Spreadsheet interface and controls
│   │   ├── Services/          Local files and platform services
│   │   └── Resources/         Fonts, icons and other application assets
│   ├── Exel.Core/             Spreadsheet model and calculation
│   │   ├── Documents/         Workbook, cell addresses and Undo/Redo
│   │   ├── Parsing/           ANTLR grammar, visitor and generated C# sources
│   │   ├── Calculation/       BigInteger evaluation and cell dependencies
│   │   └── Persistence/       JSON serialization and validation
│   └── Exel.Integration/
│       └── GoogleDrive/       OAuth and Google Drive requests
└── tests/
    └── Exel.Tests/
        └── SpreadsheetTests.cs
```

## Getting started

### Requirements

- Windows 10 version 1809 or later, or Windows 11.
- .NET 10 SDK.
- Visual Studio with support for .NET 10 and the **.NET Multi-platform App UI development** workload, including Windows tooling.
- Internet access for the initial NuGet restore and for Google Drive operations.

The generated ANTLR C# files are included in the repository. Java and an ANTLR extension are not required for a normal build.

### Open and run in Visual Studio

1. Clone `https://github.com/Moonshade-01/Exel.git` through **Git → Clone Repository**, or open an existing local copy.
2. Open `Exel.sln`.
3. Right-click the solution in **Solution Explorer** and select **Restore NuGet Packages**.
4. Set the **Exel** application project as the startup project.
5. Select **Windows Machine**, build the solution and press **F5**.

Alternatively, from the repository root:

```powershell
dotnet restore Exel.sln
dotnet build src/Exel/Exel.csproj
dotnet build src/Exel/Exel.csproj -t:Run
```

## Google Drive setup

Google Drive is optional. Local tables work without a Google account.

1. Create or select a project in [Google Cloud Console](https://console.cloud.google.com/).
2. Open **APIs & Services → Library** and enable **Google Drive API**.
3. Configure **Google Auth Platform** with an application name and contact email. For a personal Gmail account, select **External** as the audience.
4. If the application is in testing mode, add your Google account under **Audience → Test users**.
5. Under **Data Access**, add the scope `https://www.googleapis.com/auth/drive.file`.
6. Under **Clients**, create a **Desktop app** OAuth client and download its JSON file.
7. In Exel, select **Drive → Підключити Google Drive**, choose that JSON file and complete sign-in in the browser.

Keep the OAuth JSON outside the repository. It is selected through the application; it does not need to be renamed or placed in application resources.

Use **Зберегти в Drive** to upload a table and **Відкрити з Drive** to open it. Further cloud saves of the same open cloud document update its existing file. The file list shows tables created through Exel in that OAuth project.

OAuth settings and the refresh token are saved through MAUI SecureStorage. The session is restored after restart; an access token is obtained when a Drive operation needs it. **Вийти** removes the locally saved authorization. Google may require a new sign-in if access expires or is revoked.

## Usage

### Editing a table

Select a cell and enter a number or formula in the formula bar. Press **Enter** to apply it. Double-click or **F2** opens the in-cell editor; **Ctrl+A** selects its text. **Escape** cancels an edit, and **Tab** commits an in-cell edit and moves right.

For a reference example, enter `10` in `A1` and `A1*2` in `B1`. The result in `B1` is `20`; changing `A1` to `15` recalculates it to `30`. The **↗ Посилання** button also lets you insert a cell address by clicking the source cell.

Use **Формули / Значення** to switch display modes and **Синтаксис** to check expressions without evaluating arithmetic. **Ctrl+Z** and **Ctrl+Y** undo and redo document changes when the grid has focus.

Removing a row or column removes the last one and its contents. Existing addresses are not rewritten; references outside the remaining grid report `#REF`. Undo can restore the removed data.

### Expressions

| Expression | Result |
| --- | --- |
| `2+3*4` | `14` |
| `(2+3)*4` | `20` |
| `5^8` | `390625` |
| `2^3^2` | `512` |
| `17div5` | `3` |
| `17mod5` | `2` |
| `(0-17)div5` | `-3` |
| `(0-17)mod5` | `-2` |
| `max(7,min(20,4))` | `7` |
| `A1*2` | Twice the value of `A1` |

Spaces are optional between tokens, and names are case insensitive. A single initial `=` is optional. `/` and `div` both truncate toward zero; `mod` follows the dividend's sign. Powers associate right to left. Empty referenced cells evaluate to zero.

Variant 6 does not include unary signs: use `(0-5)` for a negative number. The application reports syntax errors, division by zero, invalid references, cycles and calculation limits.

### File format

Tables use `.isheet.json`, a custom JSON format containing the name, dimensions and source formulas. Values are recalculated when a table is opened. Local saving uses the Windows file dialog; **Save As** selects another destination.

XLSX import/export and Google Sheets document editing are not implemented. Uploading a table to Drive stores the same JSON format.

## Unit tests

Run the tests from the repository root:

```powershell
dotnet test tests/Exel.Tests/Exel.Tests.csproj -c Release
```

`SpreadsheetTests.cs` contains exactly **five** unit tests covering the main spreadsheet features:

1. Variant 6 operations (`+`, `-`, `*`, `/`, `div`, `mod`, `^`, `min`, `max`), precedence and BigInteger results.
2. Syntax error positions and syntax-only validation without arithmetic evaluation.
3. Recalculation of a dependency chain after an edit, case-insensitive references and empty cells.
4. An indirect cycle, its effect on dependent cells, and recovery after breaking it.
5. JSON preservation of table metadata, formulas and references, followed by recalculation.

The tests run independently of MAUI and do not contact Google.

## Generative AI usage

Generative AI assisted with UI development, implementation and README preparation.

## Third-party license

ANTLR is distributed under the BSD 3-Clause license. Its notice is included in [licenses/ANTLR-LICENSE.txt](licenses/ANTLR-LICENSE.txt).
