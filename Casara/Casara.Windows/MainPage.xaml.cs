using Casara.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Geolocation;
using Bing.Maps;
using Windows.UI.Core;
using System.Diagnostics;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace Casara
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();
        private GPSDataClass GPS = null;
        private CoreDispatcher WinCoreDispatcher;
        private float MaxIntensity;
        private float MinIntensity;
        private Stopwatch StayTimer;

        /// <summary>
        /// This can be changed to a strongly typed view model.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// NavigationHelper is used on each page to aid in navigation and 
        /// process lifetime management
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }


        public MainPage()
        {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += navigationHelper_LoadState;
            this.navigationHelper.SaveState += navigationHelper_SaveState;

            if (GPS == null)
                GPS = new GPSDataClass();

            WinCoreDispatcher = Window.Current.CoreWindow.Dispatcher;
            MaxIntensity = 100;
            MinIntensity = 0;
            StayTimer = new Stopwatch();
        }

        /// <summary>
        /// Populates the page with content passed during navigation. Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session. The state will be null the first time a page is visited.</param>
        private async void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            Geoposition Position = null;
            
            try
            {
                Position = await GPS.GetGPSLocation();
                MainMap.Center = new Bing.Maps.Location(Position.Coordinate.Point.Position.Latitude, Position.Coordinate.Point.Position.Longitude);
                MainMap.ZoomLevel = 12;
                GPS.LocUpdateThreshold = 10.0;
                DrawCircle(Position.Coordinate.Point.Position.Latitude, Position.Coordinate.Point.Position.Longitude,0.0025,0x0000ff);
            }
            catch(Exception)
            {
                StatusTextBlock.Text = "Error in navigationHelper_LoadState!\n";
            }
                        
            StatusTextBlock.Text += "State Loaded...\n";
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="sender">The source of the event; typically <see cref="NavigationHelper"/></param>
        /// <param name="e">Event data that provides an empty dictionary to be populated with
        /// serializable state.</param>
        private void navigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            if (GPS.GeoLoc != null) //Can this callback get assigned multiple times? That can cause some undefined behaviour!
                GPS.GeoLoc.PositionChanged -= new TypedEventHandler<Geolocator, PositionChangedEventArgs>(GPSPositionChanged);
        }

        #region NavigationHelper registration

        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// 
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="GridCS.Common.NavigationHelper.LoadState"/>
        /// and <see cref="GridCS.Common.NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedFrom(e);
        }

        #endregion
        
        private void DrawCircle(double Latitude, double Longitude, double Radius, Int32 Intensity)
        {
            MapPolygon Poly = new MapPolygon();
            Location Loc;

            for (int i = 0; i < 16; i++)
            {
                Loc = new Location(Latitude + Radius * Math.Cos(i * 3.14 / 8), Longitude + Radius * Math.Sin(i * 3.14 / 8));
                Poly.Locations.Add(Loc);
            }

            Poly.FillColor = Windows.UI.Color.FromArgb(255, (byte)((Intensity & 0x00ff0000) >> 16), (byte)((Intensity & 0x0000ff00) >> 8), (byte)(Intensity & 0x000000ff));

            try
            {
                if (MainMap.ShapeLayers.Count == 0)
                    MainMap.ShapeLayers.Add(new MapShapeLayer());

                MainMap.ShapeLayers[0].Shapes.Add(Poly);
            }
            catch(Exception)
            {
                StatusTextBlock.Text += "Map Error\n";
            }
            
            StatusTextBlock.Text += "DrawCircle done...\n";
        }

        private void ChangeShapeColour(MapPolygon Poly, Int32 Intensity)
        {
            Poly.FillColor = Windows.UI.Color.FromArgb(255, (byte)((Intensity & 0x00ff0000) >> 16), (byte)((Intensity & 0x0000ff00) >> 8), (byte)(Intensity & 0x000000ff));
        }

        private Int32 GetShapeColour(MapPolygon Poly)
        {
            Int32 Colour = (Poly.FillColor.A << 24) | (Poly.FillColor.R << 16) | (Poly.FillColor.G << 8) | (Poly.FillColor.B);
            return Colour;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GPS.GeoLoc != null) //Can this callback get assigned multiple times? That can cause some undefined behaviour!
                {                    
                    GPS.GeoLoc.PositionChanged += new TypedEventHandler<Geolocator, PositionChangedEventArgs>(GPSPositionChanged);
                }

                StayTimer.Reset();
                StayTimer.Start();
            }
            catch (Exception)
            {
                //StatusTextBlock.Text += "Error in StartButton_Click!\n";
            }
            
        }

        private void UpdateShapeColours(Int32 Change)
        {
            MapShapeCollection ShapeCollection = MainMap.ShapeLayers[0].Shapes;
            byte A, R = (byte)((Change & 0x00ff0000) >> 16), G = (byte)((Change & 0x0000ff00) >> 8), B = (byte)(Change & 0x000000ff);
            int i;

            for(i = 0; i < ShapeCollection.Count; i++)
            {
                A = ((MapPolygon)ShapeCollection[i]).FillColor.A;
                R += ((MapPolygon)ShapeCollection[i]).FillColor.R;
                G += ((MapPolygon)ShapeCollection[i]).FillColor.G;
                B += ((MapPolygon)ShapeCollection[i]).FillColor.B;
                ((MapPolygon)ShapeCollection[i]).FillColor = Windows.UI.Color.FromArgb(A,R,G,B);
            }
        }

        async private void GPSPositionChanged(Geolocator sender, PositionChangedEventArgs e)
        {
            Geoposition Position = null;

            try
            {
                Position = await GPS.GetGPSLocation();

                Bing.Maps.Location NewCentre = new Bing.Maps.Location(Position.Coordinate.Point.Position.Latitude, Position.Coordinate.Point.Position.Longitude);
                
                await WinCoreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    MainMap.Center = NewCentre;
                    DrawCircle(Position.Coordinate.Point.Position.Latitude, Position.Coordinate.Point.Position.Longitude, 0.0025, 0x00ff0000);
                }
                );
            }
            catch(Exception)
            {
                                
            }
            
        }

        private Int32 CalculateIntensityColour(double Intensity)
        {
            Int32 IntensityColour;

            double delta = (MaxIntensity - MinIntensity) / (3 * 255);
            IntensityColour = (Int32)Math.Ceiling(Intensity / delta);

            return IntensityColour;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (GPS.GeoLoc != null) //Can this callback get assigned multiple times? That can cause some undefined behaviour!
            {                    
                GPS.GeoLoc.PositionChanged += new TypedEventHandler<Geolocator, PositionChangedEventArgs>(GPSPositionChanged);
            }

            StayTimer.Reset();
            StayTimer.Stop();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainMap.ShapeLayers.Clear();
            }
            catch(Exception)
            {
                StatusTextBlock.Text = "error in ClearButton_Click";
            }
            
        }
    }
}
