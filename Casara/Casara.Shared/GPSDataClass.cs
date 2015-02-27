using System;
using System.Collections.Generic;
using System.Text;
using Windows.Devices.Geolocation;
using System.Threading.Tasks;

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
    }
}
