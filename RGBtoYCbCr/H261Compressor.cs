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

namespace RGBtoYCbCr
{
    public class H261Compressor
    {
        private const int BLOCK_SIZE = 64;
        private List<double[,]> blocks;
        private const double X_IS_ZERO = 1 / 1.41421356237; // sqrt(2)
        private const int TYPE_Y = 0;
        private const int TYPE_CbCr = 1;

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
                        if (type == TYPE_Y) block[r, c] /= Luminance[r, c];
                        else block[r, c] /= Chrominance[r, c];
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

        public List<double[,]> DCTBlocksAndQuantize(byte[] ycrcb)
        {
            int i = 4;
            int width = ycrcb[0] << 8 | ycrcb[1];
            int height = ycrcb[2] << 8 | ycrcb[3];

            List<double[,]> yblock;
            List<double[,]> cbcrblock;

            yblock = Get8x8Blocks(ycrcb, ref i, TYPE_Y, width, height);
            cbcrblock = Get8x8Blocks(ycrcb, ref i, TYPE_CbCr);

            yblock = DCTBlocks(yblock);
            cbcrblock = DCTBlocks(cbcrblock);

            yblock = Quantize(yblock, TYPE_Y);
            cbcrblock = Quantize(cbcrblock, TYPE_CbCr);

            foreach (double[,] block in yblock)
                blocks.Add(block);

            foreach (double[,] block in cbcrblock)
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

        private List<double[,]> DCTBlocks(List<double[,]> blocks)
        {
            List<double[,]> dct_blocks = new List<double[,]>();
            for (int i = 0; i < blocks.Count; i++)
            {
                dct_blocks.Add(DCT(blocks[i], 8, 8));
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

        public Bitmap ConvertYCbCrtoRGB(byte[] ycrcb)
        {
            int width = ycrcb[0] << 8 | ycrcb[1];   // Retrieve width from the stored bytes
            int height = ycrcb[2] << 8 | ycrcb[3];  // Retrieve height from the stored bytes


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
