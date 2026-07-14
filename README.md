<<<<<<< HEAD
# Custom Drone — ESP-NOW Radio Control System

A fully custom drone build with a DIY radio control system. The drone is based on the **Matek F411** flight controller and communicates via two **ESP8266 (ESP-01)** modules using the **ESP-NOW** protocol over Wi-Fi. Control input comes from a standard **DualShock 4** gamepad connected to a PC desktop application.

> **Languages:** C# · C++

---

## How It Works

The system is split into three logical layers:

```
[DualShock 4] ──► [PC Client] ──USB──► [TX ESP-01] ──ESP-NOW──► [RX ESP-01] ──CRSF──► [Flight Controller]
```

| Layer | Role |
|---|---|
| **PC Client** | Reads stick axes from the DualShock 4 via DirectInput, converts them to RC channel values (1000–2000 µs), packs them into a binary frame and sends it over a virtual COM port to the transmitter |
| **TX (ESP-01)** | Receives packets from the PC via CP2102 USB-UART adapter, repackages them and broadcasts over ESP-NOW |
| **RX (ESP-01)** | Catches ESP-NOW packets, converts the channel data to the standard **CRSF** protocol and forwards it to the flight controller over UART |

---

## Hardware

> Exact components may be substituted with compatible alternatives.

| Component | Qty |
|---|---|
| ESP8266 Wi-Fi module (ESP-01) | 2 |
| Matek F411 Flight Controller | 1 |
| 8520 coreless motor (2× CW + 2× CCW) | 4 |
| 65 mm 2-blade propeller, 1 mm shaft (2× CW + 2× CCW) | 4 |
| 3.8 V 660 mAh 90C Li-Po 1S | 1 |
| USB-UART converter (CP2102) | 1 |
| LD1117V33 3.3 V 950 mA linear regulator | 2 |
| 100 µF 6.3 V electrolytic capacitor | 2 |
| 1 kΩ resistor | 6 |
| Compatible frame | 1 |

Wiring diagrams and schematics are in [`/Docs`](./Docs).

---

## Repository Structure

```
Drone_project_1/
├── Docs/            # Wiring diagrams and schematics
├── Firmware/        # Betaflight backup + ESP-01 sketches (TX and RX)
├── PC_client/       # C# WPF desktop application (DroneController)
└── .gitignore
```

---

## Getting Started

### 1. Flash the ESP modules

- Flash `MAC_RX` sketch onto the **receiver** ESP-01 and note its MAC address printed over serial.
- Insert that MAC into the **transmitter** sketch, then flash the final firmware onto both ESP-01 boards.

### 2. Build the hardware

Assemble the drone and the transmitter unit following the schematics in [`/Docs`](./Docs).

### 3. Flash the flight controller

Connect the FC to Betaflight Configurator and restore the configuration backup from [`/Firmware`](./Firmware).

### 4. Build and run the PC client

Open [`/PC_client`](./PC_client) in Visual Studio 2022, target **.NET 8**, build and run.

Requirements: .NET 8 SDK, NuGet packages `SharpDX.DirectInput` and `System.IO.Ports` (restored automatically).

### 5. Connect and fly

1. Plug the DualShock 4 into the PC via USB.
2. Click **"Find and connect gamepad"** in the app — the status should turn green.
3. Select the COM port of the CP2102 transmitter and click **"Connect"**.
4. Channel bars should respond to stick input in real time.

---

## Control Mapping

| Channel | Input |
|---|---|
| Throttle | Left stick — vertical |
| Pitch | Right stick — vertical |
| Yaw | Right stick — horizontal |
| Roll | L2 (roll left) / R2 (roll right) |
| ARM | **L1** — toggle (arms only when throttle is at minimum) |
| ALTHOLD | **R1** — toggle (altitude hold via onboard barometer) |

---

## ⚠️ Safety

**Always remove propellers before any bench testing or configuration.**

Unlike a traditional RC transmitter, the throttle stick on a DualShock 4 is spring-loaded and returns to center (1500 µs ≈ 50% throttle) when released. The application will block arming if the throttle is not at the minimum position, but once armed, releasing the stick will immediately spin up the motors. Stay aware of this at all times.
=======
# Drone_project_1
Custom drone with ESP-NOW radio link and DualShock 4 controller
>>>>>>> e5a52d0892f54b365eece0d75efa757a8c9cf2fa
