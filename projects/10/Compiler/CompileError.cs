public abstract class CompileError : Exception {
    public required AST.Node node;
    public abstract new string Message();
}
public class ExpectedVar : CompileError {
    public required Sym? sym;
    public override string Message() {
        if (sym != null) {
            return $"Expected variable, got {sym.Describe()}";
        } else {
            return $"Expected variable, got unknown identifier {((AST.Ident)node).name}";
        }
    }
}
public class ExpectedCallTarget : CompileError {
    public required Sym got;
    public override string Message() {
        if (got != null) {
            return $"Expected call target, got {got.Describe()}";
        } else {
            return $"Expected call target, got unknown identifier {((AST.Ident)node).name}";
        }
    }
}
public class UnknownIdent : CompileError {
    public required AST.Ident ident;
    public override string Message() {
        return $"Unkown identifier {ident.name}";
    }
}
public class ExpectedClassType : CompileError {
    public required AST.Type got;
    public override string Message() {
            return $"Expected class type, got {got}";
    }
}