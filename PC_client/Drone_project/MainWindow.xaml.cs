using System;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SharpDX.DirectInput;

namespace DroneController
{
    public partial class MainWindow : Window
    {
        private DirectInput? _directInput;
        private Joystick? _joystick;
        private bool _gamepadConnected = false;

        private SerialPort? _serialPort;
        private bool _serialConnected = false;

        private DispatcherTimer? _pollTimer;
        private const int POLL_INTERVAL_MS = 20;

        private ushort _throttle = 1500;
        private ushort _roll = 1500;
        private ushort _pitch = 1500;
        private ushort _yaw = 1500;
        private ushort _aux1 = 1000;
        private ushort _aux2 = 1000;

        private bool _aux1State = false;
        private bool _aux2State = false;
        private bool _prevL1Pressed = false;
        private bool _prevL2Pressed = false;
        private DateTime _armBlockedUntil = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            RefreshComPorts();

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(POLL_INTERVAL_MS)
            };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        private void RefreshComPorts()
        {
            ComPortComboBox.ItemsSource = SerialPort.GetPortNames().OrderBy(p => p);
            if (ComPortComboBox.Items.Count > 0)
                ComPortComboBox.SelectedIndex = 0;
        }

        private void RefreshPortsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshComPorts();
        }

        private void ConnectSerialButton_Click(object sender, RoutedEventArgs e)
        {
            if (_serialConnected)
            {
                try { _serialPort?.Close(); } catch { }
                _serialConnected = false;
                ConnectSerialButton.Content = "Connect";
                UpdateSendStatus();
                return;
            }

            if (ComPortComboBox.SelectedItem is not string portName)
            {
                MessageBox.Show("Please select a COM port from the list.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _serialPort = new SerialPort(portName, 115200) { WriteTimeout = 100 };
                _serialPort.Open();
                _serialConnected = true;
                ConnectSerialButton.Content = "Disconnect";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open port {portName}:\n{ex.Message}",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateSendStatus();
        }

        private void ConnectGamepadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _directInput ??= new DirectInput();

                var deviceInstance = _directInput
                    .GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly)
                    .FirstOrDefault();

                if (deviceInstance == null)
                {
                    MessageBox.Show("Gamepad not found. Make sure DualShock 4 is connected via USB.",
                        "Gamepad Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _joystick = new Joystick(_directInput, deviceInstance.InstanceGuid);
                _joystick.Properties.BufferSize = 128;
                _joystick.Acquire();

                _gamepadConnected = true;
                GamepadStatusText.Text = $"connected: {deviceInstance.InstanceName}";
                GamepadStatusText.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gamepad connection error:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (_gamepadConnected && _joystick != null)
                ReadGamepad();

            UpdateChannelIndicators();

            if (_serialConnected && _serialPort != null)
                SendPacket();
        }

        private void ReadGamepad()
        {
            try
            {
                _joystick!.Poll();
                var state = _joystick.GetCurrentState();

                _throttle = AxisToUs(state.Y,         invert: true);
                _yaw     = AxisToUs(state.Z,         invert: false);
                _pitch    = AxisToUs(state.RotationZ, invert: true);

                int roll = 32767 + (state.RotationY / 2) - (state.RotationX / 2);
                roll = Math.Clamp(roll, 0, 65535);
                _roll = AxisToUs(roll, invert: false);

                bool[] buttons = state.Buttons;
                bool l1Pressed = buttons.Length > 4 && buttons[4];
                bool r1Pressed = buttons.Length > 5 && buttons[5];

                if (l1Pressed && !_prevL1Pressed)
                {
                    if (!_aux1State)
                    {
                        if (_throttle <= 1050)
                            _aux1State = true;
                        else
                            ArmBlockedFlash();
                    }
                    else
                    {
                        _aux1State = false;
                    }
                }

                if (r1Pressed && !_prevL2Pressed)
                    _aux2State = !_aux2State;

                _prevL1Pressed = l1Pressed;
                _prevL2Pressed = r1Pressed;

                _aux1 = _aux1State ? (ushort)2000 : (ushort)1000;
                _aux2 = _aux2State ? (ushort)2000 : (ushort)1000;
            }
            catch (Exception)
            {
                _gamepadConnected = false;
                GamepadStatusText.Text = "connection lost";
                GamepadStatusText.Foreground = Brushes.Red;
            }
        }

        private void ArmBlockedFlash()
        {
            ArmStatusText.Text = "ARM BLOCKED: lower throttle to minimum!";
            ArmStatusText.Foreground = Brushes.Red;
            _armBlockedUntil = DateTime.Now.AddSeconds(1.5);
        }

        private static ushort AxisToUs(int rawValue, bool invert)
        {
            double normalized = rawValue / 65535.0;
            if (invert) normalized = 1.0 - normalized;

            const double deadband = 0.05;
            double centered = (normalized - 0.5) * 2.0;

            if (Math.Abs(centered) < deadband)
                centered = 0.0;
            else
                centered = (centered - Math.Sign(centered) * deadband) / (1.0 - deadband);

            double us = 1500.0 + centered * 500.0;
            if (us < 1000) us = 1000;
            if (us > 2000) us = 2000;
            return (ushort)us;
        }

        private void UpdateChannelIndicators()
        {
            ThrottleBar.Value = _throttle;
            ThrottleValueText.Text = _throttle.ToString();

            RollBar.Value = _roll;
            RollValueText.Text = _roll.ToString();

            PitchBar.Value = _pitch;
            PitchValueText.Text = _pitch.ToString();

            YawBar.Value = _yaw;
            YawValueText.Text = _yaw.ToString();

            Aux1Bar.Value = _aux1;
            Aux1ValueText.Text = _aux1.ToString();

            Aux2Bar.Value = _aux2;
            Aux2ValueText.Text = _aux2.ToString();

            if (_armBlockedUntil != DateTime.MinValue && DateTime.Now < _armBlockedUntil)
                return;

            _armBlockedUntil = DateTime.MinValue;

            if (_aux1State)
            {
                ArmStatusText.Text = "ARMED";
                ArmStatusText.Foreground = Brushes.Red;
            }
            else
            {
                ArmStatusText.Text = "disarmed";
                ArmStatusText.Foreground = Brushes.Gray;
            }
        }

        private void UpdateSendStatus()
        {
            if (_serialConnected)
            {
                SendStatusText.Text = "transmit: active";
                SendStatusText.Foreground = Brushes.Green;
            }
            else
            {
                SendStatusText.Text = "transmit: stopped";
                SendStatusText.Foreground = Brushes.Black;
            }
        }

        private void SendPacket()
        {
            byte[] packet = new byte[15];
            packet[0] = 0xAA;
            packet[1] = 0x55;

            WriteU16LE(packet, 2,  _throttle);
            WriteU16LE(packet, 4,  _yaw);
            WriteU16LE(packet, 6,  _pitch);
            WriteU16LE(packet, 8,  _roll);
            WriteU16LE(packet, 10, _aux1);
            WriteU16LE(packet, 12, _aux2);

            byte checksum = 0;
            for (int i = 2; i < 14; i++) checksum ^= packet[i];
            packet[14] = checksum;

            try
            {
                _serialPort!.Write(packet, 0, packet.Length);
            }
            catch (Exception)
            {
                _serialConnected = false;
                ConnectSerialButton.Content = "Connect";
                UpdateSendStatus();
            }
        }

        private static void WriteU16LE(byte[] buffer, int offset, ushort value)
        {
            buffer[offset]     = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        protected override void OnClosed(EventArgs e)
        {
            _pollTimer?.Stop();
            try { _joystick?.Unacquire(); } catch { }
            _joystick?.Dispose();
            _directInput?.Dispose();
            try { _serialPort?.Close(); } catch { }
            _serialPort?.Dispose();
            base.OnClosed(e);
        }
    }
}
