using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMTranslator.Models
{
    public class MemorySegment
    {
        public string Symbol { get; set; }
        public int MaxIndex { get; set; }
    }
}
