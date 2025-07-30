using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMTranslator.Models;

namespace VMTranslator
{
    public class Translator
    {
        private Dictionary<string, MemorySegment> _memorySegmentLookup => new Dictionary<string, MemorySegment>
        {
            { "local", new MemorySegment { Symbol = "LCL",  MaxIndex = int.MaxValue } },
            { "argument", new MemorySegment { Symbol = "ARG",  MaxIndex = int.MaxValue } },
            { "this", new MemorySegment { Symbol = "THIS",  MaxIndex = int.MaxValue } },
            { "that", new MemorySegment { Symbol = "THAT",  MaxIndex = int.MaxValue } },
            { "temp", new MemorySegment { Symbol = "R5",  MaxIndex = 7 } },
            { "static", new MemorySegment { Symbol = "",  MaxIndex = 239 } },
            { "constant", new MemorySegment { Symbol = "",  MaxIndex = int.MaxValue } },
            { "pointer", new MemorySegment { Symbol = "",  MaxIndex = 1 } },
        };

        private int _comparisonLabelCounter;
        private HashSet<string> _labels = new HashSet<string>();
        private string _filePath;
        private string _fileName;
        private string _currentFuction = String.Empty;
        private int _functionCallRunningCount = 0;

        public Translator(string filePath)
        {
            _comparisonLabelCounter = 0;
            _filePath = filePath;
            _fileName = Path.GetFileNameWithoutExtension(_filePath);
        }

        public void TranslateLines(IList<ParsedCommand> commands)
        {
            List<string> output = new List<string>();

            foreach (var command in commands)
            {
                switch (command.CommandType)
                {
                    case CommandType.C_ARITHMETIC:
                        output.AddRange(HandleArithmeticCommand(command.Arg1));
                        break;
                    case CommandType.C_PUSH:
                        output.AddRange(HandlePushCommand(command.Arg1, command.Arg2));
                        break;
                    case CommandType.C_POP:
                        output.AddRange(HandlePopCommand(command.Arg1, command.Arg2));
                        break;
                    default:
                        break;
                }
            }

            File.WriteAllLines(_filePath, output.ToArray());
        }

        private IList<string> ConstructComparisonCommand(string jumpCondition)
        {
            var command =  new List<string>
            {
                "@SP",
                "A=M-1",
                "D=M",
                "A=A-1",
                "D=M-D",
                $"@{_fileName}.IF_SKIP_{_comparisonLabelCounter}",
                $"D;{jumpCondition}",

                "@SP",
                "A=M-1",
                "A=A-1",
                "M=0",
                $"@{_fileName}.IF_END_{_comparisonLabelCounter}",
                "0;JMP",

                $"({_fileName}.IF_SKIP_{_comparisonLabelCounter})",
                "@SP",
                "A=M-1",
                "A=A-1",
                "M=-1",
                $"@{_fileName}.IF_END_{_comparisonLabelCounter}",
                "0;JMP",

                $"({_fileName}.IF_END_{_comparisonLabelCounter})",
                "@SP",
                "M=M-1"
            };

            _comparisonLabelCounter++;
            return command;
        }

        private IList<string> ConstructBitwiseOperationCommand(string operation)
        {
            return new List<string> {
                        "@SP",
                        "A=M-1",
                        "D=M",
                        "A=A-1",
                        $"M={operation}",
                        "@SP",
                        "M=M-1"
                    };
        }

        private IList<string> HandleArithmeticCommand(string operationName)
        {
            switch (operationName)
            {
                case "add":
                    {
                        var lines = new List<string> { "//add" };
                        lines.AddRange(ConstructBitwiseOperationCommand("D+M"));
                        return lines;
                    }
                case "sub":
                    {
                        var lines = new List<string> { "//sub" };
                        lines.AddRange(ConstructBitwiseOperationCommand("M-D"));
                        return lines;
                    }
                case "neg":
                    return new List<string>
                    {
                        "//neg",
                        "@SP",
                        "A=M-1",
                        "M=-M"
                    };
                case "eq":
                    {
                        var lines = new List<string> { "//eq" };
                        lines.AddRange(ConstructComparisonCommand("JEQ"));
                        return lines;
                    }
                case "gt":
                    {
                        var lines = new List<string> { "//gt" };
                        lines.AddRange(ConstructComparisonCommand("JGT"));
                        return lines;
                    }
                case "lt":
                    {
                        var lines = new List<string> { "//lt" };
                        lines.AddRange(ConstructComparisonCommand("JLT"));
                        return lines;
                    }
                case "and":
                    {
                        var lines = new List<string> { "//and" };
                        lines.AddRange(ConstructBitwiseOperationCommand("D&M"));
                        return lines;
                    }
                case "or":
                    {
                        var lines = new List<string> { "//or" };
                        lines.AddRange(ConstructBitwiseOperationCommand("D|M"));
                        return lines;
                    }
                case "not":
                    return new List<string>
                    {
                        "//not",
                        "@SP",
                        "A=M-1",
                        "M=!M"
                    };
                default:
                    throw new Exception("Invalid arithmetic operation");
            }
        }

        private IList<string> HandlePushCommand(string segment, int index)
        {
            var memorySegment = _memorySegmentLookup[segment];
            if (memorySegment == null) throw new Exception("Invalid memory segment");

            if (index < 0 || ((segment == "static" || segment == "temp" || segment == "constant" || segment == "pointer") && index > memorySegment.MaxIndex))
            {
                throw new Exception("Requested index out of bounds of memory segment");
            }

            if (segment == "static")
            {
                return new List<string>
                {
                    $"//push {segment} {index}",
                    $"@{_fileName}.{index}",
                    "D=M",
                    "@SP",
                    "A=M",
                    "M=D",
                    "@SP",
                    "M=M+1"
                };
            }
            else if (segment == "temp")
            {
                return new List<string>
                {
                    $"//push {segment} {index}",
                    $"@{index}",
                    "D=A",
                    $"@{memorySegment.Symbol}",
                    "A=D+A",
                    "D=M",
                    "@SP",
                    "A=M",
                    "M=D",
                    "@SP",
                    "M=M+1"
                };
            }
            else if (segment == "constant")
            {
                return new List<string>
                {
                    $"//push {segment} {index}",
                    $"@{index}",
                    "D=A",
                    "@SP",
                    "A=M",
                    "M=D",
                    "@SP",
                    "M=M+1"
                };
            }
            else if (segment == "pointer")
            {
                return new List<string>
                {
                    $"//push {segment} {index}",
                    $"@{(index == 0 ? "THIS" : "THAT")}",
                    "D=M",
                    "@SP",
                    "A=M",
                    "M=D",
                    "@SP",
                    "M=M+1"
                };
            }
            else
            {
                return new List<string>
                {
                    $"//push {segment} {index}",
                    $"@{index}",
                    "D=A",
                    $"@{memorySegment.Symbol}",
                    "A=M",
                    "A=D+A",
                    "D=M",
                    "@SP",
                    "A=M",
                    "M=D",
                    "@SP",
                    "M=M+1"
                };
            }
        }

        private IList<string> HandlePopCommand(string segment, int index)
        {
            var memorySegment = _memorySegmentLookup[segment];


            if (index < 0 || ((segment == "static" || segment == "temp" || segment == "constant" || segment == "pointer") && index > memorySegment.MaxIndex))
            {
                throw new Exception("Requested index out of bounds of memory segment");
            }

            if (segment == "constant") throw new Exception("Cannot pop to a constant segment");

            if (segment == "static")
            {
                return new List<string>
                {
                    $"//pop {segment} {index}",
                    "@SP",
                    "A=M-1",
                    "D=M",
                    $"@{_fileName}.{index}",
                    "M=D",
                    "@SP",
                    "M=M-1"
                };
            }
            else if (segment == "temp")
            {
                return new List<string>
                {
                    $"//pop {segment} {index}",
                    $"@{index}",
                    "D=A",
                    $"@{memorySegment.Symbol}",
                    "D=D+A",
                    "@temp",
                    "M=D",
                    "@SP",
                    "A=M-1",
                    "D=M",
                    "@temp",
                    "A=M",
                    "M=D",
                    "@SP",
                    "M=M-1"
                };
            }
            else if (segment == "pointer")
            {
                return new List<string>
                {
                    $"//pop {segment} {index}",
                    $"@{(index == 0 ? "THIS" : "THAT")}",
                    "D=A",
                    "@temp",
                    "M=D",
                    "@SP",
                    "A=M-1",
                    "D=M",
                    "@temp",
                    "A=M",
                    "M=D",
                    "@SP",
                    "M=M-1"
                };
            }
            else
            {
                return new List<string>
                {
                    $"//pop {segment} {index}",
                    $"@{index}",
                    "D=A",
                    $"@{memorySegment.Symbol}",
                    "A=M",
                    "D=D+A",
                    "@temp",
                    "M=D",
                    "@SP",
                    "A=M-1",
                    "D=M",
                    "@temp",
                    "A=M",
                    "M=D",
                    "@SP",
                    "M=M-1"
                };
            }
        }

        private string HandleLabelCommand(string label)
        {
            string labelAugmented = String.Empty;

            if (_currentFuction.Trim() == String.Empty || _currentFuction == null)
            {
                labelAugmented = $"{_fileName.ToUpper()}.{_currentFuction}${label}";
            }
            else
            {
                labelAugmented = $"{_fileName.ToUpper()}.{label}";
            }
            if (_labels.Contains(labelAugmented)) throw new Exception("Label already declared");

            return $"({labelAugmented})";
        }

        private IList<string> HandleBranchingCommand(CommandType commandType, string label)
        {
            string labelAugmented = String.Empty;

            if (_currentFuction.Trim() == String.Empty || _currentFuction == null)
            {
                labelAugmented = $"{_fileName.ToUpper()}.{_currentFuction}${label}";
            }
            else
            {
                labelAugmented = $"{_fileName.ToUpper()}.{label}";
            }

            if (commandType == CommandType.C_GOTO)
            {
                return new List<string> 
                {
                    $"@{labelAugmented}", 
                    "0;JMP" 
                };
            }
            else if (commandType == CommandType.C_IF_GOTO)
            {
                return new List<string>
                {
                    "@SP",
                    "A=M-1",
                    "D=M",
                    $"@{labelAugmented}",
                    "D;JNE"
                };
            }

            throw new Exception("Command is not a branching command");

        }

        private IList<string> HandleFunctionCommand(string functionName, int nVar) 
        {
            _functionCallRunningCount = 0;
            var outputLines = new List<string>();   
            _currentFuction = functionName;

            // Construct function label
            outputLines.Add($"({_fileName.ToUpper()}.{functionName})");

            // Initialise local variables
            for (int i = 0; i < nVar; i++) 
            {
                outputLines.AddRange(new List<String>
                {
                    "@SP",
                    "A=M",
                    "M=0",
                    "@SP",
                    "M=M+1"
                });
            }

            return outputLines;
        }

        private IList<string> HandleCallCommand(string functionName, int nArgs)
        {
            _functionCallRunningCount++;

            var outputLines = new List<string>();
            var returnLabel = $"{_fileName.ToUpper()}.${_currentFuction}$ret.${_functionCallRunningCount}";

            // Push ReturnAddress
            outputLines.AddRange(new List<string>
            {
                $"@{returnLabel}",
                "D=A",
                "@SP",
                "A=M",
                "M=D",
                "@SP",
                "M=M+1"
            });


            // Push LCL
            outputLines.AddRange(new List<string>
            {
                "@LCL",
                "D=A",
                "@SP",
                "A=M",
                "M=D",
                "@SP",
                "M=M+1"
            });

            // Push ARG
            outputLines.AddRange(new List<string>
            {
                "@ARG",
                "D=A",
                "@SP",
                "A=M",
                "M=D",
                "@SP",
                "M=M+1"
            });

            // Push THIS
            outputLines.AddRange(new List<string>
            {
                "@THIS",
                "D=A",
                "@SP",
                "A=M",
                "M=D",
                "@SP",
                "M=M+1"
            });

            // Push THAT
            outputLines.AddRange(new List<string>
            {
                "@THAT",
                "D=A",
                "@SP",
                "A=M",
                "M=D",
                "@SP",
                "M=M+1"
            });

            // Pop to ARG value SP-5-nArgs
            outputLines.AddRange(new List<string>
            {
                "@SP",
                "D=A",
                $"@{5 + nArgs}",
                "D=D-A",
                "@ARG",
                "M=D"
            });

            // Pop to LCL value SP
            outputLines.AddRange(new List<string>
            {
                "@SP",
                "D=A",
                "@ARG",
                "M=D"
            });

            // Goto functionName
            outputLines.AddRange(HandleBranchingCommand(CommandType.C_GOTO, functionName));

            // Declare Xxx.foo$ret.i
            outputLines.Add($"({returnLabel})");

            return outputLines;
        }

        private IList<string> HandleReturnCommand()
        {
            _currentFuction = String.Empty;
            var outputLines = new List<string>();

            // declare endFrame temp var and set to LCL
            outputLines.AddRange(new List<string>
            {
                "@LCL",
                "D=A",
                "@endFrame",
                "M=D"
            });

            // declare retAddr temp variable
            // set retAddr to endFrame - 5
            outputLines.AddRange(new List<string>
            {
                "@5",
                "D=A",
                "@endFrame",
                "D=M-D",
                "A=D",
                "D=M",
                "@retAddr",
                "M=D"
            });

            // pop to *ARG
            outputLines.AddRange(HandlePopCommand("arg", 0));

            // set SP to ARG + 1
            outputLines.AddRange(new List<string>
            {
                "@ARG",
                "D=A",
                "@SP",
                "M=D"
            });

            // set THAT to *(endFrame - 1)
            outputLines.AddRange(new List<string>
            {
                "@endFrame",
                "D=A",
                "@1",
                "D=D-A",
                "@THAT",
                "M=D"
            });

            // set THIS to *(endFrame - 2)
            outputLines.AddRange(new List<string>
            {
                "@endFrame",
                "D=A",
                "@2",
                "D=D-A",
                "@THIS",
                "M=D"
            });

            // set ARG to *(endFrame - 3)
            outputLines.AddRange(new List<string>
            {
                "@endFrame",
                "D=A",
                "@3",
                "D=D-A",
                "@ARG",
                "M=D"
            });            
            
            // set LCL to *(endFrame - 4)
            outputLines.AddRange(new List<string>
            {
                "@endFrame",
                "D=A",
                "@4",
                "D=D-A",
                "@LCL",
                "M=D"
            });

            // goto retAddr
            outputLines.AddRange(HandleBranchingCommand(CommandType.C_GOTO, ));

        }
    }
}
