public abstract class SyntaxError : Exception {
    public required Token token;
}


public class ExpectedKeyword : SyntaxError {
    public required string keyword;
    public override string ToString () => $"Expected keyword {keyword}, got {token}";
}

public class ExpectedKeywordChoice : SyntaxError {
    public required string[] keywords;
    public override string ToString () => $"Expected one of {string.Join(", ", keywords)}, got {token}";
}

public class ExpectedSymbol : SyntaxError {
    public required string symbol;
    public override string ToString() => $"Expected symbol {symbol}, got {token}";
}

public class ExpectedType : SyntaxError {}
public class ExpectedIdent : SyntaxError {}

public class ExpectedNonVoidType : SyntaxError {}
public class ExpectedSymbolChoice : SyntaxError
{
    public required string[] symbols;
    public override string ToString() => String.Format("Expected one of: {0}", symbols);
}
public class UnexpectedKeyword : SyntaxError {}
public class UnexpectedSymbol : SyntaxError {
    public override string ToString () => $"Unexpected symbol at {token}";
}