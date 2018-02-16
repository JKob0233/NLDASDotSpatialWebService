using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NetTopologySuite.IO;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Web.Services;
using System.Web.UI.WebControls;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using GeoAPI.CoordinateSystems;
using ProjNet.Converters.WellKnownText;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using GeoAPI.CoordinateSystems.Transformations;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;

namespace PercentArea.Models
{
    public class GridCell
    {
        public GeoAPI.Geometries.Coordinate CellCentroidCoordinates { get; set; }
        public double AreaOfCell { get; set; }
        public double AreaOfPolygon { get; set; }
        public double PercentAreaCoverage { get; set; }
        
        public List<Object> CalculateDataTable(string polyfile)
        {
            double squareArea = 0;//0.015625;
            double gridArea = 0;
            double polygonArea = 0;
            ArrayList polys = new ArrayList();
            ArrayList squares = new ArrayList();
            ArrayList overlap = new ArrayList();
            List<Object> infoTable = new List<Object>();
            
            string gridfile = "";

            
            string ending = polyfile + ".zip";
            WebClient client = new WebClient();
            client.DownloadFile("ftp://newftp.epa.gov/exposure/NHDV1/HUC12_Boundries/" + ending, @"M:\\TransientStorage\\" + ending);

            string projfile = "";
            ZipFile.ExtractToDirectory(@"M:\\TransientStorage\\" + ending, @"M:\\TransientStorage\\" + polyfile);
            string unzippedLocation = (@"M:\\TransientStorage\\" + polyfile + "\\" + polyfile); //+ "\\NHDPlus" + polyfile + "\\Drainage");
            foreach(string file in Directory.GetFiles(unzippedLocation))
            {
                if (Path.GetExtension(file).Equals(".shp"))
                {
                    polyfile = file;
                }
                else if (Path.GetExtension(file).Equals(".prj"))
                {
                    projfile = file;
                }
            }

            string gridproj = "";
            client.DownloadFile("https://ldas.gsfc.nasa.gov/nldas/gis/NLDAS_Grid_Reference.zip", @"M:\\TransientStorage\\NLDAS.zip");
            ZipFile.ExtractToDirectory(@"M:\\TransientStorage\\NLDAS.zip", @"M:\\TransientStorage\\NLDAS");
            unzippedLocation = (@"M:\\TransientStorage\\NLDAS");
            foreach (string file in Directory.GetFiles(unzippedLocation))
            {
                if (Path.GetExtension(file).Equals(".shp"))
                {
                    gridfile = file;
                }
                else if (Path.GetExtension(file).Equals(".prj"))
                {
                    gridproj = file;
                }
            }
            client.Dispose();
            

            
            string line = System.IO.File.ReadAllText(projfile);

            string[] projParams = { "PARAMETER", @"PARAMETER[""false_easting"",0],", @"PARAMETER[""false_northing"",0],", @"PARAMETER[""central_meridian"",0],", @"PARAMETER[""standard_parallel_1"",0],", @"PARAMETER[""standard_parallel_2"",0],", @"PARAMETER[""latitude_Of_origin"",0]," };
            int ptr = 0;
            foreach (string x in projParams)
            {
                if (line.Contains(x))
                {
                    ptr = line.IndexOf(x);
                }
                else if (!line.Contains(x) && !x.Equals("PARAMETER"))
                {
                    line = line.Insert(ptr, x);
                }
            }
            string line2 = System.IO.File.ReadAllText(gridproj);


            
            IProjectedCoordinateSystem pcs = CoordinateSystemWktReader.Parse(line) as IProjectedCoordinateSystem;
            IGeographicCoordinateSystem gcs = GeographicCoordinateSystem.WGS84 as IGeographicCoordinateSystem;

            CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();
            ICoordinateTransformation transformTo = ctfac.CreateFromCoordinateSystems(pcs, gcs);
            IMathTransform inverseTransformTo = transformTo.MathTransform;

            //Read geometries from both shapefiles and store in array lists
            //As well as calculate shapefile areas ahead of time

            
            ShapefileDataReader reader = new ShapefileDataReader(polyfile, NetTopologySuite.Geometries.GeometryFactory.Default);
            while (reader.Read())
            {
                CoordinateList cordlist = new CoordinateList();
                foreach (Coordinate coord in reader.Geometry.Coordinates)
                {
                    double[] newCoord = {coord.X, coord.Y};
                    newCoord = inverseTransformTo.Transform(newCoord);
                    Coordinate newpt = new Coordinate(newCoord[0], newCoord[1]);
                    cordlist.Add(newpt);
                }
                Coordinate[] listofpts = cordlist.ToCoordinateArray();
                IGeometryFactory geoFactory = new NetTopologySuite.Geometries.GeometryFactory();
                NetTopologySuite.Geometries.LinearRing linear = (NetTopologySuite.Geometries.LinearRing)new GeometryFactory().CreateLinearRing(listofpts);
                Polygon projPoly = new Polygon(linear, null, geoFactory);
              
                polys.Add(projPoly);
                polygonArea += projPoly.Area;
                //polys.Add(reader.Geometry);
                //polygonArea += reader.Geometry.Area;
            }
            
            ShapefileDataReader reader2 = new ShapefileDataReader(gridfile, NetTopologySuite.Geometries.GeometryFactory.Default);
            while (reader2.Read())
            {
                squares.Add(reader2.Geometry);
                gridArea += reader2.Geometry.Area;
            }
            reader.Dispose();
            reader2.Dispose();

            squares.TrimToSize();
            polys.TrimToSize();
            
            //Creating intersections ahead of time to make selections faster
            foreach (GeoAPI.Geometries.IGeometry s in squares)
            {
                foreach (GeoAPI.Geometries.IGeometry p in polys)
                {
                    if (p.Intersects(s) && !overlap.Contains(s))
                    {
                        overlap.Add(s);
                    }
                }
            }
            overlap.TrimToSize();
            ///

            //Label1.Text = "Area of NLDAS Grid: " + gridArea.ToString();
            //Label2.Text = "\r\nArea of polygon: " + polygonArea.ToString();
            double percent = (polygonArea / gridArea) * 100;
            //Label3.Text = "\r\nPolygon covers " + percent.ToString() + "% of NLDAS Grid.";



            foreach (GeoAPI.Geometries.IGeometry s in overlap)
            {
                double interArea = 0.0;
                squareArea = s.Area;
                foreach (GeoAPI.Geometries.IGeometry p in polys)
                {
                    if (p.Intersects(s))
                    {
                        GeoAPI.Geometries.IGeometry intersection = p.Intersection(s);
                        interArea += intersection.Area;
                    }
                }
                double percent2 = (interArea / squareArea) * 100;
                List<Object> item = new List<Object>() { s.Centroid.ToString(), squareArea, interArea, percent2 };
                infoTable.Add(item);
            }
            
            System.IO.DirectoryInfo di = new DirectoryInfo(@"M:\\TransientStorage\\");

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            
            return infoTable;
        }
    }
}