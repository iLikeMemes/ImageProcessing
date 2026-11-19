using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace INFOIBV
{
    public partial class INFOIBV : Form
    {
        private Bitmap InputImage;
        private Bitmap OutputImage;

        /*
         * this enum defines the processing functions that will be shown in the dropdown (a.k.a. combobox)
         * you can expand it by adding new entries to applyProcessingFunction()
         */
        private enum ProcessingFunctions
        {
            Invert,
            AdjustContrast,
            ConvolveImage,
            DetectEdges,
            Threshold,
            BinaryErosion,
            BinaryDilation,
            BinaryOpening,
            BinaryClosing,
            GrayscaleErosion,
            GrayscaleDilation,
            Task1,
            Task2,
            Task3,
            MedianFilter,
            SequentialLabeling,
            FloodFillLabeling,
            HoughTransform,
            PeakFinding,
            HoughLineSegments,
            BinaryPipeline,
            GrayscalePipeline,
            CircleHoughTransform,
            CircleWithEdges,
            CircleWithTrimmedEdges,
            CircleWithCombinedEdges,
            DebugDetectClock,
            DetectClock
        }

        /*
         * these are the parameters for your processing functions, you should add more as you see fit
         * it is useful to set them based on controls such as sliders, which you can add to the form
         */
        private byte filterSize = 5;
        private float filterSigma = 1f;
        private byte threshold = 127; 
        private byte task1threshold;
        private int peakThreshold;
        private int minIntensityThreshold;
        private int minSegmentLength;
        private int maxGapParameter;
        sbyte[,] horizontalKernel = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
        sbyte[,] verticalKernel = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };
        bool[,] structElem = { { false, true, false }, { true, true, true }, { false, true, false } };
        int?[,] grayStructElem = { { null, 1, null }, { 1, 2, 1 }, { null, 1, null } };

        public INFOIBV()
        {
            InitializeComponent();
            populateCombobox();
        }

        /*
         * populateCombobox: populates the combobox with items as defined by the ProcessingFunctions enum
         */
        private void populateCombobox()
        {
            foreach (string itemName in Enum.GetNames(typeof(ProcessingFunctions)))
            {
                string ItemNameSpaces = Regex.Replace(Regex.Replace(itemName, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");
                comboBox.Items.Add(ItemNameSpaces);
            }
            comboBox.SelectedIndex = 0;
        }

        /*
         * loadButton_Click: process when user clicks "Load" button
         */
        private void loadImageButton_Click(object sender, EventArgs e)
        {
            if (openImageDialog.ShowDialog() == DialogResult.OK)             // open file dialog
            {
                string file = openImageDialog.FileName;                     // get the file name
                imageFileName.Text = file;                                  // show file name
                if (InputImage != null) InputImage.Dispose();               // reset image
                InputImage = new Bitmap(file);                              // create new Bitmap from file
                if (InputImage.Size.Height <= 0 || InputImage.Size.Width <= 0 ||
                    InputImage.Size.Height > 512 || InputImage.Size.Width > 512) // dimension check (may be removed or altered)
                    MessageBox.Show("Error in image dimensions (have to be > 0 and <= 512)");
                else
                    pictureBox1.Image = (Image)InputImage;                 // display input image
            }
        }


        /*
         * applyButton_Click: process when user clicks "Apply" button
         */
        private void applyButton_Click(object sender, EventArgs e)
        {
            if (InputImage == null) return;                                 // get out if no input image
            if (OutputImage != null) OutputImage.Dispose();                 // reset output image
            OutputImage = new Bitmap(InputImage.Size.Width, InputImage.Size.Height); // create new output image
            Color[,] Image = new Color[InputImage.Size.Width, InputImage.Size.Height]; // create array to speed-up operations (Bitmap functions are very slow)

            // copy input Bitmap to array            
            for (int x = 0; x < InputImage.Size.Width; x++)                 // loop over columns
                for (int y = 0; y < InputImage.Size.Height; y++)            // loop over rows
                    Image[x, y] = InputImage.GetPixel(x, y);                // set pixel color in array at (x,y)

            // execute image processing steps
            byte[,] workingImage = convertToGrayscale(Image);               // convert image to grayscale
            if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.HoughLineSegments)
            {
                peakThreshold = (int)numericUpDown4.Value;
                minIntensityThreshold = (int)numericUpDown5.Value;
                minSegmentLength = (int)numericUpDown6.Value;
                maxGapParameter = (int)numericUpDown7.Value;
                byte[,] accumulator = HoughTransform(workingImage);
                Color[,] colorImage = HoughLineSegments(workingImage, peakFinding(accumulator, peakThreshold), minIntensityThreshold, minSegmentLength, maxGapParameter);
                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, colorImage[x, y]);                  // set the pixel color at coordinate (x,y)
                    }
            }
            else if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.BinaryPipeline)
            {
                filterSigma = (float)numericUpDown2.Value;
                filterSize = (byte)numericUpDown1.Value;
                peakThreshold = (int)numericUpDown4.Value;
                minIntensityThreshold = (int)numericUpDown5.Value; 
                minSegmentLength = (int)numericUpDown6.Value;
                maxGapParameter = (int)numericUpDown7.Value;
                byte threshold = (byte)numericUpDown8.Value;
                float[,] filter = createGaussianFilter(filterSize, filterSigma);
                byte[,] smooth = convolveImage(workingImage, filter);
                byte[,] edges = edgeMagnitude(smooth, horizontalKernel, verticalKernel);
                byte[,] binary = thresholdImage(edges, threshold);
                byte[,] accumulator = HoughTransform(binary);
                Color[,] colorImage = HoughLineSegments(binary, peakFinding(accumulator, peakThreshold), minIntensityThreshold, minSegmentLength, maxGapParameter);
                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, colorImage[x, y]);                  // set the pixel color at coordinate (x,y)
                    }
            }
            else if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.GrayscalePipeline)
            {
                filterSigma = (float)numericUpDown2.Value;
                filterSize = (byte)numericUpDown1.Value;
                peakThreshold = (int)numericUpDown4.Value;
                minIntensityThreshold = (int)numericUpDown5.Value; 
                minSegmentLength = (int)numericUpDown6.Value;
                maxGapParameter = (int)numericUpDown7.Value;
                float[,] filter = createGaussianFilter(filterSize, filterSigma);
                byte[,] result = convolveImage(workingImage, filter); //smoothing
                byte[,] input = edgeMagnitude(result, horizontalKernel, verticalKernel); //detect edges
                byte[,] accumulator = HoughTransform(input); //hough transform
                Color[,] grayscale = HoughLineSegments(input, peakFinding(accumulator, peakThreshold), minIntensityThreshold, minSegmentLength, maxGapParameter);
                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, grayscale[x, y]);                  // set the pixel color at coordinate (x,y)
                    }
            }
            else if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.CircleHoughTransform)
            {
                filterSigma = (float)numericUpDown2.Value;
                filterSize = (byte)numericUpDown1.Value;
                float[,] filter = createGaussianFilter(filterSize, filterSigma);
                byte[,] result = convolveImage(workingImage, filter); //smoothing
                byte[,] edges = edgeMagnitude(result, horizontalKernel, verticalKernel); //detect edges

                float[,,] accumulator = CircleHoughTransform(edges);
                List<(int x, int y, int p, int val)> peaks = FastNMS(accumulator, 1, 5);

                Color[,] outputImage = GrayscaleToColor(edges);
                foreach ((int x, int y, int p, int val) in peaks)
                {
                    outputImage = DrawCircle(outputImage, x, y, p + 10);
                }
                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, outputImage[x, y]);                  // set the pixel color at coordinate (x,y)
                    }
            }
            else if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.CircleWithEdges)
            {
                filterSigma = (float)numericUpDown2.Value;
                filterSize = (byte)numericUpDown1.Value;
                peakThreshold = (int)numericUpDown4.Value;
                minIntensityThreshold = (int)numericUpDown5.Value; 
                minSegmentLength = (int)numericUpDown6.Value;
                maxGapParameter = (int)numericUpDown7.Value;
                float[,] filter = createGaussianFilter(filterSize, filterSigma);
                byte[,] result = convolveImage(workingImage, filter); //smoothing
                byte[,] edges = edgeMagnitude(result, horizontalKernel, verticalKernel); //detect edges

                byte[,] accumulator2 = HoughTransform(edges);
                List<((int, int), (int, int))> LineSegments = HoughLineSegmentsRaw(edges, peakFinding(accumulator2, peakThreshold), minIntensityThreshold, minSegmentLength, maxGapParameter);

                float[,,] accumulator = CircleHoughTransform(edges);
                List<(int x, int y, int p, int val)> peaks = FastNMS(accumulator, 1, 5);

                //grab the highest peak, which should be our clock
                (int x, int y, int p, int val) peak = peaks[0];
                (int x, int y, int r) trimCircle = (peak.x, peak.y, peak.p / 3); //trim circle is a smaller circle inside the clock circle

                Color[,] outputImage = GrayscaleToColor(edges);
                outputImage = drawLines(outputImage, LineSegments);

                foreach ((int x, int y, int p, int val) in peaks)
                {
                    outputImage = DrawCircle(outputImage, x, y, p + 10);
                }
                outputImage = DrawCircle(outputImage, trimCircle.x, trimCircle.y, trimCircle.r); //show trimcricle
                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, outputImage[x, y]);                  // set the pixel color at coordinate (x,y)
                    }
            }
            else if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.CircleWithTrimmedEdges)
            {
                filterSigma = (float)numericUpDown2.Value;
                filterSize = (byte)numericUpDown1.Value;
                peakThreshold = (int)numericUpDown4.Value;
                minIntensityThreshold = (int)numericUpDown5.Value; 
                minSegmentLength = (int)numericUpDown6.Value;
                maxGapParameter = (int)numericUpDown7.Value;
                float[,] filter = createGaussianFilter(filterSize, filterSigma);
                byte[,] result = convolveImage(workingImage, filter); //smoothing
                byte[,] edges = edgeMagnitude(result, horizontalKernel, verticalKernel); //detect edges

                byte[,] accumulator2 = HoughTransform(edges);
                List<((int, int), (int, int))> LineSegments = HoughLineSegmentsRaw(edges, peakFinding(accumulator2, peakThreshold), minIntensityThreshold, minSegmentLength, maxGapParameter);

                float[,,] accumulator = CircleHoughTransform(edges);
                List<(int x, int y, int p, int val)> peaks = FastNMS(accumulator, 1, 5);

                //grab the highest peak, which should be our clock
                (int x, int y, int p, int val) peak = peaks[0];
                (int x, int y, int r) trimCircle = (peak.x, peak.y, peak.p / 3); //trim circle is a smaller circle inside the clock circle

                //significantly reduce the amount of lines by trimming lines that arent inside the trimcircle and by combining lines that go in a similar direction
                LineSegments = TrimCandidates(LineSegments, trimCircle);

                Color[,] outputImage = GrayscaleToColor(edges);
                outputImage = drawLines(outputImage, LineSegments);

                foreach ((int x, int y, int p, int val) in peaks)
                {
                    outputImage = DrawCircle(outputImage, x, y, p + 10);
                }
                outputImage = DrawCircle(outputImage, trimCircle.x, trimCircle.y, trimCircle.r); //show trimcricle
                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, outputImage[x, y]);                  // set the pixel color at coordinate (x,y)
                    }
            }
            else if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.CircleWithCombinedEdges)
            {
                filterSigma = (float)numericUpDown2.Value;
                filterSize = (byte)numericUpDown1.Value;
                peakThreshold = (int)numericUpDown4.Value;
                minIntensityThreshold = (int)numericUpDown5.Value; 
                minSegmentLength = (int)numericUpDown6.Value;
                maxGapParameter = (int)numericUpDown7.Value;
                float[,] filter = createGaussianFilter(filterSize, filterSigma);
                byte[,] result = convolveImage(workingImage, filter); //smoothing
                byte[,] edges = edgeMagnitude(result, horizontalKernel, verticalKernel); //detect edges

                byte[,] accumulator2 = HoughTransform(edges);
                List<((int, int), (int, int))> LineSegments = HoughLineSegmentsRaw(edges, peakFinding(accumulator2, peakThreshold), minIntensityThreshold, minSegmentLength, maxGapParameter);

                float[,,] accumulator = CircleHoughTransform(edges);
                List<(int x, int y, int p, int val)> peaks = FastNMS(accumulator, 1, 5);

                //grab the highest peak, which should be our clock
                (int x, int y, int p, int val) peak = peaks[0];
                (int x, int y, int r) trimCircle = (peak.x, peak.y, peak.p / 3); //trim circle is a smaller circle inside the clock circle

                //significantly reduce the amount of lines by trimming lines that arent inside the trimcircle and by combining lines that go in a similar direction
                LineSegments = TrimCandidates(LineSegments, trimCircle);

                //combine lines until they cant be combined anymore
                int previousCount = LineSegments.Count;
                int currentCount = LineSegments.Count;
                do
                {
                    previousCount = currentCount;
                    LineSegments = CombineLines(LineSegments, 25f, 30f);
                    currentCount = LineSegments.Count;
                } while (previousCount > currentCount);


                Color[,] outputImage = GrayscaleToColor(edges);
                outputImage = drawLines(outputImage, LineSegments);

                foreach ((int x, int y, int p, int val) in peaks)
                {
                    outputImage = DrawCircle(outputImage, x, y, p + 10);
                }
                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, outputImage[x, y]);                  // set the pixel color at coordinate (x,y)
                    }
            }
            else if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.DebugDetectClock)
            {



                //detect edges
                filterSigma = (float)numericUpDown2.Value;
                filterSize = (byte)numericUpDown1.Value;
                peakThreshold = (int)numericUpDown4.Value;
                minIntensityThreshold = (int)numericUpDown5.Value; 
                minSegmentLength = (int)numericUpDown6.Value;
                maxGapParameter = (int)numericUpDown7.Value;
                float[,] filter = createGaussianFilter(filterSize, filterSigma);
                byte[,] result = convolveImage(workingImage, filter); //smoothing
                byte[,] edges = edgeMagnitude(result, horizontalKernel, verticalKernel); //detect edges


                byte[,] accumulator2 = HoughTransform(edges);
                List<((int, int), (int, int))> LineSegments = HoughLineSegmentsRaw(edges, peakFinding(accumulator2, peakThreshold), minIntensityThreshold, minSegmentLength, maxGapParameter);

                float[,,] accumulator = CircleHoughTransform(edges);
                List<(int x, int y, int p, int val)> peaks = FastNMS(accumulator, 1, 5);

                //grab the highest peak, which should be our clock
                (int x, int y, int p, int val) peak = peaks[0];
                (int x, int y, int r) trimCircle = (peak.x, peak.y, peak.p / 3); //trim circle is a smaller circle inside the clock circle (also reduces the circle size by a flat 10)

                //significantly reduce the amount of lines by trimming lines that arent inside the trimcircle and by combining lines that go in a similar direction
                LineSegments = TrimCandidates(LineSegments, trimCircle);

                //combine lines until they cant be combined anymore
                int previousCount = LineSegments.Count;
                int currentCount = LineSegments.Count;
                do {
                    previousCount = currentCount;
                    LineSegments = CombineLines(LineSegments, 25f, 30f);
                    currentCount = LineSegments.Count;
                } while (previousCount > currentCount);


                Color[,] outputImage = GrayscaleToColor(edges);
                //ideally we now only have lines from the clock hands remaining
                //try to find the accurate center of the circle by looking at line intersections

                List<((int x, int y) topLeft, (int x, int y) bottomRight)> boxes = new List<((int x, int y), (int x, int y))>();

                foreach (((int x, int y) p1, (int x, int y) p2) hand in LineSegments)
                {
                    //i am simply assuming these will never go out of bounds
                    int lowestX = Math.Min(hand.p1.x, hand.p2.x) -3;
                    int highestX = Math.Max(hand.p1.x, hand.p2.x) + 3;
                    int lowestY = Math.Min(hand.p1.y, hand.p2.y) - 3;
                    int highestY = Math.Max(hand.p1.y, hand.p2.y) +3;

                    boxes.Add(((lowestX, lowestY), (highestX, highestY)));
                }

                //any boxes fully inside other boxes should be removed, most likely noise lines
                List<((int x, int y), (int x, int y))> filteredBoxes = new List<((int x, int y), (int x, int y))>();

                for (int i = 0; i < boxes.Count; i++)
                {
                    bool isInside = false;
                    ((int x, int y) topLeft, (int x, int y) bottomRight) box1 = boxes[i];

                    for (int j = 0; j < boxes.Count; j++)
                    {
                        if (i == j) continue;
                        ((int x, int y) topLeft, (int x, int y) bottomRight) box2 = boxes[j];

                        // Check if box A is fully inside box B
                        if (box1.topLeft.x >= box2.topLeft.x &&
                            box1.topLeft.y >= box2.topLeft.y &&
                            box1.bottomRight.x <= box2.bottomRight.x &&
                            box1.bottomRight.y <= box2.bottomRight.y)
                        {
                            isInside = true;
                            break;
                        }
                    }

                    if (!isInside)
                        filteredBoxes.Add(boxes[i]);
                }

                for(int i = 0; i < filteredBoxes.Count; i++)
                {
                    ((int x, int y) topleft, (int x, int y) bottomright) box = filteredBoxes[i];
                    //clamp topleft and bottomright, very unlikely to be actually needed due to hands probably being in the center
                    borderClamp(ref box.topleft.x, ref box.topleft.y, workingImage.GetLength(0)-1, workingImage.GetLength(1)-1);
                    borderClamp(ref box.bottomright.x, ref box.bottomright.y, workingImage.GetLength(0) - 1, workingImage.GetLength(1) - 1);

                    outputImage = DrawBoundingBox(outputImage, box.topleft, box.bottomright);
                }


                //currently assuming there are only two lines that intersect, will break if nothing intersects
                List<(double x, double y)> intersections = GetLineIntersections(LineSegments);
                (double x, double y) intersect;
                if (intersections.Count > 0)
                {
                    intersect = RefineCenter(GetLineIntersections(LineSegments));
                }
                else
                {
                    intersect = (peak.x, peak.y);
                }

                    /*
                    foreach ((double x, double y) intersection in intersections) //debug draw all intersections
                    {
                        outputImage = DrawCircle(outputImage, (int)intersection.x, (int)intersection.y, 10);
                    }
                    */

                    (int x, int y) clockcenter = ((int)(intersect.x), (int)(intersect.y));
                int radius = (int)(peak.p + 10 * 1.15f);

                (int x , int y) topleft = (clockcenter.x - radius, clockcenter.y - radius);
                (int x , int y) bottomright = (clockcenter.x + radius, clockcenter.y + radius);
                borderClamp(ref topleft.x, ref topleft.y, workingImage.GetLength(0) - 1, workingImage.GetLength(1) - 1);
                borderClamp(ref bottomright.x, ref bottomright.y, workingImage.GetLength(0) - 1, workingImage.GetLength(1) - 1);
                outputImage = DrawBoundingBox(outputImage, topleft, bottomright);


                //outputImage = DrawCircle(outputImage, center.x, center.y, 20); //draw the refined center for debug

                outputImage = drawLines(outputImage, LineSegments);

                //outputImage = DrawCircle(outputImage, trimCircle.x, trimCircle.y, trimCircle.r); //debug, show trimcricle
                //outputImage = DrawCircle(outputImage, (int)intersect.x, (int)intersect.y, 20); //debug, draw supposed center
                foreach ((int x, int y, int p, int val) in peaks)
                {
                    outputImage = DrawCircle(outputImage, x, y, p + 10);
                }

                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, outputImage[x, y]);                  // set the pixel color at coordinate (x,y)
                    }

            }
            else if ((ProcessingFunctions)comboBox.SelectedIndex == ProcessingFunctions.DetectClock)
            {

                //params
                filterSigma = 2;
                filterSize = 3;
                peakThreshold = 19;
                minIntensityThreshold = 17;
                minSegmentLength = 24;
                maxGapParameter = 3;

                float combineAngle = 25f;
                float combineDistance = 30f;
                int circleNMSneighbourhood = 5;


                float[,] filter = createGaussianFilter(filterSize, filterSigma);
                byte[,] result = convolveImage(workingImage, filter); //smoothing
                byte[,] edges = edgeMagnitude(result, horizontalKernel, verticalKernel); //detect edges

                //hough lines
                byte[,] accumulator2 = HoughTransform(edges);
                List<((int, int), (int, int))> LineSegments = HoughLineSegmentsRaw(edges, peakFinding(accumulator2, peakThreshold), minIntensityThreshold, minSegmentLength, maxGapParameter);

                //hough circles
                float[,,] accumulator = CircleHoughTransform(edges);
                List<(int x, int y, int p, int val)> peaks = FastNMS(accumulator, 1, circleNMSneighbourhood);

                //grab the highest peak, which we assume is our clock
                (int x, int y, int p, int val) peak = peaks[0];
                (int x, int y, int r) trimCircle = (peak.x, peak.y, peak.p / 3); //trim circle is a smaller circle inside the clock circle (also reduces the circle size by a flat 10 by not doing + 10)

                //significantly reduce the amount of lines by trimming lines that arent inside the trimcircle and by combining lines that go in a similar direction
                LineSegments = TrimCandidates(LineSegments, trimCircle);

                //combine lines until they cant be combined anymore
                int previousCount = LineSegments.Count;
                int currentCount = LineSegments.Count;
                do
                {
                    previousCount = currentCount;
                    LineSegments = CombineLines(LineSegments, combineAngle, combineDistance);
                    currentCount = LineSegments.Count;
                } while (previousCount > currentCount);


                Color[,] outputImage = GrayscaleToColor(edges);

                List<((int x, int y) topLeft, (int x, int y) bottomRight)> boxes = new List<((int x, int y), (int x, int y))>();

                foreach (((int x, int y) p1, (int x, int y) p2) hand in LineSegments)
                {
                    //i am simply assuming these will never go out of bounds
                    int lowestX = Math.Min(hand.p1.x, hand.p2.x) - 3;
                    int highestX = Math.Max(hand.p1.x, hand.p2.x) + 3;
                    int lowestY = Math.Min(hand.p1.y, hand.p2.y) - 3;
                    int highestY = Math.Max(hand.p1.y, hand.p2.y) + 3;

                    boxes.Add(((lowestX, lowestY), (highestX, highestY)));
                }

                
                List<((int x, int y), (int x, int y))> filteredBoxes = new List<((int x, int y), (int x, int y))>();

                //any boxes fully inside other boxes should be removed, most likely noise lines
                for (int i = 0; i < boxes.Count; i++)
                {
                    bool isInside = false;
                    ((int x, int y) topLeft, (int x, int y) bottomRight) box1 = boxes[i];

                    for (int j = 0; j < boxes.Count; j++)
                    {
                        if (i == j) continue;
                        ((int x, int y) topLeft, (int x, int y) bottomRight) box2 = boxes[j];

                        // Check if box A is fully inside box B
                        if (box1.topLeft.x >= box2.topLeft.x &&
                            box1.topLeft.y >= box2.topLeft.y &&
                            box1.bottomRight.x <= box2.bottomRight.x &&
                            box1.bottomRight.y <= box2.bottomRight.y)
                        {
                            isInside = true;
                            break;
                        }
                    }

                    if (!isInside)
                        filteredBoxes.Add(boxes[i]);
                }

                for (int i = 0; i < filteredBoxes.Count; i++)
                {
                    ((int x, int y) topleft, (int x, int y) bottomright) box = filteredBoxes[i];
                    //clamp topleft and bottomright, very unlikely to be actually needed due to hands probably being in the center
                    borderClamp(ref box.topleft.x, ref box.topleft.y, workingImage.GetLength(0) - 1, workingImage.GetLength(1) - 1);
                    borderClamp(ref box.bottomright.x, ref box.bottomright.y, workingImage.GetLength(0) - 1, workingImage.GetLength(1) - 1);

                    outputImage = DrawBoundingBox(outputImage, box.topleft, box.bottomright);
                }


                List<(double x, double y)> intersections = GetLineIntersections(LineSegments);
                (double x, double y) intersect;
                if (intersections.Count > 0)
                {
                    intersect = RefineCenter(GetLineIntersections(LineSegments));
                }
                else
                {
                    intersect = (peak.x, peak.y); //if we cant find an intersection assume that the circle center is correct
                }

                /*
                foreach ((double x, double y) intersection in intersections) //debug draw all intersections
                {
                    outputImage = DrawCircle(outputImage, (int)intersection.x, (int)intersection.y, 10);
                }
                */

                (int x, int y) clockcenter = ((int)(intersect.x), (int)(intersect.y));
                int radius = (int)(peak.p + 10 * 1.15f); //some padding

                (int x, int y) topleft = (clockcenter.x - radius, clockcenter.y - radius);
                (int x, int y) bottomright = (clockcenter.x + radius, clockcenter.y + radius);
                borderClamp(ref topleft.x, ref topleft.y, workingImage.GetLength(0) - 1, workingImage.GetLength(1) - 1);
                borderClamp(ref bottomright.x, ref bottomright.y, workingImage.GetLength(0) - 1, workingImage.GetLength(1) - 1);
                outputImage = DrawBoundingBox(outputImage, topleft, bottomright);


                //outputImage = DrawCircle(outputImage, center.x, center.y, 20); //draw the refined center for debug

                outputImage = drawLines(outputImage, LineSegments);

                //outputImage = DrawCircle(outputImage, trimCircle.x, trimCircle.y, trimCircle.r); //debug, show trimcricle
                //outputImage = DrawCircle(outputImage, (int)intersect.x, (int)intersect.y, 20); //debug, draw supposed center
                foreach ((int x, int y, int p, int val) in peaks)
                {
                    outputImage = DrawCircle(outputImage, x, y, p + 10);
                }

                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        OutputImage.SetPixel(x, y, outputImage[x, y]);                  // set the pixel color at coordinate (x,y)
                    }

            }
            else
            {
                workingImage = applyProcessingFunction(workingImage);        // processing functions
                // copy array to output Bitmap
                for (int x = 0; x < workingImage.GetLength(0); x++)             // loop over columns
                    for (int y = 0; y < workingImage.GetLength(1); y++)         // loop over rows
                    {
                        Color newColor = Color.FromArgb(workingImage[x, y], workingImage[x, y], workingImage[x, y]);
                        OutputImage.SetPixel(x, y, newColor);                  // set the pixel color at coordinate (x,y)
                    }
            }

            pictureBox2.Image = (Image)OutputImage;                         // display output image
        }

        /*
         * applyProcessingFunction: defines behavior of function calls when "Apply" is pressed
         */
        private byte[,] applyProcessingFunction(byte[,] workingImage)
        {
            switch ((ProcessingFunctions)comboBox.SelectedIndex)
            {
                case ProcessingFunctions.Invert:
                    return invertImage(workingImage);
                case ProcessingFunctions.AdjustContrast:
                    return adjustContrast(workingImage);
                case ProcessingFunctions.ConvolveImage:
                    float[,] filter = createGaussianFilter(filterSize, filterSigma);
                    return convolveImage(workingImage, filter);
                case ProcessingFunctions.DetectEdges:
                    return edgeMagnitude(workingImage, horizontalKernel, verticalKernel);
                case ProcessingFunctions.Threshold:
                    return thresholdImage(workingImage, threshold);

                case ProcessingFunctions.BinaryErosion:
                    return binaryErodeImage(workingImage, structElem);

                case ProcessingFunctions.BinaryDilation:
                    return binaryDilateImage(workingImage, structElem);

                case ProcessingFunctions.BinaryOpening:
                    return binaryOpenImage(workingImage, structElem);

                case ProcessingFunctions.BinaryClosing:
                    return binaryCloseImage(workingImage, structElem);

                case ProcessingFunctions.GrayscaleErosion:
                    return grayscaleErodeImage(workingImage, grayStructElem);

                case ProcessingFunctions.GrayscaleDilation:
                    return grayscaleDilateImage(workingImage, grayStructElem);

                case ProcessingFunctions.Task1:
                    // Ask user for sigma and kernel size
                    filterSigma = (float)numericUpDown2.Value;
                    filterSize = (byte)numericUpDown1.Value;
                    task1threshold = (byte)numericUpDown3.Value;
                    float[,] filterT1 = createGaussianFilter(filterSize, filterSigma);
                    byte[,] image = convolveImage(workingImage, filterT1);
                    image = edgeMagnitude(image, horizontalKernel, verticalKernel);
                    image = thresholdImage(image, task1threshold);

                    return image;

                case ProcessingFunctions.Task2:

                    int?[,] task2StructElem1 = makeIntArray(3, 3);
                    int?[,] task2StructElem2 = makeIntArray(7, 7);
                    int?[,] task2StructElem3 = makeIntArray(11, 11);
                    int?[,] task2StructElem4 = makeIntArray(15, 15);
                    int?[,] task2StructElem5 = makeIntArray(19, 19);

                    //get the selected radio button
                    RadioButton selected = panel2.Controls
                    .OfType<RadioButton>()
                    .FirstOrDefault(r => r.Checked);

                    switch (selected.Name)
                    {
                        case "radioButton1":
                            byte[,] C1 = grayscaleDilateImage(workingImage, task2StructElem1);
                            int D1 = countDistinctValues(C1);
                            float E1 = calculateAverageValue(C1);
                            label5.Text = "Number of distinct values: " + D1;
                            label6.Text = "Average value: " + E1;
                            return C1;
                        case "radioButton2":
                            byte[,] C2 = grayscaleDilateImage(workingImage, task2StructElem2);
                            int D2 = countDistinctValues(C2);
                            float E2 = calculateAverageValue(C2);
                            label5.Text = "Number of distinct values: " + D2;
                            label6.Text = "Average value: " + E2;
                            return C2;
                        case "radioButton3":
                            byte[,] C3 = grayscaleDilateImage(workingImage, task2StructElem3);
                            int D3 = countDistinctValues(C3);
                            float E3 = calculateAverageValue(C3);
                            label5.Text = "Number of distinct values: " + D3;
                            label6.Text = "Average value: " + E3;
                            return C3;
                        case "radioButton4":
                            byte[,] C4 = grayscaleDilateImage(workingImage, task2StructElem4);
                            int D4 = countDistinctValues(C4);
                            float E4 = calculateAverageValue(C4);
                            label5.Text = "Number of distinct values: " + D4;
                            label6.Text = "Average value: " + E4;
                            return C4;
                        case "radioButton5":
                            byte[,] C5 = grayscaleDilateImage(workingImage, task2StructElem5);
                            int D5 = countDistinctValues(C5);
                            float E5 = calculateAverageValue(C5);
                            label5.Text = "Number of distinct values: " + D5;
                            label6.Text = "Average value: " + E5;
                            return C5;
                        default:
                            return workingImage;
                    }

                case ProcessingFunctions.Task3:

                    byte[,] imageF = thresholdImage(workingImage, 127);

                    bool[,] task3StructElem1 = makeBoolArray(3, 3);
                    bool[,] task3StructElem2 = makeBoolArray(13, 13);
                    bool[,] task3StructElem3 = makeBoolArray(23, 23);
                    bool[,] task3StructElem4 = makeBoolArray(33, 33);

                    RadioButton selected2 = panel3.Controls
                    .OfType<RadioButton>()
                    .FirstOrDefault(r => r.Checked);

                    switch (selected2.Text)
                    {
                        case ("G1"):
                            byte[,] G1 = binaryCloseImage(imageF, task3StructElem1);
                            int H1 = calculateForegroundPixelAmount(G1);
                            label4.Text = "Number of foreground pixels: " + H1;
                            return G1;
                        case ("G2"):
                            byte[,] G2 = binaryCloseImage(imageF, task3StructElem2);
                            int H2 = calculateForegroundPixelAmount(G2);
                            label4.Text = "Number of foreground pixels: " + H2;
                            return G2;
                        case ("G3"):
                            byte[,] G3 = binaryCloseImage(imageF, task3StructElem3);
                            int H3 = calculateForegroundPixelAmount(G3);
                            label4.Text = "Number of foreground pixels: " + H3;
                            return G3;
                        case ("G4"):
                            byte[,] G4 = binaryCloseImage(imageF, task3StructElem4);
                            int H4 = calculateForegroundPixelAmount(G4);
                            label4.Text = "Number of foreground pixels: " + H4;
                            return G4;
                    }



                    return null;
                case ProcessingFunctions.MedianFilter:
                    return medianFilterImage(workingImage, filterSize);

                case ProcessingFunctions.SequentialLabeling:
                    byte[,] temp = thresholdImage(workingImage, 127); //make sure we use a binary image
                    //convert to proper input
                    for (int y = 0; y < workingImage.GetLength(1); y++)
                    {                                                          // loop over columns
                        for (int x = 0; x < workingImage.GetLength(0); x++)            // loop over rows
                        {
                            if (temp[x, y] == 255)
                                temp[x, y] = 1;
                            else
                                temp[x, y] = 0;
                        }
                    }
                    temp = SequentialRegionLabeling(temp);
                    label7.Text = "Foreground regions: " + calculateForegroundRegions(temp);
                    return adjustContrast(temp);
                case ProcessingFunctions.FloodFillLabeling:
                    byte[,] prep = thresholdImage(workingImage, 127); //make sure we use a binary image
                    //convert to proper input
                    for (int y = 0; y < workingImage.GetLength(1); y++)
                    {                                                          // loop over columns
                        for (int x = 0; x < workingImage.GetLength(0); x++)            // loop over rows
                        {
                            if (prep[x, y] == 255)
                                prep[x, y] = 1;
                            else
                                prep[x, y] = 0;
                        }
                    }
                    prep = FloodFillLabeling(prep);
                    label7.Text = "Foreground regions: " + calculateForegroundRegions(prep);
                    return adjustContrast(prep);

                case ProcessingFunctions.HoughTransform:
                    int imageXsize = workingImage.GetLength(0);
                    int imageYsize = workingImage.GetLength(1);
                    double diagonal = Math.Sqrt(Math.Pow(imageXsize, 2) + Math.Pow(imageYsize, 2));
                    int rmax = (int)Math.Ceiling(diagonal);
                    OutputImage = new Bitmap(180, 2 * rmax);
                    byte[,] result = HoughTransform(workingImage);
                    return result;
                case ProcessingFunctions.PeakFinding:
                    int imageXsize_ = workingImage.GetLength(0);
                    int imageYsize_ = workingImage.GetLength(1);
                    double diagonal_ = Math.Sqrt(Math.Pow(imageXsize_, 2) + Math.Pow(imageYsize_, 2));
                    int rmax_ = (int)Math.Ceiling(diagonal_);
                    OutputImage = new Bitmap(180, 2 * rmax_);

                    byte[,] rtheta = HoughTransform(workingImage);
                    List<(int, int)> peaks = peakFinding(rtheta, 50);
                    //make an image with just the peaks visualised, and apply dilation for easier view maybe
                    byte[,] output = new byte[rtheta.GetLength(0), rtheta.GetLength(1)];
                    foreach ((int theta, int r) peak in peaks)
                    {
                        output[peak.theta, peak.r] = 255;
                    }
                    return output;
                default:
                    return null;
            }
        }

        private int?[,] makeIntArray(int width, int height)
        {
            int?[,] array = new int?[width, height];

            for (int i = 0; i < width; i++)
                for (int j = 0; j < height; j++)
                {
                    array[i, j] = 1;
                }

            return array;
        }

        private bool[,] makeBoolArray(int width, int height)
        {
            bool[,] array = new bool[width, height];

            for (int i = 0; i < width; i++)
                for (int j = 0; j < height; j++)
                {
                    array[i, j] = true;
                }

            return array;
        }

        private int countDistinctValues(byte[,] inputImage)
        {
            HashSet<int> distinctValues = new HashSet<int>();
            for (int i = 0; i < inputImage.GetLength(0); i++)
                for (int j = 0; j < inputImage.GetLength(1); j++)
                {
                    distinctValues.Add(inputImage[i, j]);
                }

            return distinctValues.Count;
        }

        private float calculateAverageValue(byte[,] inputImage)
        {
            float totalValue = 0;
            float totalPixels = (inputImage.GetLength(0) - 1) * (inputImage.GetLength(1) - 1);
            for (int i = 0; i < inputImage.GetLength(0); i++)
                for (int j = 0; j < inputImage.GetLength(1); j++)
                {
                    totalValue += inputImage[i, j];
                }

            return totalValue / totalPixels;
        }

        private int calculateForegroundPixelAmount(byte[,] inputImage)
        {
            int foregroundPixels = 0;
            for (int i = 0; i < inputImage.GetLength(0); i++)
                for (int j = 0; j < inputImage.GetLength(1); j++)
                {
                    if (inputImage[i, j] == 255)
                        foregroundPixels++;
                }

            return foregroundPixels;
        }

        private int calculateForegroundRegions(byte[,] inputImage)
        {
            int regions = 0;
            List<int> countedRegions = new List<int>();

            foreach (byte b in inputImage)
            {
                if (b > 1 && countedRegions.Contains(b) == false)
                {
                    countedRegions.Add(b);
                    regions++;
                }
            }

            return regions;
        }


        /*
         * saveButton_Click: process when user clicks "Save" button
         */
        private void saveButton_Click(object sender, EventArgs e)
        {
            if (OutputImage == null) return;                                // get out if no output image
            if (saveImageDialog.ShowDialog() == DialogResult.OK)
                OutputImage.Save(saveImageDialog.FileName);                 // save the output image
        }


        /*
         * convertToGrayScale: convert a three-channel color image to a single channel grayscale image
         * input:   inputImage          three-channel (Color) image
         * output:                      single-channel (byte) image
         */
        private byte[,] convertToGrayscale(Color[,] inputImage)
        {
            // create temporary grayscale image of the same size as input, with a single channel
            byte[,] tempImage = new byte[inputImage.GetLength(0), inputImage.GetLength(1)];

            // setup progress bar
            progressBar.Visible = true;
            progressBar.Minimum = 1;
            progressBar.Maximum = InputImage.Size.Width * InputImage.Size.Height;
            progressBar.Value = 1;
            progressBar.Step = 1;

            // process all pixels in the image
            for (int x = 0; x < InputImage.Size.Width; x++)                 // loop over columns
                for (int y = 0; y < InputImage.Size.Height; y++)            // loop over rows
                {
                    Color pixelColor = inputImage[x, y];                    // get pixel color
                    byte average = (byte)((pixelColor.R + pixelColor.B + pixelColor.G) / 3); // calculate average over the three channels
                    tempImage[x, y] = average;                              // set the new pixel color at coordinate (x,y)
                    progressBar.PerformStep();                              // increment progress bar
                }

            progressBar.Visible = false;                                    // hide progress bar

            return tempImage;
        }


        // ====================================================================
        // ============= YOUR FUNCTIONS FOR ASSIGNMENT 1 GO HERE ==============
        // ====================================================================

        /*
         * invertImage: invert a single channel (grayscale) image
         * input:   inputImage          single-channel (byte) image
         * output:                      single-channel (byte) image
         */
        private byte[,] invertImage(byte[,] inputImage)
        {
            // create temporary grayscale image
            byte[,] tempImage = new byte[inputImage.GetLength(0), inputImage.GetLength(1)];

            // process all pixels in the image
            for (int y = 0; y < inputImage.GetLength(1); y++)                 // loop over columns
                for (int x = 0; x < inputImage.GetLength(0); x++)            // loop over rows
                {
                    byte pixelIntensity = inputImage[x, y];                  // get pixel intesity (grayscale value)
                    tempImage[x, y] = (byte)(255 - pixelIntensity);         // set the new pixel color at coordinate (x,y)
                    progressBar.PerformStep();                              // increment progress bar
                }

            return tempImage;
        }


        /*
         * adjustContrast: create an image with the full range of intensity values used
         * input:   inputImage          single-channel (byte) image
         * output:                      single-channel (byte) image
         */
        private byte[,] adjustContrast(byte[,] inputImage)
        {
            // create temporary grayscale image
            byte[,] tempImage = new byte[inputImage.GetLength(0), inputImage.GetLength(1)];

            // TODO: add your functionality and checks
            byte Amin = 0;
            byte Amax = 255;

            byte Alow = byte.MaxValue;
            byte Ahigh = byte.MinValue;

            for (int y = 0; y < inputImage.GetLength(1); y++)                 // loop over columns
                for (int x = 0; x < inputImage.GetLength(0); x++)            // loop over rows
                {
                    byte pixelIntensity = inputImage[x, y];                  // get pixel intesity (grayscale value)
                    if (pixelIntensity < Alow)
                        Alow = pixelIntensity;
                    if (pixelIntensity > Ahigh)
                        Ahigh = pixelIntensity;
                }
            for (int y = 0; y < inputImage.GetLength(1); y++)
                for (int x = 0; x < inputImage.GetLength(0); x++)
                {
                    byte pixelIntensity = inputImage[x, y];
                    float multiplier = (float)(Amax - Amin) / (float)(Ahigh - Alow);
                    tempImage[x, y] = (byte)(Amin + (pixelIntensity - Alow) * (float)multiplier);
                }

            return tempImage;
        }


        /*
         * createGaussianFilter: create a Gaussian filter of specific square size and with a specified sigma
         * input:   size                length and width of the Gaussian filter (only odd sizes)
         *          sigma               standard deviation of the Gaussian distribution
         * output:                      Gaussian filter
         */
        private float[,] createGaussianFilter(byte size, float sigma)
        {
            // create temporary grayscale image
            if (size % 2 == 0) throw new ArgumentException("Filter size must be odd");
            if (size < 1) throw new ArgumentException("Filter size must be positive");
            if (sigma <= 0) throw new ArgumentException("Sigma must be positive");

            float sum = 0;
            int filteroffset = size / 2; //how far the filter extends from the center pixel
            //this means the center pixel is (filteroffset, filteroffset)
            float[,] filter = new float[size, size];
            for (int y = 0; y < size; y++)                 // loop over row
            {
                int yDistance = y - filteroffset; //distance to center pixel, doesnt have to be absolute, will be squared
                for (int x = 0; x < size; x++)            // loop over colums
                {

                    int xDistance = x - filteroffset; //distance to center pixel, doesnt have to be absolute, will be squared
                    //e^(-(x^2 + y^2) / 2*sigma^2)
                    float numerator = -(float)(Math.Pow(xDistance, 2) + (float)Math.Pow(yDistance, 2));
                    float denominator = 2 * (float)Math.Pow(sigma, 2);

                    filter[x, y] = (float)Math.Exp(numerator / denominator);
                    sum += filter[x, y];
                }
            }

            for (int y = 0; y < size; y++)                 // loop over row
            {
                for (int x = 0; x < size; x++)            // loop over colums
                {
                    filter[x, y] = filter[x, y] / sum;
                }
            }


            return filter;
        }


        /*
         * convolveImage: apply linear filtering of an input image
         * input:   inputImage          single-channel (byte) image
         *          filter              linear kernel
         * output:                      single-channel (byte) image
         */
        private byte[,] convolveImage(byte[,] inputImage, float[,] filter)
        {
            if (filter.GetLength(0) != filter.GetLength(1) && filter.GetLength(0) % 2 == 0)
                throw new ArgumentException("Filter must be square and of odd size");
            // create temporary grayscale image
            byte[,] tempImage = new byte[inputImage.GetLength(0), inputImage.GetLength(1)];

            //border handling method: extend border pixels
            //go trough every pixel of the image and apply the filter
            for (int y = 0; y < inputImage.GetLength(1); y++)
            {
                for (int x = 0; x < inputImage.GetLength(0); x++)
                {
                    tempImage[x, y] = (byte)applyFilterToPixel(inputImage, filter, x, y);
                }
            }

            return tempImage;
        }


        /*
         * applyFilterToPixel: applies a filter to a single pixel in an image and returns the new pixel value (helper function for convolveImage)
         * input:   inputImage          single-channel (byte) image
         *          filter              linear kernel
         *          x, y                pixel coordinates
         * output:                      value of the pixel at (x,y) after applying the filter
        */
        private byte applyFilterToPixel(byte[,] inputImage, float[,] filter, int x, int y)
        {
            int size = filter.GetLength(0);
            int filterOffset = size / 2; //how far the filter extends from the center pixel
            float newPixelValue = 0; //the new pixel value after applying the filter
            int minX = x - filterOffset; //top left x inside inputimage
            int minY = y - filterOffset; //top left y inside inputimage

            //go through all pixels inside the filter
            for (int filterY = 0; filterY < size; filterY++)
            {
                for (int filterX = 0; filterX < size; filterX++)
                {
                    int imageX = minX + filterX; //the x coordinate of the pixel inside the inputimage
                    int imageY = minY + filterY; //the y coordinate of the pixel inside the inputimage
                    //border handling: extend border pixels (when outside the image, use closest pixel)

                    borderClamp(ref imageX, ref imageY, inputImage.GetLength(0) - 1, inputImage.GetLength(1) - 1);

                    newPixelValue += inputImage[imageX, imageY] * filter[filterX, filterY];
                }
            }

            return (byte)newPixelValue;

        }

        private float applyFilterToPixel(byte[,] inputImage, sbyte[,] filter, int x, int y)
        {
            int size = filter.GetLength(0);
            int filterOffset = size / 2; //how far the filter extends from the center pixel
            float newPixelValue = 0; //the new pixel value after applying the filter
            int minX = x - filterOffset; //top left x inside inputimage
            int minY = y - filterOffset; //top left y inside inputimage

            //go through all pixels inside the filter
            for (int filterY = 0; filterY < size; filterY++)
            {
                for (int filterX = 0; filterX < size; filterX++)
                {
                    int imageX = minX + filterX; //the x coordinate of the pixel inside the inputimage
                    int imageY = minY + filterY; //the y coordinate of the pixel inside the inputimage
                    //border handling: extend border pixels (when outside the image, use closest pixel)

                    borderClamp(ref imageX, ref imageY, inputImage.GetLength(0) - 1, inputImage.GetLength(1) - 1);

                    newPixelValue += inputImage[imageX, imageY] * filter[filterX, filterY];
                }
            }

            return newPixelValue;

        }

        /// <summary>
        /// clamp values x and y to be within [0, maxX] and [0, maxY] respectively with maxX and maxY inclusive, returns true if either x or y was out of bounds
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        private bool borderClamp(ref int x, ref int y, int maxX, int maxY)
        {
            bool OutOfBounds = false;

            if (x < 0) { x = 0; OutOfBounds = true; }
            if (y < 0) { y = 0; OutOfBounds = true; }
            if (x > maxX) { x = maxX; OutOfBounds = true; }
            if (y > maxY) { y = maxY; OutOfBounds = true; }

            return OutOfBounds;
        }

        private bool borderClamp(int x, int y, int maxX, int maxY)
        {
            bool OutOfBounds = false;

            if (x < 0) { OutOfBounds = true; }
            if (y < 0) { OutOfBounds = true; }
            if (x > maxX) { OutOfBounds = true; }
            if (y > maxY) { OutOfBounds = true; }

            return OutOfBounds;
        }



        /*
         * edgeMagnitude: calculate the image derivative of an input image and a provided edge kernel
         * input:   inputImage          single-channel (byte) image
         *          horizontalKernel    horizontal edge kernel
         *          verticalKernel      vertical edge kernel
         * output:                      single-channel (byte) image
         */
        private byte[,] edgeMagnitude(byte[,] inputImage, sbyte[,] horizontalKernel, sbyte[,] verticalKernel)
        {
            if (horizontalKernel.GetLength(0) != horizontalKernel.GetLength(1) || verticalKernel.GetLength(0) != verticalKernel.GetLength(1) || horizontalKernel.GetLength(0) != verticalKernel.GetLength(0))
                throw new ArgumentException("Kernels must be square and of the same size");
            if (horizontalKernel.GetLength(0) % 2 == 0 || verticalKernel.GetLength(0) % 2 == 0)
                throw new ArgumentException("Kernels must be of odd size");

            // create temporary grayscale image
            byte[,] tempImage = new byte[inputImage.GetLength(0), inputImage.GetLength(1)];

            // TODO: add your functionality and checks, think about border handling and type conversion (negative values!)

            for (int y = 0; y < inputImage.GetLength(1); y++)                 // loop over columns
                for (int x = 0; x < inputImage.GetLength(0); x++)            // loop over rows
                {
                    byte pixelIntensity = inputImage[x, y];                  // get pixel intesity (grayscale value)
                    float Dx = applyFilterToPixel(inputImage, horizontalKernel, x, y) / 8;
                    float Dy = applyFilterToPixel(inputImage, verticalKernel, x, y) / 8;
                    tempImage[x, y] = (byte)Math.Sqrt(Math.Pow(Dx, 2) + Math.Pow(Dy, 2));
                }

            return tempImage;
        }


        /*
         * thresholdImage: threshold a grayscale image
         * input:   inputImage          single-channel (byte) image
         * output:                      single-channel (byte) image with on/off values
         */
        private byte[,] thresholdImage(byte[,] inputImage, byte threshold)
        {
            if (threshold < 0 || threshold > 255)
                throw new ArgumentException("Threshold must be between 0 and 255"); //byte does force this already though but still.

            // create temporary grayscale image
            byte[,] tempImage = new byte[inputImage.GetLength(0), inputImage.GetLength(1)];

            byte highIntensity = 255;

            // TODO: add your functionality and checks, think about how to represent the binary values
            for (int x = 0; x < inputImage.GetLength(0); x++)                 // loop over columns
                for (int y = 0; y < inputImage.GetLength(1); y++)            // loop over rows
                {
                    byte pixelIntensity = inputImage[x, y];                  // get pixel intesity (grayscale value)
                    if (pixelIntensity >= threshold)
                        tempImage[x, y] = highIntensity;

                    //the tempImage array is already filled with 0's by default.

                }

            return tempImage;
        }



        //helper function, applies a structuring element to a single pixel in an image.
        private byte[,] applyStructElemDilationToPixel(byte[,] inputImage, bool[,] structelem, byte[,] outputImage, int x, int y)
        {
            int size = structelem.GetLength(0);
            int filterOffset = size / 2; //how far the structelem extends from the center pixel
            int minX = x - filterOffset; //top left x inside inputimage
            int minY = y - filterOffset; //top left y inside inputimage

            //go through all pixels inside the structelem
            for (int filterY = 0; filterY < size; filterY++)
            {
                for (int filterX = 0; filterX < size; filterX++)
                {
                    int imageX = minX + filterX; //the x coordinate of the pixel inside the inputimage
                    int imageY = minY + filterY; //the y coordinate of the pixel inside the inputimage
                    //if outside the border of the image simply pass)

                    //returns true if it was out of bounds
                    if (borderClamp(ref imageX, ref imageY, inputImage.GetLength(0) - 1, inputImage.GetLength(1) - 1))
                        continue;

                    if (structelem[filterX, filterY]) //structelem[2,1]
                        outputImage[imageX, imageY] = 255;
                }
            }

            return outputImage;
        }

        private byte[,] applyStructElemErosionToPixel(byte[,] inputImage, bool[,] structelem, byte[,] outputImage, int x, int y)
        {
            int size = structelem.GetLength(0);
            int filterOffset = size / 2; //how far the structelem extends from the center pixel
            int minX = x - filterOffset; //top left x inside inputimage
            int minY = y - filterOffset; //top left y inside inputimage
            //go through all pixels inside the structelem
            for (int filterY = 0; filterY < size; filterY++)
            {
                for (int filterX = 0; filterX < size; filterX++)
                {
                    int imageX = minX + filterX; //the x coordinate of the pixel inside the inputimage
                    int imageY = minY + filterY; //the y coordinate of the pixel inside the inputimage
                    //if outside the border of the image simply pass)

                    //returns true if it was out of bounds
                    if (borderClamp(ref imageX, ref imageY, inputImage.GetLength(0) - 1, inputImage.GetLength(1) - 1))
                        continue;

                    if (structelem[filterX, filterY]) //structelem[2,1]
                    {
                        if (inputImage[imageX, imageY] == 0)
                        {
                            outputImage[x, y] = 0;
                            return outputImage;
                        }
                        else
                            continue;
                    }
                }
            }

            return outputImage;
        }


        // Binary morphology

        /*
         * binaryErodeImage: perform binary erosion on a binary image using a structuring element
         * input:   inputImage          single-channel (byte) binary image 
         *          structElem          binary structuring element (true = foreground)
         * output:                      single-channel (byte) binary image after erosion
         */
        private byte[,] binaryErodeImage(byte[,] inputImage, bool[,] structElem)
        {
            if (structElem.GetLength(0) != structElem.GetLength(1) || structElem.GetLength(0) % 2 == 0)
                throw new ArgumentException("Structuring element must be square and of odd size");

            inputImage = thresholdImage(inputImage, threshold);
            byte[,] output = (byte[,])inputImage.Clone();
            // TODO: implement binary erosion
            for (int y = 0; y < inputImage.GetLength(1); y++)                 // loop over columns
                for (int x = 0; x < inputImage.GetLength(1); x++)            // loop over rows
                {
                    byte pixelIntensity = inputImage[x, y];         // get pixel intesity 
                    //is it a foreground pixel? 
                    if (pixelIntensity == 255)
                        output = applyStructElemErosionToPixel(inputImage, structElem, output, x, y);
                }

            return output;
        }

        /*
         * binaryDilateImage: perform binary dilation on a binary image using a structuring element
         * input:   inputImage          single-channel (byte) binary image 
         *          structElem          binary structuring element (true = foreground)
         * output:                      single-channel (byte) binary image after dilation
         */
        private byte[,] binaryDilateImage(byte[,] inputImage, bool[,] structElem)
        {
            if (structElem.GetLength(0) != structElem.GetLength(1) || structElem.GetLength(0) % 2 == 0)
                throw new ArgumentException("Structuring element must be square and of odd size");

            inputImage = thresholdImage(inputImage, threshold);
            byte[,] output = (byte[,])inputImage.Clone();
            for (int y = 0; y < inputImage.GetLength(1); y++)                 // loop over columns
                for (int x = 0; x < inputImage.GetLength(0); x++)            // loop over rows
                {
                    byte pixelIntensity = inputImage[x, y];         // get pixel intesity 
                    //is it a foreground pixel? 
                    if (pixelIntensity == 255)
                        output = applyStructElemDilationToPixel(inputImage, structElem, output, x, y);
                }

            return output;
        }

        /*
         * binaryOpenImage: perform binary opening on a binary image
         * input:   inputImage          single-channel (byte) binary image 
         *          structElem          binary structuring element (true = foreground)
         * output:                      single-channel (byte) binary image after opening
         */

        private byte[,] binaryOpenImage(byte[,] inputImage, bool[,] structElem)
        {

            if (structElem.GetLength(0) != structElem.GetLength(1) || structElem.GetLength(0) % 2 == 0)
                throw new ArgumentException("Structuring element must be square and of odd size");

            byte[,] erodedImage = binaryErodeImage(inputImage, structElem);
            byte[,] output = binaryDilateImage(erodedImage, structElem);

            return output;
        }

        /*
         * binaryCloseImage: perform binary closing on a binary image
         * input:   inputImage          single-channel (byte) binary image
         *          structElem          binary structuring element (true = foreground)
         * output:                      single-channel (byte) binary image after closing
         */
        private byte[,] binaryCloseImage(byte[,] inputImage, bool[,] structElem)
        {
            if (structElem.GetLength(0) != structElem.GetLength(1) || structElem.GetLength(0) % 2 == 0)
                throw new ArgumentException("Structuring element must be square and of odd size");

            byte[,] dilatedImage = binaryDilateImage(inputImage, structElem);
            byte[,] output = binaryErodeImage(dilatedImage, structElem);

            return output;
        }



        //---------------------------------------------------------//

        // Grayscale morphology

        /*
         * grayscaleErodeImage: perform grayscale erosion on a grayscale image using a structuring element
         * input:   inputImage          single-channel (byte) grayscale image
         *          structElem          integer structuring element 
         * output:                      single-channel (byte) grayscale image after erosion
         */


        //helper function, applies a structuring element to a single pixel in an image.
        private byte[,] applyStructElemDilationToGrayscalePixel(byte[,] inputImage, int?[,] structelem, byte[,] outputImage, int x, int y)
        {
            int size = structelem.GetLength(0);
            int filterOffset = size / 2; //how far the structelem extends from the center pixel
            int minX = x - filterOffset; //top left x inside inputimage
            int minY = y - filterOffset; //top left y inside inputimage
            int maxValue = int.MinValue;
            //go through all pixels inside the structelem
            for (int filterY = 0; filterY < size; filterY++)
            {
                for (int filterX = 0; filterX < size; filterX++)
                {
                    int imageX = minX + filterX; //the x coordinate of the pixel inside the inputimage
                    int imageY = minY + filterY; //the y coordinate of the pixel inside the inputimage
                    //if outside the border of the image simply pass)

                    //returns true if it was out of bounds
                    if (borderClamp(ref imageX, ref imageY, inputImage.GetLength(0) - 1, inputImage.GetLength(1) - 1))
                        continue;

                    if (structelem[filterX, filterY] != null) //check if filter value is not null
                    {
                        int candidate = Math.Min(255, (int)inputImage[imageX, imageY] + (int)structelem[filterX, filterY]);
                        if (candidate > maxValue)
                            maxValue = candidate;
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            if (maxValue != int.MinValue) { outputImage[x, y] = (byte)maxValue; }
            ;

            return outputImage;
        }


        private byte[,] applyStructElemErosionToGrayscalePixel(byte[,] inputImage, int?[,] structelem, byte[,] outputImage, int x, int y)
        {
            int size = structelem.GetLength(0);
            int filterOffset = size / 2; //how far the structelem extends from the center pixel
            int minX = x - filterOffset; //top left x inside inputimage
            int minY = y - filterOffset; //top left y inside inputimage
            int minValue = int.MaxValue;
            //go through all pixels inside the structelem
            for (int filterY = 0; filterY < size; filterY++)
            {
                for (int filterX = 0; filterX < size; filterX++)
                {
                    int imageX = minX + filterX; //the x coordinate of the pixel inside the inputimage
                    int imageY = minY + filterY; //the y coordinate of the pixel inside the inputimage
                    //if outside the border of the image simply pass)

                    //returns true if it was out of bounds
                    if (borderClamp(ref imageX, ref imageY, inputImage.GetLength(0) - 1, inputImage.GetLength(1) - 1))
                        continue;

                    if (structelem[filterX, filterY] != null) //check if filter value is not null
                    {
                        int candidate = Math.Min(255, Math.Max(0, (int)inputImage[imageX, imageY] - (int)structelem[filterX, filterY])); //maximum 255
                        if (candidate < minValue)
                            minValue = candidate;
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            if (minValue != int.MinValue) { outputImage[x, y] = (byte)minValue; }
            ;

            return outputImage;
        }



        private byte[,] grayscaleErodeImage(byte[,] inputImage, int?[,] structElem)
        {
            if (structElem.GetLength(0) != structElem.GetLength(1) || structElem.GetLength(0) % 2 == 0)
                throw new ArgumentException("Structuring element must be square and of odd size");

            byte[,] output = (byte[,])inputImage.Clone();

            for (int y = 0; y < inputImage.GetLength(1); y++)                 // loop over columns
                for (int x = 0; x < inputImage.GetLength(0); x++)            // loop over rows
                    output = applyStructElemErosionToGrayscalePixel(inputImage, structElem, output, x, y);

            return output;
        }

        /*
         * grayscaleDilateImage: perform grayscale dilation on a grayscale image using a structuring element
         * input:   inputImage          single-channel (byte) grayscale image
         *          structElem          integer structuring element
         * output:                      single-channel (byte) grayscale image after dilation
         */
        private byte[,] grayscaleDilateImage(byte[,] inputImage, int?[,] structElem)
        {
            if (structElem.GetLength(0) != structElem.GetLength(1) || structElem.GetLength(0) % 2 == 0)
                throw new ArgumentException("Structuring element must be square and of odd size");

            byte[,] output = (byte[,])inputImage.Clone();

            // TODO: implement grayscale dilation

            for (int y = 0; y < inputImage.GetLength(1); y++)                 // loop over columns
                for (int x = 0; x < inputImage.GetLength(0); x++)            // loop over rows
                    output = applyStructElemDilationToGrayscalePixel(inputImage, structElem, output, x, y);

            return output;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputImage">grayscale image</param>
        /// <param name="filterSize">size of the filter, must be odd and positive</param>
        /// <returns>grayscale image with a median filter applied</returns>
        /// <exception cref="ArgumentException"></exception>
        private byte[,] medianFilterImage(byte[,] inputImage, int filterSize)
        {
            if (filterSize % 2 == 0 || filterSize < 1)
                throw new ArgumentException("Filter size must be odd and positive");
            byte[,] output = (byte[,])inputImage.Clone();
            int filterOffset = filterSize / 2;
            List<byte> pixelsInFilter = new List<byte>();
            for (int y = 0; y < inputImage.GetLength(1); y++)                 // loop over columns
                for (int x = 0; x < inputImage.GetLength(0); x++)            // loop over rows
                {
                    pixelsInFilter.Clear();
                    //go through all pixels inside the filter
                    for (int filterY = 0; filterY < filterSize; filterY++)
                    {
                        for (int filterX = 0; filterX < filterSize; filterX++)
                        {
                            int imageX = x - filterOffset + filterX; //the x coordinate of the pixel inside the inputimage
                            int imageY = y - filterOffset + filterY; //the y coordinate of the pixel inside the inputimage
                                                                     //if outside the border of the image simply pass)
                                                                     //returns true if it was out of bounds
                            if (borderClamp(ref imageX, ref imageY, inputImage.GetLength(0) - 1, inputImage.GetLength(1) - 1))
                                continue;
                            pixelsInFilter.Add(inputImage[imageX, imageY]);
                        }
                    }
                    pixelsInFilter.Sort();
                    output[x, y] = pixelsInFilter[pixelsInFilter.Count / 2]; //get median
                }
            return output;
        }

        private void openImageDialog_FileOk(object sender, CancelEventArgs e)
        {

        }

        private void comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox.Text == "Task 1" || comboBox.Text == "Binary Pipeline" || comboBox.Text == "Grayscale Pipeline" || comboBox.Text == "Circle Hough Transform" || comboBox.Text == "Circle With Edges" || comboBox.Text == "Circle With Trimmed Edges" || comboBox.Text == "Circle With Combined Edges" || comboBox.Text == "Debug Detect Clock")
            {
                panel1.Visible = true;
            }
            else
            {
                panel1.Visible = false;
            }

            if (comboBox.Text == "Task 2")
            {
                panel2.Visible = true;
            }
            else
            {
                panel2.Visible = false;
            }
            if (comboBox.Text == "Task 3")
            {
                panel3.Visible = true;
            }
            else
            {
                panel3.Visible = false;
            }
            if (comboBox.Text == "Sequential Labeling" || comboBox.Text == "Flood Fill Labeling")
            {
                panel4.Visible = true;
            }
            else
            {
                panel4.Visible = false;
            }
            if (comboBox.Text == "Hough Line Segments" || comboBox.Text == "Binary Pipeline" || comboBox.Text == "Grayscale Pipeline" || comboBox.Text == "Circle With Edges" || comboBox.Text == "Circle With Trimmed Edges" || comboBox.Text == "Circle With Combined Edges" || comboBox.Text == "Debug Detect Clock")
            {
                panel5.Visible = true;
            }
            else
            {
                panel5.Visible = false;
            }
            if (comboBox.Text == "Binary Pipeline")
            {
                panel6.Visible = true;
            }
            else
            {
                panel6.Visible = false;
            }
            if (comboBox.Text == "Task 1")
            {
                numericUpDown3.Visible = true;
                label3.Visible = true;
            }
            else
            {
                numericUpDown3.Visible = false;
                label3.Visible = false;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown1.Value % 2 == 0)
                numericUpDown1.Value += 1; //force odd

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {

        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void panel5_Paint(object sender, PaintEventArgs e)
        {

        }

        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {

        }
        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {

        }
        private void numericUpDown6_ValueChanged(object sender, EventArgs e)
        {

        }
        private void numericUpDown7_ValueChanged(object sender, EventArgs e)
        {

        }





        // ====================================================================
        // ============= YOUR FUNCTIONS FOR ASSIGNMENT 2 GO HERE ==============
        // ====================================================================


        /// <summary>
        /// does sequential 8-neighbourhood region labeling on a binary image
        /// </summary>
        /// <param name="image">binary image</param>
        /// <returns>grayscale label image</returns>
        private byte[,] SequentialRegionLabeling(byte[,] image)
        {
            Dictionary<byte, byte> graph = new Dictionary<byte, byte>();
            byte[,] labelImage = new byte[image.GetLength(0), image.GetLength(1)];
            byte currentLabel = 2;

            for (int y = 0; y < image.GetLength(1); y++)
            {             // loop over columns
                for (int x = 0; x < image.GetLength(0); x++)            // loop over rows
                {
                    //do we have to give a label?
                    if (image[x, y] == 1)
                    {
                        //does a neighbour already have a label?
                        byte lowestvalue = byte.MaxValue;
                        for (int y2 = y - 1; y2 <= y + 1; y2++)
                        {
                            for (int x2 = x - 1; x2 <= x + 1; x2++)
                            {

                                //is this pixel outside the image?
                                if (borderClamp(x2, y2, image.GetLength(0) - 1, image.GetLength(1) - 1))
                                {
                                    continue;
                                }

                                if (labelImage[x2, y2] < lowestvalue && labelImage[x2, y2] > 0)
                                    lowestvalue = labelImage[x2, y2];
                            }
                        }

                        //find the collisions
                        for (int y2 = y - 1; y2 <= y + 1; y2++)
                        {
                            for (int x2 = x - 1; x2 <= x + 1; x2++)
                            {

                                //is this pixel outside the image?
                                if (borderClamp(x2, y2, image.GetLength(0) - 1, image.GetLength(1) - 1))
                                {
                                    continue;
                                }

                                //first find the lowest value neighbour, then map collisions to that value?

                                if (labelImage[x2, y2] < lowestvalue && labelImage[x2, y2] > 0)
                                    lowestvalue = labelImage[x2, y2];

                            }
                        }

                        labelImage[x, y] = lowestvalue;

                        //check for collisions 
                        for (int y2 = y - 1; y2 <= y + 1; y2++)
                        {
                            for (int x2 = x - 1; x2 <= x + 1; x2++)
                            {

                                //is this pixel outside the image?
                                if (borderClamp(x2, y2, image.GetLength(0) - 1, image.GetLength(1) - 1))
                                {
                                    continue;
                                }
                                byte highest;
                                byte lowest;
                                if ((labelImage[x2, y2] != labelImage[x, y]) && labelImage[x2, y2] > 0)
                                {
                                    highest = Math.Max(labelImage[x2, y2], labelImage[x, y]);
                                    lowest = Math.Min(labelImage[x2, y2], labelImage[x, y]);


                                    highest = FindTrueLabel(graph, highest); //find the lowest value highest maps to
                                    lowest = FindTrueLabel(graph, lowest); //find the lowest value lowest maps to


                                    if (highest != lowest) //if they already map to the same value, no action needed
                                    {
                                        if (highest > lowest)
                                            graph[highest] = lowest;
                                        else
                                            graph[lowest] = highest;
                                    }
                                }
                            }
                        }
                        //a neighbour does not have a label, assign a new one
                        if (lowestvalue == byte.MaxValue)
                        {
                            labelImage[x, y] = currentLabel;
                            currentLabel++;
                        }
                    }
                }
            }
            for (int y3 = 0; y3 < image.GetLength(1); y3++)
            {             // loop over columns
                for (int x3 = 0; x3 < image.GetLength(0); x3++)            // loop over rows
                {
                    if (labelImage[x3, y3] > 1)
                    {
                        labelImage[x3, y3] = FindTrueLabel(graph, labelImage[x3, y3]);
                    }

                }
            }
            return labelImage;
        }

        private byte[,] FloodFillLabeling(byte[,] image)
        {


            byte[,] labelImage = new byte[image.GetLength(0), image.GetLength(1)];
            byte currentLabel = 2;
            bool[,] visited = new bool[image.GetLength(0), image.GetLength(1)];  // track visited pixels
            for (int y = 0; y < image.GetLength(1); y++)
            {             // loop over columns
                for (int x = 0; x < image.GetLength(0); x++)            // loop over rows
                {
                    //do we have to give a label?
                    if (image[x, y] == 1 && labelImage[x, y] == 0)
                    {
                        FloodFill(image, ref labelImage, visited, currentLabel, x, y);
                        currentLabel++;
                    }
                }
            }
            return labelImage;
        }
        //FloodFills from a certain coordinate with a certain label, assumes x,y is confirmed to be foreground
        private void FloodFill(byte[,] originalimage, ref byte[,] labelImage, bool[,] visited, byte label, int x, int y)
        {

            //reworked to need a stack due to OS call stack overflows, but this wasnt the underlying cause of the overflow, but still the stack remains
            Stack<(int x, int y)> stack = new Stack<(int x, int y)>();
            stack.Push((x, y));
            visited[x, y] = true;

            while (stack.Count > 0)
            {
                (int x, int y) pixel = stack.Pop();
                labelImage[pixel.x, pixel.y] = label;

                //add new coordinates to stack, also check if they are valid before adding them
                if (pixel.y + 1 < originalimage.GetLength(1) && originalimage[pixel.x, pixel.y + 1] == 1 && !visited[pixel.x, pixel.y + 1])
                {
                    visited[pixel.x, pixel.y + 1] = true;
                    stack.Push((pixel.x, pixel.y + 1)); // up
                }

                if (pixel.y - 1 >= 0 && originalimage[pixel.x, pixel.y - 1] == 1 && !visited[pixel.x, pixel.y - 1])
                {
                    visited[pixel.x, pixel.y - 1] = true;
                    stack.Push((pixel.x, pixel.y - 1)); // down
                }

                if (pixel.x - 1 >= 0 && originalimage[pixel.x - 1, pixel.y] == 1 && !visited[pixel.x - 1, pixel.y])
                {
                    visited[pixel.x - 1, pixel.y] = true;
                    stack.Push((pixel.x - 1, pixel.y)); // left

                }
                if (pixel.x + 1 < originalimage.GetLength(0) && originalimage[pixel.x + 1, pixel.y] == 1 && !visited[pixel.x + 1, pixel.y])
                {
                    visited[pixel.x + 1, pixel.y] = true;
                    stack.Push((pixel.x + 1, pixel.y)); // right
                }

            }


        }



        /// <summary>
        /// helper function for SequentialRegionLabeling, will traverse the graph to find what the lowest label is that can be reached from 'from'.
        ///  also updates the graph along the way to point directly to the lowest label 
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="from"></param>
        /// <returns>the lowest label reachable from 'from', assuming the graph is directed, where larger values are mapped to lower ones</returns>
        private byte FindTrueLabel(Dictionary<byte, byte> graph, byte from)
        {

            if (graph.TryGetValue(from, out byte label))
            {
                graph[from] = FindTrueLabel(graph, label); //path compression
                return graph[from];
            }
            else
            {
                return from;
            }

        }

        /// <summary>
        /// Does hough transform on a binary edge map or grayscale edge-magnitude image.
        /// </summary>
        /// <param name="inputImage">either a binary edge map OR grayscale edge-magnitude image, has unsupported results otherwise</param>
        /// <returns>2D accumulator array (r-theta image) </returns>
        private byte[,] HoughTransform(byte[,] inputImage)
        {
            //image diagonal length
            int imageXsize = inputImage.GetLength(0);
            int imageYsize = inputImage.GetLength(1);
            double diagonal = Math.Sqrt(Math.Pow(imageXsize, 2) + Math.Pow(imageYsize, 2));
            int rmax = (int)Math.Ceiling(diagonal);

            bool binary = isBinary(inputImage);

            byte[,] accumulator = new byte[180, 2 * rmax]; //r goes from -rmax to +rmax, theta from 0 to 179 (in degrees, must go to radians when calculating)
            float[,] grayscaleaccumulator = new float[180, 2 * rmax]; //r goes from -rmax to +rmax, theta from 0 to 179 (in degrees, must go to radians when calculating)
            // OutputImage = new Bitmap(accumulator.GetLength(0), accumulator.GetLength(1));
            // Pre-calculate sin/cos values
            double[] cosValues = new double[180];
            double[] sinValues = new double[180];
            for (int theta = 0; theta < 180; theta++)
            {
                double radians = degreesToRadians(theta);
                cosValues[theta] = Math.Cos(radians);
                sinValues[theta] = Math.Sin(radians);
            }

            // Use pre-calculated values in the main loop
            for (int y = 0; y < inputImage.GetLength(1); y++)
            {
                for (int x = 0; x < inputImage.GetLength(0); x++)
                {
                    if (inputImage[x, y] != 0)
                    {
                        int adjustedX = x - imageXsize / 2;
                        int adjustedY = y - imageYsize / 2;

                        for (int theta = 0; theta < 180; theta++)
                        {
                            if (theta != 45 && theta != 135)
                            {
                                int r = (int)Math.Round((adjustedX * cosValues[theta] + adjustedY * sinValues[theta]));
                                if (binary)
                                {
                                    if (accumulator[theta, r + rmax] < 255) //prevent overflow
                                    {
                                        accumulator[theta, r + rmax]++;
                                    }
                                }
                                else grayscaleaccumulator[theta, r + rmax] += inputImage[x, y];
                            }
                        }
                    }
                }
            }
            if (binary) {
                return accumulator;
            }
            else //normalize if its an edge-magnitude image
            {
                byte[,] finalAccumulator = new byte[180, 2 * rmax];
                float highest = findHighestValue(grayscaleaccumulator);
                for (int theta = 0; theta < grayscaleaccumulator.GetLength(0); theta++)
                {
                    for (int r = 0; r < grayscaleaccumulator.GetLength(1); r++)
                    {
                        finalAccumulator[theta, r] = (byte)((grayscaleaccumulator[theta, r] / highest) * 255);
                    }
                }
                return finalAccumulator;
            }
        }

        private List<(int, int)> peakFinding(byte[,] accumulator, int threshold)
        {
            //loop through the accumulator and set a pixel to zero if a neighbour is higher
            byte[,] neighbourpass = (byte[,])accumulator.Clone();
            int filterSize = 3;
            int filterOffset = filterSize / 2;
            for (int theta = 0; theta < accumulator.GetLength(0); theta++)
                for (int r = 0; r < accumulator.GetLength(1); r++)
                {
                    //go through all pixels inside the filter
                    for (int filterY = 0; filterY < filterSize; filterY++)
                    {
                        for (int filterX = 0; filterX < filterSize; filterX++)
                        {
                            int imageX = theta - filterOffset + filterX; //the x coordinate of the pixel inside the filter
                            int imageY = r - filterOffset + filterY; //the y coordinate of the pixel inside the filter
                                                                     //if outside the border of the image simply pass)
                            if (borderClamp(ref imageX, ref imageY, accumulator.GetLength(0) - 1, accumulator.GetLength(1) - 1))
                                continue;

                            if (accumulator[imageX, imageY] > accumulator[theta, r] || (accumulator[imageX, imageY] == accumulator[theta, r] && (imageX < theta || (imageX == theta && imageY < r))))
                            {
                                neighbourpass[theta, r] = 0;
                                //break the neighbour loop
                                filterX = filterSize;
                                filterY = filterSize;
                            }
                        }
                    }
                }
            //apply treshold 
            List<(int, int)> peaks = new List<(int, int)>();
            float highest = findHighestValue(accumulator);
            for (int theta = 0; theta < neighbourpass.GetLength(0); theta++)
                for (int r = 0; r < neighbourpass.GetLength(1); r++)
                {
                    if (neighbourpass[theta, r] >= (threshold))
                    {
                        peaks.Add((theta, r));
                    }
                }
            return peaks;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peaks">rtheta peaks for hough transform</param>
        /// <param name="minIntensityThreshold">minimum intensity treshold for grayscale images, pixels below this value will be considered background </param>
        /// <param name="minSegmentLength">the minimum amount of pixels that have to be on the line for it to be drawn as a line</param>
        /// <param name="maxGapParameter"></param>
        /// <returns></returns>
        private Color[,] HoughLineSegments(byte[,] inputImage, List<(int, int)> peaks, int minIntensityThreshold, int minSegmentLength, int maxGapParameter)
        {
            //for each peak, we have to check if any pixels of the input image are on the line
            //if the amount of pixels found is equal or higher than minSegmentLength it means it is an actual line
            //we only stop detecting a line if the gap that forms between pixels is equal or higher than maxGapParameter
            //only pixels that have a higher intensity then minIntensityThreshold should be detected as part of the line

            //it is a line if the euclidean length is big enough (min segment length)

            float imageXsize = inputImage.GetLength(0);
            float imageYsize = inputImage.GetLength(1);
            List<(int, int)> onPixels = new List<(int, int)>();
            List<((int, int), (int, int))> lines = new List<((int, int), (int, int))>();
            List<(int, int)> ALLonPixels = new List<(int, int)>();
            bool binary = isBinary(inputImage);

            Color[,] output = new Color[inputImage.GetLength(0), inputImage.GetLength(1)];
            for (int y = 0; y < inputImage.GetLength(1); y++)
            {
                for (int x = 0; x < inputImage.GetLength(0); x++)
                {
                    byte value = inputImage[x, y];
                    output[x, y] = Color.FromArgb(value, value, value);
                }
            }

            //pre-calculate sin/cos values
            double[] cosValues = new double[180];
            double[] sinValues = new double[180];
            for (int theta = 0; theta < 180; theta++)
            {
                double radians = degreesToRadians(theta);
                cosValues[theta] = Math.Cos(radians);
                sinValues[theta] = Math.Sin(radians);
            }

            double diagonal = Math.Sqrt(Math.Pow(imageXsize, 2) + Math.Pow(imageYsize, 2));
            int rmax = (int)Math.Ceiling(diagonal);

            //collect all on pixels in the image so we dont have to loop through the entire image for each peak
            for (int y = 0; y < inputImage.GetLength(1); y++)
            {
                for (int x = 0; x < inputImage.GetLength(0); x++)
                {
                    if (binary)
                    {
                        if (inputImage[x, y] == 255)
                            ALLonPixels.Add((x, y)); //if the pixel is on, add it to the list
                    }
                    else
                    {
                        if (inputImage[x, y] >= minIntensityThreshold)  //check whether the current pixel is on or not
                            ALLonPixels.Add((x, y)); //if the pixel is on, add it to the list
                    }
                }
            }


            foreach ((int theta, int r) peak in peaks)
            {

                onPixels = findOnPixels(ALLonPixels, peak.theta, peak.r, minIntensityThreshold, binary, (int)imageXsize, (int)imageYsize, cosValues, sinValues, rmax);
                if (onPixels.Count < 2)
                {
                    continue;
                }
                lines = collapseLines(onPixels, maxGapParameter, minSegmentLength); //rety
                output = drawLines(output, lines);

                //onPixels = findOnPixels(inputImage, peak.theta, peak.r, minIntensityThreshold, binary, (int)imageXsize, (int)imageYsize);
                /*
                foreach ((int x, int y) pixel in onPixels)
                {
                    output[pixel.x, pixel.y] = Color.Red;
                }
                */


            }
            return output;
        }




        //isolate helper functions:
        //should return all pixels on the line defined by r and theta that are on in the input image
        private List<(int, int)> findOnPixels(List<(int, int)> ALLonPixels, int theta, int r, int intensitythreshold, bool binary, int imageXsize, int imageYsize, double[] cosValues, double[] sinValues, int rmax)
        {
            List<(int, int)> onPixels = new List<(int, int)>();

            foreach ((int x, int y) p in ALLonPixels)
            {
                int adjustedX = p.x - imageXsize / 2;
                int adjustedY = p.y - imageYsize / 2;
                int adjustedR = r - rmax;
                float errorRange = 0.5f; //how far a pixel can be from the line (CHANGE-ABLE IF NEEDED)

                //r = x cosθ + y sinθ (idea is to fill in x and y of the pixel, and see if r is close enough to the r of the line)
                double val = adjustedX * cosValues[theta] + adjustedY * sinValues[theta];
                if (Math.Abs(val - adjustedR) <= errorRange)
                {
                    onPixels.Add((p.x, p.y));
                }
            }

            return onPixels; }


        //collapse onPixels into lines returns the list of lines

        private List<((int, int), (int, int))> collapseLines(List<(int x, int y)> onPixels, int maxGapParameter, int minSegmentLength)
        {
            var lines = new List<((int, int), (int, int))>();

            if (onPixels == null || onPixels.Count < 2)
                return lines;

            //sort so that we have them in order
            onPixels.Sort((a, b) =>
            {
                int compare = a.x.CompareTo(b.x);
                if (compare != 0)
                {
                    return compare;
                }
                else
                {
                    return a.y.CompareTo(b.y);
                }
            });

            (int x, int y) start = onPixels[0];
            (int x, int y) end = onPixels[0];


            for (int i = 1; i < onPixels.Count; i++)
            {
                (int x, int y) current = onPixels[i];
                double dist = Math.Sqrt(Math.Pow(current.x - end.x, 2) + Math.Pow(current.y - end.y, 2));

                //is the distance between the current end of the line and the next pixel smaller than the max gap parameter?
                if (dist <= maxGapParameter)
                {
                    // collapse into current segment
                    end = current;
                }
                else
                {
                    //this segment cannot be extended, check if its long enough be a valid line
                    double segLength = Math.Sqrt(
                        Math.Pow(end.x - start.x, 2) +
                        Math.Pow(end.y - start.y, 2));
                    if (segLength >= minSegmentLength)
                        lines.Add((start, end));

                    // Start a new segment
                    start = current;
                    end = current;
                }
            }

            //there is one last segment to check

            //add the last segment if valid
            double lastLength = Math.Sqrt(
                Math.Pow(end.x - start.x, 2) +
                Math.Pow(end.y - start.y, 2));
            if (lastLength >= minSegmentLength)
                lines.Add((start, end));

            return lines;
        }

        //draw lines using bresenhams, masterfully adapted from wikipedia
        private Color[,] drawLines(
            Color[,] outputImage,
            List<((int x, int y) start, (int x, int y) end)> lines)
        {
            int width = outputImage.GetLength(0);
            int height = outputImage.GetLength(1);

            foreach (var line in lines)
            {
                int x0 = line.start.x;
                int y0 = line.start.y;
                int x1 = line.end.x;
                int y1 = line.end.y;

                // bresenham's algorithm
                int dx = Math.Abs(x1 - x0);
                int dy = -Math.Abs(y1 - y0);
                int sx = x0 < x1 ? 1 : -1;
                int sy = y0 < y1 ? 1 : -1;
                int err = dx + dy;

                while (true)
                {
                    // bounds check
                    if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
                        outputImage[x0, y0] = Color.Red;

                    if (x0 == x1 && y0 == y1) break;

                    int e2 = 2 * err;
                    if (e2 >= dy)
                    {
                        err += dy;
                        x0 += sx;
                    }
                    if (e2 <= dx)
                    {
                        err += dx;
                        y0 += sy;
                    }
                }
            }
            return outputImage;
        }

        private bool isBinary(byte[,] image)
        {
            for (int y = 0; y < image.GetLength(1); y++)
                for (int x = 0; x < image.GetLength(0); x++)
                    if (image[x, y] != 0 && image[x, y] != 255)
                        return false;
            return true;
        }

        private double degreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        private float findHighestValue(float[,] image)
        {
            float highest = 0;
            for (int y = 0; y < image.GetLength(1); y++)
                for (int x = 0; x < image.GetLength(0); x++)
                {
                    if (image[x, y] > highest)
                        highest = image[x, y];
                }
            return highest;
        }

        private byte findHighestValue(byte[,] image)
        {
            byte highest = 0;
            for (int y = 0; y < image.GetLength(1); y++)
                for (int x = 0; x < image.GetLength(0); x++)
                {
                    if (image[x, y] > highest)
                        highest = image[x, y];
                }
            return highest;
        }

        private void INFOIBV_Load(object sender, EventArgs e)
        {

        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void label10_Click(object sender, EventArgs e)
        {

        }

        private void label13_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        // ====================================================================
        // ============= YOUR FUNCTIONS FOR ASSIGNMENT 3 GO HERE ==============
        // ====================================================================

        //input: grayscale edge-magnitude image or binary image
        //out: accumulator for possible circle centers, non-normalized raw votes

        /// <summary>
        /// Circle Hough Transform 
        /// </summary>
        /// <param name="inputImage">grayscale edge-magnitude image (binary might also work, not tested)</param>
        /// <returns>accumulator for possible circle centers, non-normalized raw votes</returns>
        private float[,,] CircleHoughTransform(byte[,] inputImage)
        {
            //image diagonal length
            int imageXsize = inputImage.GetLength(0);
            int imageYsize = inputImage.GetLength(1);
            double diagonal = Math.Sqrt(Math.Pow(imageXsize, 2) + Math.Pow(imageYsize, 2));
            int pmax = (int)Math.Ceiling(diagonal);

            float[,,] grayscaleaccumulator = new float[imageXsize + 1, imageYsize + 1, pmax / 2 - 10]; //[x,y,p] radius = p + 10


            //pre-calculate sin/cos values so we dont have to calculate them for each pixel
            double[,] cosValues = new double[((int)(pmax / 2)) + 1, 360];
            double[,] sinValues = new double[((int)(pmax / 2)) + 1, 360];

            for (int p = 10; p < pmax / 2; p++)
            {
                for (int degrees = 0; degrees < 360; degrees++)
                {
                    double radians = Math.PI * degrees / 180.0;
                    cosValues[p, degrees] = p * Math.Cos(radians);
                    sinValues[p, degrees] = p * Math.Sin(radians);
                }
            }

            //threadlocal buffer for each thread to vote into
            ThreadLocal<float[,]> localBuffer = new ThreadLocal<float[,]>(() => new float[imageXsize + 1, imageYsize + 1]);

            //loop over the possible radii in a parallel for to increase computation speed
            Parallel.For(10, pmax / 2, p =>
            {
                //create local accumulator for this radius
                var localAccumulator = localBuffer.Value;

                // before continuing the loop, clear the local buffer for this radius
                Array.Clear(localAccumulator, 0, localAccumulator.Length);

                //loop over all pixels in the input image
                for (int v = 0; v < imageYsize; v++)
                {
                    for (int u = 0; u < imageXsize; u++)
                    {
                        //only vote if this is an edge pixel
                        if (inputImage[u, v] == 0)
                            continue;

                        for (int degrees = 0; degrees < 360; degrees++)
                        {
                            //coordinate of possible circle center; we can just fill in p and degrees and substract the corresponding value because the pre-calculated values already have the radius factored in
                            int x = (int)(u - cosValues[p, degrees]);
                            int y = (int)(v - sinValues[p, degrees]);

                            if (x >= 0 && x < imageXsize && y >= 0 && y < imageYsize)
                                localAccumulator[x, y] += (float)(inputImage[u, v] * Math.Sqrt(p)); // vote for this possible center with edge magnitude, make smaller circles vote less
                        }
                    }
                }

                // merge all the previously computed local votes into the global grayscale accumulator
                for (int x = 0; x < imageXsize; x++)
                    for (int y = 0; y < imageYsize; y++)
                        grayscaleaccumulator[x, y, p - 10] += localAccumulator[x, y];
            });

            return grayscaleaccumulator;
        }
        /// <summary>
        /// Draw a cirlce on the input image
        /// </summary>
        /// <param name="inputImage"></param>
        /// <param name="centerX">location of the center X inside the image</param>
        /// <param name="centerY">location of the center Y inside the image</param>
        /// <param name="radius">radius of the circle</param>
        /// <returns>an image with the circle drawn on it</returns>
        private Color[,] DrawCircle(Color[,] inputImage, int centerX, int centerY, int radius)
        {
            int imageXsize = inputImage.GetLength(0);
            int imageYsize = inputImage.GetLength(1);

            for (int degrees = 0; degrees < 360; degrees++)
            {
                double radians = degreesToRadians(degrees);
                int x = (int)(centerX - radius * Math.Cos(radians));
                int y = (int)(centerY - radius * Math.Sin(radians));
                if (x >= 0 && x < imageXsize && y >= 0 && y < imageYsize)
                    inputImage[x, y] = Color.Red;
            }
            return inputImage;
        }
        //make the grayscale a color image,
        private Color[,] GrayscaleToColor(byte[,] inputImage)
        {
            Color[,] output = new Color[inputImage.GetLength(0), inputImage.GetLength(1)];
            for (int y = 0; y < inputImage.GetLength(1); y++)
            {
                for (int x = 0; x < inputImage.GetLength(0); x++)
                {
                    byte value = inputImage[x, y];
                    output[x, y] = Color.FromArgb(value, value, value);
                }
            }
            return output;
        }

        /// <summary>
        /// unused function for peakfinding in the houghcircle accumulator, exchanged for a faster NMS version
        /// </summary>
        /// <param name="accumulator"></param>
        /// <param name="amount"></param>
        /// <param name="minimumValue"></param>
        /// <returns></returns>
        private List<(int x, int y, int p, int val)> FindXHighest(float[,,] accumulator, int amount, int minimumValue)
        {
            List<(int x, int y, int p, int val)> peaks = new List<(int x, int y, int p, int val)>();

            for (int p = 0; p < accumulator.GetLength(2); p++)
            {
                for (int y = 0; y < accumulator.GetLength(1); y++)
                {
                    for (int x = 0; x < accumulator.GetLength(0); x++)
                    {
                        int val = (int)accumulator[x, y, p];
                        //check if this is one of the highest values
                        if (val > minimumValue)
                        {
                            //add to list
                            peaks.Add((x, y, p, val));
                            if (peaks.Count > amount)
                            {
                                peaks.Sort((a, b) => b.val.CompareTo(a.val)); //sort descending
                                int last = peaks.Count - 1;
                                peaks.RemoveAt(last);
                            }
                        }
                    }
                }
            }
            return peaks;
        }

        /// <summary>
        /// non-maximum suppression and peakfinding for circle hough transform accumulator
        /// </summary>
        /// <param name="accumulator">HoughCircle accumulator</param>
        /// <param name="topN">the amount of peaks you want returned</param>
        /// <param name="neighborhood">neighbourhood for NMS</param>
        /// <returns>the list of highest votes peaks after applying NMS</returns>
        private List<(int x, int y, int p, int val)> FastNMS(float[,,] accumulator, int topN, int neighborhood)
        {
            int width = accumulator.GetLength(0);
            int height = accumulator.GetLength(1);
            int depth = accumulator.GetLength(2);

            // Min-heap to maintain the top N peaks
            SortedSet<(int val, int x, int y, int p)> heap = new SortedSet<(int val, int x, int y, int p)>();

            // To mark suppressed voxels
            bool[,,] suppressed = new bool[width, height, depth];

            for (int p = 0; p < depth; p++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (suppressed[x, y, p]) continue; // skip suppressed voxels
                        int val = (int)accumulator[x, y, p];
                        if (val == 0) continue;

                        // Mark neighbors as suppressed
                        for (int ny = Math.Max(0, y - neighborhood); ny <= Math.Min(height - 1, y + neighborhood); ny++)
                        {
                            for (int nx = Math.Max(0, x - neighborhood); nx <= Math.Min(width - 1, x + neighborhood); nx++)
                            {
                                suppressed[nx, ny, p] = true;
                            }
                        }

                        heap.Add((val, x, y, p));

                        // Keep only top N peaks
                        if (heap.Count > topN)
                            heap.Remove(heap.Min);
                    }
                }
            }

            // Convert to list sorted descending by value
            List<(int x, int y, int p, int val)> peaks = new List<(int x, int y, int p, int val)>();
            foreach (var item in heap)
                peaks.Add((item.x, item.y, item.p, item.val));

            peaks.Sort((a, b) => b.val.CompareTo(a.val));
            return peaks;
        }

        /// <summary>
        /// Cull line segements that do not have a point inside the given circle
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="circle"></param>
        /// <returns>a list of line segments that have a point inside the given circle </returns>
        private List<((int, int), (int, int))> TrimCandidates(List<((int, int), (int, int))> lines, (int x, int y, int r) circle)
        {

            List<((int, int), (int, int))> candidates = new List<((int, int), (int, int))>();
            foreach (((int, int) p1, (int, int) p2) segment in lines)
            {
                //check if the line segments have any point inside the circle
                if (LineIntersectsCircle(segment.p1, segment.p2, circle))
                {
                    candidates.Add(segment);
                }
            }

            return candidates;
        }

        /// <summary>
        /// check if a line segment intersects a circle
        /// </summary>
        /// <param name="p1">start/endpoint of the linesegment</param>
        /// <param name="p2">endpoint/start of the linesegment</param>
        /// <param name="circle"></param>
        /// <returns>true if the line intersects the circle</returns>
        private bool LineIntersectsCircle((int x, int y) p1, (int x, int y) p2, (int x, int y, int r) circle)
        {
            float x1 = p1.x, y1 = p1.y;
            float x2 = p2.x, y2 = p2.y;
            float cx = circle.x, cy = circle.y, r = circle.r;

            // distance from point 1 to point 2
            float dx = x2 - x1;
            float dy = y2 - y1;

            // distance from point 1 to circle center
            float fx = cx - x1;
            float fy = cy - y1;

            //project circle center onto line segment
            float t = (fx * dx + fy * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            //find the closest point on the line to the cricle center
            float closestX = x1 + t * dx;
            float closestY = y1 + t * dy;

            //distance from circle center to closest point
            float distSq = (closestX - cx) * (closestX - cx) + (closestY - cy) * (closestY - cy);

            //determine if that point is inside the cirle
            return (distSq <= (r * r));
        }

        //exactly the same as HoughLineSegments but doesnt return an image immediately, only the list of line segments
        private List<((int, int), (int, int))> HoughLineSegmentsRaw(byte[,] inputImage, List<(int, int)> peaks, int minIntensityThreshold, int minSegmentLength, int maxGapParameter)
        {
            //for each peak, we have to check if any pixels of the input image are on the line
            //if the amount of pixels found is equal or higher than minSegmentLength it means it is an actual line
            //we only stop detecting a line if the gap that forms between pixels is equal or higher than maxGapParameter
            //only pixels that have a higher intensity then minIntensityThreshold should be detected as part of the line

            //it is a line if the euclidean length is big enough (min segment length)

            float imageXsize = inputImage.GetLength(0);
            float imageYsize = inputImage.GetLength(1);
            List<(int, int)> onPixels = new List<(int, int)>();
            List<((int, int), (int, int))> lines = new List<((int, int), (int, int))>();
            List<(int, int)> ALLonPixels = new List<(int, int)>();
            bool binary = isBinary(inputImage);

            Color[,] output = new Color[inputImage.GetLength(0), inputImage.GetLength(1)];
            for (int y = 0; y < inputImage.GetLength(1); y++)
            {
                for (int x = 0; x < inputImage.GetLength(0); x++)
                {
                    byte value = inputImage[x, y];
                    output[x, y] = Color.FromArgb(value, value, value);
                }
            }

            //pre-calculate sin/cos values
            double[] cosValues = new double[180];
            double[] sinValues = new double[180];
            for (int theta = 0; theta < 180; theta++)
            {
                double radians = degreesToRadians(theta);
                cosValues[theta] = Math.Cos(radians);
                sinValues[theta] = Math.Sin(radians);
            }

            double diagonal = Math.Sqrt(Math.Pow(imageXsize, 2) + Math.Pow(imageYsize, 2));
            int rmax = (int)Math.Ceiling(diagonal);

            //collect all on pixels in the image so we dont have to loop through the entire image for each peak
            for (int y = 0; y < inputImage.GetLength(1); y++)
            {
                for (int x = 0; x < inputImage.GetLength(0); x++)
                {
                    if (binary)
                    {
                        if (inputImage[x, y] == 255)
                            ALLonPixels.Add((x, y)); //if the pixel is on, add it to the list
                    }
                    else
                    {
                        if (inputImage[x, y] >= minIntensityThreshold)  //check whether the current pixel is on or not
                            ALLonPixels.Add((x, y)); //if the pixel is on, add it to the list
                    }
                }
            }


            foreach ((int theta, int r) peak in peaks)
            {

                onPixels = findOnPixels(ALLonPixels, peak.theta, peak.r, minIntensityThreshold, binary, (int)imageXsize, (int)imageYsize, cosValues, sinValues, rmax);
                if (onPixels.Count < 2)
                {
                    continue;
                }
                List<((int, int), (int, int))> collapsedLines = collapseLines(onPixels, maxGapParameter, minSegmentLength);
                lines.AddRange(collapsedLines);


            }
            return lines;
        }

        /// <summary>
        /// unused function that tried to trim lines that do not emanate from the circle center, not used due to poor results
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="circle"></param>
        /// <param name="maxAngle"></param>
        /// <returns>a list of lines that roughly emanate from the circle center based on max angle</returns>
        private List<((int, int), (int, int))> TrimNonEmenatingLines(List<((int, int) p1, (int, int) p2)> segments, (int x, int y, int r) circle, double maxAngle)  // angle threshold
        {
            List<((int, int), (int, int))> Lines = new List<((int, int), (int, int))>();
            int cx = circle.x;
            int cy = circle.y;

            foreach (var segment in segments)
            {
                (int x1, int y1) = segment.p1;
                (int x2, int y2) = segment.p2;

                //segment as vector
                double dx = x2 - x1;
                double dy = y2 - y1;

                //vector from a segment start/end (can be either) to circle center
                double fx = cx - x1;
                double fy = cy - y1;

                //projection to find closest point on the line to the circle center
                double segLengthSq = dx * dx + dy * dy; //lengte segment kwadraat
                double t = (fx * dx + fy * dy) / segLengthSq; // doet vector projectie op een of andere manier, snap alleen niet goed hoe, overgenomen uit een video
                t = Math.Max(0, Math.Min(1, t));
                double closestX = x1 + t * dx;
                double closestY = y1 + t * dy;
                double vx = closestX - cx;
                double vy = closestY - cy;

                //inproduct
                double dot = dx * vx + dy * vy;
                double segLen = Math.Sqrt(dx * dx + dy * dy); //lengte segment
                double vLen = Math.Sqrt(vx * vx + vy * vy); //afstand van dichtstbijzijnde punt op lijn naar cirkel middelpunt

                if (vLen == 0 || segLen == 0)
                    continue; 

                double angle = Math.Acos(dot / (segLen * vLen));
                double angleDegrees = angle * (180.0 / Math.PI);

                
                if (angleDegrees < maxAngle)
                    Lines.Add(segment);
            }

            return Lines;
        }

        /// <summary>
        /// Combine line segments that are close and somewhat parallel. 
        /// After completion you might want to run this again as the new lines could possibly be combined again.
        /// </summary>
        /// <param name="segments">line segments</param>
        /// <param name="maxAngle">maximum angle difference between lines for them to be combined</param>
        /// <param name="maxDistance">maximum distance between lines for them to be combined</param>
        /// <returns>a list of combined lines</returns>
        private List<((int, int), (int, int))> CombineLines(List<((int, int), (int, int))> segments, float maxAngle, float maxDistance)
        {
            double MaxAngle = degreesToRadians(maxAngle);

            bool[] merged = new bool[segments.Count]; //keep check of which segments have been merged
            List<((int, int), (int, int))> combined = new List<((int, int), (int, int))>(); //the combined lines

            for (int i = 0; i < segments.Count; i++)
            {
                if (merged[i]) continue;
                var (p1a, p2a) = segments[i];

                // Start a cluster with this segment
                List<(int, int)> points = new List<(int, int)> { p1a, p2a };
                merged[i] = true;

                double angleA = Math.Atan2(p2a.Item2 - p1a.Item2, p2a.Item1 - p1a.Item1);

                for (int j = i + 1; j < segments.Count; j++)
                {
                    if (merged[j]) continue;
                    var (p1b, p2b) = segments[j];

                    double angleB = Math.Atan2(p2b.Item2 - p1b.Item2, p2b.Item1 - p1b.Item1);

                    //check direction similarity
                    double angleDiff = Math.Abs(angleA - angleB);
                    if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;
                    if (angleDiff > MaxAngle) continue;

                    //Check proximity (any endpoint close)
                    double minDist = Math.Min(
                        Math.Min(Distance(p1a, p1b), Distance(p1a, p2b)),
                        Math.Min(Distance(p2a, p1b), Distance(p2a, p2b))
                    );

                    //or closest-point-to-line distance (each endpoint of B to line A)
                    double dist1 = DistancePointToLine(p1b, (p1a, p2a));
                    double dist2 = DistancePointToLine(p2b, (p1a, p2a));

                    double lineProximity = Math.Min(dist1, dist2);

                    if (minDist < maxDistance || lineProximity < maxDistance)
                    {
                        //combine into line cluster
                        points.Add(p1b);
                        points.Add(p2b);
                        merged[j] = true;
                    }
                }

                // Create a new merged line from farthest endpoints in this cluster
                var mergedLine = FindFarthestPoints(points);
                combined.Add(mergedLine);
            }

            return combined;
        }

        /// <summary>
        /// Calculate the shortest perpindicular distance from a point to a line
        /// </summary>
        /// <param name="p">the point</param>
        /// <param name="line">the line</param>
        /// <returns>the shortest perpindicular distance</returns>
        double DistancePointToLine((int x, int y) p, ((int x, int y) p1, (int x, int y) p2) line)
        {
            double x0 = p.x, y0 = p.y;

            double x1 = line.p1.x, y1 = line.p1.y;
            double x2 = line.p2.x, y2 = line.p2.y ;

            double numerator = Math.Abs((y2 - y1) * x0 - (x2 - x1) * y0 + x2 * y1 - y2 * x1);
            double denominator = Math.Sqrt(Math.Pow(y2 - y1, 2) + Math.Pow(x2 - x1, 2));

            return numerator / denominator;
        }

        /// <summary>
        /// Distance between two points
        /// </summary>
        /// <param name="a">point A</param>
        /// <param name="b">point B</param>
        /// <returns>the distance between point A and B </returns>
        private double Distance((int, int) a, (int, int) b)
        {
            double dx = a.Item1 - b.Item1;
            double dy = a.Item2 - b.Item2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// In a list of points, find the two points that are farthest apart
        /// </summary>
        /// <param name="points"></param>
        /// <returns>the two points that are farthest apart</returns>
        private ((int, int), (int, int)) FindFarthestPoints(List<(int, int)> points)
        {
            double maxDist = 0;
            (int, int) bestA = points[0], bestB = points[0];
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double dist = Distance(points[i], points[j]);
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        bestA = points[i];
                        bestB = points[j];
                    }
                }
            }
            return (bestA, bestB);
        }

        /// <summary>
        /// finds all intersections between line segments in the provided list
        /// </summary>
        /// <param name="lines">a list of lines</param>
        /// <returns>the list of intersection points</returns>
        private List<(double x, double y)> GetLineIntersections(List<((int x, int y), (int x, int y))> lines)
        {
            List<(double x, double y)>  intersections = new List<(double x, double y)>();

            for (int i = 0; i < lines.Count; i++)
            {
                var (p1, p2) = lines[i];
                double x1 = p1.x, y1 = p1.y;
                double x2 = p2.x, y2 = p2.y;

                for (int j = i + 1; j < lines.Count; j++)
                {
                    var (q1, q2) = lines[j];
                    double x3 = q1.x, y3 = q1.y;
                    double x4 = q2.x, y4 = q2.y;

                    double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
                    if (Math.Abs(denom) < 1e-6) continue; // lines are parallel

                    double xi = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / denom;
                    double yi = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / denom;

                    // Check if intersection lies within both segments
                    if (xi >= Math.Min(x1, x2) && xi <= Math.Max(x1, x2) &&
                        yi >= Math.Min(y1, y2) && yi <= Math.Max(y1, y2) &&
                        xi >= Math.Min(x3, x4) && xi <= Math.Max(x3, x4) &&
                        yi >= Math.Min(y3, y4) && yi <= Math.Max(y3, y4))
                    {
                        intersections.Add((xi, yi));
                    }
                }
            }

            return intersections;
        }

        /// <summary>
        /// Sum all intersection points and return their average as refined center
        /// </summary>
        /// <param name="intersections">list of intersections</param>
        /// <returns>the average location of all intersections</returns>
        private (int x, int y) RefineCenter(List<(double x, double y)> intersections)
        {
            if (intersections.Count == 0) return (0, 0);

            double sumX = 0;
            double sumY = 0;
            foreach (var (x, y) in intersections)
            {
                sumX += x;
                sumY += y;
            }

            return ((int)(sumX / intersections.Count), (int)(sumY / intersections.Count));
        }

        /// <summary>
        /// draw a bounding box
        /// </summary>
        /// <param name="inputImage">the image on which to draw the box</param>
        /// <param name="topleft">top left of the box</param>
        /// <param name="bottomright">top right of the box</param>
        /// <returns>aan image with the box drawn on it</returns>
        private Color[,] DrawBoundingBox(Color[,] inputImage, (int x, int y) topleft, (int x, int y) bottomright)
        {
            for (int y = topleft.y; y <= bottomright.y; y++)
            {
                inputImage[topleft.x, y] = Color.Blue;
                inputImage[bottomright.x, y] = Color.Blue;
            }
            for (int x = topleft.x; x <= bottomright.x; x++)
            {
                inputImage[x, topleft.y] = Color.Blue;
                inputImage[x, bottomright.y] = Color.Blue;
            }

            return inputImage;
        }


    }


}
