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
    internal class BackstageTab1ViewModel : BackstageTab
    {
        /// <summary>
        /// Called when the backstage tab is selected.
        /// </summary>
        protected override Task InitializeAsync()
        {
            return base.InitializeAsync();
        }

        /// <summary>
        /// Called when the backstage tab is unselected.
        /// </summary>
        protected override Task UninitializeAsync()
        {
            return base.UninitializeAsync();
        }

        private string _tabHeading = "Tab Title";
        public string TabHeading
        {
            get
            {
                return _tabHeading;
            }
            set
            {
                SetProperty(ref _tabHeading, value, () => TabHeading);
            }
        }
    }
}
