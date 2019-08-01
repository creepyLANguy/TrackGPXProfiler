using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

namespace TryGPX
{
  class Program
  {
    private const int HeightmapMaxResolutionZ = 255;

    private static bool _echoVerbose = false;

    private static readonly string[] outputStrings =
    {
      "Executing with following parameters:",
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
    private static int _echoIndex = 0;
    private static string _log = "";

    ///////////////////////////////////////////////////////////////////////////

    private struct Parameters
    {
      public string inputFilename { get; set; }
      public string outputFilename { get; set; }
      public int groundThresh { get; set; }
      public int skyThresh { get; set; }
      public int outputImageWidth { get; set; }
      public bool verbose { get; set; }
      public bool openImageWhenComplete { get; set; }
      public bool openLogWhenComplete { get; set; }
      public bool drawAsCurve { get; set; }
    }

    ///////////////////////////////////////////////////////////////////////////

    struct MyPoint
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
    
    ///////////////////////////////////////////////////////////////////////////
    
    static int Main(string[] args)
    {
      Parameters p = new Parameters
      {
        inputFilename = "",
        outputFilename = "",
        groundThresh = 0,
        skyThresh = 0,
        outputImageWidth = 2560,
        verbose = false,
        openImageWhenComplete = true,
        openLogWhenComplete = true,
        drawAsCurve = false
      };

      try
      {
        p = GetParametersFromArgs(args, p);
      }
      catch (Exception e)
      {
        Console.WriteLine("Error processing args!");
        Console.WriteLine(e.ToString());
        return -1;
      }

      return Run(p);
    }

    private static Parameters GetParametersFromArgs(string[] args, Parameters p)
    {
      Console.WriteLine("cmdline args:");
      foreach (var s in args){ Console.WriteLine(s); }
      
      var l = args.ToList();

      object pObj = p;

      PropertyInfo[] propertyInfo = pObj.GetType().GetProperties();
      foreach (var pi in propertyInfo)
      {
        var label = pi.Name;
        var index = l.IndexOf(label);

        if (index < 0) { continue; }

        string val = l[index + 1];
        var type = pi.PropertyType;
        var converted = Convert.ChangeType(val, type);
        pi.SetValue(pObj, converted);
      }

      Parameters pNew =(Parameters)pObj;

      if (pNew.inputFilename.Length == 0)
      {
        throw new Exception("ERROR - bad inputFilename passed as arg.");
      }

      //In case the caller assumes outputs will be named the same as inputs.
      if (pNew.outputFilename.Length == 0)
      {
        pNew.outputFilename = pNew.inputFilename.Substring(0, pNew.inputFilename.IndexOf(".gpx"));
      }

      return pNew;
    }

    private static int Run(Parameters p)
    {
      Echo();
      PrintAllParameters(p);

      _echoVerbose = p.verbose;

      Echo("\r\nReading points from \'" + p.inputFilename + "\'...", true);
      List<MyPoint> points;
      try
      {
        points = ReadPointsFromFile(p.inputFilename);
        PrintAllPoints(points);
      }
      catch
      {
        Echo("\r\nError opening file " + p.inputFilename, true);
        File.WriteAllText(p.outputFilename + ".log", _log);
        System.Diagnostics.Process.Start(p.outputFilename + ".log");
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
      var scaledDistances = ScaleDistancesToOutputImageWidth(distances, p.outputImageWidth);

      Echo();
      int newOutputImageWidth = GetNewOutputImageWidthFromScaledDistances(scaledDistances);

      Echo("\r\n\r\nPrecision lost in pixels: " + (p.outputImageWidth - newOutputImageWidth), true);

      Echo();
      var finalDrawingPoints = GetFinalDrawingPoints(scaledDistances, scaledElevations, p.skyThresh);

      Echo();
      var image = DrawImage(finalDrawingPoints, newOutputImageWidth, p.groundThresh, p.skyThresh, p.drawAsCurve);

      Echo();
      var markedImage = MarkPointsWithVerticalLine(finalDrawingPoints, image);

      string outputFileName = p.outputFilename + ".bmp";
      Echo("\r\n\r\nSaving image as \'" + outputFileName + "\'...", true);
      SaveImage(markedImage, outputFileName);
      Echo("\r\n\r\nSaved " + outputFileName, true);

      File.WriteAllText(p.outputFilename + ".log", _log);

      if (p.openImageWhenComplete) { System.Diagnostics.Process.Start(outputFileName); }
      if (p.openLogWhenComplete) { System.Diagnostics.Process.Start(p.outputFilename + ".log"); }

      return 1;
    }

    private static void PrintAllParameters(Parameters p)
    {
      PropertyInfo[] propertyInfo = p.GetType().GetProperties();
      foreach (var pi in propertyInfo)
      {
        Echo(pi.ToString() + " = " + pi.GetValue(p) + "\r\n", true);
      }
    }

    private static void SaveImage(Bitmap image, string outputFileName)
    {
      image.Save(outputFileName, ImageFormat.Bmp);
    }

    private static Bitmap MarkPointsWithVerticalLine(Point[] points, Bitmap bmp)
    {
      var shadedBmp = new Bitmap(bmp);

      var gfx = Graphics.FromImage(shadedBmp);

      var imageMaxY = shadedBmp.Height - 1;

      var pen = Pens.Red;
      foreach (var p in points)
      {
        gfx.DrawLine(pen, new Point(p.X, p.Y+1), new Point(p.X, imageMaxY));
      }
      
      return shadedBmp;
    }

    private static Bitmap DrawImage(Point[] points, int imageWidth, int minThresh, int maxThresh, bool drawAsCurve = false)
    {
      var imageHeight = HeightmapMaxResolutionZ + 1 + minThresh + maxThresh;

      var bmp = new Bitmap(imageWidth, imageHeight);
      var gfx = Graphics.FromImage(bmp);

      //Paint whole bitmap white else it defaults to black.
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

        gfx.FillPolygon(fillBrush, regionPoints);
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


    private static Point[] GetFinalDrawingPoints(List<int> scaledDistances, List<int> scaledElevations, int maxThresh)
    {
      var finalPoints = new List<Point>();

      finalPoints.Add(new Point(0, HeightmapMaxResolutionZ - scaledElevations[0]));

      for (int i = 1; i < scaledElevations.Count; ++i)
      {
        int x = scaledDistances[i - 1] + finalPoints[i - 1].X;
        int y = HeightmapMaxResolutionZ - scaledElevations[i] + maxThresh;

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
        var p1 = points[i];
        var p2 = points[i + 1];

        var latDiffSq = Math.Pow(p2.lat - p1.lat, 2);
        var lonDiffSq = Math.Pow(p2.lon - p1.lon, 2);

        var d = Math.Sqrt(latDiffSq + lonDiffSq) * scale;

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
        double d = (e / maxAchievableElevation) * HeightmapMaxResolutionZ;

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
      Echo("\r\n" + outputStrings[_echoIndex] + "...", true);
      ++_echoIndex;
    }

    private static void Echo(string s, bool forceShow = false)
    {
      if (_echoVerbose || forceShow)
      {
        Console.WriteLine(s);
      }

      _log += s;    
    }

    private static void Echo(double i)
    {
      Echo("\r\n" + i);
    }
  }
}
