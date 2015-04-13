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
//using Windows.UI.Xaml.Controls.Maps;
using Windows.Devices.Geolocation;
//using Windows.UI.Xaml.Shapes;
using Windows.UI.Core;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.Rfcomm;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace Casara
{
    public struct ArduinoDataPoint
    {
        public double Latitude;
        public double Longitude;
        public Int32 SignalStrength;
    };

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();
        private GPSDataClass GPS = null;
        private CoreDispatcher WinCoreDispatcher;
        private Int32 MaxIntensity;
        private Int32 MinIntensity;
        private Stopwatch StayTimer;
        private double MapScale;
        private BlueToothClass BTClass;
        private Task ListenTask;
        private List<ArduinoDataPoint> MeasuredSignalStrength;
        private string DataBuffer;

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
            MapScale = 591657;
            BTClass = new BlueToothClass();
            ListenTask = null;

            BTClass.ExceptionOccured += BTClass_OnExceptionOccured;
            BTClass.MessageReceived += BTClass_OnDataReceived;
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
            Esri.ArcGISRuntime.Geometry.MapPoint MapCentre = null;

            if (GPS == null)
                GPS = new GPSDataClass();

            try
            {
                Position = await GPS.GetGPSLocation();
                GPS.LocUpdateThreshold = 10.0;
                MapCentre = new Esri.ArcGISRuntime.Geometry.MapPoint(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84);
                MainMapView.SetView(MapCentre, MapScale);
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

        private void DrawCircle(double Longitude, double Latitude, double Radius, Int32 Intensity)
        {
            Esri.ArcGISRuntime.Geometry.Polygon Poly = null;
            List<Esri.ArcGISRuntime.Geometry.MapPoint> MapPointsList = new List<Esri.ArcGISRuntime.Geometry.MapPoint>();
            int MaxPoints = 16;

            for (int i = 0; i < MaxPoints; i++)
                MapPointsList.Add(new Esri.ArcGISRuntime.Geometry.MapPoint(Longitude + Radius * Math.Cos(i * 3.14 / (MaxPoints / 2)), Latitude + Radius * Math.Sin(i * 3.14 / (MaxPoints / 2)), Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84));

            Poly = new Esri.ArcGISRuntime.Geometry.Polygon(MapPointsList);

            Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = new Esri.ArcGISRuntime.Symbology.SimpleFillSymbol();
            Fill.Color = CalculateIntensityColour(Intensity, 128);
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
            catch (Exception)
            {
                StatusTextBlock.Text += "Map Error\n";
            }

            StatusTextBlock.Text += "DrawCircle done...\n";
        }

        //private void ChangeShapeColour(Esri.ArcGISRuntime.Layers.Graphic Poly, Int32 Intensity)
        //{
        //    Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = (Esri.ArcGISRuntime.Symbology.SimpleFillSymbol)Poly.Symbol;

        //    Fill.Color = Windows.UI.Color.FromArgb(255, (byte)((Intensity & 0x00ff0000) >> 16), (byte)((Intensity & 0x0000ff00) >> 8), (byte)(Intensity & 0x000000ff)); ;
        //    Fill.Style = Esri.ArcGISRuntime.Symbology.SimpleFillStyle.Solid;
        //}

        //private Int32 GetShapeColour(Esri.ArcGISRuntime.Layers.Graphic Poly)
        //{
        //    Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = (Esri.ArcGISRuntime.Symbology.SimpleFillSymbol)Poly.Symbol;

        //    Int32 Colour = (Fill.Color.A << 24) | (Fill.Color.R << 16) | (Fill.Color.G << 8) | (Fill.Color.B);
        //    return Colour;
        //}

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
            //Windows.UI.Color Colour;

            StayTimer.Stop();

            if (StayTimer.ElapsedMilliseconds > MaxIntensity)
                MaxIntensity = (Int32)StayTimer.ElapsedMilliseconds;
            else if (StayTimer.ElapsedMilliseconds < MinIntensity)
                MinIntensity = (Int32)StayTimer.ElapsedMilliseconds;

            //Colour = CalculateIntensityColour((Int32)StayTimer.ElapsedMilliseconds, 128);

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
                    //MainMap.Center = NewCentre;
                    DrawCircle(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, 0.0025, (Int32)StayTimer.ElapsedMilliseconds);
                }
                );
            }
            catch (Exception)
            {
                //StatusTextBlock.Text = "Error in geo position update"; //Cannot access UI thread in this catch statement. May cause a crash if uncommented
            }


            StayTimer.Reset();
            StayTimer.Start();
        }

        //For 100% opacity, pass a value to 255, for 0% pass a value of 0
        private Windows.UI.Color CalculateIntensityColour(Int32 Intensity, byte Opacity)
        {
            Windows.UI.Color ColourValue;
            Int32 Colour;
            Int32 Delta = (0x00ff0000 - 0x000000ff) / 1024;

            Colour = Intensity * Delta + 0x000000ff;

            ColourValue.R = (byte)((Colour & 0x00ff0000) >> 16);
            ColourValue.G = (byte)((Colour & 0x0000ff00) >> 8);
            ColourValue.B = (byte)(Colour & 0x000000ff);

            ColourValue.A = Opacity;

            return ColourValue;
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

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            //try
            //{
            //    if (GPS != null && GPS.GeoLoc != null) //Can this callback get assigned multiple times? That can cause some undefined behaviour!
            //    {
            //        GPS.GeoLoc.PositionChanged += new TypedEventHandler<Geolocator, PositionChangedEventArgs>(GPSPositionChanged);
            //    }
            //}
            //catch(Exception)
            //{
            //    StatusTextBlock.Text = "Error in StartButton_Click";
            //}
            
            //StayTimer.Reset();
            //StayTimer.Start();
            DeviceInformationCollection ConnectedDevices = await BTClass.EnumerateDevices(RfcommServiceId.SerialPort);
            //DeviceInformation ChosenDevice = ConnectedDevices.First(Device => Device.Id.Equals("HC-05"));
            await BTClass.ConnectDevice(ConnectedDevices.First(Device => Device.Name.Equals("HC-05")));
            ListenTask = BTClass.ListenForData();
            StatusTextBlock.Text += "Finished StartButton_Click\n";
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
                StatusTextBlock.Text = "Error in ClearButton_Click";
            }
            StatusTextBlock.Text += "Finished ClearButton_Click\n";
        }

        public void BTClass_OnExceptionOccured(object sender, Exception ex)
        {

        }

        public void BTClass_OnDataReceived(object sender, string message)
        {
            Debug.WriteLine("New Message:" + message);
            try
            {
                if (DataBuffer != null)
                    DataBuffer += message;
                else
                    DataBuffer = message;

                ParseMessage();
                PlotList();
                MeasuredSignalStrength.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception in BTClass_OnDataReceived");
            }
        }

        void ParseMessage()
        {
            string TrimmedMessage;
            string ProcessedString;

            if (DataBuffer.Contains("\n"))
                TrimmedMessage = DataBuffer.Substring(0, DataBuffer.LastIndexOf('\n'));
            else
                TrimmedMessage = DataBuffer;

            string[] DataPointList = TrimmedMessage.Split('\r', '\n');

            foreach (string Str in DataPointList)
            {
                if (!Str.Equals("") && Str.Count(Sep => Sep == ',') == 2)
                {
                    string[] SignalList = Str.Split(',');

                    if (MeasuredSignalStrength != null && !SignalList[0].Equals("") && !SignalList[1].Equals("") && !SignalList[2].Equals(""))
                    {
                        MeasuredSignalStrength.Add(new ArduinoDataPoint
                        {
                            SignalStrength = Convert.ToInt32(SignalList[0]),
                            Latitude = Convert.ToDouble(SignalList[1]),
                            Longitude = Convert.ToDouble(SignalList[2])
                        });

                        if (DataBuffer.Contains(Str + "\r\n"))
                            ProcessedString = Str + "\r\n";
                        else if (DataBuffer.Contains(Str + "\r"))
                            ProcessedString = Str + "\r";
                        else
                            ProcessedString = Str;

                        DataBuffer = DataBuffer.Remove(DataBuffer.IndexOf(ProcessedString), ProcessedString.Length);
                    }
                }
            }
        }

        private void PlotList()
        {
            foreach (ArduinoDataPoint Point in MeasuredSignalStrength)
            {
                if (Point.SignalStrength > MaxIntensity)
                {
                    MaxIntensity = Point.SignalStrength;
                    //UpdateShapeColours(Colour);
                }


                if (Point.SignalStrength < MinIntensity)
                {
                    MinIntensity = Point.SignalStrength;
                    //UpdateShapeColours(Colour);
                }

                DrawCircle(Point.Longitude, Point.Latitude, 0.0025, Point.SignalStrength);
            }

            StatusTextBlock.Text += "Finished plotting\n";
        }
    }
}
