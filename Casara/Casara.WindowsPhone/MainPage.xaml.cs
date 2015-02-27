using Casara.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Controls.Maps;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Core;
using System.Diagnostics;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace Casara
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
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

        public MainPage()
        {
            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

            WinCoreDispatcher = Window.Current.CoreWindow.Dispatcher;
            MaxIntensity = 1;
            MinIntensity = 0;
            StayTimer = new Stopwatch();
        }

        /// <summary>
        /// Gets the <see cref="NavigationHelper"/> associated with this <see cref="Page"/>.
        /// </summary>
        public NavigationHelper NavigationHelper
        {
            get { return this.navigationHelper; }
        }

        /// <summary>
        /// Gets the view model for this <see cref="Page"/>.
        /// This can be changed to a strongly typed view model.
        /// </summary>
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session.  The state will be null the first time a page is visited.</param>
        private async void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            Geoposition Position = null;

            if (GPS == null)
                GPS = new GPSDataClass();

            try
            {
                Position = await GPS.GetGPSLocation();
                GPS.LocUpdateThreshold = 10.0;

                MainMap.Center = new Geopoint(new BasicGeoposition()
                {
                    Latitude = Position.Coordinate.Point.Position.Latitude,
                    Longitude = Position.Coordinate.Point.Position.Longitude
                });
                MainMap.ZoomLevel = 12;
            }
            catch(Exception)
            {
                StatusTextBlock.Text = "Error in LoadState...";
            }


            StatusTextBlock.Text += "Finished NavigationHelper_LoadState\n";
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="sender">The source of the event; typically <see cref="NavigationHelper"/></param>
        /// <param name="e">Event data that provides an empty dictionary to be populated with
        /// serializable state.</param>
        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            if (GPS != null && GPS.GeoLoc != null) //Can this callback get assigned multiple times? That can cause some undefined behaviour!
                GPS.GeoLoc.PositionChanged -= new TypedEventHandler<Geolocator, PositionChangedEventArgs>(GPSPositionChanged);
        }

        #region NavigationHelper registration

        /// <summary>
        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// <para>
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="NavigationHelper.LoadState"/>
        /// and <see cref="NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.
        /// </para>
        /// </summary>
        /// <param name="e">Provides data for navigation methods and event
        /// handlers that cannot cancel the navigation request.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.navigationHelper.OnNavigatedFrom(e);
        }        

        private void DrawCircle(double Latitude, double Longitude, double Radius, Int32 Intensity)
        {
            MapPolygon Poly = new MapPolygon();
            List<BasicGeoposition> positions = new List<BasicGeoposition>();

            for (int i = 0; i < 16; i++)
            {
                positions.Add(new BasicGeoposition() { Latitude = Latitude + Radius * Math.Cos(i * 3.14 / 8), Longitude = Longitude + Radius * Math.Sin(i * 3.14 / 8) });
            }

            Poly.Path = new Geopath(positions);
            Poly.StrokeColor = Windows.UI.Color.FromArgb(120, (byte)((Intensity & 0x00ff0000) >> 16), (byte)((Intensity & 0x0000ff00) >> 8), (byte)(Intensity & 0x000000ff));
            Poly.FillColor = Poly.StrokeColor;

            try
            {
                MainMap.MapElements.Add(Poly);
            }
            catch(Exception)
            {
                StatusTextBlock.Text = "Error in adding circle";
            }
            
        }

        private void ChangeShapeColour(MapPolygon Poly, Int32 Intensity)
        {
            Poly.FillColor = Windows.UI.Color.FromArgb(120, (byte)((Intensity & 0x00ff0000) >> 16), (byte)((Intensity & 0x0000ff00) >> 8), (byte)(Intensity & 0x000000ff));
            Poly.StrokeColor = Poly.FillColor;
        }

        private Int32 GetShapeColour(MapPolygon Poly)
        {
            Int32 Colour = (Poly.FillColor.A << 24) | (Poly.FillColor.R << 16) | (Poly.FillColor.G << 8) | (Poly.FillColor.B);
            return Colour;
        }

        private void UpdateShapeColours(Int32 Change)
        {

        }

        async private void GPSPositionChanged(Geolocator sender, PositionChangedEventArgs e)
        {
            Geoposition Position = null;
            Int32 Colour;

            StayTimer.Stop();

            if (StayTimer.ElapsedMilliseconds > MaxIntensity)
                MaxIntensity = StayTimer.ElapsedMilliseconds;
            else if (StayTimer.ElapsedMilliseconds < MinIntensity)
                MinIntensity = StayTimer.ElapsedMilliseconds;

            Colour = CalculateIntensityColour(StayTimer.ElapsedMilliseconds);

            try
            {
                Position = await GPS.GetGPSLocation();

                Geopoint NewCentre = new Geopoint(new BasicGeoposition()
                {
                    Latitude = Position.Coordinate.Point.Position.Latitude,
                    Longitude = Position.Coordinate.Point.Position.Longitude
                });


                await WinCoreDispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    MainMap.Center = NewCentre;
                    DrawCircle(Position.Coordinate.Point.Position.Latitude, Position.Coordinate.Point.Position.Longitude, 0.0025, Colour);
                }
                );
            }
            catch(Exception)
            {
                //StatusTextBlock.Text = "Error in geo position update"; //Cannot access UI thread in this catch statement. May cause a crash if uncommented
            }
            

            StayTimer.Reset();
            StayTimer.Start();
        }

        private Int32 CalculateIntensityColour(double Intensity)
        {
            Int32 IntensityColour;

            double delta = (MaxIntensity - MinIntensity) / (3 * 255);
            IntensityColour = (Int32)Math.Ceiling(Intensity / delta);
            
            return IntensityColour;
        }

        #endregion

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StayTimer.Stop();
            try
            {
                GPS.GeoLoc.PositionChanged -= new TypedEventHandler<Geolocator, PositionChangedEventArgs>(GPSPositionChanged);
            }
            catch(Exception)
            {
                StatusTextBlock.Text = "Error in StopButton_Click";
            }
            StatusTextBlock.Text += "Finished StopButton_Click\n";
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GPS != null && GPS.GeoLoc != null) //Can this callback get assigned multiple times? That can cause some undefined behaviour!
                {
                    GPS.GeoLoc.PositionChanged += new TypedEventHandler<Geolocator, PositionChangedEventArgs>(GPSPositionChanged);
                }
            }
            catch(Exception)
            {
                StatusTextBlock.Text = "Error in StartButton_Click";
            }
            
            StayTimer.Reset();
            StayTimer.Start();
            StatusTextBlock.Text += "Finished StartButton_Click\n";
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainMap.MapElements.Clear();
            }
            catch(Exception)
            {
                StatusTextBlock.Text = "Error in ClearButton_Click";
            }
            StatusTextBlock.Text += "Finished ClearButton_Click\n";
        }
    }
}
