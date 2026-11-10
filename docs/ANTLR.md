# ANTLR expression parser

Exel uses ANTLR 4.13.1 with `Antlr4.Runtime.Standard` 4.13.1. The generator and runtime versions deliberately match. The Windows UI remains .NET MAUI on .NET 10.

## Source files and execution

- [`FormulaGrammar.g4`](../src/Exel.Core/Parsing/FormulaGrammar.g4) defines tokens, precedence, functions, references and the complete input rule ending in `EOF`.
- `Parsing/Generated` contains the lexer, parser and visitor interfaces generated from that grammar. Commit these C# files along with grammar changes. Do not edit them manually.
- `FormulaParser` connects the generated lexer/parser to the error listener, nesting guard and AST visitor. The former handwritten lexer and recursive descent implementation have been replaced.
- `FormulaAstVisitor` uses rule element labels (`left=`, `right=`, `operatorToken=`) and list labels (`operators+=`, `remaining+=`) to construct positioned expression nodes. Number nodes use `BigInteger`, never `double`.
- `FormulaErrorListener` rejects lexical and syntactic errors immediately. Default console listeners are removed; ANTLR recovery output is never evaluated. Character offsets refer to the original input, including whitespace and an optional initial `=`.
- `FormulaNestingListener` limits active `power` rule calls to 128 before recursive parsing can exhaust the stack. This includes nesting through parentheses, functions and right-associative powers, matching the previous parser's limit.
- `WorkbookCalculator` evaluates the AST with BigInteger arithmetic, existing bounds and cancellation, cached cell results and cycle detection. `SyntaxValidator` uses the same ANTLR pipeline without executing arithmetic.

`+`, `-`, `*`, `/`, `div`, `mod` fold left; `^` recurses right. Only binary signs and two-argument `min`/`max` belong to variant 6. Decimal fractions, assignments and unary signs from more general calculator examples are not part of this language. Existing compact formulas, case-insensitive names, Unicode whitespace and the optional initial `=` remain supported. Parsing a valid out-of-sheet reference succeeds; evaluation reports `#REF`.

## Building in Visual Studio

Open the solution, let NuGet restore `Antlr4.Runtime.Standard`, and build as usual. Java, an ANTLR Visual Studio extension and a generator NuGet package are not needed to build or run the checked-in sources.

## Regenerating after changing the grammar

Only grammar authors need Java 11 or newer and the [official ANTLR 4.13.1 complete JAR](https://www.antlr.org/download/antlr-4.13.1-complete.jar). Keep the JAR outside the repository. The [ANTLR BSD license](ANTLR-LICENSE.txt) is copied into build and publish output alongside the runtime. From the repository root, replace `C:/Tools/antlr-4.13.1-complete.jar` with its actual location and run:

```powershell
java -jar C:/Tools/antlr-4.13.1-complete.jar -Dlanguage=CSharp -visitor -no-listener -package Exel.Core.Parsing.Antlr -Xexact-output-dir -o src/Exel.Core/Parsing/Generated src/Exel.Core/Parsing/FormulaGrammar.g4
```

Commit all four generated `.cs` files. The `.interp` and `.tokens` generator artifacts are not needed by the application and may be omitted. Preserve the `.editorconfig` in that directory; it suppresses only the generated CLS attribute warning CS3021. Rebuild, run the tests, and keep the generator/runtime versions aligned.

```powershell
dotnet test tests/Exel.Tests/Exel.Tests.csproj -c Release
```

Tests cover precedence, associativity, compact cell references, source positions, invalid input, arbitrary precision, recursion limits, syntax-only checking, cell dependencies and cycles. ANTLR migration also received a temporary compatibility run against the former parser: 5,000 generated valid formulas had equal ASTs and 5,000 random inputs agreed on acceptance and AST. A full 100 × 100 sheet with dependencies produced 10,000 correct results. These extra checks run outside the shipped project.

## References

[ANTLR rule element labels](https://github.com/antlr/antlr4/blob/master/doc/parser-rules.md#rule-element-labels), [official C# target](https://github.com/antlr/antlr4/blob/master/doc/csharp-target.md), [runtime package](https://www.nuget.org/packages/Antlr4.Runtime.Standard/4.13.1). The supplied course calculator examples informed the grammar/visitor approach; this grammar implements Exel's integer spreadsheet variant.
