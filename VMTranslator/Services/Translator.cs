using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VMTranslator.Models;

namespace VMTranslator.Services
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

        private VMFile _currentFile { get; set; }
        private string _currentFunctionName = string.Empty;
        private int _functionCallRunningCount = 0;
        private int _branchingLabelRunningCount = 0;
        private HashSet<string> _declaredLabels = new HashSet<string>();

        public IList<string> TranslateVMFile(VMFile vmFile)
        {
            _currentFile = vmFile;
            var output = new List<string>();

            foreach (var command in vmFile.ParsedCommands)
            {
                var lines = command.CommandType switch
                {
                    CommandType.C_PUSH => HandlePushCommand(command.Arg1, command.Arg2),
                    CommandType.C_POP => HandlePopCommand(command.Arg1, command.Arg2),
                    CommandType.C_ARITHMETIC => HandleArithmeticCommand(command.Arg1)
                };

                output.AddRange(lines);
            }

            return output;
        }

        public IList<string> HandlePushCommand(string segment, int index)
        {
            var memorySegment = _memorySegmentLookup[segment];
            if (memorySegment == null) throw new Exception("Invalid memory segment");

            if (index < 0 || index > memorySegment.MaxIndex) throw new Exception("Requested index out of bounds of memory segment");

            var fetchValueLines = segment switch
            {
                "static" => FetchValue($"{_currentFile.FileName}.{index}"),
                "temp" => FetchValue(memorySegment.Symbol, index),
                "constant" => FetchConstant(index),
                "pointer" => FetchValue(index == 0 ? "THIS" : "THAT"),
                _ => FetchValue(memorySegment.Symbol, index, true),
            };

            return [
                $"//push {segment} {index}",
                .. fetchValueLines,
                .. PushDToStack()
            ];
        }

        private IList<string> HandlePopCommand(string segment, int index)
        {
            var memorySegment = _memorySegmentLookup[segment];
            if (memorySegment == null) throw new Exception("Invalid memory segment");

            if (index < 0 || index > memorySegment.MaxIndex) throw new Exception("Requested index out of bounds of memory segment");
            if (segment == "constant") throw new Exception("Cannot pop to a constant segment");
            
            if (segment == "static")
            {
                return [
                    $"//pop {segment} {index}",
                    ..PopStackToD(),
                    ..WriteDToLabel($"{_currentFile.FileName}.{index}")
                ];
            }
            else
            {
                var saveDestinationAddressToTempVarLines = segment switch 
                {
                    "temp" => FetchAddress(memorySegment.Symbol, index),
                    "pointer" => FetchAddress((index == 0 ? "THIS" : "THAT")),
                    _ => FetchAddress(memorySegment.Symbol, index, true)
                };

                return [
                    $"//pop {segment} {index}",
                    .. saveDestinationAddressToTempVarLines,
                    .. WriteDToLabel("temp"),
                    .. PopStackToD(),
                    .. WriteDToLabel("temp", true)
                ];
            }
        }

        private IList<string> HandleArithmeticCommand(string operation)
        {
            var header = $"//{operation}";
            var logicLines = operation switch
            {
                "add" => [
                    .. PopStackToD(),
                    "@SP",
                    "A=M-1",
                    "M=D+M"
                ],
                "sub" => [
                    .. PopStackToD(),
                    "@SP",
                    "A=M-1",
                    "M=M-D"
                ],
                "neg" => [
                    "@SP",
                    "A=M-1",
                    "M=-M"
                ],
                "and" => [
                    .. PopStackToD(),
                    "@SP",
                    "A=M-1",
                    "M=D&M"
                ],
                "or" => [
                    .. PopStackToD(),
                    "@SP",
                    "A=M-1",
                    "M=D|M"
                ],
                "not" => [
                    "@SP",
                    "A=M-1",
                    "M=!M"
                ],
                "eq" => PushConditionalToStack("JEQ"),
                "gt" => PushConditionalToStack("JGT"),
                "lt" => PushConditionalToStack("JLT"),
            };

            if (logicLines.Count == 0) throw new Exception("Invalid arithmetic operation");
            return [
                header,
                .. logicLines
            ];
        }

        private string HandleLabelCommand(string label)
        {
            string labelAugmented = string.Empty;

            if (_currentFunctionName.Trim() == string.Empty || _currentFunctionName == null)
            {
                labelAugmented = $"{_currentFile.FileName.ToUpper()}.{_currentFunctionName}${label}";
            }
            else
            {
                labelAugmented = $"{_currentFile.FileName.ToUpper()}.{label}";
            }
            if (_declaredLabels.Contains(labelAugmented)) throw new Exception("Label already declared");

            return $"({labelAugmented})";
        }

        private IList<string> HandleBranchingCommand(CommandType commandType, string label)
        {
            string labelAugmented = string.Empty;

            if (_currentFunctionName.Trim() == string.Empty || _currentFunctionName == null)
            {
                labelAugmented = $"{_currentFile.FileName.ToUpper()}.{_currentFunctionName}${label}";
            }
            else
            {
                labelAugmented = $"{_currentFile.FileName.ToUpper()}.{label}";
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
                return [
                    .. PopStackToD(),
                    $"@{labelAugmented}",
                    "D;JNE"
                ];
            }

            throw new Exception("Command is not a branching command");

        }

        private IList<string> HandleFunctionCommand(string functionName, int nVar)
        {
            _functionCallRunningCount = 0;
            var outputLines = new List<string>();
            _currentFunctionName = functionName;

            // Construct function label
            outputLines.Add($"({_currentFile.FileName.ToUpper()}.{functionName})");

            // Initialise local variables
            for (int i = 0; i < nVar; i++)
            {
                outputLines.AddRange(new List<string>
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
            var returnLabel = $"{_currentFile.FileName.ToUpper()}.${_currentFunctionName}$ret.${_functionCallRunningCount}";

            return [
                // Push return label of caller address to stack
                .. FetchValue(returnLabel),
                .. PushDToStack(),
                // Push LCL address of caller to stack
                .. FetchValue("LCL"),
                .. PushDToStack(),
                // Push ARG address of caller to stack
                .. FetchValue("ARG"),
                .. PushDToStack(),
                // Push THIS address of caller to stack
                .. FetchValue("THIS"),
                .. PushDToStack(),
                // Push THAT address of caller to stack
                .. FetchValue("THAT"),
                .. PushDToStack(),
                // Write SP - 5 - nArgs to ARG
                "@SP",
                "D=A",
                $"@{5 + nArgs}",
                "D=D-A",
                .. WriteDToLabel("ARG"),
                // Write SP to LCL
                "@SP",
                "D=A",
                .. WriteDToLabel("LCL"),
                // GOTO functionName
                .. HandleBranchingCommand(CommandType.C_GOTO, functionName),
                // Declare return label
                $"({returnLabel})"
            ];
        }

        private IList<string> HandleReturnCommand()
        {
            _currentFunctionName = string.Empty;

            return [
                // declare endFrame temp var and set to LCL
                .. FetchAddress("LCL"),
                .. WriteDToLabel("endFrame"),
                // declare retAddr temp variable and set to endFrame  - 5
                .. FetchAddress("endFrame", -5, true),
                .. WriteDToLabel("@retAddr"),
                // Pop to 
                .. HandlePopCommand("arg", 0),
                // Set SP to ARG + 1
                .. FetchAddress("ARG"),
                "@SP",
                "M=D",
                // set THAT to *(endFrame - 1)
                .. FetchAddress("endFrame", -1),
                .. WriteDToLabel("THAT"),
                // set THIS to *(endFrame - 2)
                .. FetchAddress("endFrame", -2),
                .. WriteDToLabel("THIS"),
                // set ARG to *(endFrame - 3)
                .. FetchAddress("endFrame", -3),
                .. WriteDToLabel("ARG"),
                // set LCL to *(endFrame - 4)
                .. FetchAddress("endFrame", -4),
                .. WriteDToLabel("LCL"),
                // goto retAddr
                "@retAddr",
                "A=M",
                "0;JMP"
            ];
        }






        private IList<string> FetchConstant(int value)
        {
            return [
                $"@{value}",
                "D=A"
            ];
        }

        private IList<string> FetchAddress(string label, int offset = 0, bool dereference = false)
        {
            if (offset > 0)
            {
                if (dereference)
                {
                    return [
                        $"@{offset}",
                        "D=A",
                        $"@{label}",
                        "D=D+M",
                    ];
                }
                else
                {
                    return [
                        $"@{offset}",
                        "D=A",
                        $"@{label}",
                        "D=D+A",
                    ];
                }

            }
            else if (offset < 0)
            {
                if (dereference)
                {
                    return [
                        $"@{Math.Abs(offset)}",
                        "D=A",
                        $"@{label}",
                        "D=M-D",
                    ];
                }
                else
                {
                    return [
                        $"@{Math.Abs(offset)}",
                        "D=A",
                        $"@{label}",
                        "D=A-D",
                    ];
                }
            }
            else
            {
                if (dereference)
                {
                    return [
                        $"@{label}",
                        "D=M",
                    ];
                }
                else
                {
                    return [
                        $"@{label}",
                        "D=A",
                    ];
                }
            }
        }

        private IList<string> FetchValue(string label, int offset = 0, bool dereference = false)
        {
            if (offset > 0)
            {
                if (dereference)
                {
                    return [
                        $"@{offset}",
                        "D=A",
                        $"@{label}",
                        "A=D+M",
                        "D=M"
                    ];
                }
                else
                {
                    return [
                        $"@{offset}",
                        "D=A",
                        $"@{label}",
                        "A=D+A",
                        "D=M",
                    ];
                }

            }
            else if (offset < 0)
            {
                if (dereference)
                {
                    return [
                        $"@{Math.Abs(offset)}",
                        "D=A",
                        $"@{label}",
                        "A=M-D",
                        "D=M"
                    ];
                }
                else
                {
                    return [
                        $"@{Math.Abs(offset)}",
                        "D=A",
                        $"@{label}",
                        "D=A-D",
                        "D=M"
                    ];
                }
            }
            else
            {
                if (dereference)
                {
                    return [
                        $"@{label}",
                        "A=M",
                        "D=M"
                    ];
                }
                else
                {
                    return [
                        $"@{label}",
                        "D=M",
                    ];
                }
            }
        }

        private IList<string> PushDToStack()
        {
            return [
                "@SP",
                "A=M",
                "M=D",
                ..IncrementStack()
            ];
        }

        private IList<string> PopStackToD()
        {
            return [
                "@SP",
                "A=M-1",
                "D=M",
                .. DecrementStack()
            ];
        }

        private IList<string> WriteDToLabel(string label, bool dereference = false)
        {
            if (dereference)
            {
                return [
                    $"@{label}",
                    "A=M",
                    "M=D"
                ];
            }
            else
            {
                return [
                    $"@{label}",
                    "M=D"
                ];
            }
        }

        private IList<string> WriteLabelToD(string label, bool dereference = false)
        {
            if (dereference)
            {
                return [
                    $"@{label}",
                    "A=M",
                    "D=M"
                ];
            }
            else
            {
                return [
                    $"@{label}",
                    "D=M"
                ];
            }
        }

        private IList<string> IncrementStack()
        {
            return [
                "@SP",
                "M=M+1"
            ];
        }

        private IList<string> DecrementStack()
        {
            return [
                "@SP",
                "M=M-1"
            ];
        }

        private IList<string> PushConditionalToStack(string jumpCondition)
        {
            List<string> output = [
                .. PopStackToD(),
                "@SP",
                "A=M-1",
                "D=M-D",
                $"@{_currentFile.FileName}.IF_SKIP_{_branchingLabelRunningCount}",
                $"D;{jumpCondition}",

                "@SP",
                "A=M-1",
                "M=0",
                $"@{_currentFile.FileName}.IF_END_{_branchingLabelRunningCount}",
                "0;JMP",

                $"({_currentFile.FileName}.IF_SKIP_{_branchingLabelRunningCount})",
                "@SP",
                "A=M-1",
                "M=-1",
                $"@{_currentFile.FileName}.IF_END_{_branchingLabelRunningCount}",
                "0;JMP",

                $"({_currentFile.FileName}.IF_END_{_branchingLabelRunningCount})",
            ];

            _branchingLabelRunningCount++;
            return output;
        }
    }
}
