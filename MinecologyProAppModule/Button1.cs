using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    internal class Button1 : Button
    {
        protected override void OnClick()
        {
            var routesLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(f => f.ShapeType == esriGeometryType.esriGeometryPolyline && f.Name== "Маршруты");
            var routesNewLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(f => f.ShapeType == esriGeometryType.esriGeometryPolyline && f.Name== "Маршруты объединенные");
            
            if (routesLayer == null)
            {
                MessageBox.Show($@"To run this sample you need to have a polygon feature class layer.");
                return;
            }

            if (routesNewLayer == null)
            {
                MessageBox.Show($@"To run this sample you need to have a polygon feature class layer.");
                return;
            }
            QueuedTask.Run(() =>
            {
                // create an instance of the inspector class
                //var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
                var fc = routesLayer.GetTable() as FeatureClass;
                if (fc == null) return;
                var fcDefinition = fc.GetDefinition();

                var proccessor = new AppProccessor(fc, routesLayer, routesNewLayer);
                proccessor.execute();
            });
        }

        private void showMess()
        {
            string uri = ArcGIS.Desktop.Core.Project.Current.URI;
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Project uri {uri}");
        }

        private class AppProccessor
        {
            private List<PolutionGeom> polutionGeoms = new List<PolutionGeom>();
            private ArcGIS.Desktop.Editing.Attributes.Inspector inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();

            private FeatureClass featureClass;
            private EditOperation editOperation;

            FeatureLayer routesLayer;
            FeatureLayer routesNewLayer;

            public AppProccessor (FeatureClass featureClass, FeatureLayer routesLayer, FeatureLayer routesNewLayer) {
                this.featureClass = featureClass;
                this.routesLayer = routesLayer;
                this.routesNewLayer = routesNewLayer;
            }

            public void execute()
            {
                editOperation = new EditOperation();
                editOperation.Name = "Create lines intersect";
                double d = 0;
                double d2 = 0;
                bool skip = false;
                using (var cursor = featureClass.Search())
                {
                    while (cursor.MoveNext())
                    {
                        var feature = cursor.Current as Feature;
                        if (feature == null) continue;
                        var g = feature.GetShape();
                        inspector.Load(routesLayer, feature.GetObjectID());
                        d = double.Parse(inspector["From_C13"].ToString());
                        using (var cursor2 = featureClass.Search())
                        {
                            skip = true;
                            while (cursor2.MoveNext())
                            {
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
                                var g2 = feature2.GetShape();
                                if (!GeometryEngine.Instance.Intersects(g, g2)) continue;

                                inspector.Load(routesLayer, feature2.GetObjectID());
                                d2 = double.Parse(inspector["From_C13"].ToString());

                                addGeoms(g,g2,d,d2);
                            }
                        }
                    }
                }

                saveGeoms();

                editOperation.Execute();
            }

            private void saveGeoms()
            {
                foreach (var b in polutionGeoms)
                {
                    createFeature(b);
                }
            }

            private void addGeoms(PolutionGeom polutionGeom, PolutionGeom polutionGeom2)
            {
                addGeoms(polutionGeom.geometry, polutionGeom2.geometry, polutionGeom.polution, polutionGeom2.polution);
            }

            private void addGeoms(Geometry g, Geometry g2, double d, double d2)
            {
                addPolutionGeom(new PolutionGeom(GeometryEngine.Instance.Difference(g, g2), d));
                addPolutionGeom(new PolutionGeom(GeometryEngine.Instance.Difference(g2, g), d2));
                addPolutionGeom(new PolutionGeom(GeometryEngine.Instance.Intersection(g, g2), d2 + d));
            }

            private void addPolutionGeom(PolutionGeom polutionGeom)
            {
                if (polutionGeom.geometry==null || polutionGeom.geometry.IsEmpty)
                {
                    return;
                }
                var a = polutionGeoms.Where(pg => GeometryEngine.Instance.Intersects(pg.geometry,polutionGeom.geometry));
                foreach(var b in a)
                {
                    polutionGeoms.Remove(b);
                    addGeoms(b,polutionGeom);
                }
            }

            private void createFeature(PolutionGeom polutionGeom)
            {
                var attributes = new Dictionary<string, object>();
                attributes.Add("SHAPE", polutionGeom.geometry);
                attributes.Add("Polution1", polutionGeom.polution);
                editOperation.Create(routesNewLayer, attributes);
            }
        }

        private class PolutionGeom
        {
            public Geometry geometry { set; get; }
            public double polution { set; get; }

            public PolutionGeom(Geometry geometry, double polution)
            {
                this.geometry = geometry;
                this.polution = polution;
            }
        }
    }
}
