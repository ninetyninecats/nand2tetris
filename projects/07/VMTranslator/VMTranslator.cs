using System.Reflection.Metadata;
using System.Security.Principal;
using System.Linq;

public class VMTranslator {

    static int lineno;
    static List<string> instrs = new List<string>();
    static int pc;
    static string currentFile = "";
    static string currentModule = "";
    static string currentFunction = "";

    public static void Main (string[] args) {
        if (args.Length < 2) {
            Console.WriteLine("Usage: dotnet run output.asm file.vm [file.vm ...]");
            return;
        }

        // emit the bootstrap instructions
        Emit("@256", "D = A", "@0", "M = D");
        EmitCall("Sys.init", 0);

        foreach (var file in args.Skip(1)) {
            Assemble(file);
        }

        File.WriteAllLines(args[0], instrs);
        Console.WriteLine($"Wrote {instrs.Count} instructions to '{args[0]}'.");
    }

    private static void Assemble (string file) {
        currentFile = file;
        var lastSlash = currentFile.LastIndexOf(Path.DirectorySeparatorChar);
        currentModule = currentFile.Substring(lastSlash+1);
        if (currentFile.EndsWith(".vm")) {
            currentModule = currentModule.Substring(0, currentModule.Length-3);
        }

        var lines = File.ReadAllText(file).Replace("\r", "").Split("\n");
        foreach (var rawline in lines) {
            lineno += 1;
            var line = PreProcess(rawline);
            if (line == null) continue;
            var parts = line.Split(" ");
            switch (parts[0]) {
            case "push":
                EmitPush(parts[1], Convert.ToInt32(parts[2]));
                break;
            case "pop":
                EmitPop(parts[1], Convert.ToInt32(parts[2]));
                break;
            case "add": case "sub": case "and": case "or":
                EmitBinOp(parts[0]);
                break;
            case "not": case "neg":
                EmitUnOp(parts[0]);
                break;
            case "eq": case "lt": case "gt":
                EmitBinCmp(parts[0]);
                break;
            case "label":
                Emit($"({currentFunction}.{parts[1]})");
                break;
            case "goto":
                Emit($"@{currentFunction}.{parts[1]}", "0;JMP");
                break;
            case "if-goto":
                EmitPopIntoD();
                Emit($"@{currentFunction}.{parts[1]}", "D;JNE");
                break;
            case "call":
                EmitCall(parts[1], Convert.ToInt32(parts[2]));
                break;
            case "function":
                currentFunction = parts[1];
                EmitFunction(parts[1], Convert.ToInt32(parts[2]));
                break;
            case "return":
                EmitReturn();
                break;
            default:
                Warn($"Unhandled instruction: {line}");
                break;
            }
        }
    }

    static void Warn (string message) {
        Console.WriteLine($"Warning (line {lineno}): {message}");
    }

    static string? PreProcess (string line) {
        var cidx = line.IndexOf("//");
        if (cidx >= 0) {
            line = line.Substring(0, cidx);
        }
        // line = line.Replace(" ", "");
        line = line.Trim();
        return line.Length > 0 ? line : null;
    }

    static void EmitPush(string segment, int offset) {
        switch (segment) {
        case "constant":
            Emit($"@{offset}", "D = A");
            break;

        case "local":
        case "argument":
        case "this":
        case "that":
            var baseAddr = segment == "local" ? "@1" :
                segment == "argument" ? "@2" :
                segment == "this" ? "@3" :
                /* segment == that */ "@4";
            Emit(baseAddr, "D = M", $"@{offset}", "A = D + A", "D = M");
            break;

        case "static":
            Emit($"@{currentModule}.{offset}", "D = M");
            break;
        case "temp":
        case "pointer":
            var baseOffset = segment == "temp" ? 5 : 3;
            Emit($"@{offset + baseOffset}", "D = M");
            break;
        }
        EmitPushFromD();
    }

    static void EmitPop(string segment, int offset) {
        switch (segment) {
        case "local":
        case "argument":
        case "this":
        case "that":
            var baseAddr = segment == "local" ? "@1" :
                segment == "argument" ? "@2" :
                segment == "this" ? "@3" :
                /* segment == that */ "@4";
            Emit(baseAddr, "D = M", $"@{offset}", "D = D + A", "@R13", "M = D");
            EmitPopIntoD();
            Emit("@R13", "A = M", "M = D");
            break;

        case "static":
            Emit($"@{currentModule}.{offset}", "M = D");
            break;
        case "temp":
        case "pointer":
            EmitPopIntoD();
            var baseOffset = segment == "temp" ? 5 : 3;
            Emit($"@{offset + baseOffset}", "M = D");
            break;
        }
    }

    static void EmitBinOp(string op) {
        EmitPopIntoD();
        Emit("A = M - 1");
        switch (op) {
        case "add": Emit("M = M + D"); break;
        case "sub": Emit("M = M - D"); break;
        case "and": Emit("M = M & D"); break;
        case "or": Emit("M = M | D"); break;
        }
    }
    static void EmitUnOp(string op) {
        Emit("@0", "A = M - 1");
        if (op == "neg") Emit("M = -M");
        else if (op == "not") Emit("M = !M");
        else Warn($"Unexpected Unary operator {op}");
    }
    static void EmitBinCmp(string cmp) {
        var jmp = "J" + cmp.ToUpper();
        EmitPopIntoD();
        Emit("A = M - 1", "D = M - D",
             $"@IFEQ{lineno}", $"D;{jmp}", "D = 0", $"@WB{lineno}", "0;JMP",
             $"(IFEQ{lineno})", "D = -1",
             $"(WB{lineno})", "@0", "A = M - 1", "M = D");
    }
    static int EmitPopIntoD() {
        return Emit("@0", "A = M - 1", "D = M", "@0", "M = M - 1");
    }
    static int EmitPushFromD() {
        return Emit("@0", "A = M", "M = D", "@0", "M = M + 1");
    }
    static void EmitCall(string fn, int args) {
        Emit($"// EmitCall {fn} {args}");
        var emitRA = instrs.Count;
        Emit("@TODO", "D = A");
        EmitPushFromD();
        Emit("@1", "D = M");
        EmitPushFromD();
        Emit("@2", "D = M");
        EmitPushFromD();
        Emit("@3", "D = M");
        EmitPushFromD();
        Emit("@4", "D = M");
        EmitPushFromD();
        Emit("@0", "D = M", "@1", "M = D", 
             $"@{args + 5}", "D = D - A", "@2", "M = D");
        Emit($"@{fn}", "0;JMP");
        instrs[emitRA] = $"@{pc}";
    }

    private static void EmitFunction(string fn, int locals) {
        Emit($"({fn})");
        for (var ii = 0 ; ii < locals; ii ++) {
            EmitPush("constant", 0);
        }
    }

    private static void EmitReturn() {
        Emit("// EmitReturn");
        Emit("@1", "D = M", "@5", "A = D - A", "D = M", "@R14", "M = D");
        EmitPop("argument", 0);
        Emit("@2", "D = M", "@0", "M = D + 1");
        for (var ii = 1; ii <= 4; ii ++) {
            Emit("@1", "D = M", $"@{ii}", "A = D - A", "D = M", 
                 $"@{5 - ii}", "M = D");
        }
        Emit("@R14", "A = M", "0;JMP");
    }

    static int Emit(params string[] instrs) {
        //foreach (var instr in instrs) Console.WriteLine(instr);
        foreach (var instr in instrs) {
            VMTranslator.instrs.Add(instr);
            if (!instr.StartsWith("(") && 
                !instr.StartsWith("//")) pc += 1;
        }
        return instrs.Length;
    }
}