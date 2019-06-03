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
    }
}
