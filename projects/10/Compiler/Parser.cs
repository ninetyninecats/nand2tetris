

using System.Collections;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.VisualBasic;

public class Parser {
    /// <summary>
    /// Parses a .jack file.
    /// </summary>
    /// <param name="tokenList">The tokens that comprise the file.</param>
    /// <throws exception="SyntaxError">Thrown if a syntax error is encountered.</throws>
    /// <returns>A list of AST nodes representing the classes in the file.</returns>
    public List<AST.ClassDec> Parse(SymTab global, List<Token> tokenList) {
        var nodes = new List<AST.ClassDec>();
        var tokens = new Tokens(tokenList);
        while (tokens.HasNext()) {
            nodes.Add(ParseClass(global, tokens));
        }
        return nodes;
    }
    private AST.ClassDec ParseClass(SymTab global, Tokens tokens) {
        ConsumeKeyword(tokens, "class");
        var name = ParseIdent(tokens);
        ConsumeSymbol(tokens, "{");
        var symTab = new SymTab(name.name, global);
        var classVarDecs = new List<AST.ClassVarDec>();
        var subDecs = new List<AST.SubDec>();
        while (!tokens.PeekSymbol("}")) {
            switch (ConsumeKeywordChoice(tokens, "static", "field", "constructor", "function", "method")) {
            case "static":
                classVarDecs.Add(ParseClassVarDec(tokens, true));
                break;
            case "field":
                classVarDecs.Add(ParseClassVarDec(tokens, false));
                break;
            case "constructor":
                subDecs.Add(ParseSubDec(symTab, tokens, AST.SubType.CONSTRUCTOR));
                break;
            case "function":
                subDecs.Add(ParseSubDec(symTab, tokens, AST.SubType.FUNCTION));
                break;
            case "method":
                subDecs.Add(ParseSubDec(symTab, tokens, AST.SubType.METHOD));
                break;
            }
        }
        ConsumeSymbol(tokens, "}");
        symTab.Add("static", classVarDecs.Where(vd => vd.isStatic));
        symTab.Add("this", classVarDecs.Where(vd => !vd.isStatic));
        foreach (var subdec in subDecs) symTab.Add(subdec.name.name, new SubSym {name = name});
        var classDec = new AST.ClassDec { name = name, symTab = symTab, classVarDecs = classVarDecs, subDecs = subDecs };
        global.Add(name.name, new ClassSym { name = name, type = new AST.ClassType { name = name } });
        return classDec;
    }

    private AST.SubDec ParseSubDec(SymTab classSyms, Tokens tokens, AST.SubType type)
    {
        var returnType = ParseType(tokens, true);
        var name = ParseIdent(tokens);
        ConsumeSymbol(tokens, "(");
        var args = new List<AST.VarDec>();
        if (!tokens.PeekSymbol(")")) {
            do {
                var argType = ParseType(tokens, false);
                var argName = ParseIdent(tokens);
                args.Add(new AST.VarDec { type = argType, names = new List<AST.Ident> {argName}});
            } while (ConsumeSymbolChoice(tokens, ",", ")") == ",");
        } else {
            ConsumeSymbol(tokens, ")");
        }
        ConsumeSymbol(tokens, "{");
        var locals = new List<AST.VarDec>();
        while (tokens.PeekKeyword("var")) {
            locals.Add(ParseVarDec(tokens));
        }
        var stmts =  new List<AST.Stmt>();
        while (!tokens.PeekSymbol("}")) {
            stmts.Add(ParseStmt(tokens));
            Console.WriteLine($"Parsed statement: ${stmts[stmts.Count-1]}");
        }
        ConsumeSymbol(tokens, "}");
        var symTab = new SymTab(name.name, classSyms);
        symTab.Add("argument", args, type == AST.SubType.METHOD ? 1 : 0);
        symTab.Add("local", locals);
        return new AST.SubDec {type = type, returnType = returnType, name = name, args = args, locals = locals, stmts = stmts, symTab = symTab};
    }

    private List<AST.Stmt> ParseBlock(Tokens tokens) {
        ConsumeSymbol(tokens, "{");
        var stmts =  new List<AST.Stmt>();
        while (!tokens.PeekSymbol("}")) {
            stmts.Add(ParseStmt(tokens));
            Console.WriteLine($"Parsed statement: ${stmts[stmts.Count-1]}");
        }
        ConsumeSymbol(tokens, "}");
        return stmts;
    }
    private AST.Stmt ParseStmt(Tokens tokens)
    {
        switch (ConsumeKeywordChoice(tokens, "if", "while", "let", "do", "return")) {
            case "if": 
                return ParseIf(tokens);
            case "while": 
                return ParseWhile(tokens);
            case "let": 
                return ParseLet(tokens);
            case "do": 
                return ParseDo(tokens);
            case "return": 
                return ParseReturn(tokens);
            default:
                throw new Exception("Unpossible");
        }
    }

    private AST.If ParseIf(Tokens tokens)
    {
        var lineNo = tokens.lineNo;
        var condition = ParseCondition(tokens);
        var ifBody = ParseBlock(tokens);
        List<AST.Stmt>? elseBody = null;
        if (tokens.PeekKeyword("else")) {
            ConsumeKeyword(tokens, "else");
            elseBody = ParseBlock(tokens);
        }
        return new AST.If { lineNo = lineNo, condition = condition, ifBody = ifBody, elseBody = elseBody };
    }

    private AST.Stmt ParseWhile(Tokens tokens)
    {
        return new AST.While { lineNo = tokens.lineNo, condition = ParseCondition(tokens), body = ParseBlock(tokens) };
    }

    private AST.Stmt ParseLet(Tokens tokens)
    {
        AST.LHS lhs;
        var ident = ParseIdent(tokens);
        if (tokens.PeekSymbol("[")) {
            ConsumeSymbol(tokens, "[");
            lhs = new AST.ArrayIndex { name = ident, index = ParseExpr(tokens)};
            ConsumeSymbol(tokens, "]");
        } else {
            lhs = ident;
        }
        ConsumeSymbol(tokens, "=");
        var value = ParseExpr(tokens);
        ConsumeSymbol(tokens, ";");
        return new AST.Let { lhs = lhs, value = value };
    }

    private AST.Stmt ParseDo(Tokens tokens)
    {
        AST.Ident? target = null;
        var funcName = ParseIdent(tokens);
        if (tokens.PeekSymbol(".")) {
            ConsumeSymbol(tokens, ".");
            target = funcName;
            funcName = ParseIdent(tokens);
        }
        var subCall = ParseSubCall(tokens, target, funcName);
        ConsumeSymbol(tokens, ";");
        return new AST.Do { subCall = subCall };
    }

    private AST.Stmt ParseReturn(Tokens tokens)
    {
        var returnValue = tokens.PeekSymbol(";") ? null : ParseExpr(tokens);
        ConsumeSymbol(tokens, ";");
        return new AST.Return { returnValue = returnValue };
    }

    private AST.VarDec ParseVarDec(Tokens tokens)
    {
        ConsumeKeyword(tokens, "var");
        var type = ParseType(tokens, false);
        var names = new List<AST.Ident>();
        do {
            names.Add(ParseIdent(tokens));
        } while (ConsumeSymbolChoice(tokens, ",", ";") == ",");
        return new AST.VarDec { type = type, names = names };
    }

    private AST.ClassVarDec ParseClassVarDec(Tokens tokens, bool isStatic) {
        var type = ParseType(tokens, false);
        var names = new List<AST.Ident>();
        do {
            names.Add(ParseIdent(tokens));
        } while (ConsumeSymbolChoice(tokens, ",", ";") == ",");
        return new AST.ClassVarDec { isStatic = isStatic, type = type, names = names };
    }
    
    private AST.Type ParseType(Tokens tokens, bool allowVoid) {
        var next = tokens.Next();
        if (next is Keyword kw) {
            switch (kw.keyword) {
                case "int":
                    return new AST.PrimitiveType { prim = AST.Primitive.INT };
                case "char":
                    return new AST.PrimitiveType { prim = AST.Primitive.CHAR };
                case "boolean":
                    return new AST.PrimitiveType { prim = AST.Primitive.BOOL };
                case "void":
                    if (allowVoid) {
                        return new AST.PrimitiveType { prim = AST.Primitive.VOID };
                    } else {
                        throw new ExpectedNonVoidType {token = next};
                    }
            }
        } else if (next is Identifier id) {
            return new AST.ClassType { name = new AST.Ident {name = id.identifier } };
        }
        throw new ExpectedType { token = next };
    }

    private AST.Expr ParseCondition(Tokens tokens) {
        ConsumeSymbol(tokens, "(");
        var condition = ParseExpr(tokens);
        ConsumeSymbol(tokens, ")");
        return condition;
    }
    
    private AST.Expr ParseExpr(Tokens tokens) {
        var expr = ParseTerm(tokens);
        while (tokens.Peek() is Symbol sym && AST.BinOpFor(sym.symbol) is AST.BinOpType op) {
            ConsumeSymbol(tokens, sym.symbol);
            expr = new AST.BinOp {lhs = expr, op = op, rhs = ParseTerm(tokens) };
        }
        return expr;
    }

    private AST.Expr ParseTerm(Tokens tokens)
    {
        switch (tokens.Next()) {
            case IntegerConstant ic: return new AST.IntConst { value = ic.value };
            case StringConstant sc: return new AST.StringConst{ value = sc.value };
            case Keyword kw:
                if (kw.keyword == "true") return new AST.BoolConst { value = true };
                else if (kw.keyword == "false") return new AST.BoolConst { value = false };            
                else if (kw.keyword == "null") return new AST.NullConst();
                else if (kw.keyword == "this") return new AST.ThisConst();
                else throw new UnexpectedKeyword { token = kw };
            case Symbol sym:
                if (AST.UnOpFor(sym.symbol) is AST.UnOpType op) {
                    return new AST.UnOp { op = op, expr = ParseTerm(tokens) };
                } else if (sym.symbol == "(") {
                    var expr = ParseExpr(tokens);
                    ConsumeSymbol(tokens, ")");
                    return expr;
                } else throw new UnexpectedSymbol { token = sym };
            case Identifier id:
                var ident = new AST.Ident { name = id.identifier };
                if (tokens.Peek() is Symbol sym2) {
                    switch (sym2.symbol) {
                        case ".": 
                            ConsumeSymbol(tokens, ".");
                            return ParseSubCall(tokens, ident, ParseIdent(tokens));
                        case "[":
                            ConsumeSymbol(tokens, "[");
                            var expr = new AST.ArrayIndex { name = ident, index = ParseExpr(tokens) };
                            ConsumeSymbol(tokens, "]");
                            return expr;
                        case "(":
                            return ParseSubCall(tokens, null, ident);
                    }
                }
                return ident;
            default: throw new Exception("Unpossible!");
        }
    }
    
    private AST.SubCall ParseSubCall(Tokens tokens, AST.Ident? target, AST.Ident funcName) {
        var argList = new List<AST.Expr>();
        ConsumeSymbol(tokens, "(");
        if (!tokens.PeekSymbol(")")) {
            do {
                argList.Add(ParseExpr(tokens));
            } while (ConsumeSymbolChoice(tokens, ",", ")") == ",");
        } else {
            ConsumeSymbol(tokens, ")");
        }
        return new AST.SubCall { target = target, funcName = funcName, argList = argList };
    }


    private void ConsumeKeyword(Tokens tokens, string keyword) {
        var next = tokens.Next();
        if (next is Keyword kw && kw.keyword == keyword) return;
        throw new ExpectedKeyword { token = next, keyword = keyword };
    }

    private string ConsumeKeywordChoice(Tokens tokens, params string[] keywords) {
        var next = tokens.Next();
        if (next is Keyword kw && keywords.Contains(kw.keyword)) return kw.keyword;
        throw new ExpectedKeywordChoice { token = next, keywords = keywords };
    }

    private void ConsumeSymbol(Tokens tokens, string symbol) {
        var next = tokens.Next();
        if (next is Symbol sym && sym.symbol == symbol) return;
        throw new ExpectedSymbol { token = next, symbol = symbol };
    }
    private string ConsumeSymbolChoice(Tokens tokens, params string[] symbols) {
        var next = tokens.Next();
        if (next is Symbol sym && symbols.Contains(sym.symbol)) return sym.symbol;
        throw new ExpectedSymbolChoice { token = next, symbols = symbols };
    }

    private AST.Ident ParseIdent (Tokens tokens) {
        var next = tokens.Next();
        if (next is Identifier id) return new AST.Ident { name = id.identifier };
        throw new ExpectedIdent { token = next };
    }

    private class Tokens {
        private readonly List<Token> tokens;
        private int curToken;
        public int lineNo {get; private set;}
        public Tokens(List<Token> tokens) {
            this.tokens = tokens;
        }
        public Token Next() {
            var next = tokens[curToken];
            lineNo = next.lineNo;
            curToken += 1;
            return next;
        }
        public bool HasNext() {    
            return curToken < tokens.Count;
        }
        public Token Peek() {
            return tokens[curToken];
        }

        public bool PeekKeyword(string keyword) {
            return Peek() is Keyword kw && kw.keyword == keyword;
        }

        public bool PeekSymbol(string symbol) {
            return Peek() is Symbol sym && sym.symbol == symbol;
        }
    }
}

