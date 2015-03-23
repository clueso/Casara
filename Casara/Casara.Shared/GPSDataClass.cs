using System;
using System.Collections.Generic;
using System.Text;
using Windows.Devices.Geolocation;
using System.Threading.Tasks;
using Windows.Storage;
//using Microsoft.Maps.SpatialToolbox.Bing;
//using Microsoft.Maps.SpatialToolbox.IO;
//using Microsoft.Maps.SpatialToolbox;
using System.IO;

namespace Casara
{
    class GPSDataClass
    {
        private static Geolocator Geo;

        //Constructor
        public GPSDataClass()
        {
            if (Geo == null)
                Geo = new Geolocator();

            //geo.PositionChanged += new TypedEventHandler<Geolocator, PositionChangedEventArgs>(geo_PositionChanged);
        }

        public Geolocator GeoLoc
        {
            get { return Geo; }
        }

        public double LocUpdateThreshold
        {
            get { return Geo.MovementThreshold; }
            set { Geo.MovementThreshold = value; }
        }

        public async Task<Geoposition> GetGPSLocation()//Geolocator Geo
        {
            Geoposition GPSLocation = await Geo.GetGeopositionAsync();

            return GPSLocation;
        }

        //public async Task<SpatialDataSet> ReadShapeFile(string ShapeFile, int GeometryCount)
        //{
        //    StorageFile ImageFile = await StorageFile.GetFileFromPathAsync(ShapeFile);
        //    SpatialDataSet FeatureSet;

        //    Stream fileStream = await ImageFile.OpenStreamForReadAsync();
        //    ShapefileReader FileReader = new ShapefileReader();
        //    FeatureSet = await FileReader.ReadAsync(fileStream);
        //    FeatureSet.Geometries.RemoveRange(GeometryCount, FeatureSet.Geometries.Count - GeometryCount);

        //    return FeatureSet;
        //}
    }
}
