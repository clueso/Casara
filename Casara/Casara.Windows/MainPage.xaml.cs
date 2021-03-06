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
using Windows.Storage.Pickers;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking;
using Windows.Devices.Enumeration;
using Windows.System.Display;
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
        public Int32 SignalStrength;
        public double Latitude;
        public double Longitude;
        public double Radius;
        public double Altitude;
        public bool Plotted;
    };

    public delegate void UIUpdateDelegate(object o);
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
        private List<ArduinoDataPoint> ParsedDataPoints;
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
        private DisplayRequest ActiveDisplay;
        private bool DisplayRequested;
        private readonly SynchronizationContext synchronizationContext;
        private DeviceInformationCollection ConnectedBTDevices;
        private string ActiveBTDevice;
        private int LastPlottedPoint;
        private const int GPSMessageNumOfFields = 7;

        //Arduino data indices
        private const int MeanAudioIndex = 5;
        private const int BatteryStatusIndex = 6;
        private const int DirectionIndex = 4;
        private const int SignalStrengthIndex = 0;
        private const int LatitudeIndex = 1;
        private const int LongitudeIndex = 2;
        private const int AltitudeIndex = 3;
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
            DefaultRadius = 200;
            LastPlottedPoint = 0;
            SpotSizeTextBox.Text = DefaultRadius.ToString();

            BTClass.ExceptionOccured += BTClass_OnExceptionOccured;
            BTClass.MessageReceived += BTClass_OnDataReceived;
            ParsedDataPoints = new List<ArduinoDataPoint>();
            synchronizationContext = SynchronizationContext.Current;
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

                MapCentre = new Esri.ArcGISRuntime.Geometry.MapPoint(Position.Coordinate.Point.Position.Longitude,
                                                                     Position.Coordinate.Point.Position.Latitude,
                                                                     Esri.ArcGISRuntime.Geometry.SpatialReferences.Wgs84);
                MainMapView.SetView(MapCentre, MapScale);
                GPS.LocUpdateThreshold = 10.0;
                BTStopButton.IsEnabled = false;
                BTStartButton.IsEnabled = true;
                DisplayRequested = false;
                FileSaveLocTextBox.Text = DataFolder.Path;

                ConnectedBTDevices = await BTClass.EnumerateDevices(RfcommServiceId.SerialPort);
                foreach (DeviceInformation DevInfo in ConnectedBTDevices)
                {
                    BTDeviceList.Items.Add(DevInfo.Name);
                }
                if (BTDeviceList.Items.Count > 0)
                    BTDeviceList.SelectedIndex = 0;
                await ConnectBT();
            }
            catch(Exception ex)
            {
                StatusTextBox.Text = "navigationHelper_LoadState exception:" + ex.Message + "\n";
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
        private async void navigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            if (GPS.GeoLoc != null) //Can this callback get assigned multiple times? That can cause some undefined behaviour!
                GPS.GeoLoc.PositionChanged -= new TypedEventHandler<Geolocator, PositionChangedEventArgs>(GPSPositionChanged);
            if (BTClass.IsBTConnected == true)
            {
                await DisconnectBT();
            }
 
            if (ActiveDisplay != null && DisplayRequested == true)
            {
                ActiveDisplay.RequestRelease();
                DisplayRequested = false;
            }

            if (DataFile != null)
                DataFile = null;
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
            
            try
            {
                if (MainMap.Layers.Count == 0)
                    MainMap.Layers.Add(new Esri.ArcGISRuntime.Layers.GraphicsLayer());

                if (DataLayer != null)
                    DataLayer.Graphics.Add(Graphic);
            }
            catch(Exception e)
            {
                Debug.WriteLine("DrawCircle exception:" + e.Message);
            }
        }

        private void ClearTextBoxes()
        {
            LatitudeBox.Text = "Latitude = ";
            LongitudeBox.Text = "Longitude = ";
            AccuracyBox.Text = "Accuracy = ";
            SignalStrengthTextBox.Text = "Signal = ";
            BatteryStrengthTextBox.Text = "Battery = ";
            LH16SignalStrengthBox.Text = "LH16 Signal = ";
            StatusTextBox.Text = "";
        }
        
        // AddToPlotList() is called from two places, when the data is read from the Arduino and when 
        // a previously saved data file is reloaded. The first case has more data than the second, 
        // because not all data is saved in the file. Take care that the indices used in this function
        // do not point to data which is absent in the data list. Ideally, we will always keep the saved
        // data at the head of the list to avoid this problem.
        private void AddToPlotList(string[] SignalList)
        {
            if (SignalList.Length < 4)
                return;

            if (ParsedDataPoints != null && !SignalList[SignalStrengthIndex].Equals("") && !SignalList[LatitudeIndex].Equals("") && !SignalList[LongitudeIndex].Equals(""))
            {
                //Point Data
                // 0 - Battery, 1 - Mean audio, 2 - Max Audio, 3 - Mean strength, 4 - Direction, 5 & 6 - GPS
                // 0 - Battery, 1 - Mean audio, 2 - Mean strength, 3 - Direction, 4 & 5 - GPS
                ParsedDataPoints.Add(new ArduinoDataPoint
                {
                    SignalStrength = Convert.ToInt32(SignalList[SignalStrengthIndex]),
                    Latitude = Convert.ToDouble(SignalList[LatitudeIndex]),
                    Longitude = Convert.ToDouble(SignalList[LongitudeIndex]),
                    Radius = DefaultRadius,
                    Altitude = Convert.ToDouble(SignalList[AltitudeIndex]),
                    Plotted = false
                });
            }
        }

        private async void LoadSavedFile()
        {
            string[] SignalList;
            string str;

            FileOpenPicker FilePicker = new FileOpenPicker();
            FilePicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            FilePicker.FileTypeFilter.Add(".txt");
            
            StorageFile file = await FilePicker.PickSingleFileAsync();
            if (file == null)
                return;

            Stream s = await file.OpenStreamForReadAsync();
            StreamReader sr = new StreamReader(s);
            while ((str = sr.ReadLine()) != null)
            {
                if (str[0] == '#')
                    continue;
                SignalList = str.Split(',');
                AddToPlotList(SignalList);
            }
            await PlotList(0);
            sr.Dispose();
            s.Dispose();
        }

        private void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            ClearPoints(true);
            ClearTextBoxes();
            LoadSavedFile();
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
                }
                );
            }
            catch(Exception ex)
            {
                Debug.WriteLine("GPSPositionChanged exception:" + ex.Message);
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

        //For 100% opacity, pass a value of 255, for 0% pass a value of 0
        //Colour profiles for the RGB components are translations of the G component.
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
                {
                    ParsedDataPoints.Clear();
                }
            }
            catch (Exception)
            {
                StatusTextBox.Text = "error in ClearButton_Click";
            }
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearPoints(true);
            ClearTextBoxes();
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

        private async Task DisconnectBT()
        {
            BTClass.StartDisconnectProcess();
            await ListenTask;
            BTClass.DisconnectDevice();
        }

        private async Task OpenFile()
        {
            if (DataFolder != null)
            {
                try
                {
                    DataFile = await DataFolder.CreateFileAsync(CreateFileName(), CreationCollisionOption.ReplaceExisting);
                    await Windows.Storage.FileIO.WriteTextAsync(DataFile, "# New session started " + DateTime.Now.ToString() + "\r\n");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("OpenFile: Error opening file!" + ex.Message);
                }
            }
        }

        private async Task ConnectBT()
        {
            bool BTDeviceFound = false;

            
            //Ashwin BT = HC-05, Daniel BT = RNBT-6971
            foreach (DeviceInformation DevInfo in ConnectedBTDevices)
            {
                if (DevInfo.Name.Equals(BTDeviceList.SelectedItem.ToString()))
                {
                    await BTClass.ConnectDevice(DevInfo);
                    BTDeviceFound = true;
                    ActiveBTDevice = BTDeviceList.SelectedItem.ToString();
                    break;
                }
            }

            if (!BTDeviceFound)
            {
                StatusTextBox.Text += "Selected Bluetooth device not found...stopping\n";
                return;
            }

            //Request active display so the device does not hibernate. If we want to allow
            //hibernation, we'll have to handle disconnecting/reconnecting the BT and handling
            //the traffic in the interim.
            if (ActiveDisplay == null)
                ActiveDisplay = new DisplayRequest();

            if (ActiveDisplay != null && DisplayRequested == false)
            {
                ActiveDisplay.RequestActive();
                DisplayRequested = true;
            }

            ListenTask = BTClass.ListenForData();
        }

        private async void BTStarted_Clicked(object sender, RoutedEventArgs e)
        {
            await OpenFile();
            BTClass.StartDataDisplay();
            BTStartButton.IsEnabled = false;
            BTStopButton.IsEnabled = true;
            LoadFileButton.IsEnabled = false;
            FileSaveLocButton.IsEnabled = false;
        }

        private void BTStop_Clicked(object sender, RoutedEventArgs e)
        {
            BTClass.StopDataDisplay();
            BTStartButton.IsEnabled = true;
            BTStopButton.IsEnabled = false;
            LoadFileButton.IsEnabled = true;
            FileSaveLocButton.IsEnabled = true;
        }

        public void BTClass_OnExceptionOccured(object sender, Exception ex)
        {
            Debug.WriteLine("BTClass exception" + ex.Message);
        }

        public async void BTClass_OnDataReceived(object sender, string message)
        {
            try
            {
                await Task.Run(() => ParseMessage(message));
                await PlotList(LastPlottedPoint + 1);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Exception " + ex.Message + " in BTClass_OnDataReceived");
            }   
        }

        void SetDirIndicators(Int32 DirSignalStrength)
        {
            if (DirSignalStrength < 512)
            {
                DirLeftIndicator.Value = 512 - DirSignalStrength;
                DirRightIndicator.Value = 0;
            }
            else
            {
                DirLeftIndicator.Value = 0;
                DirRightIndicator.Value = DirSignalStrength - 512;
            }
        }

        private void UpdateUI(object o)
        {
            string[] SignalList = (string[])o;

            if (ParsedDataPoints.Count > 0)
            {
                try
                {
                    ArduinoDataPoint Point = ParsedDataPoints.Last();

                    LatitudeBox.Text = "Latitude = " + Point.Latitude.ToString();
                    LongitudeBox.Text = "Longitude = " + Point.Longitude.ToString();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception " + ex.Message + " in BTClass_OnDataReceived:setting text boxes");
                }
            }
            
            //Audio strength
            if (!SignalList[MeanAudioIndex].Equals(""))
                SignalStrengthTextBox.Text = "Audio signal = " + SignalList[MeanAudioIndex].ToString();

            //LH16 Signal strength
            if (!SignalList[SignalStrengthIndex].Equals(""))
                LH16SignalStrengthBox.Text = "LH16 signal = " + SignalList[SignalStrengthIndex].ToString();

            //Direction Indication
            if (!SignalList[DirectionIndex].Equals(""))
                SetDirIndicators(Convert.ToInt32(SignalList[DirectionIndex]));

            //Battery Status
            if (!SignalList[BatteryStatusIndex].Equals(""))
                BatteryStrengthTextBox.Text = "Battery = " + SignalList[BatteryStatusIndex].ToString();
        }

        private int Median(List<int> l)
        {
            if (l.Count == 0)
                return 0;
            
            l.Sort();
            return l[l.Count / 2];
        }

        private void ParseMessage(string message)
        {
            string TrimmedMessage;
            int SubStringIndex;
            char[] separators = { '\r', '\n' };
            UIUpdateDelegate UIUpdatefn = new UIUpdateDelegate(UpdateUI);

            message = message.Trim();
            if (DataBuffer != null)
                DataBuffer += message;
            else
                DataBuffer = message;

            SubStringIndex = DataBuffer.LastIndexOf('\n');
            if (SubStringIndex < 0)
                TrimmedMessage = DataBuffer;
            else
                TrimmedMessage = DataBuffer.Substring(0, SubStringIndex);

            DataBuffer = DataBuffer.Remove(0, TrimmedMessage.Length - 1);

            string[] DataPointList = TrimmedMessage.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            
            foreach(string Str in DataPointList)
            {
                //No. of separators = No. of variables - 1
                if(!Str.Equals("") && Str.Count(Sep => Sep ==',') == (GPSMessageNumOfFields - 1))
                {
                    string[] SignalList = Str.Split(',');

                    AddToPlotList(SignalList);
                    synchronizationContext.Post(new SendOrPostCallback(UIUpdatefn), SignalList);
                }
            }
        }

        private async Task PlotList(int StartIndex)
        {
            ArduinoDataPoint Point;
            int i;
            int count = ParsedDataPoints.Count;
                        
            for (i = StartIndex; i < count; i++)
            {
                Point = ParsedDataPoints[i];

                if (Point.SignalStrength > MaxIntensity)
                {
                    MaxIntensity = Point.SignalStrength;
                }

                if (Point.SignalStrength < MinIntensity)
                {
                    MinIntensity = Point.SignalStrength;
                }

                if (Point.Plotted == false)
                {
                    DrawCircle(Point.Longitude, Point.Latitude, Point.Radius, Point.SignalStrength);
                    Point.Plotted = true;
                    if (DataFile != null)
                        await Windows.Storage.FileIO.AppendTextAsync(DataFile, Point.SignalStrength.ToString() + ","
                                + Point.Latitude.ToString("#.00000") + "," + Point.Longitude.ToString("#.00000")
                                + "," + Point.Altitude.ToString("#.00000") + "\r\n");
                    LastPlottedPoint = i;
                }
            }
            StatusTextBox.Text = "Plotted " + ParsedDataPoints.Count.ToString() + " Points\n";
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

        //Download button is disabled because terrain map does not allow exporting tile caches.
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
            catch(Exception except)
            {
                StatusTextBox.Text += "Downloading Tile Package failed..." + except.Message + " stopping\n";
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
                try
                {
                    await OnlineMapBaseLayer.InitializeAsync();
                }
                catch(Exception)
                {
                    StatusTextBox.Text = "Could not load map. Please check your internet connection and restart the app";
                    return;
                }                
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

            for (i = 0; i < ParsedDataPoints.Count; i++)
            {
                tmp = ParsedDataPoints[i];
                tmp.Radius = DefaultRadius;
                tmp.Plotted = false;
                ParsedDataPoints[i] = tmp;
            }
        }

        private async void MainPage_onSpotSizeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                DefaultRadius = Convert.ToInt32(SpotSizeTextBox.Text);
                UpdateSpotSize();
                ClearPoints(false);
                await PlotList(0);
            }
            catch(Exception)
            {
                StatusTextBox.Text += "Invalid number\n";
            }
        }

        private async void BTDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BTClass.IsBTConnected == false)
                return;

            if (ActiveBTDevice == BTDeviceList.SelectedIndex.ToString())
                return;

            await DisconnectBT();
            await ConnectBT();
            ActiveBTDevice = BTDeviceList.SelectedIndex.ToString();
        }

        private async void FileSaveLocButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker SaveLocationPicker = new FolderPicker();
            SaveLocationPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            SaveLocationPicker.FileTypeFilter.Add(".txt");

            StorageFolder folder = await SaveLocationPicker.PickSingleFolderAsync();
            DataFolder = folder;
            FileSaveLocTextBox.Text = DataFolder.Path;
        }
    }
}
