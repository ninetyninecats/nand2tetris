public class Assembler {

    static int lineno;
    public static void Main (string[] args) {
        if (args.Length != 1) {
            Console.WriteLine("Usage: dotnet run file.asm");
            return;
        }
        string[] lines;
        try {
            lines = File.ReadAllText(args[0]).Replace("\r", "").Split("\n"); 
        } catch (Exception e) {
            Console.WriteLine(e.Message);
            return;
        }

        // first pass to compute label addresses
        var instrAddr = 0;
        var labels = new Dictionary<string, int>();
        var vars = new Dictionary<string, int>();
        foreach (var rawline in lines) {
            lineno += 1;
            var line = PreProcess(rawline);
            if (line == null) continue;
            if (line.StartsWith("(")) {
                if (!line.EndsWith(")")) {
                    Warn($"Invalid label: {line}");
                } else {
                    var label = line.Substring(1, line.Length - 2);
                    try {
                        labels.Add(label, instrAddr);
                    } catch (ArgumentException) {
                        Warn($"Duplicate label: {label}");
                    }
                }
            } else {
                instrAddr += 1;
            }
        }

        var instrs = new List<string>();
        lineno = 0;
        foreach (var rawline in lines) {
            lineno += 1;
            var line = PreProcess(rawline);
            if (line == null) continue;
            int instr = 0;
            if (line[0] == '@') {
                var addr = line.Substring(1);
                // handle numeric addresses, like @123
                instr = ParseAddr(addr, labels, vars);
            } else if (line.StartsWith("(")) {
                continue;
            } else {
                SetBits(ref instr, 13, 3, 0b111);
                var parts = line.Split(';');
                var dec = parts[0].Split('=');
                if (dec.Length == 2) {
                    SetBits(ref instr, 3, 3, DestBits(dec[0]));
                    SetBits(ref instr, 6, 7, CompBits(dec[1]));
                } else {
                    SetBits(ref instr, 6, 7, CompBits(dec[0]));
                }
                if (parts.Length > 1) {
                    SetBits(ref instr, 0, 3, JumpBits(parts[1]));
                }
            }
            var itext = Convert.ToString(instr, 2).PadLeft(16, '0');
            instrs.Add(itext);
            // Console.WriteLine(itext);
        }
        var filename = args[0].EndsWith(".asm") ? args[0].Substring(0, args[0].Length-4) + ".hack" : args[0] + ".hack";
        File.WriteAllLines(filename, instrs);
        Console.WriteLine($"Wrote {instrs.Count} instructions to '{filename}'.");
    }
    static void SetBits (ref int word, int offset, int bits, int value) {
        var cmask = ((1 << bits) - 1) << offset;
        var smask = (value << offset) & cmask;
        word &= ~cmask;
        word |= smask;
    }
    static void Warn (string message) {
        Console.WriteLine($"Warning (line {lineno}): {message}");
    }

    static int DestBits (string dest) {
        int bits = 0;
        foreach (var c in dest) {
            if (c == 'A') bits |= 0b100;
            else if (c == 'D') bits |= 0b010;
            else if (c == 'M') bits |= 0b001;
            else Warn($"Unknown destination '{c}'.");
        }
        return bits;
        
    }
    static int CompBits (string comp) {
        switch (comp) {
            case "0": return 0b0101010;
            case "1": return 0b0111111;
            case "-1": return 0b0111010;
            case "D": return 0b0001100;
            case "A": return 0b0110000;
            case "M": return 0b1110000;
            case "!D": return 0b0001101;
            case "!A": return 0b0110001;
            case "!M": return 0b1110001;
            case "-D": return 0b0001111;
            case "-A": return 0b0110011;
            case "-M": return 0b1110011;
            case "D+1": return 0b0011111;
            case "A+1": return 0b0110111;
            case "M+1": return 0b1110111;
            case "D-1": return 0b0001110;
            case "A-1": return 0b0110010;
            case "M-1": return 0b1110010;
            case "D+A":
            case "A+D": return 0b0000010;
            case "D+M":
            case "M+D": return 0b1000010;
            case "D-A": return 0b0010011;
            case "A-D": return 0b0000111;
            case "D-M": return 0b1010011;
            case "M-D": return 0b1000111;
            case "D&A":
            case "A&D": return 0b0000000;
            case "D&M":
            case "M&D": return 0b1000000;
            case "D|A":
            case "A|D": return 0b0010101;
            case "D|M":
            case "M|D": return 0b1010101;
            default: 
                Warn($"Unknown comp: '{comp}'");
                return 0;
        }
    }
    static int JumpBits(string jump) {
        switch (jump) {
            case "JGT": return 0b001; 
            case "JEQ": return 0b010; 
            case "JGE": return 0b011; 
            case "JLT": return 0b100; 
            case "JNE": return 0b101; 
            case "JLE": return 0b110; 
            case "JMP": return 0b111; 
            default: 
                Warn($"Unknown jump: '{jump}'");
                return 0;
        }
    }
    static string? PreProcess (string line) {
        var cidx = line.IndexOf("//");
        if (cidx >= 0) {
            line = line.Substring(0, cidx);
        }
        line = line.Replace(" ", "");
        return line.Length > 0 ? line : null;
    }

    static int ParseAddr (string addr, Dictionary<string, int> labels, Dictionary<string, int> vars) {
        try {
            return Int16.Parse(addr);
        } catch (OverflowException) {
            Warn($"Address too large: {addr}");
        }
        catch (FormatException) {} // fall through

        // handle I/O
        if (addr == "SCREEN") return 16384;
        if (addr == "KBD") return 16384 + 8192;

        // handle @R0, @R1, etc.
        if (addr.StartsWith("R")) {
            try {
                var reg = Int16.Parse(addr.Substring(1));
                if (reg > 15) {
                    Warn($"Registers should only be 0-15: {addr}");
                }
                return reg;
            } catch (FormatException) {} // fall through
        }

        // handle labels, like @EXIT, etc.
        if (labels.TryGetValue(addr, out var instrAddr)) return instrAddr;

        // otherwise it must be a variable
        if (vars.TryGetValue(addr, out var varAddr)) return varAddr;
        varAddr = 16 + vars.Count;
        vars.Add(addr, varAddr);
        return varAddr;
    }
}
