using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FTech_CoaxlinkEx;  // Euresys SDK Wrapper

// Test할꺼야

namespace HCameraTestProgram
{
    public partial class MainWindow : Window
    {
        private const int MAX_CAM = 5;

        private readonly MainViewModel _vm = new MainViewModel();

        private CoaxlinkEx[] _grabber = new CoaxlinkEx[MAX_CAM];
        private Thread[] _displayThread = new Thread[MAX_CAM];
        private bool[] _isWorking = new bool[MAX_CAM];
        private bool[] _isColor = new bool[MAX_CAM];

        public MainWindow()
        {
            InitializeComponent();
            CoaxlinkEx.UpdateCameraList();
            this.DataContext = _vm;

            // 초기 테스트용 (실제 Open 시 교체)
            _vm.InitBitmap(0, 1280, 1024, false);
            _vm.InitBitmap(1, 1280, 1024, false);
            _vm.InitBitmap(2, 1280, 1024, true);
            _vm.InitBitmap(3, 1280, 1024, false);
            _vm.InitBitmap(4, 1280, 1024, false);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            int index = int.Parse((string)btn.Tag);

            try
            {
                if ((string)btn.Content == "Open")
                {
                    bool soft = IsSoftChecked(index);
                    _isColor[index] = soft;

                    _grabber[index] = new CoaxlinkEx(CoaxlinkEx.GetCameraInfo(index));

                    long width = _grabber[index].GetValueInteger(CoaxlinkEx.TransportLayer.Stream, "Width");
                    long height = _grabber[index].GetValueInteger(CoaxlinkEx.TransportLayer.Stream, "Height");

                    _vm.InitBitmap(index, (int)width, (int)height, _isColor[index]);
                    _vm.SetModelSerial(index, _grabber[index].DeviceModelName, _grabber[index].DeviceSerialNumber);

                    btn.Content = "Close";
                }
                else
                {
                    StopThread(index);

                    if (_grabber[index] != null)
                    {
                        _grabber[index].Dispose();
                        _grabber[index] = null;
                    }

                    _vm.SetModelSerial(index, "-", "-");
                    btn.Content = "Open";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            var btn = (System.Windows.Controls.Button)sender;
            int index = int.Parse((string)btn.Tag);

            try
            {
                if ((string)btn.Content == "Start")
                {
                    if (_grabber[index] == null)
                        throw new InvalidOperationException("Open the camera first.");

                    _isWorking[index] = true;

                    _displayThread[index] = new Thread(DisplayThreadProc)
                    {
                        IsBackground = true,
                        Name = $"DisplayThread_Cam{index + 1}"
                    };
                    _displayThread[index].Start(index);

                    _grabber[index].Start();
                    btn.Content = "Stop";
                }
                else
                {
                    StopThread(index);
                    if (_grabber[index] != null)
                        _grabber[index].Stop();
                    btn.Content = "Start";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopThread(int index)
        {
            if (_displayThread[index] != null)
            {
                _isWorking[index] = false;
                _displayThread[index].Join();
                _displayThread[index] = null;
            }
        }

        private void DisplayThreadProc(object state)
        {
            int camIndex = (int)state;

            while (_isWorking[camIndex])
            {
                Thread.Sleep(30);

                var handle = _grabber[camIndex].GrabDone;
                if (!handle.WaitOne(1000))
                    continue;

                byte[] src = _isColor[camIndex]
                    ? _grabber[camIndex].ColorBuffer
                    : _grabber[camIndex].Buffer;

                int w = _vm.GetWidth(camIndex);
                int h = _vm.GetHeight(camIndex);
                bool isColor = _isColor[camIndex];

                _vm.UpdateFrame(camIndex, src, w, h, isColor);
            }
        }

        private bool IsSoftChecked(int index)
        {
            switch (index)
            {
                case 0: return rbSoft1.IsChecked == true;
                case 1: return rbSoft2.IsChecked == true;
                case 2: return rbSoft3.IsChecked == true;
                case 3: return rbSoft4.IsChecked == true;
                case 4: return rbSoft5.IsChecked == true;
                default: return false;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            for (int i = 0; i < MAX_CAM; i++)
            {
                try { StopThread(i); } catch { }
                try
                {
                    if (_grabber[i] != null)
                    {
                        _grabber[i].Dispose();
                        _grabber[i] = null;
                    }
                }
                catch { }
            }
        }
    }

    // --------------------- ViewModel ---------------------
    public class MainViewModel : INotifyPropertyChanged
    {
        public ImageSource Cam1Source { get { return _cam1; } }
        public ImageSource Cam2Source { get { return _cam2; } }
        public ImageSource Cam3Source { get { return _cam3; } }
        public ImageSource Cam4Source { get { return _cam4; } }
        public ImageSource Cam5Source { get { return _cam5; } }

        public string Cam1Model { get { return _camModel[0]; } private set { _camModel[0] = value; OnPropertyChanged("Cam1Model"); } }
        public string Cam1Serial { get { return _camSerial[0]; } private set { _camSerial[0] = value; OnPropertyChanged("Cam1Serial"); } }
        public string Cam2Model { get { return _camModel[1]; } private set { _camModel[1] = value; OnPropertyChanged("Cam2Model"); } }
        public string Cam2Serial { get { return _camSerial[1]; } private set { _camSerial[1] = value; OnPropertyChanged("Cam2Serial"); } }
        public string Cam3Model { get { return _camModel[2]; } private set { _camModel[2] = value; OnPropertyChanged("Cam3Model"); } }
        public string Cam3Serial { get { return _camSerial[2]; } private set { _camSerial[2] = value; OnPropertyChanged("Cam3Serial"); } }
        public string Cam4Model { get { return _camModel[3]; } private set { _camModel[3] = value; OnPropertyChanged("Cam4Model"); } }
        public string Cam4Serial { get { return _camSerial[3]; } private set { _camSerial[3] = value; OnPropertyChanged("Cam4Serial"); } }
        public string Cam5Model { get { return _camModel[4]; } private set { _camModel[4] = value; OnPropertyChanged("Cam5Model"); } }
        public string Cam5Serial { get { return _camSerial[4]; } private set { _camSerial[4] = value; OnPropertyChanged("Cam5Serial"); } }

        private WriteableBitmap _cam1, _cam2, _cam3, _cam4, _cam5;
        private readonly int[] _w = new int[5];
        private readonly int[] _h = new int[5];
        private readonly string[] _camModel = new string[5] { "-", "-", "-", "-", "-" };
        private readonly string[] _camSerial = new string[5] { "-", "-", "-", "-", "-" };

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }

        public void InitBitmap(int camIndex, int width, int height, bool isColor)
        {
            var pf = isColor ? PixelFormats.Bgr24 : PixelFormats.Gray8;
            var wb = new WriteableBitmap(width, height, 96, 96, pf, null);

            _w[camIndex] = width;
            _h[camIndex] = height;

            switch (camIndex)
            {
                case 0: _cam1 = wb; OnPropertyChanged("Cam1Source"); break;
                case 1: _cam2 = wb; OnPropertyChanged("Cam2Source"); break;
                case 2: _cam3 = wb; OnPropertyChanged("Cam3Source"); break;
                case 3: _cam4 = wb; OnPropertyChanged("Cam4Source"); break;
                case 4: _cam5 = wb; OnPropertyChanged("Cam5Source"); break;
                default: throw new ArgumentOutOfRangeException("camIndex");
            }
        }

        public void SetModelSerial(int camIndex, string model, string serial)
        {
            switch (camIndex)
            {
                case 0: Cam1Model = model; Cam1Serial = serial; break;
                case 1: Cam2Model = model; Cam2Serial = serial; break;
                case 2: Cam3Model = model; Cam3Serial = serial; break;
                case 3: Cam4Model = model; Cam4Serial = serial; break;
                case 4: Cam5Model = model; Cam5Serial = serial; break;
            }
        }

        public int GetWidth(int camIndex) => _w[camIndex];
        public int GetHeight(int camIndex) => _h[camIndex];

        public void UpdateFrame(int camIndex, byte[] src, int width, int height, bool isColor)
        {
            var target = GetBitmap(camIndex);
            if (target == null) return;

            int bpp = isColor ? 3 : 1;
            int stride = width * bpp;

            Application.Current.Dispatcher.Invoke(() =>
            {
                target.WritePixels(new Int32Rect(0, 0, width, height), src, stride, 0);
            });
        }

        private WriteableBitmap GetBitmap(int camIndex)
        {
            switch (camIndex)
            {
                case 0: return _cam1;
                case 1: return _cam2;
                case 2: return _cam3;
                case 3: return _cam4;
                case 4: return _cam5;
                default: return null;
            }
        }
    }
}