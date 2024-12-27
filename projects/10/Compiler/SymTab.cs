public class SymTab {
    private static int nextStatic = 0;
    private Dictionary<string, Sym> symbols = new Dictionary<string, Sym>();
    public readonly SymTab? parent;
    public readonly string name;
    public SymTab (string name, SymTab? parent) {
        this.parent = parent;
        this.name = name;
    }
    public Sym? Lookup(string name) {
        if (symbols.TryGetValue(name, out var sym)) return sym;
        else if (parent != null) return parent.Lookup(name);
        else return null;
    }
    public int CountVars() {
        return symbols.Values.OfType<VarSym>().Count(vs => vs.segment != "static");
    }
    public void Add(string name, Sym node) {
        symbols.Add(name, node);
    }
    public void Add<N>(string segment, IEnumerable<N> decs, int offset = 0) where N : AST.DecNode {
        var isStatic = segment == "static";
        var index = isStatic ? nextStatic : offset;
        foreach (var dec in decs) {
            foreach (var name in dec.Names()) Add(name.name, new VarSym { name = name, type = dec.Type(), segment = segment, index = index++ });
        }
        if (isStatic) nextStatic = index;
    }
}
public abstract class Sym {
    public required AST.Ident name;
    public abstract string Describe();
}
public class VarSym : Sym {
    public required AST.Type type;
    public required string segment;
    public required int index;
    public override string Describe() {
        return $"variable {type} {name.name}";
    }
    public void Generate (List<string> code, string action) {
        code.Add($"{action} {segment} {index}");
    }
}
public class SubSym : Sym {
        public override string Describe() {
        return $"subroutine {name.name}";
    }

}
public class ClassSym : Sym {
        public override string Describe() {
        return $"class {name.name}";
    }

    public required AST.Type type;

}