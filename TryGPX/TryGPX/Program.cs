using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TryGPX
{
  internal class Point
  {
    public Point(double lon, double lat, double ele)
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

    private static bool verbose = false;

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

      Console.WriteLine("\r\nDrawing image...");
      var image = DrawImage(scaledElevations, scaledDistances, newOutputImageWidth, minThresh, maxThresh);

      string outputFileName = DateTime.Now + ".bmp";
      Console.WriteLine("\r\nSaving image as \'" + outputFileName + "\'...");
      SaveImage(image, outputFileName);

      return 1;
    }

    private static void SaveImage(object image, string outputFileName)
    {
      throw new NotImplementedException();
    }

    private static object DrawImage(List<double> scaledElevations, List<int> distances, int imageWidth, int minThresh, int maxThresh)
    {
      //AL.
      throw new NotImplementedException();
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

    private static List<double> GetDistancesFromPoints(List<Point> points, int scale = 1000000)
    {
      var distances = new List<double>();

      for (int i = 0; i < points.Count-1; i+=2)
      {
        Point p1 = points[i];
        Point p2 = points[i+1];

        double latDiffSq = Math.Pow(p2.lat - p1.lat, 2);
        double lonDiffSq = Math.Pow(p2.lon - p1.lon, 2);

        double d = Math.Sqrt(latDiffSq+lonDiffSq) * scale;

        distances.Add(d);

        if (verbose) { Console.WriteLine(d); }
      }

      return distances;
    }

    private static List<double> ScaleElevationsToHeightmap(List<double> adjustedElevations, Tuple<double, double> minMaxElevation)
    {
      var scaledElevations = new List<double>();

      double maxAchievableElevation = minMaxElevation.Item2 - minMaxElevation.Item1;

      foreach (var e in adjustedElevations)
      {
        double d = (e / maxAchievableElevation) * heightmapMaxValue_z;

        scaledElevations.Add(d);

        if (verbose){Console.WriteLine(d);}
      }

      return scaledElevations;
    }

    private static List<double> ReduceAllPointsByMinElevation(List<Point> points, double min)
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

    private static List<Point> ReadPointsFromFile(string filename)
    {
      var points = new List<Point>();

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

          points.Add(new Point(double.Parse(lon), double.Parse(lat), double.Parse(ele)));
        }
      }

      return points;
    }

    private static void PrintAllPoints(List<Point> points)
    {
      foreach (var p in points)
      {
        Console.WriteLine("Lon = " + p.lat);
        Console.WriteLine("Lat = " + p.lat);
        Console.WriteLine("Ele = " + p.ele);
        Console.WriteLine();
      }
    }

    private static Tuple<double, double> GetMinMaxElevationFromPoints(List<Point> points)
    {
      var pMin = points.Aggregate((curMin, x) => (x.ele < curMin.ele ? x : curMin));
      var pMax= points.Aggregate((curMax, x) => (x.ele > curMax.ele ? x : curMax));

      if (verbose){Console.WriteLine("Min Ele: " + pMin);Console.WriteLine("Max Ele: " + pMax + "\r\n");}

      return new Tuple<double, double>(pMin.ele, pMax.ele);
    }
  }
}
