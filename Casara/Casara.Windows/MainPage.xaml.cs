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
//using Bing.Maps;
using Windows.UI.Core;
using System.Diagnostics;
using Windows.UI.Xaml.Shapes;
//using Bing.Maps.HeatMaps;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage;
//using Microsoft.Maps.SpatialToolbox.Bing;
//using Microsoft.Maps.SpatialToolbox.IO;
//using Microsoft.Maps.SpatialToolbox;
//using LocalTileLayers;

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
        private double MapScale;

        //private ShapeStyle DefaultStyle = new ShapeStyle()
        //{
        //    FillColor = StyleColor.FromArgb(150, 0, 0, 255),
        //    StrokeColor = StyleColor.FromArgb(150, 125, 125, 125),
        //    StrokeThickness = 1
        //};

        //private LocalTileSource layerInfo = new LocalTileSource()
        //{
        //    ZipTilePath = new Uri("ms-appx:///Assets/HurricaneKatrina.zip"),
        //    MinZoomLevel = 1,
        //    MaxZoomLevel = 6,
        //    Bounds = new LocationRect(new Location(72, -170), new Location(14, -65))
        //};

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
            MapScale = 591657;
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
            Esri.ArcGISRuntime.Geometry.MapPoint MapCentre = null;
                       
            try
            {
                
                Position = await GPS.GetGPSLocation();
                LatitudeBox.Text += Position.Coordinate.Point.Position.Latitude.ToString();
                LongitudeBox.Text += Position.Coordinate.Point.Position.Longitude.ToString();

                MapCentre = new Esri.ArcGISRuntime.Geometry.MapPoint(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84);
                MainMapView.SetView(MapCentre, MapScale);
                GPS.LocUpdateThreshold = 10.0;
                //MainMap.Layers.Add(new Esri.ArcGISRuntime.Layers.GraphicsLayer());
                //DrawCircle(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, 0.0025, 0x00ff0000);
            }
            catch(Exception ex)
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
        
        private void DrawCircle(double Longitude, double Latitude, double Radius, Int32 Intensity)
        {
            Esri.ArcGISRuntime.Geometry.Polygon Poly = null;
            List<Esri.ArcGISRuntime.Geometry.MapPoint> MapPointsList = new List<Esri.ArcGISRuntime.Geometry.MapPoint>();
            int MaxPoints = 16;

            for (int i = 0; i < MaxPoints; i++)
                MapPointsList.Add(new Esri.ArcGISRuntime.Geometry.MapPoint(Longitude + Radius * Math.Cos(i * 3.14 / (MaxPoints / 2)), Latitude + Radius * Math.Sin(i * 3.14 / (MaxPoints / 2)), Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84));

            Poly = new Esri.ArcGISRuntime.Geometry.Polygon(MapPointsList);

            Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = new Esri.ArcGISRuntime.Symbology.SimpleFillSymbol();
            Fill.Color = Windows.UI.Colors.Blue;
            Fill.Style = Esri.ArcGISRuntime.Symbology.SimpleFillStyle.Solid;

            // Create a new graphic and set it's geometry and symbol. 
            Esri.ArcGISRuntime.Layers.Graphic Graphic = new Esri.ArcGISRuntime.Layers.Graphic();
            Graphic.Geometry = Poly;
            Graphic.Symbol = Fill;

            //Poly.FillColor = Windows.UI.Color.FromArgb(255, (byte)((Intensity & 0x00ff0000) >> 16), (byte)((Intensity & 0x0000ff00) >> 8), (byte)(Intensity & 0x000000ff));


            try
            {
                if (MainMap.Layers.Count == 0)
                    MainMap.Layers.Add(new Esri.ArcGISRuntime.Layers.GraphicsLayer());

                Esri.ArcGISRuntime.Layers.GraphicsLayer test = (Esri.ArcGISRuntime.Layers.GraphicsLayer)MainMapView.Map.Layers["ShapeLayer"];
                test.Graphics.Add(Graphic);
            }
            catch(Exception ex)
            {
                StatusTextBlock.Text += "Map Error\n";
            }

            StatusTextBlock.Text += "DrawCircle done...\n";
        }

        private void ChangeShapeColour(Esri.ArcGISRuntime.Layers.Graphic Poly, Int32 Intensity)
        {
            Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = (Esri.ArcGISRuntime.Symbology.SimpleFillSymbol)Poly.Symbol;

            Fill.Color = Windows.UI.Color.FromArgb(255, (byte)((Intensity & 0x00ff0000) >> 16), (byte)((Intensity & 0x0000ff00) >> 8), (byte)(Intensity & 0x000000ff)); ;
            Fill.Style = Esri.ArcGISRuntime.Symbology.SimpleFillStyle.Solid;
        }

        private Int32 GetShapeColour(Esri.ArcGISRuntime.Layers.Graphic Poly)
        {
            Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = (Esri.ArcGISRuntime.Symbology.SimpleFillSymbol)Poly.Symbol;

            Int32 Colour = (Fill.Color.A << 24) | (Fill.Color.R << 16) | (Fill.Color.G << 8) | (Fill.Color.B);
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
                StatusTextBlock.Text += "Error in StartButton_Click!\n";
            }            
        }

        private void UpdateShapeColours(Int32 Change)
        {
            Esri.ArcGISRuntime.Layers.GraphicsLayer GraphLayer = (Esri.ArcGISRuntime.Layers.GraphicsLayer)MainMapView.Map.Layers["ShapeLayer"];
            Esri.ArcGISRuntime.Layers.GraphicCollection GraphicsList = GraphLayer.Graphics;
            byte A, R = (byte)((Change & 0x00ff0000) >> 16), G = (byte)((Change & 0x0000ff00) >> 8), B = (byte)(Change & 0x000000ff);
            int i;

            for (i = 0; i < GraphicsList.Count; i++)
            {
                Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = (Esri.ArcGISRuntime.Symbology.SimpleFillSymbol)GraphicsList[i].Symbol;

                A = Fill.Color.A;
                R += Fill.Color.R;
                G += Fill.Color.G;
                B += Fill.Color.B;
                Fill.Color = Windows.UI.Color.FromArgb(A, R, G, B);
            }
        }

        async private void GPSPositionChanged(Geolocator sender, PositionChangedEventArgs e)
        {
            Geoposition Position = null;

            try
            {
                Position = await GPS.GetGPSLocation();

                Esri.ArcGISRuntime.Geometry.MapPoint NewCentre = new Esri.ArcGISRuntime.Geometry.MapPoint(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude,Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84);
                
                await WinCoreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    MainMapView.SetView(NewCentre,MapScale);
                    DrawCircle(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, 0.0025, 0x00ff0000);
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
                Esri.ArcGISRuntime.Layers.GraphicsLayer GraphLayer = (Esri.ArcGISRuntime.Layers.GraphicsLayer)MainMapView.Map.Layers["ShapeLayer"];
                Esri.ArcGISRuntime.Layers.GraphicCollection GraphicsList = GraphLayer.Graphics;
                GraphicsList.Clear();
            }
            catch(Exception)
            {
                StatusTextBlock.Text = "error in ClearButton_Click";
            }
            
        }

        //private void TestSpeed()
        //{
        //    int Limit = 20;
        //    int i;
        //    Random R = new Random();
        //    //Image test = (Image)MainMap.Children[0];
        //    //SolidColorBrush brush = new SolidColorBrush(Windows.UI.Colors.Blue);
            

        //    for (i = 0; i < Limit; i++)
        //    {
        //        Location Loc = new Location(R.NextDouble()*180-90,R.NextDouble()*360-180);
        //        Image img = new Image();
        //        img.Source = new BitmapImage(new Uri("ms-appx:///Assets/tweety.jpg"));
        //        img.Height = 50;
        //        img.Width = 50;
        //        //MapLayer.SetPosition((Image)img, Loc);
        //        //MainMap.Children.Add(img);
        //        //DrawCircle(49,122,0.25,0x0000ff);//R.NextDouble()*180-90,R.NextDouble()*360-180
        //        //test.DrawCircle(R.Next(50, 400), R.Next(50, 400), brush);
        //    }
        //    StatusTextBlock.Text += "Added circles...\n";
        //}

        //private void MainMap_Viewchanged(object sender, ViewChangedEventArgs e)
        //{
            //if (MainMap.Children.Count > 0)
            //{
            //    Image test = (Image)MainMap.Children[0];

            //    test.Height = 10 * MainMap.ZoomLevel;
            //    test.Width = 10 * MainMap.ZoomLevel;
            //    test.Stretch = Stretch.UniformToFill;
            //    test.InvalidateArrange();
            //    MainMap.InvalidateArrange();
            //    ScrollableImage test = (ScrollableImage)MainMap.Children[0];
            //    test.SetZoom((float)MainMap.ZoomLevel);
            //}

        //}

        //private void MainMap_LayerLoaded(object sender, LayerLoadedEventArgs e)
        //{
        //    if (e.LoadError == null)
        //        return;

        //    Debug.WriteLine(string.Format("Error while loading layer : {0} - {1}", e.Layer.ID, e.LoadError.Message));
        //}
    }
}
