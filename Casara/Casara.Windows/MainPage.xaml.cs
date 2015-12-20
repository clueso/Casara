﻿using Casara.Common;
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
        public bool Plotted;
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
        //private string BaseMapUrl = "http://sampleserver6.arcgisonline.com/arcgis/rest/services/World_Street_Map/MapServer";
        private string BaseMapUrl = "http://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer";
        private ArcGISTiledMapServiceLayer OnlineMapBaseLayer;
        private ArcGISLocalTiledLayer LocalMapBaseLayer;
        private GraphicsLayer DataLayer;
        private StorageFile TilePackageFile;
        private int DefaultRadius;
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
            MapScale = 600000;
            BTClass = new BlueToothClass();
            ListenTask = null;
            DataFolder = ApplicationData.Current.LocalFolder;
            OnlineMapBaseLayer = null;
            LocalMapBaseLayer = null;
            DataLayer = null;
            TilePackageFile = null;
            DefaultRadius = 20;
            SpotSizeTextBox.Text = DefaultRadius.ToString();

            BTClass.ExceptionOccured += BTClass_OnExceptionOccured;
            BTClass.MessageReceived += BTClass_OnDataReceived;
            //auto services = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(
                //RfcommServiceId.ObexObjectPush));
            MeasuredSignalStrength = new List<ArduinoDataPoint>();
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
                BTStopButton.IsEnabled = false;
                //MainMap.Layers.Add(new Esri.ArcGISRuntime.Layers.GraphicsLayer());
                //DrawCircle(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, 20000, 0x00ffffff);
            }
            catch(Exception)
            {
                StatusTextBox.Text = "Error in navigationHelper_LoadState!\n";
            }

            StatusTextBox.Text += "State Loaded...\n";
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

                //Esri.ArcGISRuntime.Layers.GraphicsLayer test = MainMapView.Map.Layers["ShapeLayer"] as Esri.ArcGISRuntime.Layers.GraphicsLayer;
                //test.Graphics.Add(Graphic);
                if (DataLayer != null)
                    DataLayer.Graphics.Add(Graphic);
            }
            catch(Exception)
            {
                StatusTextBox.Text += "Map Error\n";
            }

            StatusTextBox.Text += "DrawCircle done...\n";
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
                StatusTextBox.Text += "Error in StartButton_Click!\n";
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
                    //DrawCircle(Position.Coordinate.Point.Position.Longitude, Position.Coordinate.Point.Position.Latitude, 200, 0x00ff0000);
                }
                );
            }
            catch(Exception)
            {
                                
            }
            
        }

        private byte CalculateGreen(int Intensity)
        {
            if (Intensity < 128)
                return 0;
            else if (Intensity >= 128 && Intensity < 384)
                return (byte)(Intensity - 128);
            else if (Intensity >= 384 && Intensity < 640)
                return 255;
            else if (Intensity >= 640 && Intensity < 896)
                return (byte)(255 - (Intensity - 640));
            else
                return 0;
        }

        //For 100% opacity, pass a value to 255, for 0% pass a value of 0
        private Windows.UI.Color CalculateIntensityColour(Int32 Intensity, byte Opacity)
        {
            Windows.UI.Color ColourValue;
            ColourValue.R = CalculateGreen(Intensity - 256);
            ColourValue.G = CalculateGreen(Intensity);
            ColourValue.B = CalculateGreen(Intensity + 256);
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

        private void ClearPoints(bool ClearData)
        {
            try
            {
                Esri.ArcGISRuntime.Layers.GraphicsLayer GraphLayer = MainMapView.Map.Layers["ShapeLayer"] as Esri.ArcGISRuntime.Layers.GraphicsLayer;
                Esri.ArcGISRuntime.Layers.GraphicCollection GraphicsList = GraphLayer.Graphics;
                GraphicsList.Clear();
                if (ClearData == true)
                    MeasuredSignalStrength.Clear();
            }
            catch (Exception)
            {
                StatusTextBox.Text = "error in ClearButton_Click";
            }
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearPoints(true);          
        }

        private string CreateFileName()
        {
            string FileName;

            FileName = "Datafile_" + DateTime.Now.ToString() + ".txt";
            FileName = FileName.Replace(' ', '_');
            FileName = FileName.Replace(':', '_');
            FileName = FileName.Replace('-', '_');

            return FileName;
        }

        private async void BTStarted_Clicked(object sender, RoutedEventArgs e)
        {
            bool BTDeviceFound = false;

            if (DataFolder != null)
            {
                try
                {
                    DataFile = await DataFolder.CreateFileAsync(CreateFileName(), CreationCollisionOption.ReplaceExisting);
                    await Windows.Storage.FileIO.WriteTextAsync(DataFile, "New session started " + DateTime.Now.ToString() + "\r\n");
                }
                catch(Exception)
                {
                    Debug.WriteLine("Error opening file!");
                }
            }

            DeviceInformationCollection ConnectedDevices = await BTClass.EnumerateDevices(RfcommServiceId.SerialPort);
            //Ashwin BT = HC-05
            //Daniel BT = RNBT-6971
            foreach(DeviceInformation DevInfo in ConnectedDevices)
            {
                if (DevInfo.Name.Equals("HC-05") || DevInfo.Name.Equals("RNBT-6971"))
                {
                    await BTClass.ConnectDevice(DevInfo);
                    BTDeviceFound = true;
                    break;
                }
            }
            
            if(!BTDeviceFound)
            {
                StatusTextBox.Text += "No known BT device found...stopping\n";
                return;
            }

            ListenTask = BTClass.ListenForData();
            BTStartButton.IsEnabled = false;
            BTStopButton.IsEnabled = true;
            //DataBuffer = "100,49.26,-123.30\r\n20,49.25,-123.14\r\n300,49.25,-123.13\r\n600,49.26,-123.14\r\n1000,49.24,-123.14\r\n128,49.26";
            //ParseMessage();
            //PlotList();
        }

        private void BTStop_Clicked(object sender, RoutedEventArgs e)
        {
            BTClass.DisconnectDevice();
            BTStartButton.IsEnabled = true;
            BTStopButton.IsEnabled = false;
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
                
                //MeasuredSignalStrength.Clear();                
            }
            catch(Exception)
            {
                Debug.WriteLine("Exception in BTClass_OnDataReceived");
            }
            
        }

        void ParseMessage()
        {
            string TrimmedMessage;
            int SubStringIndex;
            
            SubStringIndex = DataBuffer.LastIndexOf('\n');
            if(SubStringIndex < 0)
                TrimmedMessage = DataBuffer;
            else
                TrimmedMessage = DataBuffer.Substring(0, SubStringIndex);

            DataBuffer = DataBuffer.Remove(0, TrimmedMessage.Length - 1);

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
                            Radius = DefaultRadius,
                            Plotted = false
                        });

                        //SubStringLength = Str.Length;
                        //SubStringIndex = DataBuffer.IndexOf(Str);

                        //if (DataBuffer[SubStringIndex + SubStringLength + 1] == '\r')
                        //    SubStringLength = SubStringLength + 1;
                        //if (DataBuffer[SubStringIndex + SubStringLength + 1] == '\n')
                        //    SubStringLength = SubStringLength + 1;

                        //if(DataBuffer.Contains(Str+"\r\n"))
                        //    ProcessedString = Str + "\r\n";
                        //else if(DataBuffer.Contains(Str+"\r"))
                        //    ProcessedString = Str + "\r";
                        //else
                        //    ProcessedString = Str;

                        //DataBuffer = DataBuffer.Remove(SubStringIndex, SubStringLength);
                    }                                                      
                }                
            }

            //if(DataBuffer.Contains("\n"))
            //    DataBuffer = DataBuffer.Remove(0, DataBuffer.LastIndexOf('\n') + 1);
            StatusTextBox.Text += "Done Parsing: " + MeasuredSignalStrength.Count.ToString() + " Points.\n";
        }

        private async Task PlotList()
        {
            ArduinoDataPoint Point;
            int Index;
            int i;
                        
            for (i = 0; i < MeasuredSignalStrength.Count; i++)
            {
                Point = MeasuredSignalStrength[i];

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

                if (Point.Plotted == false)
                {
                    DrawCircle(Point.Longitude, Point.Latitude, Point.Radius, Point.SignalStrength);
                    Point.Plotted = true;
                    MeasuredSignalStrength[i] = Point;
                    if (DataFile != null)
                        await Windows.Storage.FileIO.AppendTextAsync(DataFile, Point.SignalStrength.ToString() + ","
                                + Point.Latitude.ToString("#.00000") + "," + Point.Longitude.ToString("#.00000") + "\r\n");
                }
            }

            StatusTextBox.Text += "Finished plotting\n";
        }

        private void creationProgress_ProgressChanged(Object sender, ExportTileCacheJob p)
        {
            String TextBlockContent = StatusTextBox.Text;

            foreach (var m in p.Messages)
            {
                if (m.Description.Contains("Executing..."))
                {
                    StatusTextBox.Text = TextBlockContent + "Starting cache generation...\n";
                }
                // find messages with percent complete
                // "Finished:: 9 percent", e.g.
                if (m.Description.Contains("Finished::"))
                {
                    // parse out the percentage complete and update the progress bar
                    var numString = m.Description.Substring(m.Description.IndexOf("::") + 2, 3).Trim();
                    var pct = 0.0;
                    if (double.TryParse(numString, out pct))
                    {
                        try
                        {
                            TextBlockContent = StatusTextBox.Text.Remove(StatusTextBox.Text.IndexOf("Caching..."));
                        }
                        catch (Exception)
                        {
                            //Empty handler to handle exception for the first time the try block is executed.
                        }

                        StatusTextBox.Text = TextBlockContent + "Caching..." + pct.ToString() + "% complete\n";
                    }
                }
            }
        }

        private void downloadProgress_ProgressChanged(Object sender, ExportTileCacheDownloadProgress p)
        {
            double DownloadCompletePct;
            String TextBlockContent = StatusTextBox.Text;
            try
            {
                TextBlockContent = StatusTextBox.Text.Remove(StatusTextBox.Text.IndexOf("Downloading..."));
            }
            catch(Exception)
            {
                //Empty handler to handle exception for the first time the try block is executed.
            }

            DownloadCompletePct = Math.Round(p.ProgressPercentage * 100);
            StatusTextBox.Text = TextBlockContent + "Downloading...\n" + DownloadCompletePct.ToString() + "% complete\n";
        }

        private async void onDownloadClick(object sender, RoutedEventArgs e)
        {
            ExportTileCacheTask ExportTask = new ExportTileCacheTask(new Uri(BaseMapUrl));
            GenerateTileCacheParameters generateOptions = new GenerateTileCacheParameters();
            DownloadTileCacheParameters downloadOptions = new DownloadTileCacheParameters(ApplicationData.Current.TemporaryFolder);
            TimeSpan checkInterval = TimeSpan.FromSeconds(1);
            CancellationToken token = new CancellationToken();
            double CurrMapScale = MainMapView.Scale;

            StatusTextBox.Text += "Downloading tile cache...\n";

            //Tile Cache generation progress
            Progress<ExportTileCacheJob> creationProgress = new Progress<ExportTileCacheJob>();
            creationProgress.ProgressChanged += creationProgress_ProgressChanged;

            //Download progress 
            Progress<ExportTileCacheDownloadProgress> downloadProgress = new Progress<ExportTileCacheDownloadProgress>();
            downloadProgress.ProgressChanged += downloadProgress_ProgressChanged;
            
                       
            // overwrite the file if it already exists
            downloadOptions.OverwriteExistingFiles = true;

            generateOptions.Format = ExportTileCacheFormat.TilePackage;
            generateOptions.CompressTileCache = false;
            generateOptions.GeometryFilter = MainMapView.Extent; //new Envelope(15.757,73.139,14.680,74.772,Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84);
            generateOptions.MinScale = CurrMapScale;
            generateOptions.MaxScale = 1.0;
            
            // generate the tiles and download them 
            try
            {
                var result = await ExportTask.GenerateTileCacheAndDownloadAsync(generateOptions,
                                                                     downloadOptions,
                                                                     checkInterval,
                                                                     token,
                                                                     creationProgress,
                                                                     downloadProgress);
            }
            catch(Exception)
            {
                StatusTextBox.Text += "Downloading Tile Package failed...stopping\n";
                return;
            }
            
            if (LocalMapBaseLayer == null)
            {
                TilePackageFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync("World_Street_Map.tpk");
                LocalMapBaseLayer = new ArcGISLocalTiledLayer(TilePackageFile);
                LocalMapBaseLayer.MaxScale = 1.0;
                LocalMapBaseLayer.MinScale = CurrMapScale;
                await LocalMapBaseLayer.InitializeAsync();
            }
            
            if (LocalMapBaseLayer != null && LocalMapBaseLayer.InitializationException == null)
            {
                StatusTextBox.Text += "Download finished.\n";
                OnlineRadioButton.IsEnabled = true;
                LocalRadioButton.IsEnabled = true;
                MapScale = MainMapView.Scale;
            }                
            else
            {
                StatusTextBox.Text += "Download failed.\n";
            }    
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
                DataLayer.Opacity = 1;
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
                    StatusTextBox.Text += "Something wrong adding Datalayer.\n";
                }
            }
            else
            {
                StatusTextBox.Text += "Something wrong in BaseLayer\n";
            }
        }

        private void OnlineRadioButtonChecked(object sender, RoutedEventArgs e)
        {
            if(OnlineMapBaseLayer != null && OnlineMapBaseLayer.InitializationException == null)
            {
                MainMapView.Map.Layers.Clear();
                MainMapView.Map.Layers.Add(OnlineMapBaseLayer);
                if (DataLayer != null && DataLayer.InitializationException == null)
                    MainMapView.Map.Layers.Add(DataLayer);
                MainMapView.MinScale = Double.NaN;
                MainMapView.MaxScale = 1;
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
                MainMapView.MinScale = MapScale;
                MainMapView.MaxScale = 1;
            }
        }

        private void UpdateSpotSize()
        {
            int i;
            ArduinoDataPoint tmp;

            for (i = 0; i < MeasuredSignalStrength.Count; i++)
            {
                tmp = MeasuredSignalStrength[i];
                tmp.Radius = DefaultRadius;
                tmp.Plotted = false;
                MeasuredSignalStrength[i] = tmp;
            }
        }

        private async void MainPage_onSpotSizeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DefaultRadius = Convert.ToInt32(SpotSizeTextBox.Text);
                UpdateSpotSize();
                ClearPoints(false);
                await PlotList();
            }
            catch(Exception)
            {
                StatusTextBox.Text += "Invalid number\n";
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
        //    StatusTextBox.Text += "Added circles...\n";
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
