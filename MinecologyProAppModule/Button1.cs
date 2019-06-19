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
            var dotsNewLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(f => f.ShapeType == esriGeometryType.esriGeometryPoint && f.Name== "repoint");
            
            if (routesLayer == null)
            {
                MessageBox.Show($@"To run this sample you need to have a polyline feature class layer.");
                return;
            }

            if (routesNewLayer == null)
            {
                MessageBox.Show($@"To run this sample you need to have a polyline feature class layer.");
                return;
            }

            if (dotsNewLayer == null)
            {
                MessageBox.Show($@"To run this sample you need to have a point feature class layer.");
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

                    var proccessor = new AppProccessor(fc, routesLayer, routesNewLayer, dotsNewLayer);
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
            FeatureLayer dotsNewLayer;

            public AppProccessor (FeatureClass featureClass, FeatureLayer routesLayer, FeatureLayer routesNewLayer, FeatureLayer dotsNewLayer) {
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
                    PolutionGeom d2 = dots.Find(dot => GeometryEngine.Instance.Intersects(dot.geometry,gd));
                    if (d.objects.Sum(v => v.cityPolutionM3)>d2.objects.Sum(v => v.cityPolutionM3))
                    {
                        dots.Remove(d2);
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
                attributes.Add("LineLength", GeometryEngine.Instance.Length(polutionGeom.geometry));
                attributes.Add("CityPolutionM3", polutionGeom.objects.Sum(v => v.cityPolutionM3));
                attributes.Add("VilagePolutionM3", polutionGeom.objects.Sum(v => v.vilagePolutionM3));
                attributes.Add("CityPolutionT", polutionGeom.objects.Sum(v => v.cityPolutionT));
                attributes.Add("VilagePolutionT", polutionGeom.objects.Sum(v => v.vilagePolutionT));
                //attributes.Add("IDs", polutionGeom.objects);
                editOperation.Create(routesNewLayer, attributes);
            }

            private void createDotsFeature(PolutionGeom polutionGeom)
            {
                var attributes = new Dictionary<string, object>();
                attributes.Add("SHAPE", polutionGeom.geometry);
                attributes.Add("CityPolutionM3", polutionGeom.objects.Sum(v => v.cityPolutionM3));
                attributes.Add("VilagePolutionM3", polutionGeom.objects.Sum(v => v.vilagePolutionM3));
                attributes.Add("CityPolutionT", polutionGeom.objects.Sum(v => v.cityPolutionT));
                attributes.Add("VilagePolutionT", polutionGeom.objects.Sum(v => v.vilagePolutionT));
                //attributes.Add("IDs", polutionGeom.objects);
                editOperation.Create(dotsNewLayer, attributes);
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
            public double cityPolutionM3 { set; get; }
            public double vilagePolutionM3 { set; get; }
            public double cityPolutionT { set; get; }
            public double vilagePolutionT { set; get; }
            public long objectID { set; get; }
            public Polution(long objectID, double cityPolutionM3, double cityPolutionT, double vilagePolutionM3,  double vilagePolutionT)
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
