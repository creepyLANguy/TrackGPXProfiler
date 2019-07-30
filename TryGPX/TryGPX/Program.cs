using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
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

    private static int heightmapMaxValue_z = 255;

    private static bool verbose = true;

    static int Main(string[] args)
    {
      const string filename = "try.gpx";
      int minThresh = 0;
      int maxThresh = 0;
      int outputImageWidth = 2560;   
      bool verboseMessages = verbose; //AL. change to what the caller passes in.

      verbose = verboseMessages;

      Console.WriteLine("\r\nReading points from \'" + filename + "\'...");
      var points = ReadPointsFromFile(filename);
      if (points.Count == 0)
      {
        Console.WriteLine("Could not find file " + filename);
        return -1;
      }
      if (verbose){PrintAllPoints(points);}

      Console.WriteLine("\r\nGetting min/max elevations...");
      var minMaxElevation = GetMinMaxElevationFromPoints(points);

      Console.WriteLine("\r\nAdjusting elevations to zero base...");
      var adjustedElevations = ReduceAllPointsByMinElevation(points, minMaxElevation.Item1);

      Console.WriteLine("\r\nScaling elevations to heightmap resolution...");
      var scaledElevations = ScaleElevationsToHeightmap(adjustedElevations, minMaxElevation);

      Console.WriteLine("\r\nCalculating distances between points...");     
      var distances = GetDistancesFromPoints(points, 1000000);

      Console.WriteLine("\r\nScaling distances to output image width...");     
      var scaledDistances = ScaleDistancesToOutputImageWidth(distances, outputImageWidth);

      Console.WriteLine("\r\nDetermining new output image width from scaled distances...");
      int newOutputImageWidth = GetNewOutputImageWidthFromScaledDistances(scaledDistances);

      Console.WriteLine("\r\nPrecision lost in pixels: " + (outputImageWidth - newOutputImageWidth));

      Console.WriteLine("\r\nCalculating final points to draw onto bitmap...");
      var finalDrawingPoints = GetFinalDrawingPoints(scaledDistances, scaledElevations, minThresh, maxThresh);

      Console.WriteLine("\r\nDrawing image...");
      var image = DrawImage(finalDrawingPoints, scaledDistances, newOutputImageWidth);//, minThresh, maxThresh);

      string outputFileName = Guid.NewGuid() + ".bmp";
      Console.WriteLine("\r\nSaving image as \'" + outputFileName + "\'...");
      SaveImage(image, outputFileName);
      Console.WriteLine("\r\nSaved " + outputFileName);

      return 1;
    }

    private static void SaveImage(Bitmap image, string outputFileName)
    {
      image.Save(outputFileName, ImageFormat.Bmp);
    }

    private static Bitmap DrawImage(List<Point> points, List<int> distances, int imageWidth)//, int minThresh, int maxThresh)
    {
      var imageHeight = heightmapMaxValue_z + 1;//+ minThresh + maxThresh;

      var bmp = new Bitmap(imageWidth, imageHeight);
      var gfx = Graphics.FromImage(bmp);

      //Paint whole bitmap white. 
      gfx.FillRectangle(Brushes.White, 0, 0, imageWidth, imageHeight);     

      //AL.
      //TODO
      //Paint all points and connect with bresenham or bezier.

      return bmp;
    }

    private static List<Point> GetFinalDrawingPoints(List<int> scaledDistances, List<int> scaledElevations, int minThresh, int maxThresh)
    {
      var finalPoints = new List<Point>();

      finalPoints.Add(new Point(0, scaledElevations[0]));

      for (int i = 1; i < scaledElevations.Count; ++i)
      {
        int x = scaledDistances[i-1] + finalPoints[i -1].X;
        int y = scaledElevations[i];

        finalPoints.Add(new Point(x, y));       
      }

      if (verbose){foreach (var p in finalPoints){if (verbose) { Console.WriteLine("x : " + p.X + " , y : " + p.Y); }}}

      return finalPoints;
    }

    private static int GetNewOutputImageWidthFromScaledDistances(List<int> scaledDistances)
    {
      int sum = scaledDistances.Sum();
      if (verbose) { Console.WriteLine(sum); }
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

        if (verbose) { Console.WriteLine(a); }
      }

      return adjustedDistances;
    }

    private static List<double> GetDistancesFromPoints(List<MyPoint> points, int scale = 1000000)
    {
      var distances = new List<double>();

      for (int i = 0; i < points.Count-1; ++i)
      {
        MyPoint p1 = points[i];
        MyPoint p2 = points[i+1];

        double latDiffSq = Math.Pow(p2.lat - p1.lat, 2);
        double lonDiffSq = Math.Pow(p2.lon - p1.lon, 2);

        double d = Math.Sqrt(latDiffSq+lonDiffSq) * scale;

        distances.Add(d);

        if (verbose) { Console.WriteLine(d); }
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

        if (verbose){Console.WriteLine(d);}
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

        if (verbose){Console.WriteLine(reduced);}
      }

      return adjustedElevations;
    }

    private static List<MyPoint> ReadPointsFromFile(string filename)
    {
      var points = new List<MyPoint>();

      string[] lines = File.ReadAllLines("try.gpx");
      for (int i = 0; i < lines.Length; ++i)
      {
        var line = lines[i];

        if (line.Contains("<trkpt"))
        {
          var lonLabel = "lon=";
          var startLon = line.IndexOf(lonLabel) + 1 + lonLabel.Length;
          var endLon = line.Substring(startLon).IndexOf(" ") - 1;
          var lon = line.Substring(startLon, endLon);

          var latLabel = "lat=";
          var startLat = line.IndexOf(latLabel) + 1 + latLabel.Length;
          var endLat = line.Substring(startLat).IndexOf("\"");
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
        Console.WriteLine("Lon = " + p.lat);
        Console.WriteLine("Lat = " + p.lat);
        Console.WriteLine("Ele = " + p.ele);
        Console.WriteLine();
      }
    }

    private static Tuple<double, double> GetMinMaxElevationFromPoints(List<MyPoint> points)
    {
      var pMin = points.Aggregate((curMin, x) => (x.ele < curMin.ele ? x : curMin));
      var pMax= points.Aggregate((curMax, x) => (x.ele > curMax.ele ? x : curMax));

      if (verbose){Console.WriteLine("Min Ele: " + pMin);Console.WriteLine("Max Ele: " + pMax + "\r\n");}

      return new Tuple<double, double>(pMin.ele, pMax.ele);
    }
  }
}
