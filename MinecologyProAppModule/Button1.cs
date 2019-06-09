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
        protected override async void OnClick()
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

            using (var progress = new ProgressDialog("Обработка...","Отменено",10000,false))
            {
                //progress.Show();
                var status = new CancelableProgressorSource(progress);
                status.Max = 10000;
                status.Value = 0;
                await QueuedTask.Run(() =>
                {
                    // create an instance of the inspector class
                    //var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
                    var fc = routesLayer.GetTable() as FeatureClass;
                    if (fc == null) return;
                    var fcDefinition = fc.GetDefinition();

                    var proccessor = new AppProccessor(fc, routesLayer, routesNewLayer);
                    proccessor.execute(status);
                }, status.Progressor);
            }
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

            public void execute(CancelableProgressorSource status)
            {
                editOperation = new EditOperation();
                editOperation.Name = "Create lines intersect";
                double d = 0;
                double d2 = 0;
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
                        d = double.Parse(inspector["From_C13"].ToString());
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
                                d2 = double.Parse(inspector["From_C13"].ToString());
                                Polution[] p = { new Polution(d, feature.GetObjectID()) };
                                Polution[] p2 = { new Polution(d2, feature2.GetObjectID()) };
                                addGeoms(g, g2, p, p2);
                            }
                        }
                    }
                }

                status.Progressor.Message = "Сохранение...";
                saveGeoms(status);
                if (status.Progressor.CancellationToken.IsCancellationRequested)
                    return;
                editOperation.Execute();
                status.Progressor.Message = "Готово";
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
                addGeoms(polutionGeom.geometry, polutionGeom2.geometry, polutionGeom.objects.ToArray(), polutionGeom2.objects.ToArray());
            }

            private void addGeoms(Geometry g, Geometry g2, Polution[] objects1, Polution[] objects2)
            {
                var g3 = GeometryEngine.Instance.Intersection(g, g2);
                addPolutionGeom(new PolutionGeom(GeometryEngine.Instance.Difference(g, g3), objects1));
                addPolutionGeom(new PolutionGeom(GeometryEngine.Instance.Difference(g2, g3), objects2));
                addPolutionGeom(new PolutionGeom(g3, objects1.Union(objects2).ToArray()));
            }

            private void addPolutionGeom(PolutionGeom polutionGeom)
            {
                if (polutionGeom.geometry==null || polutionGeom.geometry.IsEmpty)
                {
                    return;
                }
                var a = polutionGeoms.FirstOrDefault(pg => GeometryEngine.Instance.Intersects(pg.geometry,polutionGeom.geometry)
                        && !GeometryEngine.Instance.Intersection(pg.geometry,polutionGeom.geometry).IsEmpty);
                if (a!=null)
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
                attributes.Add("SHAPE", polutionGeom.geometry);
                attributes.Add("Polution1", polutionGeom.objects.Sum(v => v.polution));
                //attributes.Add("IDs", polutionGeom.objects);
                editOperation.Create(routesNewLayer, attributes);
            }
        }

        private class PolutionGeom
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

        private class Polution
        {
            public double polution { set; get; }
            public long objectID { set; get; }
            public Polution(double polution, long objectID)
            {
                this.polution = polution;
                this.objectID = objectID;
            }
        }
    }
}
