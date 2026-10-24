# Формальний опис мови (БНФ)

```bnf
<cell-input> ::= <expression> | "=" <expression>
<expression> ::= <sum>
<sum> ::= <product> <sum-tail>
<sum-tail> ::= "+" <product> <sum-tail> | "-" <product> <sum-tail> | ε
<product> ::= <power> <product-tail>
<product-tail> ::= "*" <power> <product-tail>
                 | "/" <power> <product-tail>
                 | "div" <power> <product-tail>
                 | "mod" <power> <product-tail> | ε
<power> ::= <primary> | <primary> "^" <power>
<primary> ::= <integer> | <cell> | "(" <expression> ")"
            | <function> "(" <expression> "," <expression> ")"
<function> ::= "max" | "min"
<integer> ::= <digit> | <digit> <integer>
<cell> ::= <column> <row>
<column> ::= <letter> | <letter> <letter>
<row> ::= <nonzero-digit> <digits>
<digits> ::= <digit> <digits> | ε
<digit> ::= "0" | <nonzero-digit>
<nonzero-digit> ::= "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9"
<letter> ::= "A" | "B" | "C" | "D" | "E" | "F" | "G" | "H" | "I" | "J"
           | "K" | "L" | "M" | "N" | "O" | "P" | "Q" | "R" | "S" | "T"
           | "U" | "V" | "W" | "X" | "Y" | "Z"
```

`ε` — порожній ланцюжок. Кореневий нетермінал споживає весь ввід. Пробіли між токенами необов’язкові: `5^8`, `A1*2`, `17div5`, `A1modB2`, `max(A1,B2)` коректні. Формули з пробілами також підтримуються. Усередині числа, адреси чи ключового слова пробілів немає.

Слова DIV, MOD, MAX, MIN зарезервовані й розпізнаються без урахування регістру. Лексер може відділяти DIV/MOD від сусіднього операнда. Адреса складається з однієї або двох ASCII-літер і десяткового номера рядка. Операнди без операції (`12A1`, `A1B2`, `1 2`) відхиляються, не зливаються й не означають множення.

Хвости sum/product обчислюються зліва направо; `^` — справа наліво. Додаткові семантичні обмеження: номер рядка вміщується в Int32, адреса належить поточній таблиці, знаменник не нульовий, показник невід’ємний. Унарних знаків немає. Один початковий `=` необов’язковий і дозволений лише на початку всього вводу клітинки (після пропусків), не у вкладеному виразі. Кореневий нетермінал — <cell-input>. Межі ресурсів описано в README.
