using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace RGBtoYCbCr
{
    public partial class Form1 : Form
    {
        byte[] file;
        List<List<Tuple<int, int>>> encoded;
        List<byte> compressed;
        H261Compressor cmp;
        Bitmap originalImage;
        Bitmap convertedImage;
        string fileContent;
        string? filePath;
        public Form1()
        {
            cmp = new H261Compressor();
            fileContent = new string(string.Empty);
            originalImage = new Bitmap(ClientSize.Width, ClientSize.Height);
            convertedImage = new Bitmap(ClientSize.Width, ClientSize.Height);
            filePath = string.Empty;
            InitializeComponent();
        }



        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            string filePath;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Open Image";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    originalImage = new Bitmap(openFileDialog.FileName);
                    originalImage = new Bitmap(originalImage, originalImage.Width, originalImage.Height);

                    Invalidate();
                }
            }
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (originalImage != null)
            {
                e.Graphics.DrawImage(originalImage, Point.Empty);
                e.Graphics.DrawImage(convertedImage, new Point(originalImage.Width + 5));
            }
        }
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            filePath = "C:\\Users\\Eric\\source\\repos\\RGB-to-YCbCr\\RGBtoYCbCr\\face1.eric";
            List<byte> compressed = new List<byte>();

            byte[] ycrcb = cmp.ConvertRGBtoYCbCr(originalImage);

            cmp.AddWidthHeightData(compressed, ycrcb);

            System.IO.File.WriteAllBytes(filePath, ycrcb);

            encoded = new List<List<Tuple<int, int>>>();

            List<double[,]> blocks = cmp.DCTBlocksAndQuantize(ycrcb);
            foreach (double[,] block in blocks)
            {
                encoded.Add(cmp.RLE(block));
            }

            foreach (List<Tuple<int, int>> block in encoded)
            {
                compressed.AddRange(cmp.ConvertToBytes(block));
            }
            byte[] compressedArr = compressed.ToArray();
            this.compressed = compressed;
            System.IO.File.WriteAllBytes(filePath, compressedArr);
            //convertedImage = RGBtoYCbCrSubsampling.ConvertYCbCrtoRGB(ycrcb);
            Invalidate();

        }

        private void Form1_Load_1(object sender, EventArgs e)
        {

        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            List<byte> compressed = this.compressed;

            // Extract width and height
            int width = (compressed[0] << 8) | compressed[1];
            int height = (compressed[2] << 8) | compressed[3];
            compressed.RemoveRange(0, 4);

            // get encoded
            List<List<Tuple<int, int>>> encodedblocks = cmp.ConvertBackToListOfTuples(compressed);

            // decode
            List<double[,]> blocks = new List<double[,]>();
            foreach (List<Tuple<int, int>> encodedblock in encodedblocks)
            {
                blocks.Add(cmp.InverseRLE(encodedblock));
            }

            // dequantize
            List<double[,]> yblocks = new List<double[,]>();
            List<double[,]> cbcrblocks = new List<double[,]>();
            int i = 0, k = 0;
            while (k < width * height / 64)
            {
                yblocks.Add(blocks[i++]);
                k++;
            }
            k = 0;
            while (k < (width * height / 64) / 2 + 1)
            {
                cbcrblocks.Add(blocks[i++]);
                k++;
            }
            yblocks = cmp.Dequantize(yblocks, H261Compressor.TYPE_Y);

            // yblock before IDCT, after dequantize
            foreach (var block in yblocks)
            {

            }

            cbcrblocks = cmp.Dequantize(cbcrblocks, H261Compressor.TYPE_Y);

            // inverse DCT
            yblocks = cmp.IDCTBlocks(yblocks);
            cbcrblocks = cmp.IDCTBlocks(cbcrblocks);

            List<byte> bytes = new List<byte>();
            foreach (var block in yblocks)
            {
                List<byte> data = cmp.GetBytesFromBlock(block);
                foreach (byte b in data)
                {
                    bytes.Add(b);
                }
            }
            foreach (var block in cbcrblocks)
            {
                List<byte> data = cmp.GetBytesFromBlock(block);
                foreach (byte b in data)
                {
                    bytes.Add(b);
                }
            }
            Bitmap jpeg = cmp.ConvertYCbCrtoRGB(bytes.ToArray(), width, height);

            // Save the bitmap to the specified file path
            filePath = "C:\\Users\\Eric\\source\\repos\\RGB-to-YCbCr\\RGBtoYCbCr\\face1_newnew.jpeg";
            jpeg.Save(filePath);
        }

    }
}