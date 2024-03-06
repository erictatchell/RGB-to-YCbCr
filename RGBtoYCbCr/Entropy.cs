using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RGBtoYCbCr
{
    public class RLESymbol
    {
        public int RunLength { get; set; }
        public double Coefficient { get; set; }

        public RLESymbol(int runLength, double coefficient)
        {
            RunLength = runLength;
            Coefficient = coefficient;
        }
    }

    public class HuffmanTreeNode
    {
        public RLESymbol Symbol { get; set; }
        public HuffmanTreeNode Left { get; set; }
        public HuffmanTreeNode Right { get; set; }
    }

    public class HuffmanCode
    {
        public string Code { get; set; }
        public RLESymbol Symbol { get; set; }
    }

}
