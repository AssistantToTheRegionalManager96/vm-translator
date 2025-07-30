using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMTranslator.Models
{
    public class ParsedCommand
    {
        public CommandType CommandType { get; set; }
        public string Arg1 { get; set; }
        public int Arg2 { get; set; }
    }
}
