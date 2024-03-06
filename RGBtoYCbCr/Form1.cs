using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RGBtoYCbCr
{
    public partial class Form1 : Form
    {
        byte[] file;
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
            byte[] ycrcb = cmp.ConvertRGBtoYCbCr(originalImage);
            System.IO.File.WriteAllBytes(filePath, ycrcb);

            List<double[,]> blocks = cmp.DCTBlocksAndQuantize(ycrcb);
            //convertedImage = RGBtoYCbCrSubsampling.ConvertYCbCrtoRGB(ycrcb);
            Invalidate();

        }

        private void Form1_Load_1(object sender, EventArgs e)
        {

        }
    }
}