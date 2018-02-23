using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NetTopologySuite.IO;
using System.Collections;
using System.Drawing;
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
using System.Threading.Tasks;
using System.Diagnostics;

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
            /*
            ArrayList polys = new ArrayList();
            ArrayList squares = new ArrayList();
            ArrayList overlap = new ArrayList();*/
            List<GeoAPI.Geometries.IGeometry> polys = new List<GeoAPI.Geometries.IGeometry>();
            List<GeoAPI.Geometries.IGeometry> squares = new List<GeoAPI.Geometries.IGeometry>();
            List<GeoAPI.Geometries.IGeometry> overlap = new List<GeoAPI.Geometries.IGeometry>();
            List<Object> infoTable = new List<Object>();
            
            //////////////
            string gridfile = @"M:\DotSpatTopology\tests\NLDAS_Grid_Reference.shp";//"";
            string gridproj = @"M:\DotSpatTopology\tests\NLDAS_Grid_Reference.prj";
            //////////////

            //This block is for getting and setting shapefiles for NLDAS Grid
            /**
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
            client.Dispose();**/


            if (polyfile.EndsWith(".geojson"))
            {
                string jsonfile = System.IO.File.ReadAllText(polyfile);
                var readera = new NetTopologySuite.IO.GeoJsonReader();
                NetTopologySuite.Features.FeatureCollection result = readera.Read<NetTopologySuite.Features.FeatureCollection>(jsonfile);
                for (int i = 0; i < result.Count; i++)
                {
                    polys.Add(result[i].Geometry);
                    polygonArea += result[i].Geometry.Area;
                }
            }
            else
            {
                string ending = polyfile + ".zip";
                Guid gid = Guid.NewGuid();
                string directory = @"M:\\TransientStorage\\" + gid.ToString() + "\\";
                WebClient client = new WebClient();
                DirectoryInfo di = Directory.CreateDirectory(directory);
                client.DownloadFile("ftp://newftp.epa.gov/exposure/NHDV1/HUC12_Boundries/" + ending, directory + ending);

                string projfile = "";


                ZipFile.ExtractToDirectory(directory + ending, directory + polyfile);
                string unzippedLocation = (directory + polyfile + "\\" + polyfile); //+ "\\NHDPlus" + polyfile + "\\Drainage");
                foreach (string file in Directory.GetFiles(unzippedLocation))
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

                //This block is for setting projection parameters of input shapefile and projecting it to NLDAS grid
                //Reprojecting of coordinates is not needed for NHDPlus V2

                string line = System.IO.File.ReadAllText(projfile);
                string[] projParams = { "PARAMETER", @"PARAMETER[""latitude_Of_origin"",0]," };//@"PARAMETER[""false_easting"",0],", @"PARAMETER[""false_northing"",0],", @"PARAMETER[""central_meridian"",0],", @"PARAMETER[""standard_parallel_1"",0],", @"PARAMETER[""standard_parallel_2"",0],", @"PARAMETER[""latitude_Of_origin"",0]," };
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
                    //Reprojection not needed for NHDPLUSV2
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
                reader.Dispose();
            }
                 
            ShapefileDataReader reader2 = new ShapefileDataReader(gridfile, NetTopologySuite.Geometries.GeometryFactory.Default);
            while (reader2.Read())
            {
                squares.Add(reader2.Geometry);
                gridArea += reader2.Geometry.Area;
            }
            
            reader2.Dispose();

            Stopwatch timer = new Stopwatch();
            timer.Start();
            
            //Creating intersections ahead of time to make selections faster ---Non Parallel    36-44 milliseconds
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

            /*
            object addLock = new object();//Creating intersections ahead of time to make selections faster ---Parallel    45-55 milliseconds, slower with lock
            Parallel.ForEach(squares, (GeoAPI.Geometries.IGeometry s) =>
                {                    
                    Parallel.ForEach(polys, (GeoAPI.Geometries.IGeometry p) =>
                    {
                        if (p.Intersects(s) && !overlap.Contains(s))
                        {
                            overlap.Add(s);
                        }
                    });
                });*/

            timer.Stop();
            TimeSpan ts = timer.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Debug.WriteLine("RunTime " + elapsedTime);
            
            double percent = (polygonArea / gridArea) * 100;
            
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
            /*
            System.IO.DirectoryInfo del = new DirectoryInfo(directory);

            foreach (FileInfo file in del.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in del.GetDirectories())
            {
                dir.Delete(true);
            }*/
            /////
            infoTable.Add(new List<Object>() { elapsedTime, elapsedTime, elapsedTime, elapsedTime });
            //////
            return infoTable;
        }
    }
}