using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TryGPX
{
  internal class Point
  {
    public Point(decimal lon, decimal lat, decimal ele)
    {
      this.lon = lon;
      this.lat = lat;
      this.ele = ele;
    }

    public decimal lat;
    public decimal lon;
    public decimal ele;
  }

  class Program
  {

    private static int heightmapMaxValue_z = 255;

    private static bool verbose = true;

    static int Main(string[] args)
    {
      const string filename = "try.gpx";
      var minThresh = 0;
      var maxThresh = 0;
      int outputWidth = 1000;   
      bool verboseMessages = verbose; //AL. change to what the caller passes in.

      verbose = verboseMessages;

      var points = ReadPointsFromFile(filename);
      if (points.Count == 0)
      {
        Console.WriteLine("Could not find file " + filename);
        return -1;
      }
      if (verbose){PrintAllPoints(points);}

      Console.WriteLine("Getting min/max elevations... \r\n");
      var minMaxElevation = GetMinMaxElevationFromPoints(points);

      Console.WriteLine("Adjusting elevations to zero base...  \r\n");
      var adjustedElevations = ReduceAllPointsByMinElevation(points, minMaxElevation.Item1);

      Console.WriteLine("Scaling elevations to heightmap resolution...  \r\n");
      var scaledElevations = ScaleElevationsToHeightmap(adjustedElevations, minMaxElevation);

      Console.WriteLine("Calculating distances between points... \r\n");
      //AL.
      //var distances = GetDistancesFromPoints(points);

      //AL.
      //var image = PadImage(DrawImage(scaledElevations, distances), minThresh, maxThresh); 

      return 1;
    }

    private static object PadImage(object drawImage, int minThresh, int maxThresh)
    {
      //AL.
      throw new NotImplementedException();
    }

    private static object DrawImage(List<decimal> scaledElevations, List<decimal> distances)
    {
      //AL.
      throw new NotImplementedException();
    }

    private static List<decimal> GetDistancesFromPoints(List<Point> points)
    {
      var distances = new List<decimal>();

      //AL.
      throw new NotImplementedException();

      return distances;
    }

    private static List<decimal> ScaleElevationsToHeightmap(List<decimal> adjustedElevations, Tuple<decimal, decimal> minMaxElevation)
    {
      var scaledElevations = new List<decimal>();

      decimal maxAchievableElevation = minMaxElevation.Item2 - minMaxElevation.Item1;

      foreach (var e in adjustedElevations)
      {
        decimal d = (e / maxAchievableElevation) * heightmapMaxValue_z;

        scaledElevations.Add(d);

        if (verbose){Console.WriteLine(d);}
      }

      return scaledElevations;
    }

    private static List<decimal> ReduceAllPointsByMinElevation(List<Point> points, decimal min)
    {
      var adjustedElevations = new List<decimal>();

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
      List<Point> points = new List<Point>();

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

          points.Add(new Point(Decimal.Parse(lon), Decimal.Parse(lat), Decimal.Parse(ele)));
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

    private static Tuple<decimal, decimal> GetMinMaxElevationFromPoints(List<Point> points)
    {
      var pMin = points.Aggregate((curMin, x) => (x.ele < curMin.ele ? x : curMin));
      var pMax= points.Aggregate((curMax, x) => (x.ele > curMax.ele ? x : curMax));

      if (verbose){Console.WriteLine("Min Ele: " + pMin);Console.WriteLine("Max Ele: " + pMax + "\r\n");}

      return new Tuple<decimal, decimal>(pMin.ele, pMax.ele);
    }
  }
}
