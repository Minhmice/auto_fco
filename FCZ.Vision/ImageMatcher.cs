using System.Drawing;
using OpenCvSharp;

namespace FCZ.Vision
{
    public interface IImageMatcher
    {
        MatchResult MatchTemplate(Mat frame, Mat template, Rectangle region, double threshold);
    }

    public class MatchResult
    {
        public bool Found { get; set; }
        public System.Drawing.Point Location { get; set; }
        public double Score { get; set; }
    }

    public class ImageMatcher : IImageMatcher
    {
        public MatchResult MatchTemplate(Mat frame, Mat template, Rectangle region, double threshold)
        {
            var result = new MatchResult { Found = false };

            if (frame.Empty() || template.Empty())
            {
                return result;
            }

            try
            {
                // Crop frame according to region
                var roi = new Rect(region.X, region.Y, region.Width, region.Height);
                if (roi.X + roi.Width > frame.Width || roi.Y + roi.Height > frame.Height)
                {
                    return result;
                }

                using var croppedFrame = new Mat(frame, roi);

                // Perform template matching
                using var resultMat = new Mat();
                Cv2.MatchTemplate(croppedFrame, template, resultMat, TemplateMatchModes.CCoeffNormed);

                // Find best match
                Cv2.MinMaxLoc(resultMat, out double minVal, out double maxVal, out OpenCvSharp.Point minLoc, out OpenCvSharp.Point maxLoc);

                // Check if match is above threshold
                if (maxVal >= threshold)
                {
                    result.Found = true;
                    result.Score = maxVal;
                    // Convert location back to original frame coordinates
                    result.Location = new System.Drawing.Point(maxLoc.X + region.X, maxLoc.Y + region.Y);
                }
            }
            catch
            {
                // Return default result (Found = false)
            }

            return result;
        }
    }
}

