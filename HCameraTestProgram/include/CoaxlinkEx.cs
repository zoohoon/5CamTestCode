//-----------------------------------------------------------------------
// Euresys Test Class
// This code tested with eGrabber SDK 25.05.1.31
//
// Copyright (c) 2019, FAINSTEC,.CO.LTD. 
//
// web : www.fainstec.com
//
// e-mail : tech@fainstec.com
//
// ※ 해당 코드는 사용자의 이해를 돕기 위해 만든 테스트 코드이므로
//   반드시 충분한 검증과 테스트를 통해 참고하여 사용하길 권장 드립니다.
//-----------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Euresys.EGrabber;


namespace FTech_CoaxlinkEx
{
    class CoaxlinkEx : IDisposable
    {
        #region Discovery

        public static void UpdateCameraList()
        {
            _discovery.Discover();
        }
        public static EGrabberCameraInfo GetCameraInfo(int index)
        {
            return _discovery.Cameras[index];
        }

        #endregion

        #region Constructor & Desctructor
        public CoaxlinkEx(Euresys.EGrabber.EGrabberCameraInfo camera)
        {
            _camera = new EGrabber(camera);
            _camera.RegisterEventCallback<NewBufferData>((g, data) =>
            {
                onNewBufferEvent(g, data);
            });

            _camera.RegisterEventCallback<IoToolboxData>((g, data) =>
            {
                onIoToolboxEvent(g, data);
            });

            _camera.RegisterEventCallback<CicData>((g, data) =>
            {
                onCicEvent(g, data);
            });

            _camera.RegisterEventCallback<CxpInterfaceData>((g, data) => 
            {
                onCxpInterfaceEvent(g, data);
            });

            _camera.RegisterEventCallback<DataStreamData>((g, data) =>
            {
                onDataStreamEvent(g, data);
            });

            _camera.RegisterEventCallback<CxpDeviceData>((g, data) =>
            {
                onCxpDeviceEvent(g, data);
            });
        }
        public CoaxlinkEx(int InterfaceIndex, int DeviceIndex, int StreamIndex = 0)
        {
            _camera = new EGrabber(_genTL, InterfaceIndex, DeviceIndex, StreamIndex);
            _camera.RegisterEventCallback<NewBufferData>((g, data) =>
            {
                onNewBufferEvent(g, data);
            });

            _camera.RegisterEventCallback<IoToolboxData>((g, data) =>
            {
                onIoToolboxEvent(g, data);
            });

            _camera.RegisterEventCallback<CicData>((g, data) =>
            {
                onCicEvent(g, data);
            });

            _camera.RegisterEventCallback<CxpInterfaceData>((g, data) =>
            {
                onCxpInterfaceEvent(g, data);
            });

            _camera.RegisterEventCallback<DataStreamData>((g, data) =>
            {
                onDataStreamEvent(g, data);
            });

            _camera.RegisterEventCallback<CxpDeviceData>((g, data) =>
            {
                onCxpDeviceEvent(g, data);
            });
        }

        public void Dispose()
        {
           if(IsActived == true)
           {
               Stop();
           }
        }
        #endregion

        #region Variables
        private const int BUFFER_COUNT = 10;
        private EGrabber _camera = null;
        private static EGenTL _genTL = new Euresys.EGrabber.EGenTL(Euresys.EGrabber.CtiPath.Coaxlink);
        private static EGrabberDiscovery _discovery = new Euresys.EGrabber.EGrabberDiscovery(_genTL);
        private EventWaitHandle _grabDone = new EventWaitHandle(false, EventResetMode.AutoReset);
        private CancellationTokenSource _cancelTokenSource = null;
        private bool _isDeviceLinkReady = false;
        private bool _isDeviceLinkLost = false;
        private byte[] _imageBuffer = null;
        private int _bufferSize = 0;
        private Task _task = null;

        public enum TransportLayer
        {
            Interface,
            Device,
            Remote,
            Stream
        }

        public enum EventData
        {
            NewBufferData,
            DataStreamData
        }

        #endregion

        #region Acquisition Start & Stop
        public void Start(ulong grabCount = ulong.MaxValue)
        {
            _camera.ReallocBuffers(BUFFER_COUNT);

            if (_imageBuffer == null)
            {
                _imageBuffer = new byte[_camera.Stream.Get<int>("PayloadSize")];
            }
            else
            {
                 Array.Resize(ref _imageBuffer, _camera.Stream.Get<int>("PayloadSize"));
            }

            _camera.ResetBufferQueue();

            _cancelTokenSource = new CancellationTokenSource();

            _task = Task.Run(() =>
            {
                while (!_cancelTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        _camera.ProcessEvent(EventType.All);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            });

            _camera.Start(grabCount);            
        }

        public void Stop()
        {
            _camera.Stop();

            _cancelTokenSource.Cancel();
            _camera.CancelEvent(EventType.All);

            try
            {
                _task.Wait();
                _task.Dispose();
                _task = null;
                _cancelTokenSource.Dispose();
                _cancelTokenSource = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            /* Callback 처리되지 않은 Buffer 삭제*/
            ulong remainBuffers = _camera.Stream.GetInfo<ulong>(STREAM_INFO_CMD.STREAM_INFO_NUM_AWAIT_DELIVERY);
            _camera.FlushBuffers();
        }
        #endregion

        #region Functions
        public void RunScript(string path)
        {
            _camera.RunScript(path);
        }
   
        public void WriteMementoWithLocalTime(string message)
        {
            string date = System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm");
            string tmp = "[CoaxlinkEx][" + date + "] : ";
            _camera.Memento(tmp + message);
        }

        public void ResetDeviceLinkLost()
        {
            _isDeviceLinkLost = false;
        }
        public void ResetDeviceLinkReady()
        {
            _isDeviceLinkReady = false;
        }
        #endregion

        #region Save Image
        public void SaveRawImage(string path)
        {
            IntPtr image = Marshal.AllocHGlobal(_imageBuffer.Length);
            Marshal.Copy(_imageBuffer, 0, image, _imageBuffer.Length);

            ImageConvertInput raw = new ImageConvertInput((int)Width, (int)Height, "Mono8", image, PayloadSize, (int)Width);
            _genTL.ImageSaveToDisk(raw, path);

            Marshal.FreeHGlobal(image);
        }

        public void SaveColorImage(string path)
        {
            IntPtr image = Marshal.AllocHGlobal(_imageBuffer.Length);
            IntPtr convertImage = Marshal.AllocHGlobal(_imageBuffer.Length * 3);
            Marshal.Copy(_imageBuffer, 0, image, _imageBuffer.Length);

            ImageConvertInput raw = new ImageConvertInput((int)Width, (int)Height, "Mono8", image, PayloadSize, (int)Width);
            ImageConvertOutput bgr = new ImageConvertOutput((int)Width, (int)Height, "BGR8", convertImage, PayloadSize * 3);
            _genTL.ImageConvert(raw, bgr);
            ImageConvertInput convert = new ImageConvertInput((int)Width, (int)Height, "BGR8", bgr.Pixels, PayloadSize * 3, (int)Width * 3 );
            _genTL.ImageSaveToDisk(convert, path);
           
            Marshal.FreeHGlobal(image);
            Marshal.FreeHGlobal(convertImage);
        }
        #endregion

        #region parameter control
        public void SetValueInt(TransportLayer layer, string node, int value)
        {
            switch (layer)
            {
                case TransportLayer.Interface:
                    _camera.Interface.Set(node, value);
                    break;
                case TransportLayer.Device:
                    _camera.Device.Set(node, value);
                    break;
                case TransportLayer.Remote:
                    _camera.Remote.Set(node, value);
                    break;
                case TransportLayer.Stream:
                    _camera.Stream.Set(node, value);
                    break;
                default:
                    break;
            }
        }

        public long GetValueInteger(TransportLayer layer, string node)
        {
            long value = 0;
            switch (layer)
            {
                case TransportLayer.Interface:
                    value = _camera.Interface.Get<long>(node);
                    return value;
                case TransportLayer.Device:
                    value = _camera.Device.Get<long>(node);
                    return value;
                case TransportLayer.Remote:
                    value = _camera.Remote.Get<long>(node);
                    return value;
                case TransportLayer.Stream:
                    value = _camera.Stream.Get<long>(node);
                    return value;
                default:
                    return value;
            }
        }

        public void SetValueDouble(TransportLayer layer, string node, double value)
        {
            switch (layer)
            {
                case TransportLayer.Interface:
                    _camera.Interface.Set(node, value);
                    break;
                case TransportLayer.Device:
                    _camera.Device.Set(node, value);
                    break;
                case TransportLayer.Remote:
                    _camera.Remote.Set(node, value);
                    break;
                case TransportLayer.Stream:
                    _camera.Stream.Set(node, value);
                    break;
                default:
                    break;
            }
        }

        public double GetValueDouble(TransportLayer layer, string node)
        {
            double value = 0.0;
            switch (layer)
            {
                case TransportLayer.Interface:
                    value = _camera.Interface.Get<double>(node);
                    return value;
                case TransportLayer.Device:
                    value = _camera.Device.Get<double>(node);
                    return value;
                case TransportLayer.Remote:
                    value = _camera.Remote.Get<double>(node);
                    return value;
                case TransportLayer.Stream:
                    value = _camera.Stream.Get<double>(node);
                    return value;
                default:
                    return value;
            }
        }

        public void SetValueString(TransportLayer layer, string node, string value)
        {
            switch (layer)
            {
                case TransportLayer.Interface:
                    _camera.Interface.Set(node, value);
                    break;
                case TransportLayer.Device:
                    _camera.Device.Set(node, value);
                    break;
                case TransportLayer.Remote:
                    _camera.Remote.Set(node, value);
                    break;
                case TransportLayer.Stream:
                    _camera.Stream.Set(node, value);
                    break;
                default:
                    break;
            }
        }

        public string GetValueString(TransportLayer layer, string node)
        {
            string value = "";
            switch (layer)
            {
                case TransportLayer.Interface:
                    value = _camera.Interface.Get<string>(node);
                    return value;
                case TransportLayer.Device:
                    value = _camera.Device.Get<string>(node);
                    return value;
                case TransportLayer.Remote:
                    value = _camera.Remote.Get<string>(node);
                    return value;
                case TransportLayer.Stream:
                    value = _camera.Stream.Get<string>(node);
                    return value;
                default:
                    return value;
            }
        }

        public void OnExecuteCommand(TransportLayer layer, string node)
        {
            switch (layer)
            {
                case TransportLayer.Interface:
                    _camera.Interface.Execute(node);
                    break;
                case TransportLayer.Device:
                    _camera.Device.Execute(node);
                    break;
                case TransportLayer.Remote:
                    _camera.Remote.Execute(node);
                    break;
                case TransportLayer.Stream:
                    _camera.Stream.Execute(node);
                    break;
            }
        }
        #endregion


        #region Properties
        public EventWaitHandle GrabDone
        {
            get
            {
                return _grabDone;
            }
        }

        public bool IsOpened
        {
            get
            {
                string model = "";
                model = _camera.Device.GetInfo<string>(DEVICE_INFO_CMD.DEVICE_INFO_MODEL);
                if (model == "")
                    return false;
                else
                    return true;
            }
        }

        public bool IsActived
        {
            get
            {
                bool bActive = false;
                bActive = _camera.Stream.GetInfo<bool>(STREAM_INFO_CMD.STREAM_INFO_IS_GRABBING);
                return bActive;
            }
        }
        public byte[] Buffer
        {
            get
            {
                return _imageBuffer;
            }
        }

        public byte[] ColorBuffer
        {
            get
            {
                IntPtr image = Marshal.AllocHGlobal(_imageBuffer.Length);
                IntPtr convertImage = Marshal.AllocHGlobal(_imageBuffer.Length * 3);
                Marshal.Copy(_imageBuffer, 0, image, _imageBuffer.Length);

                ImageConvertInput raw = new ImageConvertInput((int)Width, (int)Height, "Mono8", image, PayloadSize, (int)Width);
                ImageConvertOutput bgr = new ImageConvertOutput((int)Width, (int)Height, "BGR8", convertImage, PayloadSize * 3);
                _genTL.ImageConvert(raw, bgr);

                IntPtr color = bgr.Pixels;

                byte[] colorBuffer = new byte[_imageBuffer.Length * 3];
                Marshal.Copy(color, colorBuffer, 0, _imageBuffer.Length * 3);
                Marshal.FreeHGlobal(image);
                Marshal.FreeHGlobal(convertImage);
                
                return colorBuffer;
            }
        }

        public ulong PayloadSize
        {
            get
            {
                ulong payloadSize = _camera.Stream.Get<ulong>("PayloadSize");
                return payloadSize;
            }
        }

        public ulong Width
        {
            get
            {
                ulong width = _camera.Remote.Get<ulong>("Width");
                return width;
            }
        }

        public ulong Height
        {
            get
            {
                ulong height = _camera.Remote.Get<ulong>("Height");
                return height;
            }
        }

        public string PixelFormat
        {
            get
            {
                string pixelFormat = _camera.Remote.Get<string>("PixelFormat");
                return pixelFormat;
            }
        }

        public string DeviceModelName
        {
            get
            {
                string deviceModelName  = _camera.Remote.Get<string>("DeviceModelName");
                return deviceModelName;
            }
        }

        public string DeviceSerialNumber
        {
            get
            {
                string deviceSerialNumber = _camera.Remote.Get<string>("DeviceSerialNumber");
                return deviceSerialNumber;
            }
        }

        public int BufferSize
        {
            get
            {
                return _bufferSize;
            }
        }
        
        public bool IsReady
        {
            get
            {
                return _isDeviceLinkReady;
            }
        }
        public bool IsLost
        {
            get
            {
                return _isDeviceLinkLost;
            }
        }
        #endregion
        #region Events
        private void onNewBufferEvent(EGrabber g, NewBufferData data)
        {
            using (var buffer = new ScopedBuffer(g, data))
            {
                IntPtr imgPtr;
                UInt64 size;

                size = buffer.GetInfo<UInt64>(BUFFER_INFO_CMD.BUFFER_INFO_SIZE);
                imgPtr = buffer.GetInfo<IntPtr>(BUFFER_INFO_CMD.BUFFER_INFO_BASE);

                _bufferSize = (int)size;
                Marshal.Copy(imgPtr, _imageBuffer, 0, _bufferSize);
                _grabDone.Set();
            }
        }

        private void onIoToolboxEvent(EGrabber g, IoToolboxData data)
        {
            switch (data.NumId)
            {
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_LIN1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_LIN2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_LIN3:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_LIN4:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_LIN5:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_LIN6:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_LIN7:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_LIN8:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW3:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW4:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW5:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW6:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW7:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW8:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW9:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW10:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW11:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW12:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW13:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW14:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW15:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_CFW16:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_QDC1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_QDC1_DIR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_QDC2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_QDC2_DIR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_QDC3:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_QDC3_DIR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_QDC4:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_QDC4_DIR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DIV1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DIV2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DIV3:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DIV4:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_MDV1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_MDV2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_MDV3:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_MDV4:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DEL1_1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DEL1_2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DEL2_1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DEL2_2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DEL3_1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DEL3_2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DEL4_1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DEL4_2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_USER_EVENT_1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_USER_EVENT_2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_USER_EVENT_3:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_USER_EVENT_4:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_C2C1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_C2C2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_C2C3:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_EIN1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_EIN2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT1:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT2:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT3:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT4:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT5:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT6:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT7:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT8:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT9:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT10:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT11:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT12:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT13:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT14:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT15:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_IO_TOOLBOX_DLT16:
                    break;
                default:
                    break;
            }
        }

        private void onCicEvent(EGrabber g, CicData data)
        {
            switch (data.NumId)
            {
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_CAMERA_TRIGGER_RISING_EDGE:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_CAMERA_TRIGGER_FALLING_EDGE:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_STROBE_RISING_EDGE:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_STROBE_FALLING_EDGE:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_ALLOW_NEXT_CYCLE:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_DISCARDED_CIC_TRIGGER:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_PENDING_CIC_TRIGGER:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_CXP_TRIGGER_ACK:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_CXP_TRIGGER_RESEND:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CIC_TRIGGER:
                    break;
                default:
                    break;
            }
        }

        private void onDataStreamEvent(EGrabber g, DataStreamData data)
        {
            switch (data.NumId)
            {
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_START_OF_CAMERA_READOUT:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_END_OF_CAMERA_READOUT:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_START_OF_SCAN:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_END_OF_SCAN:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_REJECTED_FRAME:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_REJECTED_SCAN:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_TRIGGER_TO_CAMERA_READOUT_TIMEOUT:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_CAMERA_READOUT_TIMEOUT:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DATASTREAM_BROKEN_FRAME:
                    break;
                default:
                    break;
            }
        }

        private void onCxpInterfaceEvent(EGrabber g, CxpInterfaceData data)
        {
            switch (data.NumId)
            {
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CRC_ERROR_CXP_A:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CRC_ERROR_CXP_B:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CRC_ERROR_CXP_C:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CRC_ERROR_CXP_D:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CRC_ERROR_CXP_E:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CRC_ERROR_CXP_F:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CRC_ERROR_CXP_G:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CRC_ERROR_CXP_H:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_DETECTED_CXP_A:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_DETECTED_CXP_B:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_DETECTED_CXP_C:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_DETECTED_CXP_D:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_DETECTED_CXP_E:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_DETECTED_CXP_F:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_DETECTED_CXP_G:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_DETECTED_CXP_H:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_UNDETECTED_CXP_A:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_UNDETECTED_CXP_B:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_UNDETECTED_CXP_C:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_UNDETECTED_CXP_D:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_UNDETECTED_CXP_E:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_UNDETECTED_CXP_F:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_UNDETECTED_CXP_G:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_CONNECTION_UNDETECTED_CXP_H:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_0_READY:
                    _isDeviceLinkReady = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_1_READY:
                    _isDeviceLinkReady = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_2_READY:
                    _isDeviceLinkReady = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_3_READY:
                    _isDeviceLinkReady = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_4_READY:
                    _isDeviceLinkReady = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_5_READY:
                    _isDeviceLinkReady = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_6_READY:
                    _isDeviceLinkReady = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_7_READY:
                    _isDeviceLinkReady = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_0_LOST:
                    _isDeviceLinkLost = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_1_LOST:
                    _isDeviceLinkLost = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_2_LOST:
                    _isDeviceLinkLost = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_3_LOST:
                    _isDeviceLinkLost = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_4_LOST:
                    _isDeviceLinkLost = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_5_LOST:
                    _isDeviceLinkLost = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_6_LOST:
                    _isDeviceLinkLost = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_7_LOST:
                    _isDeviceLinkLost = true;
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_0_CONFIGURING:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_1_CONFIGURING:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_2_CONFIGURING:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_3_CONFIGURING:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_4_CONFIGURING:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_5_CONFIGURING:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_6_CONFIGURING:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_INTERFACE_DEVICE_7_CONFIGURING:
                    break;
                default:
                    break;
            }
        }

        private void onDeviceErrorEvent(EGrabber g, DeviceErrorData data)
        {
            switch (data.NumId)
            {
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_STREAM_PACKET_SIZE_ERROR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_STREAM_PACKET_FIFO_OVERFLOW:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_CAMERA_TRIGGER_OVERRUN:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_DID_NOT_RECEIVE_TRIGGER_ACK:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_TRIGGER_PACKET_RETRY_ERROR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_INPUT_STREAM_FIFO_HALF_FULL:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_INPUT_STREAM_FIFO_FULL:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_IMAGE_HEADER_ERROR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_MIG_AXI_WRITE_ERROR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_MIG_AXI_READ_ERROR:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_PACKET_WITH_UNEXPECTED_TAG:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_FILL_LEVEL_ABOVE_IL_SOS_REJECTED:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_FILL_LEVEL_ABOVE_AF_EARLY_EOS:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_EXTERNAL_TRIGGER_REQS_TOO_CLOSE:
                    break;
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_DEVICE_ERROR_STREAM_PACKET_ARBITER_ERROR:
                    break;
                default:
                    break;
            }
        }

        private void onCxpDeviceEvent(EGrabber g, CxpDeviceData data)
        {
            switch (data.NumId)
            {
                case EVENT_DATA_NUMID.EVENT_DATA_NUMID_CXP_DEVICE_LINK_TRIGGER:
                    break;
                default:
                    break;
            }
        }
        #endregion
    }
}
