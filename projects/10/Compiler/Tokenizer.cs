using System.Runtime.CompilerServices;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices.Marshalling;
using System.Reflection.Metadata;

public static class Tokenizer {
    private enum Mode { SKIP, IDENT, INT, STRING, SYM, LINECOMMENT, BLOCKCOMMENT }

    private static HashSet<char> symbols = new HashSet<char>(new char[] {
        '{', '}', '(', ')', '[', ']', '.', ',', ';', '+', '-', '*', '/', '&', '|', '<', '>', '=', '~'
    });
    private static HashSet<string> keywords = new HashSet<string>(new string[] {
        "class", "constructor", "function", "method", "field", "static", "var", "int",
        "char", "boolean", "void", "true", "false", "null", "this", "let", "do", "if", "else",
        "while", "return"
    });

    public static List<Token> Tokenize(string fileName) {
        var tokens = new List<Token>();        
        var lineNo = 0;
        void Add(Token token) {
            token.lineNo = lineNo;
            tokens.Add(token);
        }

        var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        var input = new StreamReader(stream);
        var accum = new StringBuilder();
        Mode mode = Mode.SKIP;
        int c;
        void HandleSkip (char ch) {
                if (char.IsLetter(ch) || ch == '_') {
                    accum.Append(ch);
                    mode = Mode.IDENT;
                } else if (ch == '"') {
                    mode = Mode.STRING;
                } else if (char.IsDigit(ch)) {
                    accum.Append(ch);
                    mode = Mode.INT;
                } else if (ch == '/') {
                    if (input.Peek() == '/') {
                        mode = Mode.LINECOMMENT;
                    } else if (input.Peek() == '*') {
                        mode = Mode.BLOCKCOMMENT;
                    } else {
                        Add(new Symbol(ch.ToString()));
                    }
                } else if (symbols.Contains(ch)) {
                    Add(new Symbol(ch.ToString()));
                } else if (!(char.IsWhiteSpace(ch))) {
                    Console.WriteLine($"Unexpected Character: {ch}");
                }

        }
        while ((c = input.Read()) != -1) {
            var ch = (char)c;
            
            switch (mode) {
                case Mode.SKIP:
                HandleSkip(ch);
                break;
                case Mode.IDENT:
                if (char.IsLetter(ch) || char.IsDigit(ch) || ch == '_') {
                    accum.Append(ch);
                } else {
                    var word = accum.ToString();
                    if (keywords.Contains(word)) {
                        Add(new Keyword(word));
                    }else {
                        Add(new Identifier(word));
                    }
                    accum.Clear();
                    mode = Mode.SKIP;
                    HandleSkip(ch);
                }
                break;
                case Mode.STRING:
                if (ch == '"') {
                    Add(new StringConstant(accum.ToString()));
                    accum.Clear();
                    mode = Mode.SKIP;
                } else {
                    accum.Append(ch);
                }
                break;
                case Mode.INT:
                if (char.IsDigit(ch)) {
                    accum.Append(ch);

                } else {
                    Add(new IntegerConstant(Convert.ToInt32(accum.ToString())));
                    accum.Clear();
                    mode = Mode.SKIP;
                    HandleSkip(ch);
                }
                break;
                case Mode.SYM:
                // Shouldn't happen
                break;
                case Mode.LINECOMMENT:
                if (ch == '\n') {
                    mode = Mode.SKIP;
                }
                break;
                case Mode.BLOCKCOMMENT:
                if (ch == '*' && input.Peek() == '/') {
                    input.Read();
                    mode = Mode.SKIP;
                }
                break;
            }
            if (ch == '\n') lineNo += 1;
        }

        return tokens;
    }
}