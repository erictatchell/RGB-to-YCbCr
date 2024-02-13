using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Diagnostics;

namespace RGBtoYCbCr
{
    

    public class RGBtoYCbCrSubsampling
    {
        // RGB to YCbCr conversion matrix constants
        private static readonly double[,] RGBtoYCrCb = {
            { 0.299, 0.587, 0.114 },
            { -0.168736, -0.331264, 0.5 },
            { 0.5, -0.418688, -0.081312 }
        };

        private static readonly double[,] YCrCbtoRGB = {
            { 1, 0, 1.4 },
            { 1, -0.343, -0.711 },
            { 1, 1.765, 0 }
        };


        public static Bitmap ConvertYCbCrtoRGB(byte[] ycrcb)
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

        private static double[,] Upsample(double[,] channel)
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



        public static byte[] ConvertRGBtoYCbCr(Bitmap rgbImage)
        {
            int width = rgbImage.Width;
            int height = rgbImage.Height;
            byte[] ycrcb = new byte[(int)(width * height * 1.5F + 4 + 3)];

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
            Debug.WriteLine(Y[0, 255]);

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

        public static double[,] Subsample(double[,] channel)
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
