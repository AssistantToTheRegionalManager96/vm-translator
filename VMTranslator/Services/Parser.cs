using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMTranslator.Models;

namespace VMTranslator.Services
{
    public class Parser
    {
        private Dictionary<string, CommandType> _commandLookup = new Dictionary<string, CommandType>
        {
            { "add", CommandType.C_ARITHMETIC },
            { "sub", CommandType.C_ARITHMETIC },
            { "neg", CommandType.C_ARITHMETIC },
            { "eq", CommandType.C_ARITHMETIC },
            { "lt", CommandType.C_ARITHMETIC },
            { "gt", CommandType.C_ARITHMETIC },
            { "and", CommandType.C_ARITHMETIC },
            { "or", CommandType.C_ARITHMETIC },
            { "not", CommandType.C_ARITHMETIC },
            { "pop", CommandType.C_POP },
            { "push", CommandType.C_PUSH },
            { "label", CommandType.C_LABEL },
            { "goto", CommandType.C_GOTO },
            { "if-goto", CommandType.C_IF_GOTO },
            { "function", CommandType.C_FUNCTION },
            { "call", CommandType.C_CALL },
            { "return", CommandType.C_RETURN }
        };

        public IList<VMFile> ParseDirectory(string path)
        {
            var vmFiles = new List<VMFile>();
            var files = Directory.GetFiles(path, "*.vm");

            foreach (var file in files)
            {
                vmFiles.Add(new VMFile
                {
                    FileName = Path.GetFileNameWithoutExtension(file),
                    ParsedCommands = ParseFile(file)
                });
            }

            return vmFiles;
        }

        public IList<ParsedCommand> ParseFile(string filePath)
        {
            IList<string> lines = new List<string>();
            using (StreamReader sr = new StreamReader(filePath))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine().Split("//")[0].Trim();
                    if (line.Length != 0) lines.Add(line);
                }
            }

            IList<ParsedCommand> parsedCommands = new List<ParsedCommand>();
            foreach (var line in lines)
            {
                var parts = line.Split(null);
                parts = parts.Where(x => x.Trim().Length > 0).ToArray();
                var count = parts.Count();

                if (count < 1 || count > 3) throw new Exception("Invalid command");
                var commandPart = parts[0];
                var segmentPart = count > 1 ? parts[1] : "";

                int indexPart;
                if (count > 2)
                {
                    if (!int.TryParse(parts[2], out indexPart)) throw new Exception("Invalid command");
                }
                else indexPart = -1;

                if (!_commandLookup.TryGetValue(commandPart, out var commandType)) throw new Exception("Unidentified command");

                parsedCommands.Add(new ParsedCommand
                {
                    CommandType = commandType,
                    Arg1 = commandType == CommandType.C_ARITHMETIC ? commandPart : segmentPart,
                    Arg2 = indexPart
                });

            }

            return parsedCommands;
        }
    }
}
