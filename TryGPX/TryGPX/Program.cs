using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TryGPX
{
  internal class MyPoint
  {
    public MyPoint(double lon, double lat, double ele)
    {
      this.lon = lon;
      this.lat = lat;
      this.ele = ele;
    }

    public double lat;
    public double lon;
    public double ele;
  }

  class Program
  {
    private static readonly int heightmapMaxValue_z = 255;

    private static bool verbose = false;

    private static readonly string[] outputStrings =
    {
      "\r\nGetting min/max elevations",
      "\r\nAdjusting elevations to zero base",
      "\r\nScaling elevations to heightmap resolution",
      "\r\nCalculating distances between points",
      "\r\nScaling distances to output image width",
      "\r\nDetermining new output image width from scaled distances",
      "\r\nCalculating final points to draw onto bitmap",
      "\r\nDrawing elevation curve",
      "\r\nMarking known gpx points on curve",
    };
    private static int echoIndex = 0;
    private static string log = "";

    static int Main(string[] args)
    {
      string filename = "szk.gpx";
      int minThresh = 0;
      int maxThresh = 0;
      int outputImageWidth = 2560;
      verbose = false; //AL. change to what the caller passes in.
      bool openImageWhenComplete = true;
      bool openLogWhenComplete= true;
      bool drawAsCurve = false;

      var guid = Guid.NewGuid();

      Echo("Reading points from \'" + filename + "\'...", true);
      List<MyPoint> points;
      try
      {
        points = ReadPointsFromFile(filename);
        PrintAllPoints(points);
      }
      catch
      {
        Echo("\r\nError opening file " + filename, true);
        File.WriteAllText(guid + ".log", log);
        System.Diagnostics.Process.Start(guid + ".log");
        return -1;
      }      
      
      Echo();
      var minMaxElevation = GetMinMaxElevationFromPoints(points);

      Echo();
      var adjustedElevations = ReduceAllPointsByMinElevation(points, minMaxElevation.Item1);

      Echo();
      var scaledElevations = ScaleElevationsToHeightmap(adjustedElevations, minMaxElevation);

      Echo();
      var distances = GetDistancesFromPoints(points);

      Echo();
      var scaledDistances = ScaleDistancesToOutputImageWidth(distances, outputImageWidth);

      Echo();
      int newOutputImageWidth = GetNewOutputImageWidthFromScaledDistances(scaledDistances);

      Echo("\r\n\r\nPrecision lost in pixels: " + (outputImageWidth - newOutputImageWidth), true);

      Echo();
      var finalDrawingPoints = GetFinalDrawingPoints(scaledDistances, scaledElevations, minThresh, maxThresh);

      Echo();
      var image = DrawImage(finalDrawingPoints, scaledDistances, newOutputImageWidth, drawAsCurve);//, minThresh, maxThresh);

      Echo();
      var markedImage = MarkPointsWithVerticalLine(finalDrawingPoints, image);

      Echo("\r\n");
      string outputFileName = guid + ".bmp";
      Echo("\r\n\r\nSaving image as \'" + outputFileName + "\'...", true);
      SaveImage(markedImage, outputFileName);
      Echo("\r\n\r\nSaved " + outputFileName, true);

      File.WriteAllText(guid + ".log", log);

      if (openImageWhenComplete){System.Diagnostics.Process.Start(outputFileName);}
      if (openLogWhenComplete){System.Diagnostics.Process.Start(guid + ".log");}

      return 1;
    }

    private static void SaveImage(Bitmap image, string outputFileName)
    {
      image.Save(outputFileName, ImageFormat.Bmp);
    }

    private static Bitmap MarkPointsWithVerticalLine(Point[] points, Bitmap bmp, Pen pen = null) //, int minThresh, int maxThresh)
    {
      var shadedBmp = new Bitmap(bmp);

      var gfx = Graphics.FromImage(shadedBmp);

      var imageMaxY = shadedBmp.Height - 1;

      pen = pen == null ? Pens.Red : pen; //ror I'm bored...
      //if (pen == null){pen = Pens.Red;}

      foreach (var p in points)
      {
        gfx.DrawLine(pen, new Point(p.X, p.Y+1), new Point(p.X, imageMaxY));
      }
      
      return shadedBmp;
    }

    private static Bitmap DrawImage(Point[] points, List<int> distances, int imageWidth, bool drawAsCurve)//, int minThresh, int maxThresh)
    {
      var imageHeight = heightmapMaxValue_z + 1;//+ minThresh + maxThresh;

      var bmp = new Bitmap(imageWidth, imageHeight);
      var gfx = Graphics.FromImage(bmp);

      //Paint whole bitmap white. 
      gfx.FillRectangle(Brushes.White, 0, 0, imageWidth, imageHeight);

      var fillBrush = new SolidBrush(Color.LightBlue);
      for (int i = 0; i < points.Length - 1; ++i)
      {
        Point[] regionPoints =
        {
          points[i],
          points[i + 1],
          new Point(points[i + 1].X, imageHeight),
          new Point(points[i].X, imageHeight)
        };

        gfx.FillPolygon(fillBrush, regionPoints, FillMode.Alternate);
      }

      if (drawAsCurve)
      {
        gfx.DrawCurve(Pens.Black, points); //Avoid - can lead to big errors.
      }
      else
      {
        gfx.DrawLines(Pens.Black, points);
      }
      
      return bmp;
    }


    private static Point[] GetFinalDrawingPoints(List<int> scaledDistances, List<int> scaledElevations, int minThresh, int maxThresh)
    {
      var finalPoints = new List<Point>();

      finalPoints.Add(new Point(0, heightmapMaxValue_z - scaledElevations[0]));

      for (int i = 1; i < scaledElevations.Count; ++i)
      {
        int x = scaledDistances[i - 1] + finalPoints[i - 1].X;
        int y = heightmapMaxValue_z - scaledElevations[i];

        finalPoints.Add(new Point(x, y));
      }

      foreach (var p in finalPoints) { Echo("\r\nx : " + p.X + " , y : " + p.Y); }

      return finalPoints.ToArray();
    }

    private static int GetNewOutputImageWidthFromScaledDistances(List<int> scaledDistances)
    {
      int sum = scaledDistances.Sum();
      Echo(sum);
      return sum;
    }

    private static List<int> ScaleDistancesToOutputImageWidth(List<double> distances, int outputImageWidth)
    {
      double sumDistances = distances.Sum();

      var adjustedDistances = new List<int>();

      foreach (var d in distances)
      {
        var a = (d / sumDistances) * outputImageWidth;

        adjustedDistances.Add((int)a);

        Echo(a);
      }

      return adjustedDistances;
    }

    private static List<double> GetDistancesFromPoints(List<MyPoint> points, double scale = 1000000)
    {
      var distances = new List<double>();

      scale = scale > 0 ? scale : scale + 1;

      for (int i = 0; i < points.Count - 1; ++i)
      {
        MyPoint p1 = points[i];
        MyPoint p2 = points[i + 1];

        double latDiffSq = Math.Pow(p2.lat - p1.lat, 2);
        double lonDiffSq = Math.Pow(p2.lon - p1.lon, 2);

        double d = Math.Sqrt(latDiffSq + lonDiffSq) * scale;

        distances.Add(d);

        Echo(d);
      }

      return distances;
    }

    private static List<int> ScaleElevationsToHeightmap(List<double> adjustedElevations, Tuple<double, double> minMaxElevation)
    {
      var scaledElevations = new List<int>();

      double maxAchievableElevation = minMaxElevation.Item2 - minMaxElevation.Item1;

      foreach (var e in adjustedElevations)
      {
        double d = (e / maxAchievableElevation) * heightmapMaxValue_z;

        scaledElevations.Add((int)d);

        Echo(d);
      }

      return scaledElevations;
    }

    private static List<double> ReduceAllPointsByMinElevation(List<MyPoint> points, double min)
    {
      var adjustedElevations = new List<double>();

      foreach (var p in points)
      {
        var reduced = p.ele - min;

        adjustedElevations.Add(reduced);

        Echo(reduced);
      }


      return adjustedElevations;
    }

    private static List<MyPoint> ReadPointsFromFile(string filename)
    {
      var points = new List<MyPoint>();

      string[] lines = File.ReadAllLines(filename);
      for (int i = 0; i < lines.Length; ++i)
      {
        var line = lines[i];

        //AL.
        //Console.WriteLine(i + line);
        //

        if (line.Contains("<trkpt"))
        {
          var lonLabel = "lon=\"";
          var startLon = line.IndexOf(lonLabel) + lonLabel.Length;
          var lonSplice = line.Substring(startLon);
          var endLon = lonSplice.IndexOf("\"");
          var lon = line.Substring(startLon, endLon);

          var latLabel = "lat=\"";
          var startLat = line.IndexOf(latLabel) + latLabel.Length;
          var latSplice = line.Substring(startLat);
          var endLat = latSplice.IndexOf("\"");
          var lat = line.Substring(startLat, endLat);

          ++i;
          var ele = lines[i];
          var eleLabel = "<ele>";
          ele = ele.Substring(ele.IndexOf("<ele>") + eleLabel.Length);
          ele = ele.Substring(0, ele.IndexOf('<'));

          points.Add(new MyPoint(double.Parse(lon), double.Parse(lat), double.Parse(ele)));
        }
      }

      return points;
    }

    private static void PrintAllPoints(List<MyPoint> points)
    {
      foreach (var p in points)
      {
        Echo("\r\n");
        Echo("Lon = " + p.lat + " | ");
        Echo("Lat = " + p.lat + " | ");
        Echo("Ele = " + p.ele);
      }
    }
    
    private static Tuple<double, double> GetMinMaxElevationFromPoints(List<MyPoint> points)
    {
      var pMin = points.Aggregate((curMin, x) => (x.ele < curMin.ele ? x : curMin));
      var pMax = points.Aggregate((curMax, x) => (x.ele > curMax.ele ? x : curMax));

      Echo("\r\nMin Ele: " + pMin); Echo("\r\nMax Ele: " + pMax);
      
      return new Tuple<double, double>(pMin.ele, pMax.ele);
    }

    private static void Echo()
    {
      Echo("\r\n" + outputStrings[echoIndex] + "...", true);
      ++echoIndex;
    }

    private static void Echo(string s, bool forceShow = false)
    {
      log += s;
      if (verbose || forceShow){Console.WriteLine(s);}
    }

    private static void Echo(double i)
    {
      Echo("\r\n" + i);
    }
  }
}
