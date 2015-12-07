using Casara.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking;
using Windows.Devices.Enumeration;
//using Microsoft.Maps.SpatialToolbox.Bing;
//using Microsoft.Maps.SpatialToolbox.IO;
//using Microsoft.Maps.SpatialToolbox;
using Esri.ArcGISRuntime.Layers;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Controls;
using Casara;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace Casara
{
    public struct ArduinoDataPoint
    {
        public double Latitude;
        public double Longitude;
        public Int32 SignalStrength;
        public double Radius;
    };

    /// <summary>
    /// A basic page that provides characteristics common to most applications.
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
        private StorageFolder DataFolder;
        private StorageFile DataFile;
        private Int32 FileCounter;
        private string BaseMapUrl = "http://sampleserver6.arcgisonline.com/arcgis/rest/services/World_Street_Map/MapServer"; //"http://tiledbasemaps.arcgis.com/arcgis/rest/services/World_Topo_Map/MapServer";
        private ArcGISTiledMapServiceLayer OnlineMapBaseLayer;
        private ArcGISLocalTiledLayer LocalMapBaseLayer;
        private GraphicsLayer DataLayer;
        private StorageFile TilePackageFile;
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
            BTClass = new BlueToothClass();
            ListenTask = null;
            DataFolder = ApplicationData.Current.LocalFolder;
            OnlineMapBaseLayer = null;
            LocalMapBaseLayer = null;
            DataLayer = null;
            TilePackageFile = null;

            BTClass.ExceptionOccured += BTClass_OnExceptionOccured;
            BTClass.MessageReceived += BTClass_OnDataReceived;
            //auto services = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(
                //RfcommServiceId.ObexObjectPush));
            MeasuredSignalStrength = new List<ArduinoDataPoint>();
            FileCounter = 0;
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
                //DrawCircle(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, 20000, 0x00ffffff);
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
            //BTClass.DisconnectDevice();
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
        
        private double haversine(double angle)
        {
            return (1 - Math.Cos(angle))/2;
        }

        private void CalculateCircleVertices(List<Esri.ArcGISRuntime.Geometry.MapPoint> PointList, double Longitude, double Latitude, double Radius)
        {
            int i, Index, IndexInc;
            double dLat, dLong;
            int MaxPoints = 16; //Should always be a factor of 4
            double EarthRadius = 6400000.0; //Earth's radius in m
            int YFactor = 1;

            for (i = 0, Index = 0, IndexInc = 1; i < MaxPoints; i++, Index += IndexInc)
            {
                if (i == (MaxPoints/4) + 1)
                {
                    Index = (MaxPoints/4) - 1;
                    IndexInc = -1;
                    YFactor = -1;
                }

                if(i == ((MaxPoints * 3)/4) + 1)
                {
                    Index = -(MaxPoints/4) + 1;
                    IndexInc = 1;
                    YFactor = 1;
                }

                //Arguments to trig functions should be in radians, hence all the Math.PI and 180 shenanigans...
                dLat = Index * (Radius / EarthRadius) / (MaxPoints / 4);  //in radians
                dLong = YFactor * Math.Acos(1.0 - (haversine(Radius / EarthRadius)
                                            - haversine(Math.Abs(dLat)))
                                            / (Math.Cos(Math.PI * Latitude/180) * Math.Cos(Math.PI * Latitude/180 + dLat))
                                            * 2);
                PointList.Add(new Esri.ArcGISRuntime.Geometry.MapPoint(Longitude + dLong * 180/Math.PI, Latitude + dLat *  180/Math.PI, Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84));
            }
        }

        //Radius is in metres
        private void DrawCircle(double Longitude, double Latitude, double Radius, Int32 Intensity)
        {
            Esri.ArcGISRuntime.Geometry.Polygon Poly = null;
            List<Esri.ArcGISRuntime.Geometry.MapPoint> MapPointsList = new List<Esri.ArcGISRuntime.Geometry.MapPoint>();

            CalculateCircleVertices(MapPointsList, Longitude, Latitude, Radius);

            Poly = new Esri.ArcGISRuntime.Geometry.Polygon(MapPointsList);

            Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = new Esri.ArcGISRuntime.Symbology.SimpleFillSymbol();
            Fill.Color = CalculateIntensityColour(Intensity, 255);
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
            catch(Exception)
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

        //private void UpdateShapeColours(Int32 Change)
        //{
        //    Esri.ArcGISRuntime.Layers.GraphicsLayer GraphLayer = (Esri.ArcGISRuntime.Layers.GraphicsLayer)MainMapView.Map.Layers["ShapeLayer"];
        //    Esri.ArcGISRuntime.Layers.GraphicCollection GraphicsList = GraphLayer.Graphics;
        //    byte A, R = (byte)((Change & 0x00ff0000) >> 16), G = (byte)((Change & 0x0000ff00) >> 8), B = (byte)(Change & 0x000000ff);
        //    int i;

        //    for (i = 0; i < GraphicsList.Count; i++)
        //    {
        //        Esri.ArcGISRuntime.Symbology.SimpleFillSymbol Fill = (Esri.ArcGISRuntime.Symbology.SimpleFillSymbol)GraphicsList[i].Symbol;

        //        A = Fill.Color.A;
        //        R += Fill.Color.R;
        //        G += Fill.Color.G;
        //        B += Fill.Color.B;
        //        Fill.Color = Windows.UI.Color.FromArgb(A, R, G, B);
        //    }
        //}

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
                    DrawCircle(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, 200, 0x00ff0000);
                }
                );
            }
            catch(Exception)
            {
                                
            }
            
        }

        //For 100% opacity, pass a value to 255, for 0% pass a value of 0
        private Windows.UI.Color CalculateIntensityColour(Int32 Intensity, byte Opacity)
        {
            Windows.UI.Color ColourValue;
            //Int32 Colour;
            //Int32 Delta = (0x00ff0000 - 0x000000ff) / 1024;

            //Colour = Intensity * Delta + 0x000000ff;

            //ColourValue.R = (byte)((Colour & 0x00ff0000) >> 16);
            //ColourValue.G = (byte)((Colour & 0x0000ff00) >> 8);
            //ColourValue.B = (byte)(Colour & 0x000000ff);
            if (Intensity < 205)
                ColourValue = Windows.UI.Colors.Blue;
            else if (Intensity >= 205 && Intensity < 410)
                ColourValue = Windows.UI.Colors.Green;
            else if (Intensity >= 410 && Intensity < 615)
                ColourValue = Windows.UI.Colors.GreenYellow;
            else if (Intensity >= 615 && Intensity < 820)
                ColourValue = Windows.UI.Colors.Yellow;
            else
                ColourValue = Windows.UI.Colors.Red;

            ColourValue.A = Opacity;

            return ColourValue;
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

        private async void BTStarted_Clicked(object sender, RoutedEventArgs e)
        {
            if (DataFolder != null)
            {
                try
                {
                    DataFile = await DataFolder.CreateFileAsync("DataFile_" + FileCounter.ToString() + ".txt", CreationCollisionOption.ReplaceExisting);
                    await Windows.Storage.FileIO.WriteTextAsync(DataFile, "New session started" + DateTime.Now.ToString() + "\r\n");
                    FileCounter += 1;
                }
                catch(Exception)
                {
                    Debug.WriteLine("Error opening file!");
                }
            }

            DeviceInformationCollection ConnectedDevices = await BTClass.EnumerateDevices(RfcommServiceId.SerialPort);
            //DeviceInformation ChosenDevice = ConnectedDevices.First(Device => Device.Id.Equals("HC-05"));
            await BTClass.ConnectDevice(ConnectedDevices.First(Device => Device.Name.Equals("HC-05")));
            ListenTask = BTClass.ListenForData();
            //DataBuffer = "100,49.26,-123.30\r\n20,49.25,-123.14\r\n300,49.25,-123.13\r\n600,49.26,-123.14\r\n1000,49.24,-123.14\r\n128,49.26";
            //ParseMessage();
            //PlotList();
        }

        private void BTStop_Clicked(object sender, RoutedEventArgs e)
        {
            BTClass.DisconnectDevice();
            if (DataFile != null)
                DataFile = null;
            //DataBuffer += ",-123.17\r\n100,49.26,-123.30\r\n20,49.25,-123.14\r\n300,49.25,-123.13\r\n";
            //ParseMessage();
            //PlotList();
        }

        public void BTClass_OnExceptionOccured(object sender, Exception ex)
        {

        }

        public async void BTClass_OnDataReceived(object sender, string message)
        {
            Debug.WriteLine("New Message:" + message);

            try
            {
                if (DataBuffer != null)
                    DataBuffer += message;
                else
                    DataBuffer = message;

                ParseMessage();
                await PlotList();

                try
                {
                    LatitudeBox.Text = "Latitude = " + MeasuredSignalStrength[MeasuredSignalStrength.Count - 1].Latitude.ToString();
                    LongitudeBox.Text = "Longitude = " + MeasuredSignalStrength[MeasuredSignalStrength.Count - 1].Longitude.ToString();
                    SignalStrengthTextBox.Text = "Signal = " + MeasuredSignalStrength[MeasuredSignalStrength.Count - 1].SignalStrength.ToString();
                }
                catch(Exception)
                {
                    Debug.WriteLine("Exception in BTClass_OnDataReceived:setting text boxes");
                }
                
                MeasuredSignalStrength.Clear();                
            }
            catch(Exception)
            {
                Debug.WriteLine("Exception in BTClass_OnDataReceived");
            }
            
        }

        void ParseMessage()
        {
            string TrimmedMessage;
            string ProcessedString;

            if(DataBuffer.Contains("\n"))
                TrimmedMessage = DataBuffer.Substring(0, DataBuffer.LastIndexOf('\n'));
            else
                TrimmedMessage = DataBuffer;

            string[] DataPointList = TrimmedMessage.Split('\r', '\n');

            foreach(string Str in DataPointList)
            {
                if(!Str.Equals("") && Str.Count(Sep => Sep ==',') == 2)
                {
                    string[] SignalList = Str.Split(',');

                    if (MeasuredSignalStrength != null && !SignalList[0].Equals("") && !SignalList[1].Equals("") && !SignalList[2].Equals(""))
                    {
                        MeasuredSignalStrength.Add(new ArduinoDataPoint
                        {
                            SignalStrength = Convert.ToInt32(SignalList[0]),
                            Latitude = Convert.ToDouble(SignalList[1]),
                            Longitude = Convert.ToDouble(SignalList[2]),
                            Radius = 200.0
                        });

                        if(DataBuffer.Contains(Str+"\r\n"))
                            ProcessedString = Str + "\r\n";
                        else if(DataBuffer.Contains(Str+"\r"))
                            ProcessedString = Str + "\r";
                        else
                            ProcessedString = Str;

                        DataBuffer = DataBuffer.Remove(DataBuffer.IndexOf(ProcessedString), ProcessedString.Length);                            
                    }                                                      
                }                
            }

            //if(DataBuffer.Contains("\n"))
            //    DataBuffer = DataBuffer.Remove(0, DataBuffer.LastIndexOf('\n') + 1);
            StatusTextBlock.Text += "Done Parsing: " + MeasuredSignalStrength.Count.ToString() + " Points.\n";
        }

        private async Task PlotList()
        {
            foreach(ArduinoDataPoint Point in MeasuredSignalStrength)
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

                DrawCircle(Point.Longitude, Point.Latitude, Point.Radius, Point.SignalStrength);
                if (DataFile != null)
                    await Windows.Storage.FileIO.AppendTextAsync(DataFile, Point.SignalStrength.ToString() + ","
                            + Point.Latitude.ToString("#.00000") + "," + Point.Longitude.ToString("#.00000") + "\r\n");

            }

            StatusTextBlock.Text += "Finished plotting\n";
        }

        private async void onDownloadClick(object sender, RoutedEventArgs e)
        {
            //string BaseMapUrl = "http://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer";
            ExportTileCacheTask ExportTask = new ExportTileCacheTask(new Uri(BaseMapUrl));
            GenerateTileCacheParameters generateOptions = new GenerateTileCacheParameters();
            DownloadTileCacheParameters downloadOptions = new DownloadTileCacheParameters(ApplicationData.Current.TemporaryFolder);
            TimeSpan checkInterval = TimeSpan.FromSeconds(2);
            CancellationToken token = new CancellationToken();
                       
            // overwrite the file if it already exists
            downloadOptions.OverwriteExistingFiles = true;

            generateOptions.Format = ExportTileCacheFormat.TilePackage;
            generateOptions.GeometryFilter = MainMapView.Extent; //new Envelope(15.757,73.139,14.680,74.772,Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84);
            generateOptions.MinScale = 6000000.0;
            generateOptions.MaxScale = 1.0;

            StatusTextBlock.Text += "Downloading tile cache...\n";
            // generate the tiles and download them 
            try
            {
                var result = await ExportTask.GenerateTileCacheAndDownloadAsync(generateOptions,
                                                                     downloadOptions,
                                                                     checkInterval,
                                                                     token,
                                                                     null,
                                                                     null);
            }
            catch(Exception)
            {
              
            }
            

            if (LocalMapBaseLayer == null)
            {
                TilePackageFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync("World_Street_Map.tpk");
                LocalMapBaseLayer = new ArcGISLocalTiledLayer(TilePackageFile);
                await LocalMapBaseLayer.InitializeAsync();   
            }
            if (LocalMapBaseLayer.InitializationException == null)
                StatusTextBlock.Text += "Download finished.\n";
            else
                StatusTextBlock.Text += "Download failed.\n";

            
        }

        private async void onMainMapViewLoaded(object sender, RoutedEventArgs e)
        {
            if (OnlineMapBaseLayer == null)
            {
                OnlineMapBaseLayer = new ArcGISTiledMapServiceLayer(new Uri(BaseMapUrl));
                await OnlineMapBaseLayer.InitializeAsync();
            }

            if (DataLayer == null)
            {
                DataLayer = new GraphicsLayer();
                DataLayer.ID = "ShapeLayer";
                DataLayer.Opacity = 0.5;
                await DataLayer.InitializeAsync();
            }

            if (OnlineMapBaseLayer != null && OnlineMapBaseLayer.InitializationException == null)
            {
                MainMapView.Map.Layers.Add(OnlineMapBaseLayer);
                if (DataLayer != null && DataLayer.InitializationException == null)
                {
                    MainMapView.Map.Layers.Add(DataLayer);
                }
                else
                {
                    StatusTextBlock.Text += "Something wrong adding Datalayer.\n";
                }
            }
            else
            {
                StatusTextBlock.Text += "Something wrong in BaseLayer\n";
            }
            
            //if (LocalMapBaseLayer == null)
            //{
            //    TilePackageFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync("World_Street_Map.tpk");
            //    LocalMapBaseLayer = new ArcGISLocalTiledLayer(TilePackageFile);
            //    await LocalMapBaseLayer.InitializeAsync();
            //}
            //if (LocalMapBaseLayer.InitializationException == null)
            //    StatusTextBlock.Text += "Download finished.\n";
            //else
            //    StatusTextBlock.Text += "Download failed.\n";
            //if (LocalMapBaseLayer != null && LocalMapBaseLayer.InitializationException == null)
            //{
            //    MainMapView.Map.Layers.Clear();
            //    MainMapView.Map.Layers.Add(LocalMapBaseLayer);
            //    if (DataLayer != null && DataLayer.InitializationException == null)
            //        MainMapView.Map.Layers.Add(DataLayer);
            //}

        }

        private void OnlineRadioButtonChecked(object sender, RoutedEventArgs e)
        {
            if(OnlineMapBaseLayer != null && OnlineMapBaseLayer.InitializationException == null)
            {
                MainMapView.Map.Layers.Clear();
                MainMapView.Map.Layers.Add(OnlineMapBaseLayer);
                if (DataLayer != null && DataLayer.InitializationException == null)
                    MainMapView.Map.Layers.Add(DataLayer);
            }
        }

        private void LocalRadioButtonChecked(object sender, RoutedEventArgs e)
        {
            if (LocalMapBaseLayer != null && LocalMapBaseLayer.InitializationException == null)
            {
                MainMapView.Map.Layers.Clear();
                MainMapView.Map.Layers.Add(LocalMapBaseLayer);
                if (DataLayer != null && DataLayer.InitializationException == null)
                    MainMapView.Map.Layers.Add(DataLayer);
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
