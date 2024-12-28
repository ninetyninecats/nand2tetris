using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml;

public static class AST {
    public abstract class Node {
        public virtual void Generate(SymTab symTab, List<string> code) {}
    }
    public abstract class DecNode : Node {
        public abstract Type Type();
        public abstract IEnumerable<Ident> Names(); 
    }

    public abstract class Expr : Node {

    }
    public class Ident : Expr, LHS, TermTypes {
        public required String name;
        public override void Generate(SymTab symTab, List<string> code) {
            GenerateVar(symTab, code, "push", this);
        }
    }

    public class IntConst : Expr, TermTypes {
        public required int value;
        public override void Generate(SymTab symTab, List<string> code) {
            code.Add($"push constant {value}");
        }
    }
    public class StringConst : Expr, TermTypes {
        public required String value;

        public override void Generate(SymTab symTab, List<string> code) {
            code.Add($"push constant {value.Length}");
            code.Add("call String.new 1");
            code.Add("pop temp 0");
            foreach (var c in value) {
                code.Add("push temp 0");
                code.Add($"push constant {(int)c}");
                code.Add("call String.appendChar 2");
                code.Add("pop temp 0");
            }
            code.Add("push temp 0");
        }
    }
    public class ArrayIndex : Expr, LHS, TermTypes {
        public required Ident name;
        public required Expr index;
        public void PushAddress(SymTab symTab, List<string> code) {
            GenerateVar(symTab, code, "push", name);
            index.Generate(symTab, code);
            code.Add("add");
        }
        public override void Generate(SymTab symTab, List<string> code) {
            PushAddress(symTab, code);
            code.Add("pop pointer 1");
            code.Add("push that 0");
        }
    }
    public class BoolConst : Expr, TermTypes {
        public required bool value;
        public override void Generate(SymTab symTab, List<string> code) {
            if (value) {
                code.Add("push constant 1");
                code.Add("neg");
            } else {
                code.Add("push constant 0");
            }
        }
    }
    public class NullConst : Expr, TermTypes {
        public override void Generate(SymTab symTab, List<string> code) {
            code.Add("push constant 0");
        }
    }
    public class ThisConst : Expr, TermTypes {
        public override void Generate(SymTab symTab, List<string> code) {
            code.Add("push pointer 0");
        }
    }
    public class Term : Expr {
        public required TermTypes term;
    }
    
    public enum BinOpType { PLUS, MINUS, MULT, DIV, AND, OR, LESS, GREATER, EQUALS }
    public static BinOpType? BinOpFor (string sym) {
        switch (sym) {
            case "+": return BinOpType.PLUS;
            case "-": return BinOpType.MINUS;
            case "*": return BinOpType.MULT;
            case "/": return BinOpType.DIV;
            case "&": return BinOpType.AND;
            case "|": return BinOpType.OR;
            case "<": return BinOpType.LESS;
            case ">": return BinOpType.GREATER;
            case "=": return BinOpType.EQUALS;
            default: return null;
        }
    }

    private static Dictionary<BinOpType, string> binOpToVmOp = new Dictionary<BinOpType, string>() {
        {BinOpType.PLUS, "add"},
        {BinOpType.MINUS, "sub"},
        {BinOpType.AND, "and"},
        {BinOpType.OR, "or"},
        {BinOpType.LESS, "lt"},
        {BinOpType.GREATER, "gt"},
        {BinOpType.EQUALS, "eq"}
    };

    public class BinOp : Expr {
        public required Expr lhs;
        public required BinOpType op;
        public required Expr rhs;
        public override void Generate(SymTab symTab, List<string> code) {
            lhs.Generate(symTab, code);
            rhs.Generate(symTab, code);
            switch (op) {
                case BinOpType.MULT:
                    code.Add("call Math.multiply 2");
                    break;
                case BinOpType.DIV:
                    code.Add("call Math.divide 2");
                    break;
                default:
                    code.Add(binOpToVmOp[op]);
                    break;
            }
        }
    }
    
    public enum UnOpType { NEGATE, NOT }
    public static UnOpType? UnOpFor (string sym) {
        switch (sym) {
            case "-": return UnOpType.NEGATE;
            case "~": return UnOpType.NOT;
            default: return null;
        }
    }

    public class UnOp : Expr, TermTypes {
        public required UnOpType op;
        public required Expr expr;
        public override void Generate(SymTab symTab, List<string> code) {
            expr.Generate(symTab, code);
            if (op == UnOpType.NEGATE) {
                code.Add("neg");
            } else if (op == UnOpType.NOT) {
                code.Add("not");
            } else {
                throw new Exception("Unpossible!");
            }
        }
    }
    public interface TermTypes {}
    public abstract class Stmt : Node {

    }
    public interface LHS {}
    public class Let : Stmt {
        public required LHS lhs;
        public required Expr value;
        public override void Generate(SymTab symTab, List<string> code) {
            if (lhs is ArrayIndex arrayIndex) {
                arrayIndex.PushAddress(symTab, code);
            }
            value.Generate(symTab, code);
            if (lhs is Ident ident) {
                GenerateVar(symTab, code, "pop", ident);
            } else if (lhs is ArrayIndex arrayIndex1) {
                code.Add("pop temp 0");
                code.Add("pop pointer 1");
                code.Add("push temp 0");
                code.Add("pop that 0");
            } else throw new Exception("Unpossible");
        }
    }
    private static void GenerateVar(SymTab symTab, List<string> code, string action, Ident ident)
        {
            var sym = symTab.Lookup(ident.name);
            if (sym is VarSym varSym) varSym.Generate(code, action);
            else throw new ExpectedVar { node = ident, sym = sym };
        }

    public class While : Stmt {
        public required int lineNo;
        public required Expr condition;
        public required List<Stmt> body;
        public override void Generate(SymTab symTab, List<string> code) {
            var whileTrueLabel = $"WHILE{lineNo}";
            var whileDoneLabel = $"ELIHW{lineNo}";
            code.Add($"label {whileTrueLabel}");
            condition.Generate(symTab, code);
            code.Add("not");
            code.Add($"if-goto {whileDoneLabel}");
            foreach (var stmt in body) stmt.Generate(symTab, code);
            code.Add($"goto {whileTrueLabel}");
            code.Add($"label {whileDoneLabel}");
        }
    }
    public class If : Stmt {        
        public required int lineNo;
        public required Expr condition;
        public required List<Stmt> ifBody;
        public required List<Stmt>? elseBody;
        public override void Generate(SymTab symTab, List<string> code) {
            condition.Generate(symTab, code);
            var ifTrueLabel = $"IF{lineNo}";
            var ifDoneLabel = $"FI{lineNo}";
            code.Add($"if-goto {ifTrueLabel}");
            if (elseBody is List<Stmt> stmts) {
                foreach (var stmt in stmts) stmt.Generate(symTab, code);
            }
            code.Add($"goto {ifDoneLabel}");
            code.Add($"label {ifTrueLabel}");
            foreach (var stmt in ifBody) stmt.Generate(symTab, code);
            code.Add($"label {ifDoneLabel}");
        }

    }
    public class Do : Stmt {
        public required SubCall subCall;
        public override void Generate(SymTab symTab, List<string> code) {
            subCall.Generate(symTab, code);
        }
    }
    public class Return : Stmt {
        public Expr? returnValue;
        public override void Generate(SymTab symTab, List<string> code){
            returnValue?.Generate(symTab, code);
            code.Add("return");
        }
    }
    public class SubCall : Expr {
        public required Ident? target;
        public required Ident funcName;
        public required List<Expr> argList;
        public override void Generate(SymTab symTab, List<string> code) {
            Type targetType = new ClassType {name = new Ident {name = symTab.parent!.name} };
            bool isStatic = false;
            if (target is Ident ident) {
                var targetSym = symTab.Lookup(ident.name);
                switch (targetSym) {
                    case VarSym varSym:
                        varSym.Generate(code, "push");
                        targetType = varSym.type;
                        break;
                    case ClassSym classSym:
                        targetType = classSym.type;
                        isStatic = true;
                        break;
                    case SubSym subSym:
                        throw new ExpectedCallTarget { node = this, got = subSym };
                    default:
                        throw new UnknownIdent { node = this, ident = ident };
                }
            } else {
                code.Add("push pointer 0");
            }
            foreach (var arg in argList) arg.Generate(symTab, code);
            switch (targetType) {
                case ClassType classType:
                    code.Add($"call {classType.name.name}.{funcName.name} {argList.Count + (isStatic ? 0 : 1)}");
                    break;
                default: throw new ExpectedClassType { node = this, got = targetType };
            }

        }
    }
    public class ClassDec : DecNode {
        public required Ident name;
        public required SymTab symTab;
        public required List<ClassVarDec> classVarDecs;
        public required List<SubDec> subDecs;
        public override Type Type() => new ClassType { name = name };
        public override IEnumerable<Ident> Names() => Enumerable.Repeat(name, 1);
        public override void Generate(SymTab globalSymTab, List<string> code) {
            foreach (var subDec in subDecs) subDec.Generate(symTab, code);
        }
    }
    public abstract class Type : Node {
    }
    public class PrimitiveType : Type {
        public required Primitive prim;
    }
    public class ClassType : Type {
        public required Ident name;
    }
    public enum Primitive { INT, CHAR, BOOL, VOID }
    public class ClassVarDec : DecNode {
        public required bool isStatic;
        public required Type type;
        public required List<Ident> names;
        public override Type Type() => type;
        public override IEnumerable<Ident> Names() => names;
}
    public enum SubType { CONSTRUCTOR, FUNCTION, METHOD }
    public class SubDec : DecNode {
        public required SubType type;
        public required Type returnType;
        public required Ident name;
        public required List<VarDec> args;
        public required List<VarDec> locals;
        public required List<Stmt> stmts;
        public required SymTab symTab;
        public override Type Type()
        {
            throw new Exception("Unpossible");
        }
        public override IEnumerable<Ident> Names() => Enumerable.Repeat(name, 1);        
        public override void Generate(SymTab classSymTab, List<string> code) {
            var fqName = $"{classSymTab.name}.{name.name}";
            code.Add($"function {fqName} {locals.Select(vd => vd.names.Count).Sum()}");
            if (type == SubType.METHOD) {
                code.Add("push argument 0");
                code.Add("pop pointer 0");
            } else if (type == SubType.CONSTRUCTOR) {
                code.Add($"push constant {classSymTab.CountVars()}");
                code.Add("call Memory.alloc 1");
                code.Add("pop pointer 0");
            }
            foreach (var stmt in stmts) stmt.Generate(symTab, code);
        }
    }
    public class VarDec : DecNode {
        public required Type type;
        public required List<Ident> names;
        public override Type Type() => type;
        public override IEnumerable<Ident> Names() => names;
    }
}
