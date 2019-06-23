using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace MinecologyProAppModule
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static Module1 Current
        {
            get
            {
                return _this ?? (_this = (Module1)FrameworkApplication.FindModule("MinecologyProAppModule_Module"));
            }
        }

        #region Overrides
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //TODO - add your business logic
            //return false to ~cancel~ Application close
            return true;
        }

        #endregion Overrides

        /// <summary>
        /// The methods retrieves the outer ring(s) of the input polygon.
        /// </summary>
        /// <param name="inputPolygon">Input Polygon.</param>
        /// <returns>The outer most (exterior, clockwise) ring(s) of the polygon. If the input is null or empty, a null pointer is returned.</returns>
        public Task<Polygon> GetOutermostRingsAsync(Polygon inputPolygon)
        {
            return QueuedTask.Run(() => GetOutermostRings(inputPolygon));
        }

        /// <summary>
        /// The methods retrieves the outer ring(s) of the input polygon.
        /// This method must be called on the MCT. Use QueuedTask.Run.
        /// </summary>
        /// <param name="inputPolygon">Input Polygon.</param>
        /// <returns>The outer most (exterior, clockwise) ring(s) of the polygon. If the input is null or empty, a null pointer is returned.</returns>
        /// <remarks>This method must be called on the MCT. Use QueuedTask.Run.</remarks>
        public Polygon GetOutermostRings(Polygon inputPolygon)
        {
            if (inputPolygon == null || inputPolygon.IsEmpty)
                return null;

            PolygonBuilder outerRings = new PolygonBuilder();
            List<Polygon> internalRings = new List<Polygon>();

            // explode the parts of the polygon into a list of individual geometries
            var parts = MultipartToSinglePart(inputPolygon);

            // get an enumeration of clockwise geometries (area > 0) ordered by the area
            var clockwiseParts = parts.Where(geom => ((Polygon)geom).Area > 0).OrderByDescending(geom => ((Polygon)geom).Area);

            // for each of the exterior rings
            foreach (var part in clockwiseParts)
            {
                // add the first (the largest) ring into the internal collection
                if (internalRings.Count == 0)
                    internalRings.Add(part as Polygon);

                // use flag to indicate if current part is within the already selection polygons
                bool isWithin = false;

                foreach (var item in internalRings)
                {
                    if (GeometryEngine.Instance.Within(part, item))
                        isWithin = true;
                }

                // if the current polygon is not within any polygon of the internal collection
                // then it is disjoint and needs to be added to 
                if (isWithin == false)
                    internalRings.Add(part as Polygon);
            }

            // now assemble a new polygon geometry based on the internal polygon collection
            foreach (var ring in internalRings)
            {
                outerRings.AddParts(ring.Parts);
            }

            // return the final geometry of the outer rings
            return outerRings.ToGeometry();
        }


        /// <summary>
        /// This method takes an input multi part geometry and breaks the parts into individual standalone geometries.
        /// </summary>
        /// <param name="inputGeometry">The geometry to be exploded into the individual parts.</param>
        /// <returns>An enumeration of individual parts as standalone geometries. The type of geometry is maintained, i.e.
        /// if the input geometry is of type Polyline then each geometry in the return is of type Polyline as well.
        /// If the input geometry is of type Unknown then an empty list is returned.</returns>
        public Task<IEnumerable<Geometry>> MultipartToSinglePartAsync(Geometry inputGeometry)
        {
            return QueuedTask.Run(() => MultipartToSinglePart(inputGeometry));
        }

        /// <summary>
        /// This method takes an input multi part geometry and breaks the parts into individual standalone geometries.
        /// This method must be called on the MCT. Use QueuedTask.Run.
        /// </summary>
        /// <param name="inputGeometry">The geometry to be exploded into the individual parts.</param>
        /// <returns>An enumeration of individual parts as standalone geometries. The type of geometry is maintained, i.e.
        /// if the input geometry is of type Polyline then each geometry in the return is of type Polyline as well.
        /// If the input geometry is of type Unknown then an empty list is returned.</returns>
        /// <remarks>This method must be called on the MCT. Use QueuedTask.Run.</remarks>
        public IEnumerable<Geometry> MultipartToSinglePart(Geometry inputGeometry)
        {
            // list holding the part(s) of the input geometry
            List<Geometry> singleParts = new List<Geometry>();

            // check if the input is a null pointer or if the geometry is empty
            if (inputGeometry == null || inputGeometry.IsEmpty)
                return singleParts;

            // based on the type of geometry, take the parts/points and add them individually into a list
            switch (inputGeometry.GeometryType)
            {
                case GeometryType.Envelope:
                    singleParts.Add(inputGeometry.Clone() as Envelope);
                    break;
                case GeometryType.Multipatch:
                    singleParts.Add(inputGeometry.Clone() as Multipatch);
                    break;
                case GeometryType.Multipoint:
                    var multiPoint = inputGeometry as Multipoint;

                    foreach (var point in multiPoint.Points)
                    {
                        // add each point of collection as a standalone point into the list
                        singleParts.Add(point);
                    }
                    break;
                case GeometryType.Point:
                    singleParts.Add(inputGeometry.Clone() as MapPoint);
                    break;
                case GeometryType.Polygon:
                    var polygon = inputGeometry as Polygon;

                    foreach (var polygonPart in polygon.Parts)
                    {
                        // use the PolygonBuilder turning the segments into a standalone 
                        // polygon instance
                        singleParts.Add(PolygonBuilder.CreatePolygon(polygonPart));
                    }
                    break;
                case GeometryType.Polyline:
                    var polyline = inputGeometry as Polyline;

                    foreach (var polylinePart in polyline.Parts)
                    {
                        // use the PolylineBuilder turning the segments into a standalone
                        // polyline instance
                        singleParts.Add(PolylineBuilder.CreatePolyline(polylinePart));
                    }
                    break;
                case GeometryType.Unknown:
                    break;
                default:
                    break;
            }

            return singleParts;
        }

        private void LookupItems()
        {
            QueuedTask.Run(() =>
            {
                /*using (Geodatabase fileGeodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(GdbPath))))
                {
                    IReadOnlyList<FeatureClassDefinition> fcdefinitions = fileGeodatabase.GetDefinitions<FeatureClassDefinition>();
                    lock (_lockGdbDefinitions)
                    {
                        _gdbDefinitions.Clear();
                        foreach (var definition in fcdefinitions)
                        {
                            _gdbDefinitions.Add(new GdbItem() { Name = definition.GetName(), Type = definition.DatasetType.ToString() });
                        }
                    }
                    IReadOnlyList<TableDefinition> tbdefinitions = fileGeodatabase.GetDefinitions<TableDefinition>();
                    lock (_lockGdbDefinitions)
                    {
                        foreach (var definition in tbdefinitions)
                        {
                            _gdbDefinitions.Add(new GdbItem() { Name = definition.GetName(), Type = definition.DatasetType.ToString() });
                        }
                    }

                }*/
            }).ContinueWith(t =>
            {
                if (t.Exception == null) return;
                var aggException = t.Exception.Flatten();
                foreach (var exception in aggException.InnerExceptions)
                    System.Diagnostics.Debug.WriteLine(exception.Message);
            });
        }



        public class AppProccessor
        {
            private List<PolutionGeom> polutionGeoms = new List<PolutionGeom>();
            private ArcGIS.Desktop.Editing.Attributes.Inspector inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();

            private FeatureClass featureClass;
            private EditOperation editOperation;

            FeatureLayer routesLayer;
            FeatureLayer routesNewLayer;
            FeatureLayer dotsNewLayer;

            public AppProccessor(FeatureClass featureClass, FeatureLayer routesLayer, FeatureLayer routesNewLayer, FeatureLayer dotsNewLayer)
            {
                this.featureClass = featureClass;
                this.routesLayer = routesLayer;
                this.routesNewLayer = routesNewLayer;
                this.dotsNewLayer = dotsNewLayer;
            }

            public void execute(CancelableProgressorSource status)
            {
                editOperation = new EditOperation();
                editOperation.Name = "Create lines intersect";
                double dc10 = 0;
                double dc11 = 0;
                double dc12 = 0;
                double dc13 = 0;
                double d2c10 = 0;
                double d2c11 = 0;
                double d2c12 = 0;
                double d2c13 = 0;
                bool skip = false;

                Selection selection = routesLayer.GetSelection();
                QueryFilter queryFilter = new QueryFilter();
                queryFilter.ObjectIDs = selection.GetObjectIDs();

                uint count = 0;
                using (var cursor = featureClass.Search(queryFilter))
                {
                    while (cursor.MoveNext())
                    {
                        count++;
                    }
                }

                status.Progressor.Max = count;
                uint scount = 0;
                using (var cursor = featureClass.Search(queryFilter))
                {
                    while (cursor.MoveNext())
                    {
                        scount++;
                        status.Progressor.Value += 1;
                        status.Progressor.Message = "Обработка маршрутов " + scount.ToString() + " из " + count.ToString();
                        var feature = cursor.Current as Feature;
                        if (feature == null) continue;
                        var g = feature.GetShape();
                        inspector.Load(routesLayer, feature.GetObjectID());
                        dc10 = double.Parse(inspector["From_C10"].ToString());
                        dc11 = double.Parse(inspector["From_C11"].ToString());
                        dc12 = double.Parse(inspector["From_C12"].ToString());
                        dc13 = double.Parse(inspector["From_C13"].ToString());
                        uint scount2 = 0;
                        using (var cursor2 = featureClass.Search(queryFilter))
                        {
                            skip = true;
                            while (cursor2.MoveNext())
                            {
                                if (status.Progressor.CancellationToken.IsCancellationRequested)
                                    return;
                                var feature2 = cursor2.Current as Feature;
                                if (feature2 == null) continue;
                                if (feature2.GetObjectID() == feature.GetObjectID())
                                {
                                    skip = false;
                                    continue;
                                }
                                else if (skip)
                                {
                                    continue; //Оптимизация 
                                }
                                scount2++;
                                status.Progressor.Message = "Обработка маршрутов " + scount.ToString() + " из " + count.ToString() + " (доп. " + scount2.ToString() + "/" + (count - scount).ToString() + ")";
                                var g2 = feature2.GetShape();
                                if (!GeometryEngine.Instance.Intersects(g, g2)) continue;

                                inspector.Load(routesLayer, feature2.GetObjectID());
                                d2c10 = double.Parse(inspector["From_C10"].ToString());
                                d2c11 = double.Parse(inspector["From_C11"].ToString());
                                d2c12 = double.Parse(inspector["From_C12"].ToString());
                                d2c13 = double.Parse(inspector["From_C13"].ToString());
                                Polution[] p = { new Polution(feature.GetObjectID(), dc10, dc11, dc12, dc13) };
                                Polution[] p2 = { new Polution(feature2.GetObjectID(), d2c10, d2c11, d2c12, d2c13) };
                                addGeoms(new PolutionGeom(g, p), new PolutionGeom(g2, p2));
                            }
                        }
                    }
                }

                status.Progressor.Message = "Создание узлов...";
                createDotsGeoms(status);

                status.Progressor.Message = "Сохранение...";
                saveGeoms(status);
                if (status.Progressor.CancellationToken.IsCancellationRequested)
                    return;
                editOperation.Execute();
                status.Progressor.Message = "Готово";
            }

            private void createDotsGeoms(CancelableProgressorSource status)
            {
                List<PolutionGeom> dots = new List<PolutionGeom>();
                foreach (var b in polutionGeoms)
                {
                    if (status.Progressor.CancellationToken.IsCancellationRequested)
                        return;
                    Geometry gd = GeometryEngine.Instance.QueryPoint(b.geometry as Polyline, SegmentExtension.NoExtension, 0, AsRatioOrLength.AsLength);
                    PolutionGeom d = new PolutionGeom(gd, b.objects.ToArray());
                    PolutionGeom d2 = dots.Find(dot => GeometryEngine.Instance.Intersects(dot.geometry, gd));
                    if (d2 != null)
                    {
                        if (d.objects.Sum(v => v.cityPolutionM3) > d2.objects.Sum(v => v.cityPolutionM3))
                        {
                            dots.Remove(d2);
                            dots.Add(d);
                        }
                    }
                    else
                    {
                        dots.Add(d);
                    }
                }

                foreach (var b in dots)
                {
                    if (status.Progressor.CancellationToken.IsCancellationRequested)
                        return;
                    createDotsFeature(b);
                }
            }

            private void saveGeoms(CancelableProgressorSource status)
            {
                foreach (var b in polutionGeoms)
                {
                    if (status.Progressor.CancellationToken.IsCancellationRequested)
                        return;
                    createFeature(b);
                }
            }

            private void addGeoms(PolutionGeom polutionGeom, PolutionGeom polutionGeom2)
            {
                var g3 = GeometryEngine.Instance.Intersection(polutionGeom.geometry, polutionGeom2.geometry);
                addPolutionGeom(new PolutionGeom(GeometryEngine.Instance.Difference(polutionGeom.geometry, g3), polutionGeom.objects.ToArray()));
                addPolutionGeom(new PolutionGeom(GeometryEngine.Instance.Difference(polutionGeom2.geometry, g3), polutionGeom2.objects.ToArray()));
                addPolutionGeom(new PolutionGeom(g3, polutionGeom.objects.ToArray().Union(polutionGeom2.objects.ToArray()).ToArray()));
            }

            private void addPolutionGeom(PolutionGeom polutionGeom)
            {
                if (polutionGeom.geometry == null || polutionGeom.geometry.IsEmpty)
                {
                    return;
                }
                var a = polutionGeoms.FirstOrDefault(pg => GeometryEngine.Instance.Intersects(pg.geometry, polutionGeom.geometry)
                        && !GeometryEngine.Instance.Intersection(pg.geometry, polutionGeom.geometry).IsEmpty);
                if (a != null)
                {
                    polutionGeoms.Remove(a);
                    addGeoms(a, polutionGeom);
                }
                else
                {
                    polutionGeoms.Add(polutionGeom);
                }
            }

            private void createFeature(PolutionGeom polutionGeom)
            {
                var attributes = new Dictionary<string, object>();
                var cpm3 = polutionGeom.objects.Sum(v => v.cityPolutionM3);
                var vpm3 = polutionGeom.objects.Sum(v => v.vilagePolutionM3);
                var cpt = polutionGeom.objects.Sum(v => v.cityPolutionT);
                var vpt = polutionGeom.objects.Sum(v => v.vilagePolutionT);
                attributes.Add("SHAPE", polutionGeom.geometry);
                attributes.Add("LineLength", GeometryEngine.Instance.Length(polutionGeom.geometry));
                attributes.Add("CityPolutionM3", cpm3);
                attributes.Add("VilagePolutionM3", vpm3);
                attributes.Add("CityPolutionT", cpt);
                attributes.Add("VilagePolutionT", vpt);
                attributes.Add("AllPolutionM3", cpm3 + vpm3);
                attributes.Add("AllPolutionT", cpt + vpt);
                //attributes.Add("IDs", polutionGeom.objects);
                editOperation.Create(routesNewLayer, attributes);
            }

            private void createDotsFeature(PolutionGeom polutionGeom)
            {
                var attributes = new Dictionary<string, object>();
                var cpm3 = polutionGeom.objects.Sum(v => v.cityPolutionM3);
                var vpm3 = polutionGeom.objects.Sum(v => v.vilagePolutionM3);
                var cpt = polutionGeom.objects.Sum(v => v.cityPolutionT);
                var vpt = polutionGeom.objects.Sum(v => v.vilagePolutionT);
                attributes.Add("SHAPE", polutionGeom.geometry);
                attributes.Add("CityPolutionM3", cpm3);
                attributes.Add("VilagePolutionM3", vpm3);
                attributes.Add("CityPolutionT", cpt);
                attributes.Add("VilagePolutionT", vpt);
                attributes.Add("AllPolutionM3", cpm3 + vpm3);
                attributes.Add("AllPolutionT", cpt + vpt);
                //attributes.Add("IDs", polutionGeom.objects);
                editOperation.Create(dotsNewLayer, attributes);
            }
        }

        public class PolutionGeom
        {
            public Geometry geometry { set; get; }

            public List<Polution> objects { set; get; }

            public PolutionGeom(Geometry geometry, Polution[] objects)
            {
                this.objects = new List<Polution>();
                this.geometry = geometry;
                foreach (Polution obj in objects)
                {
                    if (this.objects.Exists(v => v.objectID == obj.objectID))
                        continue;
                    this.objects.Add(obj);
                }

            }
        }

        public class Polution
        {
            public double cityPolutionM3 { set; get; }
            public double vilagePolutionM3 { set; get; }
            public double cityPolutionT { set; get; }
            public double vilagePolutionT { set; get; }
            public long objectID { set; get; }
            public Polution(long objectID, double cityPolutionM3, double cityPolutionT, double vilagePolutionM3, double vilagePolutionT)
            {
                this.cityPolutionM3 = cityPolutionM3;
                this.vilagePolutionM3 = vilagePolutionM3;
                this.cityPolutionT = cityPolutionT;
                this.vilagePolutionT = vilagePolutionT;
                this.objectID = objectID;
            }
        }
    }
}
