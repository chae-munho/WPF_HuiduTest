using SDKLibrary;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using HuiduTest.Models;
using System.Drawing; // Color 사용
using System.Windows.Controls; // ListBox 등 UI 컨트롤용

namespace HuiduTest
{
    public partial class MainWindow : Window
    {
        private HDCommunicationManager _comm;
        private ObservableCollection<DeviceItem> _devices = new ObservableCollection<DeviceItem>();
        private ObservableCollection<string> _logs = new ObservableCollection<string>();
        private Device _selected;
        private string _selectedImage = null;

        public MainWindow()
        {
            InitializeComponent();
            DeviceList.ItemsSource = _devices;
            LogList.ItemsSource = _logs;
            InitSdk();
        }

        private void InitSdk()
        {
            try
            {
                _comm = new HDCommunicationManager();
                _comm.MsgReport += Comm_MsgReport;
                _comm.ResolvedInfoReport += Comm_ResolvedInfoReport;

                int port = 10001;
                _comm.Listen(new IPEndPoint(IPAddress.Any, port));
                Log($"Listening on 0.0.0.0:{port}");

                try
                {
                    _comm.StartScanLANDevice();
                    Log("Scanning LAN devices...");
                }
                catch
                {
                    Log("LAN scan not supported by this SDK version.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SDK init failed: {ex.Message}");
            }
        }

        private void Comm_MsgReport(object sender, string msg)
        {
            Dispatcher.Invoke(() =>
            {
                if (sender is Device dev)
                {
                    string id = dev.GetDeviceInfo().deviceID;
                    Log($"{id}: {msg}");

                    if (msg == "online" || msg == "offline")
                        RefreshDevices();
                }
                else
                {
                    Log(msg);
                }
            });
        }

        private void Comm_ResolvedInfoReport(Device device, ResolveInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                string id = device.GetDeviceInfo().deviceID;
                Log($"{id} {info.cmdType} {info.errorCode} {info.method}");
            });
        }

        private void RefreshDevices()
        {
            _devices.Clear();
            var list = _comm.GetDevices();
            foreach (var d in list)
                _devices.Add(new DeviceItem(d));

            if (_devices.Any())
            {
                _selected = _devices.First().Device;
                DeviceList.SelectedIndex = 0;
                Log($"Selected: {_selected.GetDeviceInfo().deviceID}");
            }
        }

        private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = (DeviceList.SelectedItem as DeviceItem)?.Device;
        }

        private void Log(string msg)
        {
            _logs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            LogList.ScrollIntoView(_logs.LastOrDefault());
        }

        private void SendTextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("No controller connected.");
                return;
            }

            try
            {
                var info = _selected.GetDeviceInfo();
                var screen = new HdScreen(new ScreenParam() { isNewScreen = true });

                var program = new HdProgram(new ProgramParam()
                {
                    type = ProgramType.normal,
                    guid = Guid.NewGuid().ToString()
                });

                screen.Programs.Add(program);

                var area = program.AddArea(new AreaParam()
                {
                    guid = Guid.NewGuid().ToString(),
                    x = 0,
                    y = 0,
                    width = info.screenWidth,
                    height = info.screenHeight
                });

                var textItem = new TextAreaItemParam()
                {
                    guid = Guid.NewGuid().ToString(),
                    text = TextInput.Text,
                    fontName = "Arial",
                    fontSize = 32,
                    color = System.Drawing.Color.Red
                };

                // ↓ 이 부분만 '객체 생성' 대신 '필드 설정'으로 변경
                textItem.effect.inEffet = EffectType.IMMEDIATE_SHOW;
                textItem.effect.outEffet = EffectType.NOT_CLEAR_AREA;
                textItem.effect.inSpeed = 5;
                textItem.effect.outSpeed = 5;
                textItem.effect.duration = 5;

                area.AddText(textItem);


                string xml = _selected.SendScreen(screen);
                Log("✅ Text sent successfully!");
                Log(xml);
            }
            catch (Exception ex)
            {
                Log($"❌ SendText Error: {ex.Message}");
            }
        }

        private void SelectImageBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog()
            {
                Filter = "Image Files|*.jpg;*.png;*.bmp",
                Title = "Select Image"
            };

            if (dlg.ShowDialog() == true)
            {
                _selectedImage = dlg.FileName;
                SelectedImagePath.Text = System.IO.Path.GetFileName(_selectedImage);
            }
        }

        private void SendImageBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("No controller connected.");
                return;
            }
            if (string.IsNullOrEmpty(_selectedImage))
            {
                MessageBox.Show("No image selected.");
                return;
            }

            try
            {
                // 1️⃣ 먼저 장치로 업로드
                var uploadInfo = _selected.AddUploadFile(_selectedImage, false);
                _selected.StartUploadFile();

                Thread.Sleep(1000); // 업로드 대기 (필요 시 이벤트 기반으로 변경 가능)
                string fileName = System.IO.Path.GetFileName(_selectedImage);

                // 2️⃣ 화면 구성
                var info = _selected.GetDeviceInfo();
                var screen = new HdScreen(new ScreenParam() { isNewScreen = true });
                var program = new HdProgram(new ProgramParam() { guid = Guid.NewGuid().ToString(), type = ProgramType.normal });
                screen.Programs.Add(program);

                var area = program.AddArea(new AreaParam()
                {
                    guid = Guid.NewGuid().ToString(),
                    x = 0,
                    y = 0,
                    width = info.screenWidth,
                    height = info.screenHeight
                });

                var imageItem = new ImageAreaItemParam()
                {
                    guid = Guid.NewGuid().ToString(),
                    file = fileName
                };

                // ↓ 동일하게 effect는 'new' 하지 말고 필드만 설정
                imageItem.effect.inEffet = EffectType.IMMEDIATE_SHOW;
                imageItem.effect.outEffet = EffectType.NOT_CLEAR_AREA;
                imageItem.effect.inSpeed = 5;
                imageItem.effect.outSpeed = 5;
                imageItem.effect.duration = 5;

                area.AddImage(imageItem);


                string xml = _selected.SendScreen(screen);
                Log("✅ Image sent successfully!");
                Log(xml);
            }
            catch (Exception ex)
            {
                Log($"❌ SendImage Error: {ex.Message}");
            }
        }
    }
}
