grammar FormulaGrammar;

// The complete cell input must match; '=' is optional only at the beginning.
formula : EQUALS? value=sum EOF;

// Flat operator lists preserve left associativity without recursive parse trees.
sum : first=product (operators+=(PLUS | MINUS) remaining+=product)*;
product : first=power (operators+=(MULTIPLY | DIVIDE | DIV | MOD) remaining+=power)*;
// Recursing on the right makes 2^3^2 mean 2^(3^2).
power : left=primary (operatorToken=CARET right=power)?;

primary
    : value=INTEGER                                           # IntegerLiteral
    | address=CELL                                            # CellReference
    | LPAREN value=sum RPAREN                                 # Parenthesized
    | name=(MAX | MIN) LPAREN left=sum COMMA right=sum RPAREN   # FunctionCall
    ;

EQUALS : '=';
PLUS : '+';
MINUS : '-';
MULTIPLY : '*';
DIVIDE : '/';
CARET : '^';
LPAREN : '(';
RPAREN : ')';
COMMA : ',';
DIV : [dD] [iI] [vV];
MOD : [mM] [oO] [dD];
MAX : [mM] [aA] [xX];
MIN : [mM] [iI] [nN];
// No catch-all identifier: reserved operators may touch their operands.
CELL : [a-zA-Z] [a-zA-Z]? [1-9] [0-9]*;
INTEGER : [0-9]+;
// Match the whitespace accepted by char.IsWhiteSpace in the editor.
WS : [ \t\r\n\u000B\u000C\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+ -> skip;
