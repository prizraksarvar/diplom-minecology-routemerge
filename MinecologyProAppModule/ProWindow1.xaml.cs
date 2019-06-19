using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MinecologyProAppModule
{
    /// <summary>
    /// Interaction logic for ProWindow1.xaml
    /// </summary>
    public partial class ProWindow1 : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        //ComboBox mcb1;
        //ComboBox mcb2;
        //ComboBox mcb3;
        public ProWindow1()
        {
            InitializeComponent();
            //mcb1 = FindName("mcb1") as ComboBox;
            //mcb2 = FindName("mcb2") as ComboBox;
            //mcb3 = FindName("mcb3") as ComboBox;

            var featureLayers = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(f => f.ShapeType == esriGeometryType.esriGeometryPolyline);
            foreach (var f in featureLayers)
            {
                var item = new ComboBoxItem();
                item.Content = f.Name;
                mcb1.Items.Add(item);
                var item2 = new ComboBoxItem();
                item2.Content = f.Name;
                mcb2.Items.Add(item2);
            }

            var featureLayers2 = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(f => f.ShapeType == esriGeometryType.esriGeometryPoint);
            foreach (var f in featureLayers2)
            {
                var item = new ComboBoxItem();
                item.Content = f.Name;
                mcb3.Items.Add(item);
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var a1 = mcb1.Items[mcb1.SelectedIndex] as ComboBoxItem;
            var a2 = mcb2.Items[mcb2.SelectedIndex] as ComboBoxItem;
            var a3 = mcb3.Items[mcb3.SelectedIndex] as ComboBoxItem;

            var routesLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(f => f.ShapeType == esriGeometryType.esriGeometryPolyline && f.Name == a1.Content as String);
            var routesNewLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(f => f.ShapeType == esriGeometryType.esriGeometryPolyline && f.Name == a2.Content as String);
            var dotsNewLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(f => f.ShapeType == esriGeometryType.esriGeometryPoint && f.Name == a3.Content as String);

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

            using (var progress = new ProgressDialog("Обработка...", "Отменено", 10000, false))
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

                    var proccessor = new Module1.AppProccessor(fc, routesLayer, routesNewLayer, dotsNewLayer);
                    proccessor.execute(status);
                }, status.Progressor);
            }

            Close();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ComboBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {

        }
    }

    internal class MyComboBox1 : ArcGIS.Desktop.Framework.Contracts.ComboBox
    {
        public MyComboBox1()
        {
            var featureLayers = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(f => f.ShapeType == esriGeometryType.esriGeometryPolyline);
            foreach (var f in featureLayers)
            {
                var item = new ComboBoxItem();
                item.Name = f.Name;
                this.Add(item);
            }
            
        }
    }

    internal class MyComboBox2 : ArcGIS.Desktop.Framework.Contracts.ComboBox
    {
        public MyComboBox2()
        {
            var featureLayers = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(f => f.ShapeType == esriGeometryType.esriGeometryPolyline);
            foreach (var f in featureLayers)
            {
                var item = new ComboBoxItem();
                item.Name = f.Name;
                this.Add(item);
            }

        }
    }
}
