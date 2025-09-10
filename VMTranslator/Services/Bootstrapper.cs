using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMTranslator.Services
{
    public class Bootstrapper
    {
        public Bootstrapper()
        {
                
        }

        public IList<string> GenerateBootstrapCode()
        {
            return [
                "//Bootstrapping",
                ..SetMemoryPointer("SP", 256),
                ..SetMemoryPointer("LCL", 300),
                ..SetMemoryPointer("ARG", 400),
                ..SetMemoryPointer("THIS", 3000),
                ..SetMemoryPointer("THAT", 4000),

                "//Initialising Sys.init",
                "@Sys.init",
                "0;JMP"
            ];
        }

        private IList<string> SetMemoryPointer(string pointer, int address)
        {
            return [
                $"@{address}",
                "D=A",
                $"@{pointer}",
                "M=D"
            ];
        }
    }
}
