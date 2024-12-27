public abstract class Token {
    public int lineNo;
    public string ToXML() {
        return $"<{Element}> {Contents} </{Element}>";
    }
    public override string ToString () => $"{Element}:{Contents}";
    protected abstract string Element {get;}
    protected abstract string Contents {get;}
}

public class Keyword : Token {
    public readonly string keyword;
    public Keyword(string keyword) {
        this.keyword = keyword;
    }
    protected override string Element => "keyword";
    protected override string Contents => keyword;
}

public class Identifier : Token {
    public readonly string identifier;
    public Identifier(string identifier) {
        this.identifier = identifier;
    }
    protected override string Element => "identifier";
    protected override string Contents => identifier;
}

public class IntegerConstant : Token {
    public readonly int value;
    public IntegerConstant(int value) {
        this.value = value;
    }
    protected override string Element => "intConst";
    protected override string Contents => value.ToString();
}
public class StringConstant : Token {
    public readonly string value;
    public StringConstant(string value) {
        this.value = value;
    }
    protected override string Element => "stringConst";
    protected override string Contents => value;
}
public class Symbol : Token {
    public string symbol;
    public Symbol(string symbol) {
        this.symbol = symbol;
    }
    protected override string Element => "symbol";
    protected override string Contents => symbol;
}