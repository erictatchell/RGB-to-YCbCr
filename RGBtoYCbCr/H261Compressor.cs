using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Forms.Design;
using System.Data;

namespace RGBtoYCbCr
{
    public class H261Compressor
    {
        private int width = 0;
        private int height = 0;
        private const int BLOCK_SIZE = 64;
        private List<double[,]> blocks;
        private const double X_IS_ZERO = 1 / 1.41421356237; // sqrt(2)
        public const int TYPE_Y = 0;
        public const int TYPE_CbCr = 1;

        public H261Compressor(List<double[,]>? blocks = null)
        {
            this.blocks = blocks ?? new List<double[,]>();
        }

        double C(int x)
        {
            if (x == 0)
                return X_IS_ZERO;
            else
                return 1;
        }
        // RGB to YCbCr conversion matrix constants
        private readonly double[,] RGBtoYCrCb = {
            { 0.299, 0.587, 0.114 },
            { -0.168736, -0.331264, 0.5 },
            { 0.5, -0.418688, -0.081312 }
        };

        private readonly double[,] YCrCbtoRGB = {
            { 1, 0, 1.4 },
            { 1, -0.343, -0.711 },
            { 1, 1.765, 0 }
        };

        // luminance quantization matrix constants
        private readonly int[,] Luminance =
        {
            {16, 11, 10, 16, 24, 40, 51, 61 },
            {12, 12, 14, 19, 26, 58, 60, 55 },
            {14, 13, 16, 24, 40, 57, 69, 56 },
            {14, 17, 22, 29, 51, 87, 80, 62 },
            {18, 22, 37, 56, 68, 109, 103, 77 },
            {24, 35, 55, 64, 81, 104, 113, 92 },
            {49, 64, 78, 87, 103, 121, 120, 101 },
            {72, 92, 95, 98, 112, 100, 103, 99 }
        };

        // chrominance quantization matrix constants
        private readonly int[,] Chrominance =
        {
            {17, 18, 24, 47, 99, 99, 99, 99 },
            {18, 21, 26, 66, 99, 99, 99, 99 },
            {24, 26, 56, 99, 99, 99, 99, 99 },
            {47, 66, 99, 99, 99, 99, 99, 99 },
            {99, 99, 99, 99, 99, 99, 99, 99 },
            {99, 99, 99, 99, 99, 99, 99, 99 },
            {99, 99, 99, 99, 99, 99, 99, 99 },
            {99, 99, 99, 99, 99, 99, 99, 99 }
        };

        private List<double[,]> Quantize(List<double[,]> blocks, int type)
        {
            foreach (double[,] block in blocks)
            {
                for (int r = 0; r < 8; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        if (type == TYPE_Y)
                            block[r, c] = Math.Round(block[r, c] /= Luminance[r, c]);
                        else
                            block[r, c] = Math.Round(block[r, c] /= Chrominance[r, c]);
                    }
                }
            }
            return blocks;
        }

        public List<double[,]> Dequantize(List<double[,]> blocks, int type)
        {
            foreach (double[,] block in blocks)
            {
                for (int r = 0; r < 8; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        if (type == TYPE_Y)
                            block[r, c] = Math.Round(block[r, c] *= Luminance[r, c]);
                        else
                            block[r, c] = Math.Round(block[r, c] *= Chrominance[r, c]);
                    }
                }
            }
            return blocks;
        }


        private double[,] GetBlock(byte[] ycrcb, ref int i, int type, int width = 0, int height = 0) // pass by reference
        {
            double[,] block = new double[8, 8];
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    // if Y, ensure cr's and cb's dont sneak in
                    // i.e YYYY...YYYYCrCr
                    if (i >= ycrcb.Length || (i >= width * height && type == TYPE_Y)) 
                        block[r, c] = 0;
                    else
                        block[r, c] = ycrcb[i++];
                }
            }
            return block;
        }

        public List<byte> GetBytesFromBlock(double[,] block)
        {
            List<byte> bytes = new List<byte>();
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (block[r, c] != 0)
                    {
                        bytes.Add((byte)block[r, c]);
                    }
                }
            }
            return bytes;
        }





        public void AddWidthHeightData(List<byte> list, byte[] ycrcb)
        {
            int width = ycrcb[0] << 8 | ycrcb[1];
            int height = ycrcb[2] << 8 | ycrcb[3];

            list.Add((byte)(width >> 8));
            list.Add((byte)(width & 0xFF));
            list.Add((byte)(height >> 8));
            list.Add((byte)(height & 0xFF));
        }


        public List<byte> ConvertToBytes(List<Tuple<int, int>> tupleList)
        {
            List<byte> byteList = new List<byte>();

            // Iterate through the tuples and add them to the byte list
            foreach (var tuple in tupleList)
            {
                byteList.Add((byte)tuple.Item1);
                byteList.Add((byte)tuple.Item2);
            }

            // Add a delimiter to indicate the end of a block
            byteList.Add(byte.MinValue);
            byteList.Add(byte.MinValue);

            return byteList;
        }

        public List<List<Tuple<int, int>>> ConvertBackToListOfTuples(List<byte> byteList)
        {
            List<List<Tuple<int, int>>> listOfBlocks = new List<List<Tuple<int, int>>>();
            List<Tuple<int, int>> currentBlock = new List<Tuple<int, int>>();

            // Iterate through the byte list
            for (int i = 0; i < byteList.Count; i++)
            {
                // Check if the current byte represents the block delimiter
                if (byteList[i] == byte.MinValue && byteList[i + 1] == byte.MinValue)
                {
                    // Add the current block to the list of blocks
                    listOfBlocks.Add(currentBlock);

                    // Reset the current block for the next iteration
                    currentBlock = new List<Tuple<int, int>>();

                    // Increment i by 1 to skip the second delimiter byte
                    i++;
                }
                else
                {
                    // Decode the value from bytes
                    int item1 = DecodeValue(byteList[i++]);
                    int item2 = DecodeValue(byteList[i]);
                    currentBlock.Add(new Tuple<int, int>(item1, item2));
                }
            }

            return listOfBlocks;
        }

        // Helper method to decode a byte into its original value
        private int DecodeValue(byte value)
        {
            // If the byte value is greater than 127, it's a negative number encoded as an unsigned byte
            if (value > 127)
            {
                // Convert it back to a negative number
                return value - 256;
            }
            else
            {
                // Positive number
                return value;
            }
        }



        public double[,] InverseRLE(List<Tuple<int, int>> list)
        {
            double[,] result = new double[8, 8];
            int rowIndex = 0;
            int colIndex = 0;

            foreach (Tuple<int, int> pair in list)
            {
                for (int i = 0; i < pair.Item1; i++)
                {
                    result[rowIndex, colIndex++] = 0;
                    if (colIndex == 8)
                    {
                        colIndex = 0;
                        rowIndex++;
                    }
                }
                result[rowIndex, colIndex++] = pair.Item2;
                if (colIndex == 8)
                {
                    colIndex = 0;
                    rowIndex++;
                }
            }

            result = ReverseZigzagOrder(result);
            return result;
        }


        public List<Tuple<int, int>> RLE(double[,] array)
        {
            List<Tuple<int, int>> result = new List<Tuple<int, int>>();
            int runLength = 0;
            double value = 0;
            array = ZigzagOrder(array);

            for (int i = 0; i < array.GetLength(0); i++)
            {
                for (int j = 0; j < array.GetLength(1); j++)
                {
                    if (Math.Round(array[i, j]) == 0)
                    {
                        runLength++;
                    }
                    else
                    {
                        value = array[i, j];
                        Tuple<int, int> pair = new Tuple<int, int>(runLength, (int)value);
                        result.Add(pair);
                        runLength = 0;
                    }
                }
            }

            return result;
        }

        private double[,] ZigzagOrder(double[,] quantizedBlock)
        {
            int numRows = quantizedBlock.GetLength(0);
            int numCols = quantizedBlock.GetLength(1);
            double[,] zigzagOrder = new double[numRows, numCols];

            int[] zigzagIndices = new int[]
            {
                0, 1, 8, 16, 9, 2, 3, 10,
                17, 24, 32, 25, 18, 11, 4, 5,
                12, 19, 26, 33, 40, 48, 41, 34,
                27, 20, 13, 6, 7, 14, 21, 28,
                35, 42, 49, 56, 57, 50, 43, 36,
                29, 22, 15, 23, 30, 37, 44, 51,
                58, 59, 52, 45, 38, 31, 39, 46,
                53, 60, 61, 54, 47, 55, 62, 63
            };

            int index = 0;
            for (int i = 0; i < numRows; i++)
            {
                for (int j = 0; j < numCols; j++)
                {
                    zigzagOrder[i, j] = quantizedBlock[zigzagIndices[index] / numRows, zigzagIndices[index] % numCols];
                    index++;
                }
            }

            return zigzagOrder;
        }

        private double[,] ReverseZigzagOrder(double[,] zigzagOrder)
        {
            int numRows = zigzagOrder.GetLength(0);
            int numCols = zigzagOrder.GetLength(1);
            double[,] regularOrder = new double[numRows, numCols];

            int[] zigzagIndices = new int[]
            {
                0, 1, 8, 16, 9, 2, 3, 10,
                17, 24, 32, 25, 18, 11, 4, 5,
                12, 19, 26, 33, 40, 48, 41, 34,
                27, 20, 13, 6, 7, 14, 21, 28,
                35, 42, 49, 56, 57, 50, 43, 36,
                29, 22, 15, 23, 30, 37, 44, 51,
                58, 59, 52, 45, 38, 31, 39, 46,
                53, 60, 61, 54, 47, 55, 62, 63
            };

            int index = 0;
            for (int i = 0; i < numRows; i++)
            {
                for (int j = 0; j < numCols; j++)
                {
                    regularOrder[zigzagIndices[index] / numRows, zigzagIndices[index] % numCols] = zigzagOrder[i, j];
                    index++;
                }
            }

            return regularOrder;
        }


        public List<double[,]> DCTBlocksAndQuantize(byte[] ycrcb)
        {
            int i = 4;
            int width = ycrcb[0] << 8 | ycrcb[1];
            int height = ycrcb[2] << 8 | ycrcb[3];

            List<double[,]> yblocks;
            List<double[,]> cbcrblocks;

            yblocks = Get8x8Blocks(ycrcb, ref i, TYPE_Y, width, height);
            cbcrblocks = Get8x8Blocks(ycrcb, ref i, TYPE_CbCr);

            yblocks = DCTBlocks(yblocks);
            cbcrblocks = DCTBlocks(cbcrblocks);

            // yblock after dct, before quantize
            foreach (var block in yblocks)
            {
                Debug.WriteLine(block);
            }

            yblocks = Quantize(yblocks, TYPE_Y);
            cbcrblocks = Quantize(cbcrblocks, TYPE_CbCr);

            foreach (double[,] block in yblocks)
                blocks.Add(block);

            foreach (double[,] block in cbcrblocks)
                blocks.Add(block);


            return blocks;
        }
        private List<double[,]> Get8x8Blocks(byte[] ycrcb, ref int i, int type, int width = 0, int height = 0)
        {
            List<double[,]> blocks = new List<double[,]>();

            // byte structure is YYYYYYY...CbCbCbCb.....CrCrCrCr
            // image is 4:2:0 subsampled, therefore there are w * h Y's, and (w * h) / 2 cb's and cr's
            // if y, go as long as w * h
            // if cbcr, go as long as the length. 
            while (type == TYPE_CbCr ? i < ycrcb.Length : i < width * height)
            {
                double[,] cbcrblock = GetBlock(ycrcb, ref i, type, width, height);
                blocks.Add(cbcrblock);
            }
            return blocks;
        }

        public double satuaration(double value)
        {
            if (value > 255)
            {
                return 255;
            }
            else if (value < 0)
            {
                return 0;
            }
            else
            {
                return value;
            }
        }

        private List<double[,]> DCTBlocks(List<double[,]> blocks)
        {
            List<double[,]> dct_blocks = new List<double[,]>();
            for (int i = 0; i < blocks.Count; i++)
            {
                dct_blocks.Add(DCT(blocks[i], 8, 8));
            }
            return dct_blocks;
        }

        public List<double[,]> IDCTBlocks(List<double[,]> blocks)
        {
            List<double[,]> dct_blocks = new List<double[,]>();
            for (int i = 0; i < blocks.Count; i++)
            {
                dct_blocks.Add(IDCT(blocks[i], 8, 8));
            }
            return dct_blocks;
        }

        public double[,] DCT(double[,] F, int n, int m)
        {
            double[,] result = new double[n, m];
            for (int u = 0; u < n; u++)
            {
                for (int v = 0; v < m; v++)
                {

                    double sum = 0;
                    for (int x = 0; x < n; x++)
                    {
                        for (int y = 0; y < m; y++)
                        {
                            sum += Math.Cos(((2 * x + 1) * u * Math.PI) / (2 * n)) * Math.Cos(((2 * y + 1) * v * Math.PI) / (2 * m)) * F[x, y];
                        }
                    }
                    sum *= C(u) * C(v) * (2 / Math.Sqrt(n * m));
                    result[u, v] = sum;
                }
            }
            return result;

        }

        public double[,] IDCT(double[,] H, int n, int m)
        {
            double[,] result = new double[n, m];
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < m; y++)
                {
                    double sum = 0;
                    for (int u = 0; u < n; u++)
                    {
                        for (int v = 0; v < m; v++)
                        {
                            sum += 2 * ((C(u) * C(v)) / Math.Sqrt(n * m)) * Math.Cos(((2 * x + 1) * u * Math.PI) / (2 * n)) * Math.Cos(((2 * y + 1) * v * Math.PI) / (2 * m)) * H[u, v];
                        }
                    }
                    result[x, y] = sum;
                }
            }
            return result;
        }
        public Bitmap ConvertYCbCrtoRGB(byte[] ycrcb, int width, int height)
        {
            double[,] Y = new double[width, height];
            double[,] Cb = new double[width / 2, height / 2];
            double[,] Cr = new double[width / 2, height / 2];

            int i = 4;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Y[x, y] = ycrcb[i++];
                }
            }
            for (int y = 0; y < height / 2; y++)
            {
                for (int x = 0; x < width / 2; x++)
                {
                    Cb[x, y] = ycrcb[i++];
                }
            }
            for (int y = 0; y < height / 2; y++)
            {
                for (int x = 0; x < width / 2; x++)
                {
                    Cr[x, y] = ycrcb[i++];
                }
            }

            Cb = Upsample(Cb);
            Cr = Upsample(Cr);

            Bitmap rgbImage = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double yValue = Y[x, y];
                    double cbValue = Cb[x, y] - 128;
                    double crValue = Cr[x, y] - 128;

                    /*
                     
                    byte array ycrcb = WH YYYYYYYY CbCbCbCb CrCrCrCr
                    Y = [4, 4]
                    Cb = [2, 2]
                    Cr = [2, 2]

                    height = 4
                    width = 4
                    for y = 0 to height
                        for x = 0 to width

                    Cr and Cb cannot read past 2, indexoutofbounds exception

                    */

                    // Convert YCbCr to RGB
                    double r = YCrCbtoRGB[0, 0] * yValue +
                               YCrCbtoRGB[0, 1] * cbValue +
                               YCrCbtoRGB[0, 2] * crValue;

                    double g = YCrCbtoRGB[1, 0] * yValue +
                               YCrCbtoRGB[1, 1] * cbValue +
                               YCrCbtoRGB[1, 2] * crValue;

                    double b = YCrCbtoRGB[2, 0] * yValue +
                               YCrCbtoRGB[2, 1] * cbValue +
                               YCrCbtoRGB[2, 2] * crValue;

                    r = Math.Max(0, Math.Min(255, r));
                    g = Math.Max(0, Math.Min(255, g));
                    b = Math.Max(0, Math.Min(255, b));

                    Color rgbColor = Color.FromArgb((int)r, (int)g, (int)b);
                    rgbImage.SetPixel(x, y, rgbColor);
                }
            }

            return rgbImage;
        }

        private double[,] Upsample(double[,] channel)
        {
            int width = channel.GetLength(0) * 2;
            int height = channel.GetLength(1) * 2;
            double[,] upsampled = new double[width, height];

            int cy = 0;
            for (int y = 0; y < height / 2; y ++)
            {
                int cx = 0;
                for (int x = 0; x < width / 2; x ++)
                {
                    // 2 x 2 block of pixels
                    upsampled[cx, cy] = channel[x, y];
                    upsampled[cx + 1, cy] = channel[x, y];
                    upsampled[cx, cy + 1] = channel[x, y];
                    upsampled[cx + 1, cy + 1] = channel[x, y];
                    cx += 2;
                }
                cy += 2;
            }

            return upsampled;
        }


        public byte[] ConvertRGBtoYCbCr(Bitmap rgbImage)
        {
            int width = rgbImage.Width;
            int height = rgbImage.Height;
            this.width = width;
            this.height = height;
            byte[] ycrcb = new byte[(int)(width * height * 1.5F + 4)];

            double[,] Y = new double[width, height];
            double[,] Cb = new double[width, height];
            double[,] Cr = new double[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixelColor = rgbImage.GetPixel(x, y);

                    Y[x, y] = RGBtoYCrCb[0, 0] * pixelColor.R +
                               RGBtoYCrCb[0, 1] * pixelColor.G +
                               RGBtoYCrCb[0, 2] * pixelColor.B;

                    Cb[x, y] = RGBtoYCrCb[1, 0] * pixelColor.R +
                                RGBtoYCrCb[1, 1] * pixelColor.G +
                                RGBtoYCrCb[1, 2] * pixelColor.B + 128;

                    Cr[x, y] = RGBtoYCrCb[2, 0] * pixelColor.R +
                                RGBtoYCrCb[2, 1] * pixelColor.G +
                                RGBtoYCrCb[2, 2] * pixelColor.B + 128;
                }
            }

            Cb = Subsample(Cb);
            Cr = Subsample(Cr);
            int i = 0;
            ycrcb[i++] = (byte)(width >> 8);     // Store the most significant byte of width
            ycrcb[i++] = (byte)(width & 0xFF);   // Store the least significant byte of width
            ycrcb[i++] = (byte)(height >> 8);    // Store the most significant byte of height
            ycrcb[i++] = (byte)(height & 0xFF);  // Store the least significant byte of height

            for(int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    ycrcb[i++] = (byte)(Y[x, y]);
                }
            }
            Debug.WriteLine(i + ", Value:" + ycrcb[i]);
            for(int y = 0; y < height / 2; y++)
            {
                for (int x = 0; x < width / 2; x++)
                {
                    ycrcb[i++] = (byte)(Cb[x, y]);
                }
            }
            for (int y = 0; y < height / 2; y++)
            {
                for (int x = 0; x < width / 2; x++)
                {
                    ycrcb[i++] = (byte)(Cr[x, y]);
                }
            }
            return ycrcb;
        }

        public double[,] Subsample(double[,] channel)
        {
            int height = channel.GetLength(1) / 2;
            int width = channel.GetLength(0) / 2;
            double[,] newnew = new double[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    newnew[x, y] = channel[x * 2, y * 2];
                }
            }
            return newnew;
        }
    };
}
