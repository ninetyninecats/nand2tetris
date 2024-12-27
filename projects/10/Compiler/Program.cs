public static class Program {
    public static void Main(string[] args) {
        if (args.Length == 0) {
            Console.WriteLine("Usage: dotnet run file.jack");
            return;
        }
        var global = new SymTab("_global", null);
        var nodes = new List<AST.ClassDec>();
        foreach (var arg in args){
            var tokens = Tokenizer.Tokenize(arg);
            // foreach (var token in tokens) {
            //    Console.WriteLine(token.ToXML());
            // }
            var parser = new Parser();
            try {
                nodes.AddRange(parser.Parse(global, tokens));
            } catch (SyntaxError err) {
                Console.WriteLine($"Error parsing {arg} on line {err.token.lineNo+1} at token '{err.token}'");
                Console.WriteLine(err);
                return;
            }
        }
        foreach (var node in nodes) {
            try {
            var code = new List<string>();
            node.Generate(global, code);
            File.WriteAllLines(node.name.name + ".vm", code);
            } catch (CompileError ce) {
                Console.WriteLine($"Error compiling {node.name.name}");
                Console.WriteLine(ce.Message());
            }
        }
    }
}