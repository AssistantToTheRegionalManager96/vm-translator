using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMTranslator.Models
{
    public class VMFile
    {
        public string FileName { get; set; }
        public IList<ParsedCommand> ParsedCommands { get; set; }
    }
}
